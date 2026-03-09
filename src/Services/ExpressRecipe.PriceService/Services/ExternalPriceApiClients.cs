using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;

namespace ExpressRecipe.PriceService.Services;

/// <summary>
/// Kroger Public API client.
/// OAuth2 client_credentials flow; token cached for 30 minutes.
/// Disabled by default — set ExternalApis:Kroger:Enabled=true and provide credentials.
/// </summary>
public sealed class KrogerApiClient : IExternalPriceApiClient
{
    public const string DataSourceCode = "KROGER";

    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<KrogerApiClient> _logger;
    private readonly string? _clientId;
    private readonly string? _clientSecret;
    private readonly string _baseUrl;

    public bool IsEnabled { get; }

    public KrogerApiClient(
        HttpClient httpClient,
        IMemoryCache cache,
        IConfiguration configuration,
        ILogger<KrogerApiClient> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
        IsEnabled = configuration.GetValue<bool>("ExternalApis:Kroger:Enabled", false);
        _baseUrl = configuration["ExternalApis:Kroger:BaseUrl"] ?? "https://api.kroger.com/v1";
        _clientId = configuration["ExternalApis:Kroger:ClientId"];
        _clientSecret = configuration["ExternalApis:Kroger:ClientSecret"];
    }

    public async Task<List<ExternalPriceResult>> GetPricesAsync(string upc, CancellationToken ct)
    {
        if (!IsEnabled) { return new List<ExternalPriceResult>(); }
        return await SearchByNameAsync(upc, null, ct);
    }

    public async Task<List<ExternalPriceResult>> SearchByNameAsync(string name, string? zipCode, CancellationToken ct)
    {
        if (!IsEnabled) { return new List<ExternalPriceResult>(); }

        try
        {
            var token = await GetBearerTokenAsync(ct);
            if (string.IsNullOrEmpty(token)) { return new List<ExternalPriceResult>(); }

            var url = $"{_baseUrl}/products?filter.term={Uri.EscapeDataString(name)}&filter.limit=50";
            if (!string.IsNullOrWhiteSpace(zipCode))
            {
                url += $"&filter.locationId={Uri.EscapeDataString(zipCode)}";
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await _httpClient.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Kroger API returned {StatusCode}", response.StatusCode);
                return new List<ExternalPriceResult>();
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<KrogerProductResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return result?.Data?.Select(p => new ExternalPriceResult
            {
                ProductName = p.Description ?? string.Empty,
                Upc = p.Upc,
                StoreName = "Kroger",
                StoreChain = "Kroger",
                Price = p.Items?.FirstOrDefault()?.Price?.Regular ?? 0m,
                RegularPrice = p.Items?.FirstOrDefault()?.Price?.Regular,
                DataSource = DataSourceCode,
                ExternalId = p.ProductId,
                ObservedAt = DateTimeOffset.UtcNow
            }).ToList() ?? new List<ExternalPriceResult>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Kroger API search failed for '{Name}'", name);
            return new List<ExternalPriceResult>();
        }
    }

    private async Task<string?> GetBearerTokenAsync(CancellationToken ct)
    {
        const string cacheKey = "kroger:bearer_token";
        if (_cache.TryGetValue(cacheKey, out string? cached)) { return cached; }

        if (string.IsNullOrEmpty(_clientId) || string.IsNullOrEmpty(_clientSecret))
        {
            _logger.LogWarning("Kroger: ClientId or ClientSecret not configured");
            return null;
        }

        var credentials = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{_clientId}:{_clientSecret}"));
        using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/connect/oauth2/token");
        tokenRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        tokenRequest.Content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "client_credentials"),
            new KeyValuePair<string, string>("scope", "product.compact")
        });

        using var tokenResponse = await _httpClient.SendAsync(tokenRequest, ct);
        if (!tokenResponse.IsSuccessStatusCode)
        {
            _logger.LogWarning("Kroger: Token request failed with {StatusCode}", tokenResponse.StatusCode);
            return null;
        }

        var json = await tokenResponse.Content.ReadAsStringAsync(ct);
        var tokenResult = JsonSerializer.Deserialize<KrogerTokenResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        var token = tokenResult?.AccessToken;
        if (!string.IsNullOrEmpty(token))
        {
            _cache.Set(cacheKey, token, TimeSpan.FromMinutes(29));
        }
        return token;
    }

    // ── Kroger response shapes ─────────────────────────────────────────────

    private sealed class KrogerTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; init; }
    }

    private sealed class KrogerProductResponse
    {
        public List<KrogerProduct>? Data { get; init; }
    }

    private sealed class KrogerProduct
    {
        public string? ProductId { get; init; }
        public string? Description { get; init; }
        public string? Upc { get; init; }
        public List<KrogerProductItem>? Items { get; init; }
    }

    private sealed class KrogerProductItem
    {
        public KrogerPrice? Price { get; init; }
    }

    private sealed class KrogerPrice
    {
        public decimal Regular { get; init; }
        public decimal? Promo { get; init; }
    }
}

/// <summary>
/// Flipp flyer API client stub.
/// Disabled by default — set ExternalApis:Flipp:Enabled=true and provide ApiKey.
/// </summary>
public sealed class FlippApiClient : IExternalPriceApiClient
{
    public const string DataSourceCode = "FLIPP";

    private readonly HttpClient _httpClient;
    private readonly ILogger<FlippApiClient> _logger;
    private readonly string? _apiKey;
    private readonly string _baseUrl;

    public bool IsEnabled { get; }

    public FlippApiClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<FlippApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        IsEnabled = configuration.GetValue<bool>("ExternalApis:Flipp:Enabled", false);
        _baseUrl = configuration["ExternalApis:Flipp:BaseUrl"] ?? "https://backflipp.wishabi.com/flipp";
        _apiKey = configuration["ExternalApis:Flipp:ApiKey"];
    }

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
            var url = $"{_baseUrl}/items/search?q={Uri.EscapeDataString(name)}";
            if (!string.IsNullOrWhiteSpace(zipCode)) { url += $"&postal_code={Uri.EscapeDataString(zipCode)}"; }
            if (!string.IsNullOrWhiteSpace(_apiKey)) { url += $"&access_token={_apiKey}"; }

            var json = await _httpClient.GetStringAsync(url, ct);
            // Parse is a stub — Flipp schema may change; return empty for now
            _logger.LogDebug("Flipp: received response ({Length} chars) for '{Name}'", json.Length, name);
            return new List<ExternalPriceResult>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Flipp API search failed for '{Name}'", name);
            return new List<ExternalPriceResult>();
        }
    }
}

/// <summary>
/// Food Lion API client stub.
/// Disabled by default — set ExternalApis:FoodLion:Enabled=true and provide ApiKey.
/// </summary>
public sealed class FoodLionApiClient : IExternalPriceApiClient
{
    public const string DataSourceCode = "FOOD_LION";

    private readonly HttpClient _httpClient;
    private readonly ILogger<FoodLionApiClient> _logger;
    private readonly string? _apiKey;
    private readonly string _baseUrl;

    public bool IsEnabled { get; }

    public FoodLionApiClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<FoodLionApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        IsEnabled = configuration.GetValue<bool>("ExternalApis:FoodLion:Enabled", false);
        _baseUrl = configuration["ExternalApis:FoodLion:BaseUrl"] ?? "https://api.foodlion.com";
        _apiKey = configuration["ExternalApis:FoodLion:ApiKey"];
    }

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
            var url = $"{_baseUrl}/products/search?q={Uri.EscapeDataString(name)}";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (!string.IsNullOrWhiteSpace(_apiKey))
            {
                request.Headers.TryAddWithoutValidation("X-Api-Key", _apiKey);
            }
            using var response = await _httpClient.SendAsync(request, ct);
            var json = await response.Content.ReadAsStringAsync(ct);
            _logger.LogDebug("FoodLion: received response ({Length} chars) for '{Name}'", json.Length, name);
            return new List<ExternalPriceResult>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FoodLion API search failed for '{Name}'", name);
            return new List<ExternalPriceResult>();
        }
    }
}
