using System.Net.Http.Json;
using Microsoft.Data.SqlClient;

namespace ExpressRecipe.MealPlanningService.Workers;

public sealed class MealCookNotificationWorker : BackgroundService
{
    private readonly string _connectionString;
    private readonly IHttpClientFactory _http;
    private readonly ILogger<MealCookNotificationWorker> _logger;
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(5);

    public MealCookNotificationWorker(string connectionString, IHttpClientFactory http, ILogger<MealCookNotificationWorker> logger)
    {
        _connectionString = connectionString;
        _http             = http;
        _logger           = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await CheckAndNotifyAsync(stoppingToken); }
            catch (Exception ex) { _logger.LogError(ex, "MealCookNotificationWorker failed"); }
            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task CheckAndNotifyAsync(CancellationToken ct)
    {
        const string sql = @"
            SELECT pm.Id, pm.MealPlanId, mp.UserId,
                   pm.RecipeId, pm.CustomMealName, pm.MealType,
                   pm.PlannedDate, pm.PlannedTime, pm.Servings,
                   pm.NotificationSent, pm.FreezerReminderSent,
                   msc.TargetTime, msc.NotifyEnabled, msc.NotifyMinutesBefore,
                   msc.FreezerReminderEnabled, msc.FreezerReminderHoursBefore
            FROM PlannedMeal pm
            JOIN MealPlan mp ON mp.Id = pm.MealPlanId
            LEFT JOIN MealScheduleConfig msc ON msc.UserId = mp.UserId AND msc.MealType = pm.MealType
            WHERE pm.PlannedDate = CAST(GETUTCDATE() AS DATE) AND pm.IsCompleted = 0
              AND (pm.NotificationSent = 0 OR pm.FreezerReminderSent = 0) AND mp.IsDeleted = 0";

        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        await using SqlDataReader r = await cmd.ExecuteReaderAsync(ct);

        List<PendingMealNotification> pending = new();
        while (await r.ReadAsync(ct))
        {
            pending.Add(new PendingMealNotification
            {
                MealId                     = r.GetGuid(0),
                PlanId                     = r.GetGuid(1),
                UserId                     = r.GetGuid(2),
                RecipeId                   = r.IsDBNull(3) ? null : r.GetGuid(3),
                MealName                   = r.IsDBNull(4) ? r.GetString(5) : r.GetString(4),
                MealType                   = r.GetString(5),
                PlannedDate                = DateOnly.FromDateTime(r.GetDateTime(6)),
                PlannedTime                = r.IsDBNull(7) ? null : TimeOnly.FromTimeSpan(r.GetTimeSpan(7)),
                NotificationSent           = r.GetBoolean(9),
                FreezerReminderSent        = r.GetBoolean(10),
                ScheduleTargetTime         = r.IsDBNull(11) ? null : TimeOnly.FromTimeSpan(r.GetTimeSpan(11)),
                NotifyEnabled              = !r.IsDBNull(12) && r.GetBoolean(12),
                NotifyMinutesBefore        = r.IsDBNull(13) ? 30 : r.GetInt32(13),
                FreezerReminderEnabled     = !r.IsDBNull(14) && r.GetBoolean(14),
                FreezerReminderHoursBefore = r.IsDBNull(15) ? 8  : r.GetInt32(15)
            });
        }
        await r.CloseAsync();

        DateTime now = DateTime.Now;
        foreach (PendingMealNotification meal in pending)
        {
            TimeOnly effective = meal.PlannedTime ?? meal.ScheduleTargetTime ?? new TimeOnly(18, 0);
            DateTime mealAt    = meal.PlannedDate.ToDateTime(effective, DateTimeKind.Local);

            if (!meal.NotificationSent && meal.NotifyEnabled)
            {
                DateTime notifyAt = mealAt.AddMinutes(-meal.NotifyMinutesBefore);
                if (now >= notifyAt && now < mealAt)
                {
                    try
                    {
                        await SendNotificationAsync(meal.UserId, "CookStartReminder",
                            "⏰ Time to start cooking",
                            $"Start preparing {meal.MealName} — {meal.MealType} is at {effective:h:mm tt}", ct);
                        await MarkFlagAsync(meal.MealId, "NotificationSent", conn, ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send CookStartReminder notification for meal {MealId}", meal.MealId);
                    }
                }
            }

            if (!meal.FreezerReminderSent && meal.FreezerReminderEnabled)
            {
                DateTime freezerAt = mealAt.AddHours(-meal.FreezerReminderHoursBefore);
                if (now >= freezerAt && now < mealAt)
                {
                    try
                    {
                        await SendNotificationAsync(meal.UserId, "FreezerToFridgeReminder",
                            "🥶 Move to fridge",
                            $"Move ingredients for {meal.MealName} from freezer to fridge now", ct);
                        await MarkFlagAsync(meal.MealId, "FreezerReminderSent", conn, ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send FreezerToFridgeReminder notification for meal {MealId}", meal.MealId);
                    }
                }
            }
        }
    }

    private async Task SendNotificationAsync(Guid userId, string type, string title, string message, CancellationToken ct)
    {
        HttpResponseMessage response = await _http.CreateClient("NotificationService")
            .PostAsJsonAsync("/api/notifications/internal/create", new { userId, type, title, message }, ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("NotificationService returned {StatusCode} for user {UserId} type {Type}",
                response.StatusCode, userId, type);
        }
    }

    private static async Task MarkFlagAsync(Guid mealId, string column, SqlConnection conn, CancellationToken ct)
    {
        // Validate column name against whitelist to prevent SQL injection
        if (column is not "NotificationSent" and not "FreezerReminderSent")
        {
            throw new ArgumentException($"Invalid column name: {column}", nameof(column));
        }
        await using SqlCommand cmd = new($"UPDATE PlannedMeal SET {column} = 1 WHERE Id = @Id", conn);
        cmd.Parameters.AddWithValue("@Id", mealId);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}

internal sealed class PendingMealNotification
{
    public Guid     MealId                     { get; init; }
    public Guid     PlanId                     { get; init; }
    public Guid     UserId                     { get; init; }
    public Guid?    RecipeId                   { get; init; }
    public string   MealName                   { get; init; } = string.Empty;
    public string   MealType                   { get; init; } = string.Empty;
    public DateOnly PlannedDate                { get; init; }
    public TimeOnly? PlannedTime               { get; init; }
    public bool     NotificationSent           { get; init; }
    public bool     FreezerReminderSent        { get; init; }
    public TimeOnly? ScheduleTargetTime        { get; init; }
    public bool     NotifyEnabled              { get; init; }
    public int      NotifyMinutesBefore        { get; init; }
    public bool     FreezerReminderEnabled     { get; init; }
    public int      FreezerReminderHoursBefore { get; init; }
}
