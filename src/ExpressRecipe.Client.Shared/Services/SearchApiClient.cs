using System.Net.Http.Json;

namespace ExpressRecipe.Client.Shared.Services
{
    // DTOs for Search Service
    public class GlobalSearchRequest
    {
        public string Query { get; set; } = string.Empty;
        public List<string> SearchTypes { get; set; } = []; // Products, Recipes, Ingredients, etc.
        public int MaxResults { get; set; } = 20;
        public bool IncludeAllergenInfo { get; set; } = true;
    }

    public class GlobalSearchResult
    {
        public string Query { get; set; } = string.Empty;
        public int TotalResults { get; set; }
        public List<SearchResultItem> Results { get; set; } = [];
        public Dictionary<string, int> ResultCounts { get; set; } = []; // Type -> Count
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
        public Dictionary<string, string> Metadata { get; set; } = [];
        public float Relevance { get; set; } // 0.0 - 1.0
        public List<string> MatchedTerms { get; set; } = [];
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
        public List<SearchFilter> Filters { get; set; } = [];
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
            GlobalSearchRequest request = new GlobalSearchRequest
            {
                Query = query,
                SearchTypes = searchTypes ?? ["Products", "Recipes", "Ingredients"],
                MaxResults = 20
            };

            HttpResponseMessage response = await _httpClient.PostAsJsonAsync("/api/search", request);

            return !response.IsSuccessStatusCode ? null : await response.Content.ReadFromJsonAsync<GlobalSearchResult>();
        }

        public async Task<GlobalSearchResult?> AdvancedSearchAsync(AdvancedSearchRequest request)
        {
            HttpResponseMessage response = await _httpClient.PostAsJsonAsync("/api/search/advanced", request);

            return !response.IsSuccessStatusCode ? null : await response.Content.ReadFromJsonAsync<GlobalSearchResult>();
        }

        public async Task<List<SearchSuggestion>> GetSearchSuggestionsAsync(string partialQuery)
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"/api/search/suggestions?q={Uri.EscapeDataString(partialQuery)}");

            return !response.IsSuccessStatusCode ? [] : await response.Content.ReadFromJsonAsync<List<SearchSuggestion>>() ?? [];
        }

        public async Task<List<RecentSearch>> GetRecentSearchesAsync(int limit = 10)
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"/api/search/recent?limit={limit}");

            return !response.IsSuccessStatusCode ? [] : await response.Content.ReadFromJsonAsync<List<RecentSearch>>() ?? [];
        }

        public async Task<bool> ClearRecentSearchesAsync()
        {
            HttpResponseMessage response = await _httpClient.DeleteAsync("/api/search/recent");
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> SaveSearchAsync(string query)
        {
            HttpResponseMessage response = await _httpClient.PostAsJsonAsync("/api/search/save", new { Query = query });
            return response.IsSuccessStatusCode;
        }
    }
}
