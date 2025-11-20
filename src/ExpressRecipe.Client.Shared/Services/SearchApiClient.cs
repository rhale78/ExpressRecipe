using System.Net.Http.Json;

namespace ExpressRecipe.Client.Shared.Services;

// DTOs for Search Service
public class GlobalSearchRequest
{
    public string Query { get; set; } = string.Empty;
    public List<string> SearchTypes { get; set; } = new(); // Products, Recipes, Ingredients, etc.
    public int MaxResults { get; set; } = 20;
    public bool IncludeAllergenInfo { get; set; } = true;
}

public class GlobalSearchResult
{
    public string Query { get; set; } = string.Empty;
    public int TotalResults { get; set; }
    public List<SearchResultItem> Results { get; set; } = new();
    public Dictionary<string, int> ResultCounts { get; set; } = new(); // Type -> Count
    public DateTime SearchedAt { get; set; }
}

public class SearchResultItem
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty; // Product, Recipe, Ingredient, etc.
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
    public float Relevance { get; set; } // 0.0 - 1.0
    public List<string> MatchedTerms { get; set; } = new();
    public bool HasAllergenMatch { get; set; }
}

public class SearchSuggestion
{
    public string Term { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int Frequency { get; set; }
}

public class RecentSearch
{
    public Guid Id { get; set; }
    public string Query { get; set; } = string.Empty;
    public DateTime SearchedAt { get; set; }
    public int ResultCount { get; set; }
}

public class SearchFilter
{
    public string Field { get; set; } = string.Empty;
    public string Operator { get; set; } = "equals"; // equals, contains, gt, lt, between
    public object? Value { get; set; }
}

public class AdvancedSearchRequest
{
    public string? Query { get; set; }
    public List<SearchFilter> Filters { get; set; } = new();
    public string? SortBy { get; set; }
    public string SortOrder { get; set; } = "asc";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public interface ISearchApiClient
{
    Task<GlobalSearchResult?> SearchAsync(string query, List<string>? searchTypes = null);
    Task<GlobalSearchResult?> AdvancedSearchAsync(AdvancedSearchRequest request);
    Task<List<SearchSuggestion>> GetSearchSuggestionsAsync(string partialQuery);
    Task<List<RecentSearch>> GetRecentSearchesAsync(int limit = 10);
    Task<bool> ClearRecentSearchesAsync();
    Task<bool> SaveSearchAsync(string query);
}

public class SearchApiClient : ISearchApiClient
{
    private readonly HttpClient _httpClient;

    public SearchApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<GlobalSearchResult?> SearchAsync(string query, List<string>? searchTypes = null)
    {
        var request = new GlobalSearchRequest
        {
            Query = query,
            SearchTypes = searchTypes ?? new List<string> { "Products", "Recipes", "Ingredients" },
            MaxResults = 20
        };

        var response = await _httpClient.PostAsJsonAsync("/api/search", request);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<GlobalSearchResult>();
    }

    public async Task<GlobalSearchResult?> AdvancedSearchAsync(AdvancedSearchRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/search/advanced", request);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<GlobalSearchResult>();
    }

    public async Task<List<SearchSuggestion>> GetSearchSuggestionsAsync(string partialQuery)
    {
        var response = await _httpClient.GetAsync($"/api/search/suggestions?q={Uri.EscapeDataString(partialQuery)}");

        if (!response.IsSuccessStatusCode)
        {
            return new List<SearchSuggestion>();
        }

        return await response.Content.ReadFromJsonAsync<List<SearchSuggestion>>() ?? new List<SearchSuggestion>();
    }

    public async Task<List<RecentSearch>> GetRecentSearchesAsync(int limit = 10)
    {
        var response = await _httpClient.GetAsync($"/api/search/recent?limit={limit}");

        if (!response.IsSuccessStatusCode)
        {
            return new List<RecentSearch>();
        }

        return await response.Content.ReadFromJsonAsync<List<RecentSearch>>() ?? new List<RecentSearch>();
    }

    public async Task<bool> ClearRecentSearchesAsync()
    {
        var response = await _httpClient.DeleteAsync("/api/search/recent");
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> SaveSearchAsync(string query)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/search/save", new { Query = query });
        return response.IsSuccessStatusCode;
    }
}
