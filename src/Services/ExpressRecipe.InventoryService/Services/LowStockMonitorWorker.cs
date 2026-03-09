using ExpressRecipe.InventoryService.Data;
using System.Net.Http.Json;

namespace ExpressRecipe.InventoryService.Services;

public class LowStockMonitorWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LowStockMonitorWorker> _logger;

    public LowStockMonitorWorker(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<LowStockMonitorWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Low Stock Monitor Worker started");

        int intervalHours = _configuration.GetValue<int>("InventoryIntelligence:LowStockMonitorIntervalHours", 4);
        TimeSpan interval = TimeSpan.FromHours(intervalHours);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessLowStockAsync(stoppingToken);
                await Task.Delay(interval, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error in Low Stock Monitor Worker");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }

        _logger.LogInformation("Low Stock Monitor Worker stopped");
    }

    private async Task ProcessLowStockAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = _serviceProvider.CreateScope();
        IInventoryRepository repository = scope.ServiceProvider.GetRequiredService<IInventoryRepository>();
        IHttpClientFactory httpFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();

        _logger.LogInformation("Processing low stock predictions...");

        // Read configuration once outside the per-user loop
        int daysAhead = _configuration.GetValue<int>("InventoryIntelligence:LowStockDaysAhead", 3);
        bool autoAdd = _configuration.GetValue<bool>("InventoryIntelligence:AutoAddLowStockToList", false);

        List<Guid> userIds = await repository.GetDistinctUserIdsWithPurchaseHistoryAsync(cancellationToken);

        foreach (Guid userId in userIds)
        {
            try
            {
                await ProcessUserLowStockAsync(userId, daysAhead, autoAdd, repository, httpFactory, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing low stock for user {UserId}", userId);
            }
        }

        // Check all active price watch alerts and re-check for deals
        await RecheckPriceWatchAlertsAsync(repository, httpFactory, cancellationToken);

        // Resolve watch alerts where a recent purchase covers the product
        await ResolveAlertsWithRecentPurchasesAsync(repository, cancellationToken);

        _logger.LogInformation("Low stock processing completed");
    }

    private async Task ProcessUserLowStockAsync(
        Guid userId,
        int daysAhead,
        bool autoAdd,
        IInventoryRepository repository,
        IHttpClientFactory httpFactory,
        CancellationToken cancellationToken)
    {
        List<ProductConsumptionPatternDto> lowStockItems = await repository.GetLowStockByPredictionAsync(userId, daysAhead, cancellationToken);

        // Load active alerts once per user to avoid O(patterns × alerts) DB calls
        List<PriceWatchAlertDto> userAlerts = await repository.GetActiveWatchAlertsByUserAsync(userId, cancellationToken);

        foreach (ProductConsumptionPatternDto pattern in lowStockItems)
        {
            if (pattern.ProductId == null)
            {
                continue;
            }

            // Ensure a price watch alert exists for this item
            bool hasAlert = userAlerts.Any(a => a.ProductId == pattern.ProductId);

            if (!hasAlert)
            {
                PriceWatchAlertRecord alertRecord = new PriceWatchAlertRecord
                {
                    UserId = userId,
                    HouseholdId = pattern.HouseholdId,
                    ProductId = pattern.ProductId
                };
                Guid newAlertId = await repository.CreatePriceWatchAlertAsync(alertRecord, cancellationToken);
                // Add a minimal in-memory entry so subsequent patterns see it without another DB call
                userAlerts.Add(new PriceWatchAlertDto { Id = newAlertId, UserId = userId, ProductId = pattern.ProductId, DealFound = false });
            }

            // Check PriceService for active deals
            bool dealFound = false;
            Guid? dealStoreId = null;
            decimal? dealPrice = null;
            DateTime? dealEndsAt = null;

            try
            {
                HttpClient priceClient = httpFactory.CreateClient("priceservice");
                HttpResponseMessage response = await priceClient.GetAsync(
                    $"/api/prices/{pattern.ProductId}/deals/active", cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    ActiveDealResponse? deal = await response.Content.ReadFromJsonAsync<ActiveDealResponse>(cancellationToken: cancellationToken);
                    if (deal != null && deal.HasDeal)
                    {
                        dealFound = true;
                        dealStoreId = deal.StoreId;
                        dealPrice = deal.Price;
                        dealEndsAt = deal.EndsAt;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to check price deals for product {ProductId}", pattern.ProductId);
            }

            if (dealFound && dealStoreId.HasValue && dealPrice.HasValue && dealEndsAt.HasValue)
            {
                // Only notify (and update the alert) when transitioning from no-deal to deal-found
                PriceWatchAlertDto? matchingAlert = userAlerts.FirstOrDefault(a =>
                    a.ProductId == pattern.ProductId && !a.DealFound);

                if (matchingAlert != null)
                {
                    await repository.UpdatePriceWatchDealFoundAsync(
                        matchingAlert.Id, dealStoreId.Value, dealPrice.Value, dealEndsAt.Value, cancellationToken);

                    // Update in-memory state to prevent re-notification this cycle
                    matchingAlert.DealFound = true;

                    await SendNotificationAsync(httpFactory, userId, "LowStockWithDeal", "High", new
                    {
                        UserId = userId,
                        ProductId = pattern.ProductId,
                        CustomName = pattern.CustomName,
                        DealPrice = dealPrice,
                        DealStoreId = dealStoreId
                    }, cancellationToken);
                }
            }
            else
            {
                await SendNotificationAsync(httpFactory, userId, "LowStock", "Normal", new
                {
                    UserId = userId,
                    ProductId = pattern.ProductId,
                    CustomName = pattern.CustomName,
                    EstimatedNextPurchaseDate = pattern.EstimatedNextPurchaseDate
                }, cancellationToken);
            }

            if (autoAdd)
            {
                await AutoAddToShoppingListAsync(httpFactory, userId, pattern, cancellationToken);
            }
        }
    }

    private async Task RecheckPriceWatchAlertsAsync(
        IInventoryRepository repository,
        IHttpClientFactory httpFactory,
        CancellationToken cancellationToken)
    {
        List<PriceWatchAlertDto> activeAlerts = await repository.GetActiveWatchAlertsAsync(cancellationToken);

        foreach (PriceWatchAlertDto alert in activeAlerts)
        {
            if (alert.ProductId == null || alert.DealFound)
            {
                continue;
            }

            try
            {
                HttpClient priceClient = httpFactory.CreateClient("priceservice");
                HttpResponseMessage response = await priceClient.GetAsync(
                    $"/api/prices/{alert.ProductId}/deals/active", cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    ActiveDealResponse? deal = await response.Content.ReadFromJsonAsync<ActiveDealResponse>(cancellationToken: cancellationToken);
                    if (deal != null && deal.HasDeal && deal.StoreId.HasValue && deal.Price.HasValue && deal.EndsAt.HasValue)
                    {
                        // Respect TargetPrice: only trigger when deal is at or below the target
                        bool meetsTarget = alert.TargetPrice == null || deal.Price.Value <= alert.TargetPrice.Value;
                        if (!meetsTarget)
                        {
                            continue;
                        }

                        await repository.UpdatePriceWatchDealFoundAsync(
                            alert.Id, deal.StoreId.Value, deal.Price.Value, deal.EndsAt.Value, cancellationToken);

                        await SendNotificationAsync(httpFactory, alert.UserId, "PriceWatchHit", "High", new
                        {
                            UserId = alert.UserId,
                            ProductId = alert.ProductId,
                            DealPrice = deal.Price,
                            DealStoreId = deal.StoreId,
                            TargetPrice = alert.TargetPrice
                        }, cancellationToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to recheck price for alert {AlertId}", alert.Id);
            }
        }
    }

    private async Task ResolveAlertsWithRecentPurchasesAsync(
        IInventoryRepository repository,
        CancellationToken cancellationToken)
    {
        List<PriceWatchAlertDto> activeAlerts = await repository.GetActiveWatchAlertsAsync(cancellationToken);

        foreach (PriceWatchAlertDto alert in activeAlerts)
        {
            if (alert.ProductId == null)
            {
                continue;
            }

            // Check for a purchase of this product in the last 24 hours
            List<PurchaseEventDto> recentPurchases = await repository.GetPurchaseHistoryAsync(
                alert.UserId, alert.ProductId, 1, cancellationToken);

            if (recentPurchases.Count > 0)
            {
                await repository.ResolvePriceWatchAlertAsync(alert.Id, cancellationToken);
                _logger.LogInformation(
                    "Resolved price watch alert {AlertId} due to recent purchase of product {ProductId}",
                    alert.Id, alert.ProductId);
            }
        }
    }

    private async Task SendNotificationAsync(
        IHttpClientFactory httpFactory,
        Guid userId,
        string type,
        string priority,
        object payload,
        CancellationToken cancellationToken)
    {
        try
        {
            HttpClient notificationClient = httpFactory.CreateClient("notificationservice");
            object body = new
            {
                UserId = userId,
                Type = type,
                Priority = priority,
                Data = payload
            };
            await notificationClient.PostAsJsonAsync("/api/notifications/internal", body, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send {Type} notification for user {UserId}", type, userId);
        }
    }

    private async Task AutoAddToShoppingListAsync(
        IHttpClientFactory httpFactory,
        Guid userId,
        ProductConsumptionPatternDto pattern,
        CancellationToken cancellationToken)
    {
        try
        {
            HttpClient shoppingClient = httpFactory.CreateClient("shoppingservice");
            object payload = new
            {
                UserId = userId,
                HouseholdId = pattern.HouseholdId,
                ProductId = pattern.ProductId,
                IngredientId = pattern.IngredientId,
                CustomName = pattern.CustomName,
                Source = "LowStockPrediction"
            };
            await shoppingClient.PostAsJsonAsync("/api/shopping/lowstock/auto-add", payload, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to auto-add low-stock item for user {UserId}", userId);
        }
    }

    private sealed class ActiveDealResponse
    {
        public bool HasDeal { get; set; }
        public Guid? StoreId { get; set; }
        public decimal? Price { get; set; }
        public DateTime? EndsAt { get; set; }
    }
}
