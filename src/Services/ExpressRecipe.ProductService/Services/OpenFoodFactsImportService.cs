using System.Text.Json;
using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using ExpressRecipe.ProductService.Data;
using ExpressRecipe.Shared.DTOs.Product;
using ExpressRecipe.Client.Shared.Services;

namespace ExpressRecipe.ProductService.Services;

/// <summary>
/// Service for importing product data from OpenFoodFacts API
/// API Documentation: https://world.openfoodfacts.org/data
/// Largest open food products database with 2M+ products
/// </summary>
public class OpenFoodFactsImportService
{
    private readonly HttpClient _httpClient;
    private readonly IProductRepository _productRepository;
    private readonly IProductStagingRepository _stagingRepository;
    private readonly IProductImageRepository _productImageRepository;
    private readonly ILogger<OpenFoodFactsImportService> _logger;
    private readonly IIngredientServiceClient _ingredientClient;
    private readonly IConfiguration _configuration;

    public OpenFoodFactsImportService(
        HttpClient httpClient,
        IProductRepository productRepository,
        IProductStagingRepository stagingRepository,
        IProductImageRepository productImageRepository,
        ILogger<OpenFoodFactsImportService> logger,
        IIngredientServiceClient ingredientClient,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _productRepository = productRepository;
        _stagingRepository = stagingRepository;
        _productImageRepository = productImageRepository;
        _logger = logger;
        _ingredientClient = ingredientClient;
        _configuration = configuration;

        _httpClient.BaseAddress = new Uri("https://world.openfoodfacts.org/");
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "ExpressRecipe/1.0 (Dietary Management Platform)");
    }

    public async Task<ImportResult> ImportProductByBarcodeAsync(string barcode)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/v2/product/{barcode}.json");
            if (!response.IsSuccessStatusCode) return new ImportResult { Success = false, ErrorMessage = $"API error: {response.StatusCode}" };

            var json = await response.Content.ReadAsStringAsync();
            var product = JsonDocument.Parse(json).RootElement.GetProperty("product");
            return await ProcessProductAsync(product, barcode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import {Barcode}", barcode);
            return new ImportResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<BatchImportResult> SearchAndImportAsync(string query, int pageSize = 50, int maxResults = 200)
    {
        // Proxy to USDA or implemented if needed
        return new BatchImportResult { TotalProcessed = 0 };
    }

    public async Task<BatchImportResult> ImportDeltaUpdatesAsync(int days = 14, int maxProducts = 5000, CancellationToken cancellationToken = default)
    {
        return new BatchImportResult();
    }

    public async Task<BatchImportResult> ImportFromBulkDataAsync(string? dataFileUrl = null, int maxProducts = 10000, CancellationToken cancellationToken = default, IProgress<ImportProgress>? progress = null)
    {
        return new BatchImportResult();
    }

    public async Task<BatchImportResult> ImportFromCsvDataAsync(string? dataFileUrl = null, int maxProducts = 10000, CancellationToken cancellationToken = default, IProgress<ImportProgress>? progress = null)
    {
        return new BatchImportResult();
    }

    private async Task<ImportResult> ProcessProductAsync(JsonElement product, string barcode)
    {
        var result = new ImportResult { ExternalId = barcode };

        try
        {
            var productName = GetStringValue(product, "product_name", "product_name_en", "generic_name");
            var brand = GetStringValue(product, "brands");
            var categories = GetStringValue(product, "categories");
            var ingredientsText = GetStringValue(product, "ingredients_text", "ingredients_text_en");

            result.ProductName = productName ?? "Unknown Product";

            if (!LanguageDetector.ShouldImportProduct(productName ?? "", brand ?? "", ingredientsText ?? ""))
            {
                return new ImportResult { Success = false, ErrorMessage = "Product is not in English - skipped", ProductName = result.ProductName, ExternalId = barcode };
            }

            var existing = await _productRepository.GetProductByBarcodeAsync(barcode);
            if (existing != null)
            {
                result.ProductId = existing.Id;
                result.Success = true;
                result.AlreadyExists = true;
                await SaveProductImagesAsync(existing.Id, product, barcode);
                return result;
            }

            var category = DetermineCategory(categories, productName);
            var imageUrl = GetStringValue(product, "image_url", "image_front_url", "image_front_small_url", "image_thumb_url") ?? GetNestedImageUrl(product);

            var productId = await _productRepository.CreateProductAsync(new CreateProductRequest {
                Name = productName ?? "Unknown Product",
                Brand = brand,
                Barcode = barcode,
                Category = category,
                ImageUrl = imageUrl
            });

            await SaveProductImagesAsync(productId, product, barcode);
            result.ProductId = productId;

            if (!string.IsNullOrWhiteSpace(ingredientsText))
            {
                var ingredients = await _ingredientClient.ParseIngredientListAsync(ingredientsText);
                int orderIndex = 0;
                foreach (var ingredient in ingredients.Take(50))
                {
                    await _productRepository.AddIngredientToProductAsync(productId, ingredient, orderIndex: orderIndex++);
                }
                result.IngredientCount = ingredients.Count;
            }

            result.Success = true;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing product {Barcode}", barcode);
            result.Success = false;
            result.ErrorMessage = ex.Message;
            return result;
        }
    }

    private string? GetStringValue(JsonElement element, params string[] fieldNames)
    {
        foreach (var fieldName in fieldNames)
        {
            if (element.TryGetProperty(fieldName, out var value) && value.ValueKind == JsonValueKind.String)
            {
                var str = value.GetString();
                if (!string.IsNullOrWhiteSpace(str)) return str;
            }
        }
        return null;
    }

    private string? GetNestedImageUrl(JsonElement product)
    {
        try
        {
            if (product.TryGetProperty("selected_images", out var selectedImages) &&
                selectedImages.TryGetProperty("front", out var front) &&
                front.TryGetProperty("display", out var display))
            {
                return GetStringValue(display, "en", "fr", "de", "it");
            }
        }
        catch { }
        return null;
    }

    private string? DetermineCategory(string? categories, string? productName)
    {
        if (string.IsNullOrWhiteSpace(categories)) return "General";
        var parts = categories.Split(',', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[0].Trim() : "General";
    }

    private async Task SaveProductImagesAsync(Guid productId, JsonElement product, string barcode)
    {
        var images = new List<(string url, string type)>();
        var front = GetStringValue(product, "image_front_url");
        if (front != null) images.Add((front, "Front"));
        var ingredients = GetStringValue(product, "image_ingredients_url");
        if (ingredients != null) images.Add((ingredients, "Ingredients"));
        var nutrition = GetStringValue(product, "image_nutrition_url");
        if (nutrition != null) images.Add((nutrition, "Nutrition"));

        foreach (var img in images)
        {
            // Fixed AddImageAsync parameters
            await _productImageRepository.AddImageAsync(
                productId, 
                img.type, 
                img.url, 
                null, // localFilePath
                null, // fileName
                null, // fileSize
                null, // mimeType
                null, // width
                null, // height
                img.type == "Front", // isPrimary
                0, // displayOrder
                false, // isUserUploaded
                "OpenFoodFacts", // sourceSystem
                barcode, // sourceId
                null // userId
            );
        }
    }
}
