namespace ExpressRecipe.RecallService.Data;

public interface IRecallRepository
{
    // Recalls
    Task<Guid> CreateRecallAsync(string recallNumber, string source, string title, string description, string severity, DateTime recallDate, string? reason, string? distributionArea);
    Task<List<RecallDto>> GetRecentRecallsAsync(int limit = 100);
    Task<List<RecallDto>> SearchRecallsAsync(string searchTerm, string? severity = null, DateTime? startDate = null, DateTime? endDate = null);
    Task<RecallDto?> GetRecallAsync(Guid recallId);
    Task UpdateRecallAsync(Guid recallId, string status);

    // Recall Products
    Task AddProductToRecallAsync(Guid recallId, string productName, string? brand, string? upc, string? lotCode);
    Task<List<RecallProductDto>> GetRecallProductsAsync(Guid recallId);
    Task<List<RecallDto>> GetRecallsByProductAsync(string productName, string? brand = null, string? upc = null);

    // User Alerts
    Task<Guid> CreateRecallAlertAsync(Guid userId, Guid recallId, string matchType, string matchedValue, bool isAcknowledged);
    Task<List<RecallAlertDto>> GetUserAlertsAsync(Guid userId, bool unacknowledgedOnly = true);
    Task AcknowledgeAlertAsync(Guid alertId);
    Task<int> GetUnacknowledgedCountAsync(Guid userId);

    // Recall Subscriptions
    Task<Guid> SubscribeToRecallsAsync(Guid userId, string? category = null, string? brand = null, string? keyword = null);
    Task<List<RecallSubscriptionDto>> GetUserSubscriptionsAsync(Guid userId);
    Task UnsubscribeAsync(Guid subscriptionId);
    Task<List<Guid>> GetAffectedUsersAsync(Guid recallId);
}

public class RecallDto
{
    public Guid Id { get; set; }
    public string RecallNumber { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public DateTime RecallDate { get; set; }
    public string? Reason { get; set; }
    public string? DistributionArea { get; set; }
    public string Status { get; set; } = string.Empty;
    public int AffectedProductCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class RecallProductDto
{
    public Guid Id { get; set; }
    public Guid RecallId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string? UPC { get; set; }
    public string? LotCode { get; set; }
}

public class RecallAlertDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid RecallId { get; set; }
    public string RecallTitle { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string MatchType { get; set; } = string.Empty;
    public string MatchedValue { get; set; } = string.Empty;
    public bool IsAcknowledged { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class RecallSubscriptionDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string? Category { get; set; }
    public string? Brand { get; set; }
    public string? Keyword { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
