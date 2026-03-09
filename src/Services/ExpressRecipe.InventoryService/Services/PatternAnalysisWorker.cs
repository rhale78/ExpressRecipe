using ExpressRecipe.InventoryService.Data;
using System.Net.Http.Json;

namespace ExpressRecipe.InventoryService.Services;

public class PatternAnalysisWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PatternAnalysisWorker> _logger;

    public PatternAnalysisWorker(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<PatternAnalysisWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Pattern Analysis Worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                TimeSpan delay = ComputeDelayUntilNextRun();
                _logger.LogInformation("Pattern Analysis Worker sleeping for {Delay}", delay);
                await Task.Delay(delay, stoppingToken);

                await ProcessPatternAnalysisAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error in Pattern Analysis Worker");
                await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
            }
        }

        _logger.LogInformation("Pattern Analysis Worker stopped");
    }

    private TimeSpan ComputeDelayUntilNextRun()
    {
        string timeStr = _configuration.GetValue<string>("InventoryIntelligence:PatternAnalysisTimeUtc", "02:00");
        if (!TimeSpan.TryParse(timeStr, out TimeSpan targetTime))
        {
            _logger.LogWarning(
                "Invalid InventoryIntelligence:PatternAnalysisTimeUtc value '{ConfiguredValue}'. Falling back to 02:00.",
                timeStr);
            targetTime = TimeSpan.FromHours(2);
        }

        DateTime now = DateTime.UtcNow;
        DateTime nextRun = now.Date.Add(targetTime);
        if (nextRun <= now)
        {
            nextRun = nextRun.AddDays(1);
        }

        return nextRun - now;
    }

    internal async Task ProcessPatternAnalysisAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = _serviceProvider.CreateScope();
        IInventoryRepository repository = scope.ServiceProvider.GetRequiredService<IInventoryRepository>();
        IHttpClientFactory httpFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();

        _logger.LogInformation("Starting pattern analysis run");

        List<Guid> userIds = await repository.GetDistinctUserIdsWithPurchaseHistoryAsync(cancellationToken);
        _logger.LogInformation("Analysing purchase patterns for {Count} users", userIds.Count);

        foreach (Guid userId in userIds)
        {
            try
            {
                await ProcessUserPatternsAsync(userId, repository, httpFactory, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing patterns for user {UserId}", userId);
            }
        }

        _logger.LogInformation("Pattern analysis run complete");
    }

    private async Task ProcessUserPatternsAsync(
        Guid userId,
        IInventoryRepository repository,
        IHttpClientFactory httpFactory,
        CancellationToken cancellationToken)
    {
        // Get all purchase events for this user grouped by ProductId
        List<PurchaseEventDto> allPurchases = await repository.GetPurchaseHistoryAsync(userId, null, 3650, cancellationToken);

        // Fetch existing patterns so we can detect newly-abandoned products
        List<ProductConsumptionPatternDto> existingPatterns = await repository.GetConsumptionPatternsAsync(userId, cancellationToken);
        Dictionary<string, bool> previousAbandonedState = existingPatterns.ToDictionary(
            p => BuildProductKey(p.ProductId, p.IngredientId, p.CustomName),
            p => p.IsAbandoned);

        // Group by product (prefer ProductId, fall back to IngredientId then CustomName)
        IEnumerable<IGrouping<string, PurchaseEventDto>> groups = allPurchases
            .GroupBy(e => BuildProductKey(e.ProductId, e.IngredientId, e.CustomName));

        foreach (IGrouping<string, PurchaseEventDto> group in groups)
        {
            List<PurchaseEventDto> purchases = group.OrderBy(p => p.PurchasedAt).ToList();
            if (purchases.Count == 0)
            {
                continue;
            }

            ProductConsumptionPatternRecord pattern = ComputePattern(userId, purchases);

            bool wasAbandoned = previousAbandonedState.TryGetValue(group.Key, out bool prev) && prev;

            await repository.UpsertConsumptionPatternAsync(pattern, cancellationToken);

            // If newly abandoned, create inquiry and notify
            if (pattern.IsAbandoned && !wasAbandoned)
            {
                PurchaseEventDto representative = purchases.Last();
                Guid inquiryId = await repository.CreateAbandonedInquiryAsync(
                    userId, representative.ProductId, representative.CustomName, cancellationToken);

                await PublishAbandonedProductNotificationAsync(
                    userId, representative, inquiryId, httpFactory, cancellationToken);
            }
        }
    }

    internal static ProductConsumptionPatternRecord ComputePattern(Guid userId, List<PurchaseEventDto> purchases)
    {
        PurchaseEventDto representative = purchases.Last();

        // Compute gaps between consecutive purchases in days
        List<double> gaps = new List<double>();
        for (int i = 1; i < purchases.Count; i++)
        {
            double days = (purchases[i].PurchasedAt - purchases[i - 1].PurchasedAt).TotalDays;
            gaps.Add(days);
        }

        double? avgDays = null;
        double? stdDevDays = null;
        DateTime? estimatedNext = null;

        if (gaps.Count > 0)
        {
            avgDays = gaps.Average();
            if (gaps.Count > 1)
            {
                double mean = avgDays.Value;
                double variance = gaps.Sum(g => (g - mean) * (g - mean)) / gaps.Count;
                stdDevDays = Math.Sqrt(variance);
            }

            estimatedNext = purchases.Last().PurchasedAt.AddDays(avgDays.Value);
        }

        DateTime lastPurchased = purchases.Last().PurchasedAt;
        double daysSinceLast = (DateTime.UtcNow - lastPurchased).TotalDays;

        bool isAbandoned = false;
        if (purchases.Count > 1 && avgDays.HasValue)
        {
            isAbandoned = daysSinceLast > Math.Max(avgDays.Value * 3.0, 90);
        }
        else if (purchases.Count == 1)
        {
            // Single purchase: abandoned if > 90 days ago
            isAbandoned = daysSinceLast > 90;
        }

        return new ProductConsumptionPatternRecord
        {
            UserId = userId,
            HouseholdId = representative.HouseholdId,
            ProductId = representative.ProductId,
            IngredientId = representative.IngredientId,
            CustomName = representative.CustomName,
            AvgDaysBetweenPurchases = avgDays.HasValue ? (decimal)avgDays.Value : null,
            StdDevDays = stdDevDays.HasValue ? (decimal)stdDevDays.Value : null,
            PurchaseCount = purchases.Count,
            FirstPurchasedAt = purchases.First().PurchasedAt,
            LastPurchasedAt = lastPurchased,
            EstimatedNextPurchaseDate = estimatedNext,
            LowStockAlertDaysAhead = 3,
            IsAbandoned = isAbandoned,
            AbandonedAfterCount = isAbandoned ? purchases.Count : null
        };
    }

    private static string BuildProductKey(Guid? productId, Guid? ingredientId, string? customName)
    {
        if (productId.HasValue)
        {
            return $"p:{productId}";
        }

        if (ingredientId.HasValue)
        {
            return $"i:{ingredientId}";
        }

        return $"c:{customName ?? "unknown"}";
    }

    private async Task PublishAbandonedProductNotificationAsync(
        Guid userId,
        PurchaseEventDto representative,
        Guid inquiryId,
        IHttpClientFactory httpFactory,
        CancellationToken cancellationToken)
    {
        try
        {
            HttpClient notificationClient = httpFactory.CreateClient("notificationservice");
            object payload = new
            {
                UserId = userId,
                Type = "AbandonedProductInquiry",
                Priority = "Normal",
                InquiryId = inquiryId,
                ProductId = representative.ProductId,
                ProductName = representative.CustomName
            };
            await notificationClient.PostAsJsonAsync("/api/notifications/internal", payload, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send abandoned product notification for user {UserId}", userId);
        }
    }
}
