using ExpressRecipe.Messaging.Core.Abstractions;
using ExpressRecipe.Messaging.Core.Messages;
using ExpressRecipe.Messaging.Core.Options;
using ExpressRecipe.SafeForkService.Data;
using ExpressRecipe.Shared.Messages;

namespace ExpressRecipe.SafeForkService.Services;

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

        _logger.LogInformation("[HouseholdMemberSubscriber] Subscribed to household member events");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task HandleMemberAddedAsync(HouseholdMemberAddedEvent evt, MessageContext context, CancellationToken ct)
    {
        try
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            IAllergenProfileRepository repo = scope.ServiceProvider.GetRequiredService<IAllergenProfileRepository>();

            _logger.LogInformation("Initialising allergen profile for new member {MemberId} in household {HouseholdId}",
                evt.MemberId, evt.HouseholdId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialise allergen profile for member {MemberId}", evt.MemberId);
        }
    }

    private async Task HandleMemberRemovedAsync(HouseholdMemberRemovedEvent evt, MessageContext context, CancellationToken ct)
    {
        try
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            IAllergenProfileRepository repo = scope.ServiceProvider.GetRequiredService<IAllergenProfileRepository>();

            await repo.SoftDeleteAllForMemberAsync(evt.MemberId, ct);

            _logger.LogInformation("Soft-deleted all allergen profile entries for removed member {MemberId}", evt.MemberId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to soft-delete allergen profiles for member {MemberId}", evt.MemberId);
        }
    }
}
