using ExpressRecipe.InventoryService.Data;
using System.Net.Http.Json;

namespace ExpressRecipe.InventoryService.Services;

public class ExpirationAlertWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ExpirationAlertWorker> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromHours(6);

    public ExpirationAlertWorker(IServiceProvider serviceProvider, ILogger<ExpirationAlertWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Expiration Alert Worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessExpirationAlertsAsync(stoppingToken);
                await Task.Delay(_interval, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error in Expiration Alert Worker");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }

        _logger.LogInformation("Expiration Alert Worker stopped");
    }

    private async Task ProcessExpirationAlertsAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = _serviceProvider.CreateScope();
        IInventoryRepository repository = scope.ServiceProvider.GetRequiredService<IInventoryRepository>();
        IHttpClientFactory httpFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();

        _logger.LogInformation("Processing expiration alerts...");

        List<Guid> userIds = await repository.GetDistinctUserIdsWithInventoryAsync(cancellationToken);
        _logger.LogInformation("Found {UserCount} users with expiring inventory", userIds.Count);

        foreach (Guid userId in userIds)
        {
            try
            {
                await ProcessUserExpirationAlertsAsync(userId, repository, httpFactory, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing expiration alerts for user {UserId}", userId);
            }
        }

        _logger.LogInformation("Expiration alerts processed successfully");
    }

    private async Task ProcessUserExpirationAlertsAsync(
        Guid userId,
        IInventoryRepository repository,
        IHttpClientFactory httpFactory,
        CancellationToken cancellationToken)
    {
        // Get all items expiring within 14 days (covers Warning + Critical + Expired)
        List<InventoryItemDto> expiringItems = await repository.GetExpiringItemsAsync(userId, 14);

        // Load active alerts once for this user to avoid O(n) DB calls per item
        List<ExpirationAlertDto> existingAlerts = await repository.GetActiveAlertsAsync(userId);

        foreach (InventoryItemDto item in expiringItems)
        {
            if (item.ExpirationDate == null)
            {
                continue;
            }

            int daysUntilExpiration = (int)(item.ExpirationDate.Value - DateTime.UtcNow).TotalDays;
            string alertType = DetermineAlertType(daysUntilExpiration);

            // Skip if an alert for this item at this severity already exists (de-duplicate)
            bool alreadyExists = existingAlerts.Any(a =>
                a.InventoryItemId == item.Id &&
                a.AlertType == alertType &&
                !a.IsDismissed);

            if (alreadyExists)
            {
                continue;
            }

            // Insert a single alert row for this item at the determined severity
            await repository.CreateSingleExpirationAlertAsync(userId, item.Id, alertType, daysUntilExpiration);

            // Publish notification event
            await PublishExpirationNotificationAsync(userId, item, alertType, daysUntilExpiration, httpFactory, cancellationToken);
        }
    }

    private static string DetermineAlertType(int daysUntilExpiration)
    {
        if (daysUntilExpiration <= 0)
        {
            return "Expired";
        }

        if (daysUntilExpiration <= 7)
        {
            return "Critical";
        }

        return "Warning";
    }

    // Internal for testing
    internal static string DetermineAlertTypeForTest(int daysUntilExpiration)
        => DetermineAlertType(daysUntilExpiration);

    private async Task PublishExpirationNotificationAsync(
        Guid userId,
        InventoryItemDto item,
        string alertType,
        int daysUntilExpiration,
        IHttpClientFactory httpFactory,
        CancellationToken cancellationToken)
    {
        string priority = alertType switch
        {
            "Expired" => "Urgent",
            "Critical" => "High",
            _ => "Normal"
        };

        try
        {
            HttpClient notificationClient = httpFactory.CreateClient("notificationservice");
            object payload = new
            {
                UserId = userId,
                Type = "Expiration",
                Priority = priority,
                ItemId = item.Id,
                ItemName = item.CustomName ?? item.ProductName ?? "Unknown item",
                AlertType = alertType,
                DaysUntilExpiration = daysUntilExpiration
            };
            await notificationClient.PostAsJsonAsync("/api/notifications/internal", payload, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send expiration notification for item {ItemId}", item.Id);
        }
    }
}
