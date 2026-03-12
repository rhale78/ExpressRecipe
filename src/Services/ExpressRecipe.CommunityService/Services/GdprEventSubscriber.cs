using ExpressRecipe.CommunityService.Data;
using ExpressRecipe.Messaging.Core.Abstractions;
using ExpressRecipe.Messaging.Core.Messages;
using ExpressRecipe.Messaging.Core.Options;
using ExpressRecipe.Shared.Messages;

namespace ExpressRecipe.CommunityService.Services;

/// <summary>
/// Handles GDPR events for CommunityService.
/// <list type="bullet">
///   <item><see cref="GdprDeleteEvent"/> – anonymises the user's identity in community records
///   (contributions, reviews, submissions) instead of hard-deleting to preserve referential
///   integrity of public content.</item>
///   <item><see cref="GdprForgetEvent"/> – same anonymisation path (Right to be Forgotten).</item>
/// </list>
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
        await _bus.SubscribeAsync<GdprForgetEvent>(HandleForgetAsync, opts, cancellationToken);
        _logger.LogInformation("[CommunityService] Subscribed to GDPR delete/forget events");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task HandleDeleteAsync(
        GdprDeleteEvent evt,
        MessageContext context,
        CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            "[CommunityService] GDPR delete received for user {UserId} (requestId {RequestId})",
            evt.UserId, evt.RequestId);

        await AnonymizeAsync(evt.UserId, cancellationToken);
    }

    private async Task HandleForgetAsync(
        GdprForgetEvent evt,
        MessageContext context,
        CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            "[CommunityService] GDPR forget received for user {UserId} (requestId {RequestId})",
            evt.UserId, evt.RequestId);

        await AnonymizeAsync(evt.UserId, cancellationToken);
    }

    private async Task AnonymizeAsync(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            ICommunityRepository repo = scope.ServiceProvider.GetRequiredService<ICommunityRepository>();
            await repo.AnonymizeUserDataAsync(userId, cancellationToken);
            _logger.LogInformation(
                "[CommunityService] GDPR anonymisation complete for user {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[CommunityService] Error processing GDPR event for user {UserId}", userId);
        }
    }
}
