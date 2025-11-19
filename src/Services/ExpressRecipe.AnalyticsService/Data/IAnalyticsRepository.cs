namespace ExpressRecipe.AnalyticsService.Data;

public interface IAnalyticsRepository
{
    // User Events
    Task<Guid> TrackEventAsync(Guid userId, string eventType, string eventName, Dictionary<string, string> properties, string? sessionId, string? deviceId);
    Task<List<UserEventDto>> GetUserEventsAsync(Guid userId, DateTime? startDate = null, DateTime? endDate = null, int limit = 100);
    Task<List<UserEventDto>> GetEventsByTypeAsync(string eventType, DateTime startDate, DateTime endDate);
    Task<Dictionary<string, int>> GetEventCountsAsync(Guid userId, DateTime startDate, DateTime endDate);

    // Usage Statistics
    Task UpdateUsageStatsAsync(Guid userId, DateTime date, int sessionCount, int actionCount, int minutesActive);
    Task<List<UsageStatisticsDto>> GetUserUsageStatsAsync(Guid userId, DateTime startDate, DateTime endDate);
    Task<UsageSummaryDto> GetUsageSummaryAsync(Guid userId, string period);

    // Pattern Detection
    Task<Guid> RecordPatternAsync(Guid userId, string patternType, string description, decimal confidence, Dictionary<string, string> metadata);
    Task<List<PatternDetectionDto>> GetUserPatternsAsync(Guid userId, string? patternType = null);
    Task<List<PatternDetectionDto>> GetGlobalPatternsAsync(string patternType, int limit = 50);

    // Insights
    Task<Guid> CreateInsightAsync(Guid userId, string insightType, string title, string description, string category, decimal importance, Dictionary<string, string> data);
    Task<List<InsightDto>> GetUserInsightsAsync(Guid userId, bool unreadOnly = false);
    Task<InsightDto?> GetInsightAsync(Guid insightId);
    Task MarkInsightAsReadAsync(Guid insightId);
    Task DismissInsightAsync(Guid insightId);

    // Aggregate Metrics
    Task SaveAggregateMetricAsync(string metricName, string aggregationType, DateTime periodStart, DateTime periodEnd, decimal value, Dictionary<string, string>? dimensions);
    Task<List<AggregateMetricDto>> GetMetricsAsync(string metricName, DateTime startDate, DateTime endDate, Dictionary<string, string>? filters = null);
    Task<Dictionary<string, decimal>> GetDashboardMetricsAsync(Guid userId);
}

public class UserEventDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string EventName { get; set; } = string.Empty;
    public Dictionary<string, string> Properties { get; set; } = new();
    public string? SessionId { get; set; }
    public string? DeviceId { get; set; }
    public DateTime Timestamp { get; set; }
}

public class UsageStatisticsDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DateTime Date { get; set; }
    public int SessionCount { get; set; }
    public int ActionCount { get; set; }
    public int MinutesActive { get; set; }
    public int RecipesViewed { get; set; }
    public int ProductsScanned { get; set; }
    public int MealsPlanned { get; set; }
}

public class UsageSummaryDto
{
    public Guid UserId { get; set; }
    public string Period { get; set; } = string.Empty;
    public int TotalSessions { get; set; }
    public int TotalActions { get; set; }
    public int TotalMinutes { get; set; }
    public int DaysActive { get; set; }
    public Dictionary<string, int> TopActions { get; set; } = new();
    public List<DailyUsageDto> DailyBreakdown { get; set; } = new();
}

public class DailyUsageDto
{
    public DateTime Date { get; set; }
    public int Sessions { get; set; }
    public int Actions { get; set; }
    public int Minutes { get; set; }
}

public class PatternDetectionDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string PatternType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Confidence { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
    public DateTime DetectedAt { get; set; }
}

public class InsightDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string InsightType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Importance { get; set; }
    public Dictionary<string, string> Data { get; set; } = new();
    public bool IsRead { get; set; }
    public bool IsDismissed { get; set; }
    public DateTime GeneratedAt { get; set; }
}

public class AggregateMetricDto
{
    public Guid Id { get; set; }
    public string MetricName { get; set; } = string.Empty;
    public string AggregationType { get; set; } = string.Empty;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public decimal Value { get; set; }
    public Dictionary<string, string> Dimensions { get; set; } = new();
    public DateTime CalculatedAt { get; set; }
}
