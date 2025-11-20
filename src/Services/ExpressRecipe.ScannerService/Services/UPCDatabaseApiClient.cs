using System.Text.Json;
using System.Text.Json.Serialization;

namespace ExpressRecipe.ScannerService.Services;

/// <summary>
/// Client for UPCitemdb.com API - UPC barcode database
/// API Documentation: https://www.upcitemdb.com/api/explorer
/// Note: Requires API key for production use (free tier available)
/// </summary>
public class UPCDatabaseApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<UPCDatabaseApiClient> _logger;
    private readonly string? _apiKey;

    public UPCDatabaseApiClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<UPCDatabaseApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = configuration["UPCDatabase:ApiKey"];

        _httpClient.BaseAddress = new Uri("https://api.upcitemdb.com/");

        if (!string.IsNullOrEmpty(_apiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("user_key", _apiKey);
        }
    }

    /// <summary>
    /// Lookup product by UPC/EAN barcode
    /// </summary>
    public async Task<UPCDatabaseProduct?> GetProductByBarcodeAsync(string barcode)
    {
        try
        {
            _logger.LogInformation("Looking up barcode in UPC Database: {Barcode}", barcode);

            // Clean barcode
            barcode = barcode.Replace(" ", "").Replace("-", "");

            var response = await _httpClient.GetAsync($"prod/trial/lookup?upc={barcode}");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("UPC Database API returned {StatusCode} for barcode {Barcode}",
                    response.StatusCode, barcode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<UPCDatabaseResponse>(json);

            if (result == null || result.Items == null || result.Items.Count == 0)
            {
                _logger.LogInformation("Product not found in UPC Database: {Barcode}", barcode);
                return null;
            }

            var product = result.Items[0];
            _logger.LogInformation("Found product in UPC Database: {Title}", product.Title);
            return product;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to lookup barcode in UPC Database: {Barcode}", barcode);
            return null;
        }
    }

    /// <summary>
    /// Search products by keyword
    /// </summary>
    public async Task<List<UPCDatabaseProduct>> SearchProductsAsync(string query, int offset = 0, int limit = 10)
    {
        try
        {
            _logger.LogInformation("Searching UPC Database for: {Query}", query);

            var response = await _httpClient.GetAsync(
                $"prod/trial/search?s={Uri.EscapeDataString(query)}&offset={offset}&limit={limit}");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("UPC Database search returned {StatusCode}", response.StatusCode);
                return new List<UPCDatabaseProduct>();
            }

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<UPCDatabaseResponse>(json);

            return result?.Items ?? new List<UPCDatabaseProduct>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search UPC Database: {Query}", query);
            return new List<UPCDatabaseProduct>();
        }
    }
}

/// <summary>
/// UPC Database API response
/// </summary>
public class UPCDatabaseResponse
{
    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("offset")]
    public int Offset { get; set; }

    [JsonPropertyName("items")]
    public List<UPCDatabaseProduct>? Items { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

/// <summary>
/// Product data from UPC Database
/// </summary>
public class UPCDatabaseProduct
{
    [JsonPropertyName("ean")]
    public string? EAN { get; set; }

    [JsonPropertyName("upc")]
    public string? UPC { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("brand")]
    public string? Brand { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("color")]
    public string? Color { get; set; }

    [JsonPropertyName("size")]
    public string? Size { get; set; }

    [JsonPropertyName("dimension")]
    public string? Dimension { get; set; }

    [JsonPropertyName("weight")]
    public string? Weight { get; set; }

    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("currency")]
    public string? Currency { get; set; }

    [JsonPropertyName("lowest_recorded_price")]
    public decimal? LowestRecordedPrice { get; set; }

    [JsonPropertyName("highest_recorded_price")]
    public decimal? HighestRecordedPrice { get; set; }

    [JsonPropertyName("images")]
    public List<string>? Images { get; set; }

    [JsonPropertyName("offers")]
    public List<UPCDatabaseOffer>? Offers { get; set; }

    [JsonPropertyName("asin")]
    public string? ASIN { get; set; }

    [JsonPropertyName("elid")]
    public string? ELID { get; set; }
}

/// <summary>
/// Price offer from UPC Database
/// </summary>
public class UPCDatabaseOffer
{
    [JsonPropertyName("merchant")]
    public string? Merchant { get; set; }

    [JsonPropertyName("domain")]
    public string? Domain { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("currency")]
    public string? Currency { get; set; }

    [JsonPropertyName("list_price")]
    public string? ListPrice { get; set; }

    [JsonPropertyName("price")]
    public decimal? Price { get; set; }

    [JsonPropertyName("shipping")]
    public string? Shipping { get; set; }

    [JsonPropertyName("condition")]
    public string? Condition { get; set; }

    [JsonPropertyName("availability")]
    public string? Availability { get; set; }

    [JsonPropertyName("link")]
    public string? Link { get; set; }

    [JsonPropertyName("updated_t")]
    public long? UpdatedTimestamp { get; set; }
}
