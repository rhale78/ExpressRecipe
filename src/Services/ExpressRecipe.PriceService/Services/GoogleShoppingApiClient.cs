using System.Text.Json;
using System.Text.Json.Serialization;

namespace ExpressRecipe.PriceService.Services;

/// <summary>
/// Client for Google Shopping (Google Content API for Shopping)
/// API Documentation: https://developers.google.com/shopping-content/guides/quickstart
/// Note: Requires Google API Key and Merchant Center account
/// </summary>
public class GoogleShoppingApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GoogleShoppingApiClient> _logger;
    private readonly string? _apiKey;

    public GoogleShoppingApiClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<GoogleShoppingApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = configuration["GoogleShopping:ApiKey"];

        _httpClient.BaseAddress = new Uri("https://www.googleapis.com/");
    }

    /// <summary>
    /// Search products via Google Shopping
    /// </summary>
    public async Task<List<GoogleShoppingProduct>> SearchProductsAsync(string query, int maxResults = 10)
    {
        try
        {
            if (string.IsNullOrEmpty(_apiKey))
            {
                _logger.LogWarning("Google Shopping API key not configured");
                return new List<GoogleShoppingProduct>();
            }

            _logger.LogInformation("Searching Google Shopping for: {Query}", query);

            var url = $"customsearch/v1?key={_apiKey}&cx=YOUR_SEARCH_ENGINE_ID&q={Uri.EscapeDataString(query)}&num={maxResults}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Google Shopping API returned {StatusCode}", response.StatusCode);
                return new List<GoogleShoppingProduct>();
            }

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<GoogleShoppingResponse>(json);

            return result?.Items ?? new List<GoogleShoppingProduct>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search Google Shopping: {Query}", query);
            return new List<GoogleShoppingProduct>();
        }
    }

    /// <summary>
    /// Get product offers by GTIN (Global Trade Item Number - includes UPC/EAN)
    /// </summary>
    public async Task<List<GoogleShoppingProduct>> SearchByGTINAsync(string gtin)
    {
        try
        {
            if (string.IsNullOrEmpty(_apiKey))
            {
                _logger.LogWarning("Google Shopping API key not configured");
                return new List<GoogleShoppingProduct>();
            }

            _logger.LogInformation("Searching Google Shopping by GTIN: {GTIN}", gtin);

            // Search with GTIN in query
            return await SearchProductsAsync($"gtin:{gtin}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search Google Shopping by GTIN: {GTIN}", gtin);
            return new List<GoogleShoppingProduct>();
        }
    }
}

/// <summary>
/// Google Shopping API response
/// </summary>
public class GoogleShoppingResponse
{
    [JsonPropertyName("kind")]
    public string? Kind { get; set; }

    [JsonPropertyName("url")]
    public GoogleShoppingUrl? Url { get; set; }

    [JsonPropertyName("queries")]
    public Dictionary<string, List<GoogleShoppingQuery>>? Queries { get; set; }

    [JsonPropertyName("items")]
    public List<GoogleShoppingProduct>? Items { get; set; }
}

/// <summary>
/// Google Shopping URL info
/// </summary>
public class GoogleShoppingUrl
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("template")]
    public string? Template { get; set; }
}

/// <summary>
/// Google Shopping query metadata
/// </summary>
public class GoogleShoppingQuery
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("totalResults")]
    public string? TotalResults { get; set; }

    [JsonPropertyName("searchTerms")]
    public string? SearchTerms { get; set; }

    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("startIndex")]
    public int StartIndex { get; set; }
}

/// <summary>
/// Google Shopping product/offer
/// </summary>
public class GoogleShoppingProduct
{
    [JsonPropertyName("kind")]
    public string? Kind { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("htmlTitle")]
    public string? HtmlTitle { get; set; }

    [JsonPropertyName("link")]
    public string? Link { get; set; }

    [JsonPropertyName("displayLink")]
    public string? DisplayLink { get; set; }

    [JsonPropertyName("snippet")]
    public string? Snippet { get; set; }

    [JsonPropertyName("htmlSnippet")]
    public string? HtmlSnippet { get; set; }

    [JsonPropertyName("pagemap")]
    public GoogleShoppingPageMap? PageMap { get; set; }
}

/// <summary>
/// Page map with structured data
/// </summary>
public class GoogleShoppingPageMap
{
    [JsonPropertyName("offer")]
    public List<GoogleShoppingOffer>? Offers { get; set; }

    [JsonPropertyName("product")]
    public List<GoogleShoppingProductInfo>? Products { get; set; }

    [JsonPropertyName("aggregaterating")]
    public List<GoogleShoppingRating>? Ratings { get; set; }
}

/// <summary>
/// Price offer information
/// </summary>
public class GoogleShoppingOffer
{
    [JsonPropertyName("price")]
    public string? Price { get; set; }

    [JsonPropertyName("pricecurrency")]
    public string? PriceCurrency { get; set; }

    [JsonPropertyName("availability")]
    public string? Availability { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }
}

/// <summary>
/// Product metadata
/// </summary>
public class GoogleShoppingProductInfo
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("image")]
    public string? Image { get; set; }

    [JsonPropertyName("brand")]
    public string? Brand { get; set; }

    [JsonPropertyName("gtin")]
    public string? GTIN { get; set; }

    [JsonPropertyName("gtin13")]
    public string? GTIN13 { get; set; }

    [JsonPropertyName("gtin14")]
    public string? GTIN14 { get; set; }

    [JsonPropertyName("mpn")]
    public string? MPN { get; set; }
}

/// <summary>
/// Product rating information
/// </summary>
public class GoogleShoppingRating
{
    [JsonPropertyName("ratingvalue")]
    public string? RatingValue { get; set; }

    [JsonPropertyName("reviewcount")]
    public string? ReviewCount { get; set; }

    [JsonPropertyName("bestrating")]
    public string? BestRating { get; set; }

    [JsonPropertyName("worstrating")]
    public string? WorstRating { get; set; }
}
