namespace ExpressRecipe.SearchService.Data;

public interface ISearchRepository
{
    // Search Index
    Task<Guid> IndexEntityAsync(string entityType, Guid entityId, string title, string description, string? category, List<string> tags, Dictionary<string, string> metadata);
    Task UpdateIndexAsync(Guid indexId, string title, string description, string? category, List<string> tags);
    Task RemoveFromIndexAsync(string entityType, Guid entityId);
    Task RebuildIndexAsync(string entityType);

    // Search Operations
    Task<SearchResultDto> SearchAsync(string query, string? entityType = null, string? category = null, List<string>? tags = null, int limit = 50, int offset = 0);
    Task<List<SearchSuggestionDto>> GetSuggestionsAsync(string partialQuery, int limit = 10);
    Task<SearchResultDto> SearchByTagsAsync(List<string> tags, string? entityType = null, int limit = 50);

    // Search History
    Task<Guid> RecordSearchAsync(Guid userId, string query, string? entityType, int resultCount, bool hadResults);
    Task<List<SearchHistoryDto>> GetUserSearchHistoryAsync(Guid userId, int limit = 20);
    Task<List<PopularSearchDto>> GetPopularSearchesAsync(string? entityType = null, int daysBack = 30, int limit = 20);
    Task ClearUserSearchHistoryAsync(Guid userId);

    // User Preferences
    Task SaveSearchPreferenceAsync(Guid userId, string preferenceKey, string preferenceValue);
    Task<Dictionary<string, string>> GetUserPreferencesAsync(Guid userId);
    Task DeleteSearchPreferenceAsync(Guid userId, string preferenceKey);

    // Recommendations
    Task<Guid> CreateRecommendationAsync(Guid userId, string entityType, Guid entityId, string reason, decimal score);
    Task<List<RecommendationDto>> GetUserRecommendationsAsync(Guid userId, string? entityType = null, int limit = 20);
    Task RefreshRecommendationsAsync(Guid userId);
}

public class SearchResultDto
{
    public string Query { get; set; } = string.Empty;
    public int TotalResults { get; set; }
    public int Offset { get; set; }
    public int Limit { get; set; }
    public List<SearchItemDto> Items { get; set; } = new();
    public Dictionary<string, int> FacetCounts { get; set; } = new();
}

public class SearchItemDto
{
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Category { get; set; }
    public List<string> Tags { get; set; } = new();
    public decimal Relevance { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public class SearchSuggestionDto
{
    public string Suggestion { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public int Frequency { get; set; }
}

public class SearchHistoryDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Query { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public int ResultCount { get; set; }
    public bool HadResults { get; set; }
    public DateTime SearchedAt { get; set; }
}

public class PopularSearchDto
{
    public string Query { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public int SearchCount { get; set; }
    public int UniqueUsers { get; set; }
}

public class RecommendationDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public decimal Score { get; set; }
    public DateTime GeneratedAt { get; set; }
}
