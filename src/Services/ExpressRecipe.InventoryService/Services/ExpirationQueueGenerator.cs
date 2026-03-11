using System.Net.Http.Json;
using ExpressRecipe.InventoryService.Data;

namespace ExpressRecipe.InventoryService.Services;

/// <summary>
/// Runs daily at 7 AM to generate WorkQueue expiration alerts for each household.
/// Calls MealPlanningService's internal upsert endpoint for each expiring item.
/// </summary>
public sealed class ExpirationQueueGenerator : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ExpirationQueueGenerator> _logger;
    private readonly string _mealPlanningServiceUrl;

    public ExpirationQueueGenerator(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<ExpirationQueueGenerator> logger)
    {
        _serviceProvider        = serviceProvider;
        _logger                 = logger;
        _mealPlanningServiceUrl = configuration["Services:MealPlanningService"] ?? "http://localhost:5106";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ExpirationQueueGenerator started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error in ExpirationQueueGenerator");
            }

            TimeSpan delay = TimeUntilNextHour(7);
            await Task.Delay(delay, stoppingToken);
        }

        _logger.LogInformation("ExpirationQueueGenerator stopped");
    }

    internal async Task RunAsync(CancellationToken ct)
    {
        using IServiceScope scope = _serviceProvider.CreateScope();
        IInventoryRepository repository  = scope.ServiceProvider.GetRequiredService<IInventoryRepository>();
        IHttpClientFactory   httpFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();

        List<Guid> householdIds = await repository.GetDistinctHouseholdIdsWithInventoryAsync(ct);
        _logger.LogInformation("ExpirationQueueGenerator: processing {Count} households", householdIds.Count);

        using HttpClient client = httpFactory.CreateClient("MealPlanningService");

        foreach (Guid householdId in householdIds)
        {
            try
            {
                List<InventoryItemDto> items = await repository.GetExpiringItemsByHouseholdAsync(householdId, 7, ct);

                foreach (InventoryItemDto item in items)
                {
                    if (item.ExpirationDate is null) { continue; }

                    int days = (int)Math.Floor((item.ExpirationDate.Value - DateTime.UtcNow).TotalDays);
                    string itemType = DetermineItemType(days);
                    int priority    = DeterminePriority(days);

                    string itemName = item.CustomName ?? item.ProductName ?? "Item";
                    string title    = days <= 0
                        ? $"{itemName} has expired"
                        : $"{itemName} expires in {days} day{(days == 1 ? "" : "s")}";

                    DateTime expiresAt = item.ExpirationDate.Value.AddDays(3);

                    object payload = new
                    {
                        HouseholdId    = householdId,
                        ItemType       = itemType,
                        Priority       = priority,
                        Title          = title,
                        Body           = (string?)null,
                        ActionPayload  = (string?)null,
                        SourceEntityId = item.Id,
                        SourceService  = "Inventory",
                        ExpiresAt      = expiresAt
                    };

                    HttpResponseMessage response = await client.PostAsJsonAsync(
                        $"{_mealPlanningServiceUrl}/api/work-queue/internal/upsert", payload, ct);

                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning(
                            "ExpirationQueueGenerator: upsert returned {Status} for item {ItemId}",
                            response.StatusCode, item.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "ExpirationQueueGenerator: error processing household {HouseholdId}", householdId);
            }
        }
    }

    // Exposed as internal for unit testing
    internal static string DetermineItemType(int daysUntilExpiration)
    {
        if (daysUntilExpiration <= 0) { return "Expired"; }
        if (daysUntilExpiration <= 2) { return "ExpiringCritical"; }
        return "ExpiringSoon";
    }

    internal static int DeterminePriority(int daysUntilExpiration)
    {
        if (daysUntilExpiration <= 0) { return 1; }  // Expired
        if (daysUntilExpiration <= 2) { return 3; }  // ExpiringCritical
        return 6;                                     // ExpiringSoon
    }

    private static TimeSpan TimeUntilNextHour(int targetHour)
    {
        DateTime now  = DateTime.Now;
        DateTime next = now.Date.AddHours(targetHour);
        if (next <= now) { next = next.AddDays(1); }
        return next - now;
    }
}
