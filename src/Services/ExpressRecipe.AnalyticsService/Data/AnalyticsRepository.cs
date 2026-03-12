using ExpressRecipe.Data.Common;
using ExpressRecipe.Shared.Services;
using System.Text.Json;

namespace ExpressRecipe.AnalyticsService.Data;

public class AnalyticsRepository : SqlHelper, IAnalyticsRepository
{
    private readonly HybridCacheService? _cache;
    private const string CachePrefix = "analytics:";

    public AnalyticsRepository(string connectionString, HybridCacheService? cache = null)
        : base(connectionString)
    {
        _cache = cache;
    }

    // ── User Events ──────────────────────────────────────────────────────────

    public async Task<Guid> TrackEventAsync(Guid userId, string eventType, string eventName,
        Dictionary<string, string> properties, string? sessionId, string? deviceId)
    {
        const string sql = @"
            INSERT INTO UserEvent (UserId, EventType, EventCategory, Metadata, SessionId, DeviceInfo, Timestamp)
            OUTPUT INSERTED.Id
            VALUES (@UserId, @EventType, @EventCategory, @Metadata, @SessionId, @DeviceInfo, GETUTCDATE())";

        return await ExecuteScalarAsync<Guid>(sql,
            CreateParameter("@UserId", userId),
            CreateParameter("@EventType", eventType),
            CreateParameter("@EventCategory", eventName),
            CreateParameter("@Metadata", properties.Count > 0 ? JsonSerializer.Serialize(properties) : null),
            CreateParameter("@SessionId", sessionId),
            CreateParameter("@DeviceInfo", deviceId))!;
    }

    public async Task<List<UserEventDto>> GetUserEventsAsync(Guid userId, DateTime? startDate = null,
        DateTime? endDate = null, int limit = 100)
    {
        const string sql = @"
            SELECT TOP (@Limit) Id, UserId, EventType, EventCategory, Metadata, SessionId, DeviceInfo, Timestamp
            FROM UserEvent
            WHERE UserId = @UserId
              AND (@Start IS NULL OR Timestamp >= @Start)
              AND (@End IS NULL OR Timestamp <= @End)
            ORDER BY Timestamp DESC";

        return await ExecuteReaderAsync(sql, MapUserEvent,
            CreateParameter("@Limit", limit),
            CreateParameter("@UserId", userId),
            CreateParameter("@Start", startDate),
            CreateParameter("@End", endDate));
    }

    public async Task<List<UserEventDto>> GetEventsByTypeAsync(string eventType, DateTime startDate, DateTime endDate)
    {
        const string sql = @"
            SELECT Id, UserId, EventType, EventCategory, Metadata, SessionId, DeviceInfo, Timestamp
            FROM UserEvent
            WHERE EventType = @EventType AND Timestamp BETWEEN @Start AND @End
            ORDER BY Timestamp DESC";

        return await ExecuteReaderAsync(sql, MapUserEvent,
            CreateParameter("@EventType", eventType),
            CreateParameter("@Start", startDate),
            CreateParameter("@End", endDate));
    }

    public async Task<Dictionary<string, int>> GetEventCountsAsync(Guid userId, DateTime startDate, DateTime endDate)
    {
        const string sql = @"
            SELECT EventType, COUNT(*) AS Cnt
            FROM UserEvent
            WHERE UserId = @UserId AND Timestamp BETWEEN @Start AND @End
            GROUP BY EventType";

        var rows = await ExecuteReaderAsync(sql,
            r => (GetString(r, "EventType") ?? string.Empty, r.GetInt32(r.GetOrdinal("Cnt"))),
            CreateParameter("@UserId", userId),
            CreateParameter("@Start", startDate),
            CreateParameter("@End", endDate));

        return rows.ToDictionary(x => x.Item1, x => x.Item2);
    }

    // ── Usage Statistics ──────────────────────────────────────────────────────

    public async Task UpdateUsageStatsAsync(Guid userId, DateTime date, int sessionCount, int actionCount, int minutesActive)
    {
        const string sql = @"
            MERGE UsageStatistics AS target
            USING (SELECT @UserId AS UserId, @MetricType AS MetricType, @PeriodType AS PeriodType, @PeriodStart AS PeriodStart) AS src
                ON target.UserId = src.UserId
               AND target.MetricType = src.MetricType
               AND target.PeriodType = src.PeriodType
               AND target.PeriodStart = src.PeriodStart
            WHEN MATCHED THEN
                UPDATE SET MetricValue = MetricValue + @Value, CalculatedAt = GETUTCDATE()
            WHEN NOT MATCHED THEN
                INSERT (UserId, MetricType, MetricValue, PeriodType, PeriodStart, PeriodEnd, CalculatedAt)
                VALUES (@UserId, @MetricType, @Value, @PeriodType, @PeriodStart, @PeriodEnd, GETUTCDATE());";

        var periodStart = date.Date;
        var periodEnd = date.Date.AddDays(1).AddTicks(-1);

        foreach (var (metric, value) in new[] {
            ("Sessions", (decimal)sessionCount),
            ("Actions", (decimal)actionCount),
            ("MinutesActive", (decimal)minutesActive)
        })
        {
            await ExecuteNonQueryAsync(sql,
                CreateParameter("@UserId", userId),
                CreateParameter("@MetricType", metric),
                CreateParameter("@Value", value),
                CreateParameter("@PeriodType", "Daily"),
                CreateParameter("@PeriodStart", periodStart),
                CreateParameter("@PeriodEnd", periodEnd));
        }

        // Evict summary cache for this user so next read reflects the new stats
        if (_cache != null)
        {
            foreach (var period in new[] { "day", "week", "month", "year" })
                await _cache.RemoveAsync($"{CachePrefix}summary:{userId}:{period}");
        }
    }

    public async Task<List<UsageStatisticsDto>> GetUserUsageStatsAsync(Guid userId, DateTime startDate, DateTime endDate)
    {
        // The table stores one row per (UserId, PeriodStart, MetricType) — we need to pivot
        // multiple metric rows per period into a single UsageStatisticsDto per day.
        const string sql = @"
            SELECT Id, UserId, MetricType, MetricValue, PeriodType, PeriodStart, PeriodEnd, CalculatedAt
            FROM UsageStatistics
            WHERE UserId = @UserId AND PeriodStart >= @Start AND PeriodEnd <= @End
            ORDER BY PeriodStart DESC";

        var rows = await ExecuteReaderAsync(sql,
            r => new
            {
                Id = GetGuid(r, "Id"),
                UserId = GetGuid(r, "UserId"),
                MetricType = GetString(r, "MetricType") ?? string.Empty,
                MetricValue = (decimal)r["MetricValue"],
                PeriodStart = (DateTime)r["PeriodStart"]
            },
            CreateParameter("@UserId", userId),
            CreateParameter("@Start", startDate),
            CreateParameter("@End", endDate));

        // Group metric rows by period date and aggregate into one DTO per day
        return rows
            .GroupBy(r => r.PeriodStart.Date)
            .Select(g =>
            {
                var dto = new UsageStatisticsDto
                {
                    Id = g.First().Id,
                    UserId = g.First().UserId,
                    Date = g.Key
                };
                foreach (var row in g)
                {
                    var v = (int)row.MetricValue;
                    switch (row.MetricType)
                    {
                        case "Sessions": dto.SessionCount = v; break;
                        case "Actions": dto.ActionCount = v; break;
                        case "MinutesActive": dto.MinutesActive = v; break;
                        case "RecipesViewed": dto.RecipesViewed = v; break;
                        case "ProductsScanned": dto.ProductsScanned = v; break;
                        case "MealsPlanned": dto.MealsPlanned = v; break;
                    }
                }
                return dto;
            })
            .OrderByDescending(d => d.Date)
            .ToList();
    }

    public async Task<UsageSummaryDto> GetUsageSummaryAsync(Guid userId, string period)
    {
        if (_cache != null)
        {
            return await _cache.GetOrSetAsync(
                $"{CachePrefix}summary:{userId}:{period}",
                async (ct) => await GetUsageSummaryFromDbAsync(userId, period),
                expiration: TimeSpan.FromMinutes(10))
                ?? new UsageSummaryDto { UserId = userId, Period = period };
        }

        return await GetUsageSummaryFromDbAsync(userId, period);
    }

    private async Task<UsageSummaryDto> GetUsageSummaryFromDbAsync(Guid userId, string period)
    {
        var (start, end) = GetPeriodRange(period);
        const string sql = @"
            SELECT MetricType, SUM(MetricValue) AS Total
            FROM UsageStatistics
            WHERE UserId = @UserId AND PeriodStart >= @Start AND PeriodEnd <= @End AND PeriodType = 'Daily'
            GROUP BY MetricType";

        var rows = await ExecuteReaderAsync(sql,
            r => (GetString(r, "MetricType") ?? string.Empty, (int)(decimal)r["Total"]),
            CreateParameter("@UserId", userId),
            CreateParameter("@Start", start),
            CreateParameter("@End", end));

        var summary = new UsageSummaryDto { UserId = userId, Period = period };
        foreach (var (metric, value) in rows)
        {
            switch (metric)
            {
                case "Sessions": summary.TotalSessions = value; break;
                case "Actions": summary.TotalActions = value; break;
                case "MinutesActive": summary.TotalMinutes = value; break;
            }
        }
        return summary;
    }

    // ── Pattern Detection ─────────────────────────────────────────────────────

    public async Task<Guid> RecordPatternAsync(Guid userId, string patternType, string description,
        decimal confidence, Dictionary<string, string> metadata)
    {
        const string sql = @"
            INSERT INTO PatternDetection (UserId, PatternType, PatternDescription, Confidence, Evidence, DetectedAt, IsActive, LastSeenAt)
            OUTPUT INSERTED.Id
            VALUES (@UserId, @PatternType, @Desc, @Confidence, @Evidence, GETUTCDATE(), 1, GETUTCDATE())";

        return await ExecuteScalarAsync<Guid>(sql,
            CreateParameter("@UserId", userId),
            CreateParameter("@PatternType", patternType),
            CreateParameter("@Desc", description),
            CreateParameter("@Confidence", confidence),
            CreateParameter("@Evidence", metadata.Count > 0 ? JsonSerializer.Serialize(metadata) : null))!;
    }

    public async Task<List<PatternDetectionDto>> GetUserPatternsAsync(Guid userId, string? patternType = null)
    {
        const string sql = @"
            SELECT Id, UserId, PatternType, PatternDescription, Confidence, Evidence, DetectedAt
            FROM PatternDetection
            WHERE UserId = @UserId AND IsActive = 1
              AND (@PatternType IS NULL OR PatternType = @PatternType)
            ORDER BY DetectedAt DESC";

        return await ExecuteReaderAsync(sql, MapPattern,
            CreateParameter("@UserId", userId),
            CreateParameter("@PatternType", patternType));
    }

    public async Task<List<PatternDetectionDto>> GetGlobalPatternsAsync(string patternType, int limit = 50)
    {
        const string sql = @"
            SELECT TOP (@Limit) Id, UserId, PatternType, PatternDescription, Confidence, Evidence, DetectedAt
            FROM PatternDetection
            WHERE PatternType = @PatternType AND IsActive = 1
            ORDER BY Confidence DESC, DetectedAt DESC";

        return await ExecuteReaderAsync(sql, MapPattern,
            CreateParameter("@Limit", limit),
            CreateParameter("@PatternType", patternType));
    }

    // ── Insights ───────────────────────────────────────────────────────────────

    public async Task<Guid> CreateInsightAsync(Guid userId, string insightType, string title,
        string description, string category, decimal importance, Dictionary<string, string> data)
    {
        const string sql = @"
            INSERT INTO Insight (UserId, InsightType, Title, Message, Priority, ActionableItem, IsViewed, IsDismissed, GeneratedAt)
            OUTPUT INSERTED.Id
            VALUES (@UserId, @InsightType, @Title, @Message, @Priority, @Data, 0, 0, GETUTCDATE())";

        return await ExecuteScalarAsync<Guid>(sql,
            CreateParameter("@UserId", userId),
            CreateParameter("@InsightType", insightType),
            CreateParameter("@Title", title),
            CreateParameter("@Message", description),
            CreateParameter("@Priority", category),
            CreateParameter("@Data", data.Count > 0 ? JsonSerializer.Serialize(data) : null))!;
    }

    public async Task<List<InsightDto>> GetUserInsightsAsync(Guid userId, bool unreadOnly = false)
    {
        const string sql = @"
            SELECT Id, UserId, InsightType, Title, Message, Priority, IsViewed, IsDismissed, GeneratedAt
            FROM Insight
            WHERE UserId = @UserId AND IsDismissed = 0
              AND (@UnreadOnly = 0 OR IsViewed = 0)
            ORDER BY GeneratedAt DESC";

        return await ExecuteReaderAsync(sql, MapInsight,
            CreateParameter("@UserId", userId),
            CreateParameter("@UnreadOnly", unreadOnly ? 1 : 0));
    }

    public async Task<InsightDto?> GetInsightAsync(Guid insightId)
    {
        const string sql = @"
            SELECT Id, UserId, InsightType, Title, Message, Priority, IsViewed, IsDismissed, GeneratedAt
            FROM Insight WHERE Id = @Id";

        var results = await ExecuteReaderAsync(sql, MapInsight, CreateParameter("@Id", insightId));
        return results.FirstOrDefault();
    }

    public async Task MarkInsightAsReadAsync(Guid insightId)
    {
        const string sql = "UPDATE Insight SET IsViewed = 1, ViewedAt = GETUTCDATE() WHERE Id = @Id";
        await ExecuteNonQueryAsync(sql, CreateParameter("@Id", insightId));
    }

    public async Task DismissInsightAsync(Guid insightId)
    {
        const string sql = "UPDATE Insight SET IsDismissed = 1, DismissedAt = GETUTCDATE() WHERE Id = @Id";
        await ExecuteNonQueryAsync(sql, CreateParameter("@Id", insightId));
    }

    // ── Aggregate Metrics ─────────────────────────────────────────────────────

    public async Task SaveAggregateMetricAsync(string metricName, string aggregationType,
        DateTime periodStart, DateTime periodEnd, decimal value, Dictionary<string, string>? dimensions)
    {
        const string sql = @"
            MERGE AggregateMetrics AS target
            USING (SELECT @MetricName AS MetricName, @AggType AS AggType, @PeriodStart AS PeriodStart) AS src
                ON target.MetricName = src.MetricName
               AND target.AggregationType = src.AggType
               AND target.PeriodStart = src.PeriodStart
            WHEN MATCHED THEN
                UPDATE SET MetricValue = @Value, Dimensions = @Dimensions, CalculatedAt = GETUTCDATE()
            WHEN NOT MATCHED THEN
                INSERT (MetricName, MetricValue, Dimensions, AggregationType, PeriodStart, PeriodEnd, CalculatedAt)
                VALUES (@MetricName, @Value, @Dimensions, @AggType, @PeriodStart, @PeriodEnd, GETUTCDATE());";

        await ExecuteNonQueryAsync(sql,
            CreateParameter("@MetricName", metricName),
            CreateParameter("@AggType", aggregationType),
            CreateParameter("@PeriodStart", periodStart),
            CreateParameter("@PeriodEnd", periodEnd),
            CreateParameter("@Value", value),
            CreateParameter("@Dimensions", dimensions?.Count > 0 ? JsonSerializer.Serialize(dimensions) : null));
    }

    public async Task<List<AggregateMetricDto>> GetMetricsAsync(string metricName, DateTime startDate,
        DateTime endDate, Dictionary<string, string>? filters = null)
    {
        const string sql = @"
            SELECT Id, MetricName, AggregationType, PeriodStart, PeriodEnd, MetricValue, Dimensions, CalculatedAt
            FROM AggregateMetrics
            WHERE MetricName = @MetricName AND PeriodStart >= @Start AND PeriodEnd <= @End
            ORDER BY PeriodStart DESC";

        return await ExecuteReaderAsync(sql,
            r => new AggregateMetricDto
            {
                Id = GetGuid(r, "Id"),
                MetricName = GetString(r, "MetricName") ?? string.Empty,
                AggregationType = GetString(r, "AggregationType") ?? string.Empty,
                PeriodStart = (DateTime)r["PeriodStart"],
                PeriodEnd = (DateTime)r["PeriodEnd"],
                Value = (decimal)r["MetricValue"],
                Dimensions = r["Dimensions"] is string d ? JsonSerializer.Deserialize<Dictionary<string, string>>(d) ?? new() : new(),
                CalculatedAt = (DateTime)r["CalculatedAt"]
            },
            CreateParameter("@MetricName", metricName),
            CreateParameter("@Start", startDate),
            CreateParameter("@End", endDate));
    }

    public async Task<Dictionary<string, decimal>> GetDashboardMetricsAsync(Guid userId)
    {
        const string sql = @"
            SELECT MetricType, SUM(MetricValue) AS Total
            FROM UsageStatistics
            WHERE UserId = @UserId AND PeriodStart >= @Start
            GROUP BY MetricType";

        var rows = await ExecuteReaderAsync(sql,
            r => (GetString(r, "MetricType") ?? string.Empty, (decimal)r["Total"]),
            CreateParameter("@UserId", userId),
            CreateParameter("@Start", DateTime.UtcNow.AddDays(-30)));

        return rows.ToDictionary(x => x.Item1, x => x.Item2);
    }

    // ── Private mappers ───────────────────────────────────────────────────────

    private static UserEventDto MapUserEvent(System.Data.IDataRecord r) => new()
    {
        Id = (Guid)r["Id"],
        UserId = (Guid)r["UserId"],
        EventType = (string)r["EventType"],
        EventName = r["EventCategory"] is string s ? s : string.Empty,
        Properties = r["Metadata"] is string m ? JsonSerializer.Deserialize<Dictionary<string, string>>(m) ?? new() : new(),
        SessionId = r["SessionId"] as string,
        DeviceId = r["DeviceInfo"] as string,
        Timestamp = (DateTime)r["Timestamp"]
    };

    private static PatternDetectionDto MapPattern(System.Data.IDataRecord r) => new()
    {
        Id = (Guid)r["Id"],
        UserId = (Guid)r["UserId"],
        PatternType = (string)r["PatternType"],
        Description = (string)r["PatternDescription"],
        Confidence = (decimal)r["Confidence"],
        Metadata = r["Evidence"] is string e ? JsonSerializer.Deserialize<Dictionary<string, string>>(e) ?? new() : new(),
        DetectedAt = (DateTime)r["DetectedAt"]
    };

    private static InsightDto MapInsight(System.Data.IDataRecord r) => new()
    {
        Id = (Guid)r["Id"],
        UserId = (Guid)r["UserId"],
        InsightType = (string)r["InsightType"],
        Title = (string)r["Title"],
        Description = (string)r["Message"],
        Category = (string)r["Priority"],
        IsRead = (bool)r["IsViewed"],
        IsDismissed = (bool)r["IsDismissed"],
        GeneratedAt = (DateTime)r["GeneratedAt"]
    };

    private static (DateTime start, DateTime end) GetPeriodRange(string period) => period switch
    {
        "week" => (DateTime.UtcNow.AddDays(-7), DateTime.UtcNow),
        "month" => (DateTime.UtcNow.AddDays(-30), DateTime.UtcNow),
        "year" => (DateTime.UtcNow.AddDays(-365), DateTime.UtcNow),
        _ => (DateTime.UtcNow.AddDays(-30), DateTime.UtcNow)
    };

    public async Task DeleteUserDataAsync(Guid userId, CancellationToken ct = default)
    {
        const string sql = @"
DELETE FROM PatternDetection  WHERE UserId = @UserId;
DELETE FROM Insight           WHERE UserId = @UserId;
DELETE FROM UsageStatistics   WHERE UserId = @UserId;
DELETE FROM UserEvent         WHERE UserId = @UserId;";

        await ExecuteNonQueryAsync(sql, ct, CreateParameter("@UserId", userId));
    }
}
