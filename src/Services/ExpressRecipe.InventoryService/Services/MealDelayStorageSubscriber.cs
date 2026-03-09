using ExpressRecipe.InventoryService.Data;
using ExpressRecipe.Messaging.Core.Abstractions;
using ExpressRecipe.Messaging.Core.Messages;
using ExpressRecipe.Messaging.Core.Options;
using ExpressRecipe.Shared.Messages;
using System.Net.Http.Json;

namespace ExpressRecipe.InventoryService.Services;

public sealed class MealDelayStorageSubscriber : IHostedService
{
    private readonly IMessageBus _bus;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _http;
    private readonly ILogger<MealDelayStorageSubscriber> _logger;

    public MealDelayStorageSubscriber(
        IMessageBus bus,
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory http,
        ILogger<MealDelayStorageSubscriber> logger)
    {
        _bus = bus;
        _scopeFactory = scopeFactory;
        _http = http;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        SubscribeOptions opts = new SubscribeOptions { RoutingMode = RoutingMode.Broadcast };
        await _bus.SubscribeAsync<MealPlanUpdatedEvent>(HandleEventAsync, opts, cancellationToken);
        _logger.LogInformation("[MealDelayStorageSubscriber] Subscribed to mealplan.updated events");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    internal async Task HandleEventAsync(
        MealPlanUpdatedEvent evt,
        MessageContext context,
        CancellationToken ct)
    {
        // Only care about delayed meals (new date > old date by more than 2 days)
        if (evt.NewPlannedDate is null || evt.OldPlannedDate is null)
        {
            return;
        }

        double daysDelta = (evt.NewPlannedDate.Value - evt.OldPlannedDate.Value).TotalDays;
        if (daysDelta < 2)
        {
            return;
        }

        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        IInventoryStorageReminderQuery query = scope.ServiceProvider
            .GetRequiredService<IInventoryStorageReminderQuery>();
        IStorageLocationExtendedRepository storage = scope.ServiceProvider
            .GetRequiredService<IStorageLocationExtendedRepository>();

        // Get perishable items linked to this household that are in fridge/counter storage
        List<PerishableInventoryItem> items = await query.GetPerishableItemsForRecipeAsync(
            evt.HouseholdId, evt.RecipeId ?? Guid.Empty, ct);

        List<PerishableInventoryItem> atRisk = items
            .Where(i => i.StorageType is "Refrigerator" or "Counter" or null)
            .Where(i => i.FoodCategory is "Meat" or "Poultry" or "Seafood" or "Dairy" or null)
            .ToList();

        if (atRisk.Count == 0)
        {
            return;
        }

        // Find freezer locations for this household
        List<StorageLocationSuggestionDto> freezers = await storage.SuggestLocationsAsync(
            evt.HouseholdId, "Frozen", ct);
        string freezerHint = freezers.Count > 0
            ? $" Consider moving to: {freezers[0].Name}."
            : " Consider moving to the freezer.";

        string itemList = string.Join(", ", atRisk.Select(i => i.ItemName));
        HttpClient client = _http.CreateClient("notificationservice");
        try
        {
            await client.PostAsJsonAsync("/api/notifications/internal/household", new
            {
                householdId = evt.HouseholdId,
                type = "MealDelayStorageReminder",
                title = "🧊 Meal rescheduled — check your perishables",
                message = $"Your meal was moved out {daysDelta:F0} days. " +
                          $"{itemList} may need to move to the freezer.{freezerHint}"
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to send meal delay storage reminder for household {HouseholdId}", evt.HouseholdId);
        }
    }
}
