using ExpressRecipe.Messaging.Core.Abstractions;
using ExpressRecipe.Messaging.Core.Messages;
using ExpressRecipe.Messaging.Core.Options;
using ExpressRecipe.ProfileService.Data;
using ExpressRecipe.Shared.Messages;

namespace ExpressRecipe.ProfileService.Services;

/// <summary>
/// Subscribes to <see cref="GdprDeleteEvent"/> and:
/// 1. Unlinks the user from all <c>HouseholdMember</c> rows (sets LinkedUserId = NULL, HasUserAccount = 0).
/// 2. Publishes a <see cref="MemberGdprDeleteEvent"/> for every affected member so that
///    downstream services (PreferencesService, SafeForkService) can hard-delete member-scoped data.
/// </summary>
public sealed class GdprEventSubscriber : IHostedService
{
    private readonly IMessageBus _bus;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<GdprEventSubscriber> _logger;

    public GdprEventSubscriber(
        IMessageBus bus,
        IServiceScopeFactory scopeFactory,
        ILogger<GdprEventSubscriber> logger)
    {
        _bus = bus;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        SubscribeOptions opts = new SubscribeOptions { RoutingMode = RoutingMode.Broadcast };
        await _bus.SubscribeAsync<GdprDeleteEvent>(HandleDeleteAsync, opts, cancellationToken);
        _logger.LogInformation("[ProfileService] Subscribed to GDPR delete events");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task HandleDeleteAsync(
        GdprDeleteEvent evt,
        MessageContext context,
        CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            "[ProfileService] GDPR delete received for user {UserId} (requestId {RequestId})",
            evt.UserId, evt.RequestId);

        try
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            IHouseholdMemberRepository repo =
                scope.ServiceProvider.GetRequiredService<IHouseholdMemberRepository>();

            IReadOnlyList<Guid> memberIds =
                await repo.DeleteUserDataAsync(evt.UserId, cancellationToken);

            _logger.LogInformation(
                "[ProfileService] GDPR: unlinked {Count} member(s) for user {UserId}",
                memberIds.Count, evt.UserId);

            // Publish cascading delete event for each member so downstream services can clean up
            foreach (Guid memberId in memberIds)
            {
                MemberGdprDeleteEvent memberEvt = new MemberGdprDeleteEvent(
                    memberId, evt.UserId, evt.RequestId, DateTimeOffset.UtcNow);

                await _bus.PublishAsync(memberEvt, cancellationToken: cancellationToken);

                _logger.LogInformation(
                    "[ProfileService] Published MemberGdprDeleteEvent for member {MemberId}", memberId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[ProfileService] Error processing GDPR delete for user {UserId}", evt.UserId);
        }
    }
}
