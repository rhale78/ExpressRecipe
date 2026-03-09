using System.Net.Http.Json;
using ExpressRecipe.MealPlanningService.Data;
using ExpressRecipe.MealPlanningService.Logging;

namespace ExpressRecipe.MealPlanningService.Workers;

/// <summary>
/// Finds CookingHistory rows where Rating IS NULL, CookedAt is more than 30 minutes ago,
/// and RatingPromptSent=0, then POSTs a CookingRatingPrompt notification to NotificationService.
/// Runs every 15 minutes.
/// </summary>
public class CookingRatingPromptWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CookingRatingPromptWorker> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(15);
    private readonly string _notificationUrl;

    public CookingRatingPromptWorker(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<CookingRatingPromptWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _notificationUrl = configuration["Services:NotificationService"] ?? "http://notificationservice";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("CookingRatingPromptWorker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessRatingPromptsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error in CookingRatingPromptWorker");
            }

            await Task.Delay(_interval, stoppingToken);
        }

        _logger.LogInformation("CookingRatingPromptWorker stopped");
    }

    private async Task ProcessRatingPromptsAsync(CancellationToken ct)
    {
        using IServiceScope scope = _serviceProvider.CreateScope();
        IMealPlanningRepository repository = scope.ServiceProvider.GetRequiredService<IMealPlanningRepository>();
        IHttpClientFactory factory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();

        List<RatingPromptRow> rows = await repository.GetUnratedCookingHistoryAsync(ct);

        if (rows.Count == 0)
        {
            return;
        }

        _logger.LogInformation("Sending {Count} cooking rating prompt notifications", rows.Count);

        using HttpClient client = factory.CreateClient("NotificationService");

        foreach (RatingPromptRow row in rows)
        {
            try
            {
                _logger.LogSendingRatingPrompt(row.UserId, row.Id, row.RecipeName);
                NotificationPayload payload = new()
                {
                    UserId    = row.UserId,
                    Type      = "CookingRatingPrompt",
                    Title     = $"How did {row.RecipeName} turn out?",
                    Message   = "Tap to rate your meal and help us improve your suggestions.",
                    ActionUrl = $"/mealplan/rate/{row.Id}"
                };

                HttpResponseMessage response = await client.PostAsJsonAsync(
                    $"{_notificationUrl}/api/notification/internal",
                    payload,
                    ct);

                if (response.IsSuccessStatusCode)
                {
                    await repository.MarkRatingPromptSentAsync(row.Id, ct);
                }
                else
                {
                    _logger.LogWarning(
                        "NotificationService returned {Status} for history {HistoryId}",
                        response.StatusCode, row.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send rating prompt for history {HistoryId}", row.Id);
                // Leave RatingPromptSent=0 to retry next cycle
            }
        }
    }

    private class NotificationPayload
    {
        public Guid UserId { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? ActionUrl { get; set; }
    }
}
