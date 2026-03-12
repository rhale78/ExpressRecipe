using ExpressRecipe.RecallService.Data;
using ExpressRecipe.RecallService.Services;
using System.Net.Http.Json;

namespace ExpressRecipe.RecallService.Services;

public class RecallMonitorWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RecallMonitorWorker> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public RecallMonitorWorker(IServiceProvider serviceProvider, ILogger<RecallMonitorWorker> logger, IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Recall Monitor Worker started");

        // Check if auto-import is enabled (allows disabling at config level)
        var autoImport = _configuration.GetValue<bool>("RecallImport:AutoImport", true);
        if (!autoImport)
        {
            _logger.LogInformation("RecallMonitorWorker: auto-import is disabled in configuration. Worker will not run.");
            return;
        }

        // Derive check interval from configuration; minimum 1 minute to avoid hammering external APIs
        var intervalHours = _configuration.GetValue<double>("RecallImport:ImportIntervalHours", 1.0);
        var interval = TimeSpan.FromHours(Math.Max(intervalHours, 1.0 / 60));

        // Wait 1 minute before first run to allow services to initialize
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckForNewRecallsAsync(interval, stoppingToken);

                // Inter-item delay to prevent overwhelming CPU/disk between import checks (configured via RecallImport:BatchDelayMs)
                // Note: this cool-down is applied after each import run, before the next regular interval begins
                var batchDelayMs = _configuration.GetValue<int>("RecallImport:BatchDelayMs", 0);
                if (batchDelayMs > 0)
                    await Task.Delay(batchDelayMs, stoppingToken);

                await Task.Delay(interval, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error in Recall Monitor Worker");
                await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
            }
        }

        _logger.LogInformation("Recall Monitor Worker stopped");
    }

    private async Task CheckForNewRecallsAsync(TimeSpan interval, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRecallRepository>();
        var importService = scope.ServiceProvider.GetService<FDARecallImportService>();

        _logger.LogInformation("Checking for new recalls...");

        if (importService != null)
        {
            try
            {
                var fdaLimit = _configuration.GetValue<int>("RecallImport:FdaRecallLimit", 50);

                // Import recent recalls from FDA
                var result = await importService.ImportRecentRecallsAsync(limit: fdaLimit);
                _logger.LogInformation("Imported {Count} new FDA recalls, {Errors} errors",
                    result.SuccessCount, result.FailureCount);

                // Import meat/poultry recalls from FDA (includes USDA-regulated products)
                var meatPoultryResult = await importService.ImportMeatPoultryRecallsFromFDAAsync(limit: fdaLimit);
                _logger.LogInformation("Imported {Count} meat/poultry recalls, {Errors} errors",
                    meatPoultryResult.SuccessCount, meatPoultryResult.FailureCount);

                // Note: ImportUSDARecallsAsync() is deprecated - no public USDA API exists
                // Meat/poultry recalls are now imported from FDA API above

                // TODO: Check user subscriptions and send alerts
                // This would require cross-service communication with NotificationService
                if (result.SuccessCount > 0 || meatPoultryResult.SuccessCount > 0)
                {
                    await NotifyAffectedUsersAsync(repository, interval, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing recalls");
            }
        }

        _logger.LogInformation("Recall check completed");
    }

    private async Task NotifyAffectedUsersAsync(IRecallRepository repository, TimeSpan interval, CancellationToken cancellationToken)
    {
        // Get recent recalls (last 24 hours) to find newly imported ones
        List<RecallDto> recentRecalls = await repository.GetRecentRecallsAsync(limit: 20);
        // Filter to recalls created since the last worker interval to avoid duplicate notifications
        var newRecalls = recentRecalls.Where(r => r.CreatedAt >= DateTime.UtcNow.Add(-interval)).ToList();

        if (newRecalls.Count == 0)
        {
            return;
        }

        _logger.LogInformation("Sending notifications for {Count} new recalls", newRecalls.Count);

        HttpClient notificationClient = _httpClientFactory.CreateClient("notificationservice");

        foreach (RecallDto recall in newRecalls)
        {
            try
            {
                // Get users subscribed to alerts for this recall
                List<Guid> affectedUsers = await repository.GetAffectedUsersAsync(recall.Id);

                if (affectedUsers.Count == 0)
                {
                    continue;
                }

                string priority = recall.Severity?.ToUpperInvariant() switch
                {
                    "HIGH" or "CLASS I" => "Urgent",
                    "MEDIUM" or "CLASS II" => "High",
                    _ => "Normal"
                };

                foreach (Guid userId in affectedUsers)
                {
                    // Create a recall alert row
                    await repository.CreateRecallAlertAsync(
                        userId,
                        recall.Id,
                        matchType: "Subscription",
                        matchedValue: recall.Title,
                        isAcknowledged: false);

                    // Send notification via NotificationService
                    var payload = new
                    {
                        UserId = userId,
                        Type = "RecallAlert",
                        Priority = priority,
                        Title = $"Food Recall Alert: {recall.Title}",
                        Message = $"A {recall.Severity} recall has been issued. {recall.Reason ?? recall.Description}",
                        RelatedEntityType = "Recall",
                        RelatedEntityId = recall.Id
                    };

                    try
                    {
                        await notificationClient.PostAsJsonAsync(
                            "/api/Notification/internal",
                            payload,
                            cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to send recall notification to user {UserId} for recall {RecallId}", userId, recall.Id);
                    }
                }

                _logger.LogInformation("Notified {UserCount} users about recall {RecallId}", affectedUsers.Count, recall.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing notifications for recall {RecallId}", recall.Id);
            }
        }
    }
}
