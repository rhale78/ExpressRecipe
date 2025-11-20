using System.Text.Json;
using System.Text.Json.Serialization;

namespace ExpressRecipe.ScannerService.Services;

/// <summary>
/// Client for OpenFoodFacts API - world's largest open food products database
/// API Documentation: https://wiki.openfoodfacts.org/API
/// </summary>
public class OpenFoodFactsApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenFoodFactsApiClient> _logger;

    public OpenFoodFactsApiClient(HttpClient httpClient, ILogger<OpenFoodFactsApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        _httpClient.BaseAddress = new Uri("https://world.openfoodfacts.org/");
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "ExpressRecipe/1.0 (dietary management app)");
    }

    /// <summary>
    /// Lookup product by barcode (EAN-13, UPC-A, etc.)
    /// </summary>
    public async Task<OpenFoodFactsProduct?> GetProductByBarcodeAsync(string barcode)
    {
        try
        {
            _logger.LogInformation("Looking up barcode in OpenFoodFacts: {Barcode}", barcode);

            // Clean barcode - remove spaces and dashes
            barcode = barcode.Replace(" ", "").Replace("-", "");

            var response = await _httpClient.GetAsync($"api/v2/product/{barcode}.json");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("OpenFoodFacts API returned {StatusCode} for barcode {Barcode}",
                    response.StatusCode, barcode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<OpenFoodFactsResponse>(json);

            if (result?.Status != 1 || result.Product == null)
            {
                _logger.LogInformation("Product not found in OpenFoodFacts: {Barcode}", barcode);
                return null;
            }

            _logger.LogInformation("Found product in OpenFoodFacts: {ProductName}", result.Product.ProductName);
            return result.Product;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to lookup barcode in OpenFoodFacts: {Barcode}", barcode);
            return null;
        }
    }

    /// <summary>
    /// Search products by name
    /// </summary>
    public async Task<List<OpenFoodFactsProduct>> SearchProductsAsync(string query, int page = 1, int pageSize = 20)
    {
        try
        {
            _logger.LogInformation("Searching OpenFoodFacts for: {Query}", query);

            var response = await _httpClient.GetAsync(
                $"cgi/search.pl?search_terms={Uri.EscapeDataString(query)}&page={page}&page_size={pageSize}&json=1");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("OpenFoodFacts search returned {StatusCode}", response.StatusCode);
                return new List<OpenFoodFactsProduct>();
            }

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<OpenFoodFactsSearchResponse>(json);

            return result?.Products ?? new List<OpenFoodFactsProduct>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search OpenFoodFacts: {Query}", query);
            return new List<OpenFoodFactsProduct>();
        }
    }
}

/// <summary>
/// OpenFoodFacts API response wrapper
/// </summary>
public class OpenFoodFactsResponse
{
    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("status_verbose")]
    public string? StatusVerbose { get; set; }

    [JsonPropertyName("product")]
    public OpenFoodFactsProduct? Product { get; set; }
}

/// <summary>
/// OpenFoodFacts search response
/// </summary>
public class OpenFoodFactsSearchResponse
{
    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("page_size")]
    public int PageSize { get; set; }

    [JsonPropertyName("products")]
    public List<OpenFoodFactsProduct>? Products { get; set; }
}

/// <summary>
/// OpenFoodFacts product data
/// </summary>
public class OpenFoodFactsProduct
{
    [JsonPropertyName("code")]
    public string? Barcode { get; set; }

    [JsonPropertyName("product_name")]
    public string? ProductName { get; set; }

    [JsonPropertyName("generic_name")]
    public string? GenericName { get; set; }

    [JsonPropertyName("brands")]
    public string? Brands { get; set; }

    [JsonPropertyName("brands_tags")]
    public List<string>? BrandsTags { get; set; }

    [JsonPropertyName("categories")]
    public string? Categories { get; set; }

    [JsonPropertyName("categories_tags")]
    public List<string>? CategoriesTags { get; set; }

    [JsonPropertyName("image_url")]
    public string? ImageUrl { get; set; }

    [JsonPropertyName("image_small_url")]
    public string? ImageSmallUrl { get; set; }

    [JsonPropertyName("image_front_url")]
    public string? ImageFrontUrl { get; set; }

    [JsonPropertyName("ingredients_text")]
    public string? IngredientsText { get; set; }

    [JsonPropertyName("ingredients")]
    public List<OpenFoodFactsIngredient>? Ingredients { get; set; }

    [JsonPropertyName("allergens")]
    public string? Allergens { get; set; }

    [JsonPropertyName("allergens_tags")]
    public List<string>? AllergensTags { get; set; }

    [JsonPropertyName("traces")]
    public string? Traces { get; set; }

    [JsonPropertyName("traces_tags")]
    public List<string>? TracesTags { get; set; }

    [JsonPropertyName("nutriments")]
    public OpenFoodFactsNutriments? Nutriments { get; set; }

    [JsonPropertyName("nutrition_grades")]
    public string? NutritionGrade { get; set; }

    [JsonPropertyName("serving_size")]
    public string? ServingSize { get; set; }

    [JsonPropertyName("serving_quantity")]
    public decimal? ServingQuantity { get; set; }

    [JsonPropertyName("quantity")]
    public string? Quantity { get; set; }

    [JsonPropertyName("packaging")]
    public string? Packaging { get; set; }

    [JsonPropertyName("countries")]
    public string? Countries { get; set; }

    [JsonPropertyName("countries_tags")]
    public List<string>? CountriesTags { get; set; }

    [JsonPropertyName("stores")]
    public string? Stores { get; set; }

    [JsonPropertyName("expiration_date")]
    public string? ExpirationDate { get; set; }

    [JsonPropertyName("product_quantity")]
    public decimal? ProductQuantity { get; set; }
}

/// <summary>
/// Ingredient information from OpenFoodFacts
/// </summary>
public class OpenFoodFactsIngredient
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("percent")]
    public decimal? Percent { get; set; }

    [JsonPropertyName("vegan")]
    public string? Vegan { get; set; }

    [JsonPropertyName("vegetarian")]
    public string? Vegetarian { get; set; }
}

/// <summary>
/// Nutritional information from OpenFoodFacts
/// </summary>
public class OpenFoodFactsNutriments
{
    [JsonPropertyName("energy-kcal")]
    public decimal? EnergyKcal { get; set; }

    [JsonPropertyName("energy-kcal_100g")]
    public decimal? EnergyKcal100g { get; set; }

    [JsonPropertyName("fat")]
    public decimal? Fat { get; set; }

    [JsonPropertyName("fat_100g")]
    public decimal? Fat100g { get; set; }

    [JsonPropertyName("saturated-fat")]
    public decimal? SaturatedFat { get; set; }

    [JsonPropertyName("saturated-fat_100g")]
    public decimal? SaturatedFat100g { get; set; }

    [JsonPropertyName("carbohydrates")]
    public decimal? Carbohydrates { get; set; }

    [JsonPropertyName("carbohydrates_100g")]
    public decimal? Carbohydrates100g { get; set; }

    [JsonPropertyName("sugars")]
    public decimal? Sugars { get; set; }

    [JsonPropertyName("sugars_100g")]
    public decimal? Sugars100g { get; set; }

    [JsonPropertyName("fiber")]
    public decimal? Fiber { get; set; }

    [JsonPropertyName("fiber_100g")]
    public decimal? Fiber100g { get; set; }

    [JsonPropertyName("proteins")]
    public decimal? Proteins { get; set; }

    [JsonPropertyName("proteins_100g")]
    public decimal? Proteins100g { get; set; }

    [JsonPropertyName("salt")]
    public decimal? Salt { get; set; }

    [JsonPropertyName("salt_100g")]
    public decimal? Salt100g { get; set; }

    [JsonPropertyName("sodium")]
    public decimal? Sodium { get; set; }

    [JsonPropertyName("sodium_100g")]
    public decimal? Sodium100g { get; set; }
}
