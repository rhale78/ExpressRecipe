using System.Net.Http.Json;
using ExpressRecipe.MealPlanningService.Data;
using Microsoft.Data.SqlClient;

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

    public CookingRatingPromptWorker(
        IServiceProvider serviceProvider,
        ILogger<CookingRatingPromptWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
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
        IConfiguration config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        string connectionString = config.GetConnectionString("mealplandb")
            ?? throw new InvalidOperationException("Database connection string 'mealplandb' not found");

        IHttpClientFactory factory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
        string notificationUrl = config["Services:NotificationService"] ?? "http://notificationservice";

        List<RatingPromptRow> rows = await GetUnratedCookingHistoryAsync(connectionString, ct);

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
                NotificationPayload payload = new()
                {
                    UserId    = row.UserId,
                    Type      = "CookingRatingPrompt",
                    Title     = $"How did {row.RecipeName} turn out?",
                    Message   = "Tap to rate your meal and help us improve your suggestions.",
                    ActionUrl = $"/mealplan/rate/{row.Id}"
                };

                HttpResponseMessage response = await client.PostAsJsonAsync(
                    $"{notificationUrl}/api/notification/internal",
                    payload,
                    ct);

                if (response.IsSuccessStatusCode)
                {
                    await MarkRatingPromptSentAsync(connectionString, row.Id, ct);
                    _logger.LogDebug("Sent rating prompt for history {HistoryId}", row.Id);
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

    private static async Task<List<RatingPromptRow>> GetUnratedCookingHistoryAsync(
        string connectionString, CancellationToken ct)
    {
        const string sql = @"
            SELECT Id, UserId, RecipeName
            FROM CookingHistory
            WHERE Rating IS NULL
              AND CookedAt < DATEADD(minute, -30, GETUTCDATE())
              AND RatingPromptSent = 0";

        await using SqlConnection connection = new(connectionString);
        await connection.OpenAsync(ct);

        await using SqlCommand command = new(sql, connection);

        List<RatingPromptRow> rows = new();
        await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new RatingPromptRow(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetString(2)));
        }

        return rows;
    }

    private static async Task MarkRatingPromptSentAsync(
        string connectionString, Guid historyId, CancellationToken ct)
    {
        const string sql = "UPDATE CookingHistory SET RatingPromptSent = 1 WHERE Id = @HistoryId";

        await using SqlConnection connection = new(connectionString);
        await connection.OpenAsync(ct);

        await using SqlCommand command = new(sql, connection);
        command.Parameters.AddWithValue("@HistoryId", historyId);
        await command.ExecuteNonQueryAsync(ct);
    }

    private record RatingPromptRow(Guid Id, Guid UserId, string RecipeName);

    private class NotificationPayload
    {
        public Guid UserId { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? ActionUrl { get; set; }
    }
}
