using ExpressRecipe.Messaging.Core.Abstractions;
using ExpressRecipe.Messaging.Core.Messages;
using ExpressRecipe.Messaging.Core.Options;
using ExpressRecipe.PreferencesService.Data;
using ExpressRecipe.Shared.Messages;

namespace ExpressRecipe.PreferencesService.Services;

public sealed class HouseholdMemberSubscriber : IHostedService
{
    private readonly IMessageBus _bus;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<HouseholdMemberSubscriber> _logger;

    public HouseholdMemberSubscriber(
        IMessageBus bus,
        IServiceScopeFactory scopeFactory,
        ILogger<HouseholdMemberSubscriber> logger)
    {
        _bus = bus;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        SubscribeOptions broadcastOpts = new SubscribeOptions { RoutingMode = RoutingMode.Broadcast };

        await _bus.SubscribeAsync<HouseholdMemberAddedEvent>(HandleMemberAddedAsync, broadcastOpts, cancellationToken);
        await _bus.SubscribeAsync<HouseholdMemberRemovedEvent>(HandleMemberRemovedAsync, broadcastOpts, cancellationToken);
        await _bus.SubscribeAsync<MemberGdprDeleteEvent>(HandleMemberGdprDeleteAsync, broadcastOpts, cancellationToken);

        _logger.LogInformation("[HouseholdMemberSubscriber] Subscribed to household member events");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task HandleMemberAddedAsync(HouseholdMemberAddedEvent evt, MessageContext context, CancellationToken ct)
    {
        try
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            ICookProfileRepository repo = scope.ServiceProvider.GetRequiredService<ICookProfileRepository>();

            await repo.InitializeCookProfileAsync(evt.MemberId, ct);

            _logger.LogInformation(
                "Cook profile initialised for member {MemberId} in household {HouseholdId}",
                evt.MemberId, evt.HouseholdId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialise cook profile for member {MemberId}", evt.MemberId);
        }
    }

    private async Task HandleMemberRemovedAsync(HouseholdMemberRemovedEvent evt, MessageContext context, CancellationToken ct)
    {
        try
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            ICookProfileRepository repo = scope.ServiceProvider.GetRequiredService<ICookProfileRepository>();

            await repo.SoftDeleteCookProfileAsync(evt.MemberId, ct);

            _logger.LogInformation(
                "Soft-deleted cook profile for removed member {MemberId}", evt.MemberId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to soft-delete cook profile for member {MemberId}", evt.MemberId);
        }
    }

    private async Task HandleMemberGdprDeleteAsync(
        MemberGdprDeleteEvent evt,
        MessageContext context,
        CancellationToken ct)
    {
        _logger.LogWarning(
            "[PreferencesService] GDPR member delete received for member {MemberId}", evt.MemberId);

        try
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            ICookProfileRepository repo = scope.ServiceProvider.GetRequiredService<ICookProfileRepository>();

            await repo.DeleteMemberDataAsync(evt.MemberId, ct);

            _logger.LogInformation(
                "[PreferencesService] GDPR: hard-deleted cook profile data for member {MemberId}", evt.MemberId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[PreferencesService] Error processing GDPR member delete for {MemberId}", evt.MemberId);
        }
    }
}
