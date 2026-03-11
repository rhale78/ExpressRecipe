using System.Text.Json;
using ExpressRecipe.MealPlanningService.Data;
using ExpressRecipe.Messaging.Core.Abstractions;
using ExpressRecipe.Messaging.Core.Messages;
using ExpressRecipe.Messaging.Core.Options;
using ExpressRecipe.Shared.Messages;

namespace ExpressRecipe.MealPlanningService.Workers;

/// <summary>
/// Subscribes to <see cref="RecipeCookedEvent"/> and creates a RateRecipe work-queue item
/// when the cooked recipe does not yet have a rating.
/// </summary>
public sealed class RecipeCookedEventSubscriber : IHostedService
{
    private readonly IMessageBus _bus;
    private readonly IWorkQueueRepository _queue;
    private readonly ILogger<RecipeCookedEventSubscriber> _logger;

    public RecipeCookedEventSubscriber(
        IMessageBus bus,
        IWorkQueueRepository queue,
        ILogger<RecipeCookedEventSubscriber> logger)
    {
        _bus    = bus;
        _queue  = queue;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        SubscribeOptions opts = new() { RoutingMode = RoutingMode.Broadcast };
        await _bus.SubscribeAsync<RecipeCookedEvent>(HandleAsync, opts, cancellationToken);
        _logger.LogInformation("[RecipeCookedEventSubscriber] Subscribed to recipe.cooked events");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    internal async Task HandleAsync(
        RecipeCookedEvent evt,
        MessageContext context,
        CancellationToken ct)
    {
        if (evt.HasRating)
        {
            // Already rated — no queue item needed
            return;
        }

        if (evt.HouseholdId is null)
        {
            _logger.LogWarning(
                "RecipeCookedEvent for history {HistoryId} has no HouseholdId — skipping RateRecipe queue item",
                evt.CookingHistoryId);
            return;
        }

        string payload = JsonSerializer.Serialize(new
        {
            recipeId         = evt.RecipeId,
            cookingHistoryId = evt.CookingHistoryId,
            cookedAt         = evt.CookedAt
        });

        try
        {
            await _queue.UpsertItemAsync(
                householdId:    evt.HouseholdId.Value,
                itemType:       "RateRecipe",
                priority:       WorkQueuePriority.RateRecipe,
                title:          "How did the recipe turn out?",
                body:           "Tap to rate and log notes",
                actionPayload:  payload,
                sourceEntityId: evt.CookingHistoryId,  // deduplicate by cooking session
                sourceService:  "Recipe",
                expiresAt:      DateTime.UtcNow.AddDays(7),  // auto-expire after a week
                ct:             ct);

            _logger.LogInformation(
                "RateRecipe queue item upserted for household {HouseholdId} session {HistoryId}",
                evt.HouseholdId, evt.CookingHistoryId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to upsert RateRecipe queue item for history {HistoryId}",
                evt.CookingHistoryId);
            throw;
        }
    }
}
