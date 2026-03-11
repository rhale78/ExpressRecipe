using System.Net.Http.Json;
using ExpressRecipe.InventoryService.Data;

namespace ExpressRecipe.InventoryService.Services;

/// <summary>
/// Runs daily at 8 AM to generate WorkQueue low-stock reorder reminders for each household.
/// Only creates reminders for items the household has previously purchased
/// (avoids spamming for one-off ingredients).
/// Calls MealPlanningService's internal upsert endpoint for each low-stock item.
/// </summary>
public sealed class LowStockQueueGenerator : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<LowStockQueueGenerator> _logger;
    private readonly string _mealPlanningServiceUrl;

    public LowStockQueueGenerator(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<LowStockQueueGenerator> logger)
    {
        _serviceProvider        = serviceProvider;
        _logger                 = logger;
        _mealPlanningServiceUrl = configuration["Services:MealPlanningService"] ?? "http://localhost:5106";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("LowStockQueueGenerator started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error in LowStockQueueGenerator");
            }

            TimeSpan delay = TimeUntilNextHour(8);
            await Task.Delay(delay, stoppingToken);
        }

        _logger.LogInformation("LowStockQueueGenerator stopped");
    }

    internal async Task RunAsync(CancellationToken ct)
    {
        using IServiceScope scope = _serviceProvider.CreateScope();
        IInventoryRepository repository  = scope.ServiceProvider.GetRequiredService<IInventoryRepository>();
        IHttpClientFactory   httpFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();

        // Use households that have inventory with expiration data as a proxy for active households
        List<Guid> householdIds = await repository.GetDistinctHouseholdIdsWithInventoryAsync(ct);
        _logger.LogInformation("LowStockQueueGenerator: processing {Count} households", householdIds.Count);

        using HttpClient client = httpFactory.CreateClient("MealPlanningService");

        foreach (Guid householdId in householdIds)
        {
            try
            {
                List<InventoryItemDto> lowStockItems = await repository.GetLowStockItemsByHouseholdAsync(
                    householdId, threshold: 2.0m, ct);

                foreach (InventoryItemDto item in lowStockItems)
                {
                    // Only create reminders for items the household has ordered before
                    if (item.ProductId is null) { continue; }

                    bool hasPurchased = await repository.HasHouseholdPurchasedProductAsync(
                        householdId, item.ProductId.Value, ct);

                    if (!hasPurchased) { continue; }

                    string itemName = item.CustomName ?? item.ProductName ?? "Item";
                    string title    = $"{itemName} is running low";

                    object payload = new
                    {
                        HouseholdId    = householdId,
                        ItemType       = "LowStockReorder",
                        Priority       = 8,
                        Title          = title,
                        Body           = $"Only {item.Quantity:G4} {item.Unit ?? "units"} remaining",
                        ActionPayload  = (string?)null,
                        SourceEntityId = item.Id,
                        SourceService  = "Inventory",
                        ExpiresAt      = DateTime.UtcNow.AddDays(7)
                    };

                    HttpResponseMessage response = await client.PostAsJsonAsync(
                        $"{_mealPlanningServiceUrl}/api/work-queue/internal/upsert", payload, ct);

                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning(
                            "LowStockQueueGenerator: upsert returned {Status} for item {ItemId}",
                            response.StatusCode, item.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "LowStockQueueGenerator: error processing household {HouseholdId}", householdId);
            }
        }
    }

    private static TimeSpan TimeUntilNextHour(int targetHour)
    {
        DateTime now  = DateTime.Now;
        DateTime next = now.Date.AddHours(targetHour);
        if (next <= now) { next = next.AddDays(1); }
        return next - now;
    }
}
