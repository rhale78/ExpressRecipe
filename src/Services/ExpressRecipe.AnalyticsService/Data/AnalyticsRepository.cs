using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace ExpressRecipe.AnalyticsService.Data;

public class AnalyticsRepository : IAnalyticsRepository
{
    private readonly string _connectionString;
    private readonly ILogger<AnalyticsRepository> _logger;

    public AnalyticsRepository(string connectionString, ILogger<AnalyticsRepository> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    // ── User Events ──────────────────────────────────────────────────────────

    public async Task<Guid> TrackEventAsync(Guid userId, string eventType, string eventName,
        Dictionary<string, string> properties, string? sessionId, string? deviceId)
    {
        const string sql = @"
            INSERT INTO UserEvent (UserId, EventType, EventCategory, Metadata, SessionId, DeviceInfo, Timestamp)
            OUTPUT INSERTED.Id
            VALUES (@UserId, @EventType, @EventCategory, @Metadata, @SessionId, @DeviceInfo, GETUTCDATE())";

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.Parameters.AddWithValue("@EventType", eventType);
        cmd.Parameters.AddWithValue("@EventCategory", eventName);
        cmd.Parameters.AddWithValue("@Metadata", properties.Count > 0 ? JsonSerializer.Serialize(properties) : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@SessionId", sessionId ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@DeviceInfo", deviceId ?? (object)DBNull.Value);
        return (Guid)(await cmd.ExecuteScalarAsync())!;
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

        var results = new List<UserEventDto>();
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Limit", limit);
        cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.Parameters.AddWithValue("@Start", startDate.HasValue ? (object)startDate.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@End", endDate.HasValue ? (object)endDate.Value : DBNull.Value);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            results.Add(MapUserEvent(reader));
        return results;
    }

    public async Task<List<UserEventDto>> GetEventsByTypeAsync(string eventType, DateTime startDate, DateTime endDate)
    {
        const string sql = @"
            SELECT Id, UserId, EventType, EventCategory, Metadata, SessionId, DeviceInfo, Timestamp
            FROM UserEvent
            WHERE EventType = @EventType AND Timestamp BETWEEN @Start AND @End
            ORDER BY Timestamp DESC";

        var results = new List<UserEventDto>();
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@EventType", eventType);
        cmd.Parameters.AddWithValue("@Start", startDate);
        cmd.Parameters.AddWithValue("@End", endDate);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            results.Add(MapUserEvent(reader));
        return results;
    }

    public async Task<Dictionary<string, int>> GetEventCountsAsync(Guid userId, DateTime startDate, DateTime endDate)
    {
        const string sql = @"
            SELECT EventType, COUNT(*) AS Cnt
            FROM UserEvent
            WHERE UserId = @UserId AND Timestamp BETWEEN @Start AND @End
            GROUP BY EventType";

        var result = new Dictionary<string, int>();
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.Parameters.AddWithValue("@Start", startDate);
        cmd.Parameters.AddWithValue("@End", endDate);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            result[reader.GetString(0)] = reader.GetInt32(1);
        return result;
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

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        foreach (var (metric, value) in new[] {
            ("Sessions", (decimal)sessionCount),
            ("Actions", (decimal)actionCount),
            ("MinutesActive", (decimal)minutesActive)
        })
        {
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@MetricType", metric);
            cmd.Parameters.AddWithValue("@Value", value);
            cmd.Parameters.AddWithValue("@PeriodType", "Daily");
            cmd.Parameters.AddWithValue("@PeriodStart", periodStart);
            cmd.Parameters.AddWithValue("@PeriodEnd", periodEnd);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public async Task<List<UsageStatisticsDto>> GetUserUsageStatsAsync(Guid userId, DateTime startDate, DateTime endDate)
    {
        const string sql = @"
            SELECT Id, UserId, MetricType, MetricValue, PeriodType, PeriodStart, PeriodEnd, CalculatedAt
            FROM UsageStatistics
            WHERE UserId = @UserId AND PeriodStart >= @Start AND PeriodEnd <= @End
            ORDER BY PeriodStart DESC";

        var results = new List<UsageStatisticsDto>();
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.Parameters.AddWithValue("@Start", startDate);
        cmd.Parameters.AddWithValue("@End", endDate);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new UsageStatisticsDto
            {
                Id = reader.GetGuid(0),
                UserId = reader.GetGuid(1),
                Date = reader.GetDateTime(5) // PeriodStart
            });
        }
        return results;
    }

    public async Task<UsageSummaryDto> GetUsageSummaryAsync(Guid userId, string period)
    {
        var (start, end) = GetPeriodRange(period);
        const string sql = @"
            SELECT MetricType, SUM(MetricValue)
            FROM UsageStatistics
            WHERE UserId = @UserId AND PeriodStart >= @Start AND PeriodEnd <= @End AND PeriodType = 'Daily'
            GROUP BY MetricType";

        var summary = new UsageSummaryDto { UserId = userId, Period = period };
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.Parameters.AddWithValue("@Start", start);
        cmd.Parameters.AddWithValue("@End", end);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var metricType = reader.GetString(0);
            var value = (int)reader.GetDecimal(1);
            switch (metricType)
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

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.Parameters.AddWithValue("@PatternType", patternType);
        cmd.Parameters.AddWithValue("@Desc", description);
        cmd.Parameters.AddWithValue("@Confidence", confidence);
        cmd.Parameters.AddWithValue("@Evidence", metadata.Count > 0 ? JsonSerializer.Serialize(metadata) : (object)DBNull.Value);
        return (Guid)(await cmd.ExecuteScalarAsync())!;
    }

    public async Task<List<PatternDetectionDto>> GetUserPatternsAsync(Guid userId, string? patternType = null)
    {
        const string sql = @"
            SELECT Id, UserId, PatternType, PatternDescription, Confidence, Evidence, DetectedAt
            FROM PatternDetection
            WHERE UserId = @UserId AND IsActive = 1
              AND (@PatternType IS NULL OR PatternType = @PatternType)
            ORDER BY DetectedAt DESC";

        var results = new List<PatternDetectionDto>();
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.Parameters.AddWithValue("@PatternType", patternType ?? (object)DBNull.Value);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            results.Add(MapPattern(reader));
        return results;
    }

    public async Task<List<PatternDetectionDto>> GetGlobalPatternsAsync(string patternType, int limit = 50)
    {
        const string sql = @"
            SELECT TOP (@Limit) Id, UserId, PatternType, PatternDescription, Confidence, Evidence, DetectedAt
            FROM PatternDetection
            WHERE PatternType = @PatternType AND IsActive = 1
            ORDER BY Confidence DESC, DetectedAt DESC";

        var results = new List<PatternDetectionDto>();
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Limit", limit);
        cmd.Parameters.AddWithValue("@PatternType", patternType);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            results.Add(MapPattern(reader));
        return results;
    }

    // ── Insights ───────────────────────────────────────────────────────────────

    public async Task<Guid> CreateInsightAsync(Guid userId, string insightType, string title,
        string description, string category, decimal importance, Dictionary<string, string> data)
    {
        const string sql = @"
            INSERT INTO Insight (UserId, InsightType, Title, Message, Priority, ActionableItem, IsViewed, IsDismissed, GeneratedAt)
            OUTPUT INSERTED.Id
            VALUES (@UserId, @InsightType, @Title, @Message, @Priority, @Data, 0, 0, GETUTCDATE())";

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.Parameters.AddWithValue("@InsightType", insightType);
        cmd.Parameters.AddWithValue("@Title", title);
        cmd.Parameters.AddWithValue("@Message", description);
        cmd.Parameters.AddWithValue("@Priority", category);
        cmd.Parameters.AddWithValue("@Data", data.Count > 0 ? JsonSerializer.Serialize(data) : (object)DBNull.Value);
        return (Guid)(await cmd.ExecuteScalarAsync())!;
    }

    public async Task<List<InsightDto>> GetUserInsightsAsync(Guid userId, bool unreadOnly = false)
    {
        const string sql = @"
            SELECT Id, UserId, InsightType, Title, Message, Priority, IsViewed, IsDismissed, GeneratedAt
            FROM Insight
            WHERE UserId = @UserId AND IsDismissed = 0
              AND (@UnreadOnly = 0 OR IsViewed = 0)
            ORDER BY GeneratedAt DESC";

        var results = new List<InsightDto>();
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.Parameters.AddWithValue("@UnreadOnly", unreadOnly ? 1 : 0);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            results.Add(MapInsight(reader));
        return results;
    }

    public async Task<InsightDto?> GetInsightAsync(Guid insightId)
    {
        const string sql = @"
            SELECT Id, UserId, InsightType, Title, Message, Priority, IsViewed, IsDismissed, GeneratedAt
            FROM Insight WHERE Id = @Id";

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Id", insightId);
        await using var reader = await cmd.ExecuteReaderAsync();
        return await reader.ReadAsync() ? MapInsight(reader) : null;
    }

    public async Task MarkInsightAsReadAsync(Guid insightId)
    {
        const string sql = "UPDATE Insight SET IsViewed = 1, ViewedAt = GETUTCDATE() WHERE Id = @Id";
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Id", insightId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DismissInsightAsync(Guid insightId)
    {
        const string sql = "UPDATE Insight SET IsDismissed = 1, DismissedAt = GETUTCDATE() WHERE Id = @Id";
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Id", insightId);
        await cmd.ExecuteNonQueryAsync();
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

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@MetricName", metricName);
        cmd.Parameters.AddWithValue("@AggType", aggregationType);
        cmd.Parameters.AddWithValue("@PeriodStart", periodStart);
        cmd.Parameters.AddWithValue("@PeriodEnd", periodEnd);
        cmd.Parameters.AddWithValue("@Value", value);
        cmd.Parameters.AddWithValue("@Dimensions", dimensions?.Count > 0 ? JsonSerializer.Serialize(dimensions) : (object)DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<AggregateMetricDto>> GetMetricsAsync(string metricName, DateTime startDate,
        DateTime endDate, Dictionary<string, string>? filters = null)
    {
        const string sql = @"
            SELECT Id, MetricName, AggregationType, PeriodStart, PeriodEnd, MetricValue, Dimensions, CalculatedAt
            FROM AggregateMetrics
            WHERE MetricName = @MetricName AND PeriodStart >= @Start AND PeriodEnd <= @End
            ORDER BY PeriodStart DESC";

        var results = new List<AggregateMetricDto>();
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@MetricName", metricName);
        cmd.Parameters.AddWithValue("@Start", startDate);
        cmd.Parameters.AddWithValue("@End", endDate);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new AggregateMetricDto
            {
                Id = reader.GetGuid(0),
                MetricName = reader.GetString(1),
                AggregationType = reader.GetString(2),
                PeriodStart = reader.GetDateTime(3),
                PeriodEnd = reader.GetDateTime(4),
                Value = reader.GetDecimal(5),
                Dimensions = reader.IsDBNull(6) ? new() : JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(6)) ?? new(),
                CalculatedAt = reader.GetDateTime(7)
            });
        }
        return results;
    }

    public async Task<Dictionary<string, decimal>> GetDashboardMetricsAsync(Guid userId)
    {
        const string sql = @"
            SELECT MetricType, SUM(MetricValue) AS Total
            FROM UsageStatistics
            WHERE UserId = @UserId AND PeriodStart >= @Start
            GROUP BY MetricType";

        var result = new Dictionary<string, decimal>();
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.Parameters.AddWithValue("@Start", DateTime.UtcNow.AddDays(-30));
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            result[reader.GetString(0)] = reader.GetDecimal(1);
        return result;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static UserEventDto MapUserEvent(SqlDataReader r) => new()
    {
        Id = r.GetGuid(0),
        UserId = r.GetGuid(1),
        EventType = r.GetString(2),
        EventName = r.IsDBNull(3) ? string.Empty : r.GetString(3),
        Properties = r.IsDBNull(4) ? new() : JsonSerializer.Deserialize<Dictionary<string, string>>(r.GetString(4)) ?? new(),
        SessionId = r.IsDBNull(5) ? null : r.GetString(5),
        DeviceId = r.IsDBNull(6) ? null : r.GetString(6),
        Timestamp = r.GetDateTime(7)
    };

    private static PatternDetectionDto MapPattern(SqlDataReader r) => new()
    {
        Id = r.GetGuid(0),
        UserId = r.GetGuid(1),
        PatternType = r.GetString(2),
        Description = r.GetString(3),
        Confidence = r.GetDecimal(4),
        Metadata = r.IsDBNull(5) ? new() : JsonSerializer.Deserialize<Dictionary<string, string>>(r.GetString(5)) ?? new(),
        DetectedAt = r.GetDateTime(6)
    };

    private static InsightDto MapInsight(SqlDataReader r) => new()
    {
        Id = r.GetGuid(0),
        UserId = r.GetGuid(1),
        InsightType = r.GetString(2),
        Title = r.GetString(3),
        Description = r.GetString(4),
        Category = r.GetString(5),
        IsRead = r.GetBoolean(6),
        IsDismissed = r.GetBoolean(7),
        GeneratedAt = r.GetDateTime(8)
    };

    private static (DateTime start, DateTime end) GetPeriodRange(string period) => period switch
    {
        "week" => (DateTime.UtcNow.AddDays(-7), DateTime.UtcNow),
        "month" => (DateTime.UtcNow.AddDays(-30), DateTime.UtcNow),
        "year" => (DateTime.UtcNow.AddDays(-365), DateTime.UtcNow),
        _ => (DateTime.UtcNow.AddDays(-30), DateTime.UtcNow)
    };
}
