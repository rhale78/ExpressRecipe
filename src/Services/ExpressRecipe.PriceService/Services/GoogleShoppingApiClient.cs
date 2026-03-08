using System.Text.Json;
using System.Text.Json.Serialization;

namespace ExpressRecipe.PriceService.Services;

/// <summary>
/// Client for Google Shopping (Google Custom Search API with Shopping results).
/// Disabled by default — set ExternalApis:GoogleShopping:Enabled=true and provide ApiKey + SearchEngineId.
/// Requires a Programmable Search Engine (cx) scoped to shopping sites.
/// API Documentation: https://developers.google.com/custom-search/v1/overview
/// </summary>
public class GoogleShoppingApiClient : IExternalPriceApiClient
{
    public const string DataSourceCode = "GOOGLE_SHOPPING";

    private readonly HttpClient _httpClient;
    private readonly ILogger<GoogleShoppingApiClient> _logger;
    private readonly string? _apiKey;
    private readonly string? _searchEngineId;

    public bool IsEnabled { get; }

    public GoogleShoppingApiClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<GoogleShoppingApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        IsEnabled = configuration.GetValue<bool>("ExternalApis:GoogleShopping:Enabled", false);
        _apiKey = configuration["ExternalApis:GoogleShopping:ApiKey"]
                  ?? configuration["GoogleShopping:ApiKey"];
        _searchEngineId = configuration["ExternalApis:GoogleShopping:SearchEngineId"]
                          ?? configuration["GoogleShopping:SearchEngineId"];

        _httpClient.BaseAddress = new Uri("https://www.googleapis.com/");
    }

    // IExternalPriceApiClient implementation
    public Task<List<ExternalPriceResult>> GetPricesAsync(string upc, CancellationToken ct)
    {
        if (!IsEnabled) { return Task.FromResult(new List<ExternalPriceResult>()); }
        return SearchByNameAsync(upc, null, ct);
    }

    public async Task<List<ExternalPriceResult>> SearchByNameAsync(string name, string? zipCode, CancellationToken ct)
    {
        if (!IsEnabled) { return new List<ExternalPriceResult>(); }

        try
        {
            var products = await SearchProductsAsync(name, ct: ct);
            return products.Select(p =>
            {
                var offer = p.PageMap?.Offers?.FirstOrDefault();
                var priceStr = offer?.Price?.Replace("$", string.Empty);
                decimal.TryParse(priceStr, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var price);
                return new ExternalPriceResult
                {
                    ProductName = p.Title ?? string.Empty,
                    Price = price,
                    DataSource = DataSourceCode,
                    ExternalId = p.Link,
                    ObservedAt = DateTimeOffset.UtcNow
                };
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GoogleShopping search failed for '{Name}'", name);
            return new List<ExternalPriceResult>();
        }
    }

    /// <summary>
    /// Search products via Google Custom Search (Shopping results).
    /// </summary>
    public async Task<List<GoogleShoppingProduct>> SearchProductsAsync(string query, int maxResults = 10, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrEmpty(_apiKey) || string.IsNullOrEmpty(_searchEngineId))
            {
                _logger.LogWarning("Google Shopping API key or Search Engine ID not configured");
                return new List<GoogleShoppingProduct>();
            }

            _logger.LogInformation("Searching Google Shopping for: {Query}", query);

            var url = $"customsearch/v1?key={_apiKey}&cx={_searchEngineId}&q={Uri.EscapeDataString(query)}&num={maxResults}";
            var response = await _httpClient.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Google Shopping API returned {StatusCode}", response.StatusCode);
                return new List<GoogleShoppingProduct>();
            }

            var json = await response.Content.ReadAsStringAsync(ct);
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
    public async Task<List<GoogleShoppingProduct>> SearchByGTINAsync(string gtin, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrEmpty(_apiKey))
            {
                _logger.LogWarning("Google Shopping API key not configured");
                return new List<GoogleShoppingProduct>();
            }

            _logger.LogInformation("Searching Google Shopping by GTIN: {GTIN}", gtin);

            return await SearchProductsAsync($"gtin:{gtin}", ct: ct);
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
