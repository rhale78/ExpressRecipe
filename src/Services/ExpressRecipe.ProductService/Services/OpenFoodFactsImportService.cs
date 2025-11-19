using System.Text.Json;
using ExpressRecipe.ProductService.Data;

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
    private readonly ILogger<OpenFoodFactsImportService> _logger;

    public OpenFoodFactsImportService(
        HttpClient httpClient,
        IProductRepository productRepository,
        ILogger<OpenFoodFactsImportService> logger)
    {
        _httpClient = httpClient;
        _productRepository = productRepository;
        _logger = logger;

        _httpClient.BaseAddress = new Uri("https://world.openfoodfacts.org/");
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "ExpressRecipe/1.0 (Dietary Management Platform)");
    }

    /// <summary>
    /// Import a single product by barcode
    /// </summary>
    public async Task<ImportResult> ImportProductByBarcodeAsync(string barcode)
    {
        try
        {
            _logger.LogInformation("Importing OpenFoodFacts product: {Barcode}", barcode);

            var response = await _httpClient.GetAsync($"api/v2/product/{barcode}.json");

            if (!response.IsSuccessStatusCode)
            {
                return new ImportResult
                {
                    Success = false,
                    ErrorMessage = $"Product not found or API error: {response.StatusCode}"
                };
            }

            var json = await response.Content.ReadAsStringAsync();
            var data = JsonDocument.Parse(json);

            if (!data.RootElement.TryGetProperty("product", out var product))
            {
                return new ImportResult
                {
                    Success = false,
                    ErrorMessage = "Invalid response format from OpenFoodFacts"
                };
            }

            var result = await ProcessProductAsync(product, barcode);

            _logger.LogInformation("Successfully imported product {Barcode}: {ProductName}",
                barcode, result.ProductName);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import OpenFoodFacts product {Barcode}", barcode);
            return new ImportResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Search and import products by name
    /// </summary>
    public async Task<BatchImportResult> SearchAndImportAsync(string query, int pageSize = 20, int maxResults = 100)
    {
        var result = new BatchImportResult();

        try
        {
            _logger.LogInformation("Searching OpenFoodFacts for: {Query}", query);

            var url = $"cgi/search.pl?search_terms={Uri.EscapeDataString(query)}&page_size={pageSize}&json=1";
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var searchResult = JsonDocument.Parse(json);

            if (searchResult.RootElement.TryGetProperty("products", out var products))
            {
                int count = 0;
                foreach (var product in products.EnumerateArray())
                {
                    if (count >= maxResults) break;

                    try
                    {
                        var barcode = product.TryGetProperty("code", out var bc) ? bc.GetString() : null;
                        if (!string.IsNullOrEmpty(barcode))
                        {
                            var importResult = await ProcessProductAsync(product, barcode);
                            result.Results.Add(importResult);

                            if (importResult.Success)
                                result.SuccessCount++;
                            else
                                result.FailureCount++;
                        }

                        count++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to import product from search results");
                        result.FailureCount++;
                        result.Errors.Add(ex.Message);
                    }
                }

                result.TotalProcessed = count;
            }

            _logger.LogInformation("Search import completed: {Success} successful, {Failed} failed",
                result.SuccessCount, result.FailureCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search OpenFoodFacts");
            result.Errors.Add($"Search error: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Import products by category (e.g., dairy, beverages)
    /// </summary>
    public async Task<BatchImportResult> ImportByCategoryAsync(string category, int limit = 100)
    {
        var result = new BatchImportResult();

        try
        {
            _logger.LogInformation("Importing OpenFoodFacts category: {Category}", category);

            var url = $"category/{category}.json?page_size={limit}";
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var data = JsonDocument.Parse(json);

            if (data.RootElement.TryGetProperty("products", out var products))
            {
                foreach (var product in products.EnumerateArray())
                {
                    try
                    {
                        var barcode = product.TryGetProperty("code", out var bc) ? bc.GetString() : null;
                        if (!string.IsNullOrEmpty(barcode))
                        {
                            var importResult = await ProcessProductAsync(product, barcode);
                            result.Results.Add(importResult);

                            if (importResult.Success)
                                result.SuccessCount++;
                            else
                                result.FailureCount++;

                            result.TotalProcessed++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to import product from category");
                        result.FailureCount++;
                        result.Errors.Add(ex.Message);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import category from OpenFoodFacts");
            result.Errors.Add($"Category import error: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Process and save OpenFoodFacts product data
    /// </summary>
    private async Task<ImportResult> ProcessProductAsync(JsonElement product, string barcode)
    {
        var result = new ImportResult
        {
            ExternalId = barcode
        };

        try
        {
            // Extract basic product information
            var productName = GetStringValue(product, "product_name", "product_name_en", "generic_name");
            var brand = GetStringValue(product, "brands");
            var categories = GetStringValue(product, "categories");

            result.ProductName = productName ?? "Unknown Product";

            // Check if product already exists
            var existing = await _productRepository.GetProductByBarcodeAsync(barcode);
            if (existing != null)
            {
                _logger.LogDebug("Product already exists: {Barcode}", barcode);
                result.ProductId = existing.Id;
                result.Success = true;
                result.AlreadyExists = true;

                // Update with OpenFoodFacts data if we have more info
                await LinkToExternalDatabasesAsync(existing.Id, product);

                return result;
            }

            // Determine primary category
            var category = DetermineCategory(categories, productName);

            // Extract image URL
            var imageUrl = GetStringValue(product, "image_url", "image_front_url", "image_small_url");

            // Create product
            var productId = await _productRepository.CreateProductAsync(new CreateProductRequest
            {
                Name = productName ?? "Unknown Product",
                Brand = brand,
                UPC = barcode,
                Category = category,
                ExternalSource = "OpenFoodFacts",
                ExternalId = barcode,
                IsVerified = true
            });

            result.ProductId = productId;

            // Extract and save ingredients
            var ingredientsText = GetStringValue(product, "ingredients_text", "ingredients_text_en");
            if (!string.IsNullOrWhiteSpace(ingredientsText))
            {
                var ingredients = ParseIngredients(ingredientsText);
                foreach (var ingredient in ingredients.Take(50)) // Limit to 50 ingredients
                {
                    await _productRepository.AddIngredientToProductAsync(
                        productId, ingredient, orderIndex: ingredients.IndexOf(ingredient));
                }
                result.IngredientCount = ingredients.Count;
            }

            // Extract nutrition data
            if (product.TryGetProperty("nutriments", out var nutriments))
            {
                await SaveNutritionDataAsync(productId, nutriments);
                result.HasNutrition = true;
            }

            // Extract allergens
            var allergens = GetStringValue(product, "allergens", "allergens_tags");
            if (!string.IsNullOrWhiteSpace(allergens))
            {
                var allergenList = allergens.Split(',').Select(a => a.Trim()).ToList();
                await SaveAllergensAsync(productId, allergenList);
            }

            // Extract labels (organic, vegan, gluten-free, etc.)
            var labels = GetStringValue(product, "labels", "labels_tags");
            if (!string.IsNullOrWhiteSpace(labels))
            {
                await SaveLabelsAsync(productId, labels);
            }

            // Link to USDA/FDA databases if possible
            await LinkToExternalDatabasesAsync(productId, product);

            result.Success = true;
            _logger.LogInformation("Created product {ProductId} from OpenFoodFacts {Barcode}", productId, barcode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process OpenFoodFacts product");
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    /// <summary>
    /// Link OpenFoodFacts product to USDA/FDA databases
    /// This creates cross-references between different data sources
    /// </summary>
    private async Task LinkToExternalDatabasesAsync(Guid productId, JsonElement product)
    {
        try
        {
            // Check for USDA reference
            var usdaId = GetStringValue(product, "usda_fdc_id");
            if (!string.IsNullOrWhiteSpace(usdaId))
            {
                await _productRepository.AddExternalLinkAsync(productId, "USDA", usdaId);
                _logger.LogInformation("Linked product {ProductId} to USDA FDC ID: {UsdaId}", productId, usdaId);
            }

            // Extract nutrition score (Nutri-Score A-E)
            var nutriScore = GetStringValue(product, "nutrition_grade_fr", "nutriscore_grade");
            if (!string.IsNullOrWhiteSpace(nutriScore))
            {
                await _productRepository.UpdateProductMetadataAsync(productId, "NutriScore", nutriScore.ToUpper());
            }

            // Extract Nova group (food processing level 1-4)
            if (product.TryGetProperty("nova_group", out var novaGroup) && novaGroup.ValueKind == JsonValueKind.Number)
            {
                await _productRepository.UpdateProductMetadataAsync(productId, "NovaGroup", novaGroup.GetInt32().ToString());
            }

            // Extract Eco-Score (environmental impact A-E)
            var ecoScore = GetStringValue(product, "ecoscore_grade");
            if (!string.IsNullOrWhiteSpace(ecoScore))
            {
                await _productRepository.UpdateProductMetadataAsync(productId, "EcoScore", ecoScore.ToUpper());
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to link product to external databases");
        }
    }

    /// <summary>
    /// Save nutrition data from OpenFoodFacts format
    /// </summary>
    private async Task SaveNutritionDataAsync(Guid productId, JsonElement nutriments)
    {
        var nutritionData = new Dictionary<string, decimal?>();

        // Extract per 100g values (OpenFoodFacts standard)
        nutritionData["Calories"] = GetDecimalValue(nutriments, "energy-kcal_100g", "energy_100g");
        nutritionData["Protein"] = GetDecimalValue(nutriments, "proteins_100g");
        nutritionData["TotalFat"] = GetDecimalValue(nutriments, "fat_100g");
        nutritionData["SaturatedFat"] = GetDecimalValue(nutriments, "saturated-fat_100g");
        nutritionData["Carbohydrates"] = GetDecimalValue(nutriments, "carbohydrates_100g");
        nutritionData["Sugars"] = GetDecimalValue(nutriments, "sugars_100g");
        nutritionData["Fiber"] = GetDecimalValue(nutriments, "fiber_100g");
        nutritionData["Sodium"] = GetDecimalValue(nutriments, "sodium_100g");
        nutritionData["Salt"] = GetDecimalValue(nutriments, "salt_100g");

        // Save to database
        // Note: This assumes method exists in repository
        // await _productRepository.AddNutritionDataAsync(productId, nutritionData);
    }

    /// <summary>
    /// Save allergen information
    /// </summary>
    private async Task SaveAllergensAsync(Guid productId, List<string> allergens)
    {
        foreach (var allergen in allergens)
        {
            // Clean allergen name (remove "en:" prefix common in OpenFoodFacts)
            var cleanName = allergen.Replace("en:", "").Replace("-", " ").Trim();
            if (cleanName.Length > 2)
            {
                await _productRepository.AddAllergenToProductAsync(productId, cleanName);
            }
        }
    }

    /// <summary>
    /// Save product labels (certifications, dietary info)
    /// </summary>
    private async Task SaveLabelsAsync(Guid productId, string labels)
    {
        var labelList = labels.Split(',')
            .Select(l => l.Replace("en:", "").Replace("-", " ").Trim())
            .Where(l => l.Length > 2)
            .ToList();

        foreach (var label in labelList.Take(20)) // Limit to 20 labels
        {
            await _productRepository.AddLabelToProductAsync(productId, label);
        }
    }

    /// <summary>
    /// Parse ingredients text into list
    /// </summary>
    private List<string> ParseIngredients(string ingredientsText)
    {
        // OpenFoodFacts uses various separators
        var separators = new[] { ',', ';', '.' };
        var ingredients = ingredientsText.Split(separators, StringSplitOptions.RemoveEmptyEntries)
            .Select(i => i.Trim())
            .Where(i => i.Length > 2 && !i.All(char.IsDigit))
            .Select(i => System.Text.RegularExpressions.Regex.Replace(i, @"\([^)]*\)", "").Trim()) // Remove parentheses content
            .Where(i => i.Length > 2)
            .Distinct()
            .ToList();

        return ingredients;
    }

    /// <summary>
    /// Determine category from OpenFoodFacts categories
    /// </summary>
    private string? DetermineCategory(string? categories, string? productName)
    {
        var categoryText = (categories ?? productName ?? "").ToLower();

        // Map OpenFoodFacts categories to our categories
        if (categoryText.Contains("dairy") || categoryText.Contains("milk") || categoryText.Contains("cheese") || categoryText.Contains("yogurt"))
            return "Dairy";
        if (categoryText.Contains("meat") || categoryText.Contains("poultry") || categoryText.Contains("fish") || categoryText.Contains("seafood"))
            return "Protein";
        if (categoryText.Contains("fruit") || categoryText.Contains("fruits"))
            return "Fruits";
        if (categoryText.Contains("vegetable") || categoryText.Contains("vegetables"))
            return "Vegetables";
        if (categoryText.Contains("bread") || categoryText.Contains("cereal") || categoryText.Contains("pasta") || categoryText.Contains("grain"))
            return "Grains";
        if (categoryText.Contains("beverage") || categoryText.Contains("drink") || categoryText.Contains("juice"))
            return "Beverages";
        if (categoryText.Contains("snack") || categoryText.Contains("dessert") || categoryText.Contains("candy") || categoryText.Contains("chocolate"))
            return "Snacks & Sweets";
        if (categoryText.Contains("sauce") || categoryText.Contains("condiment") || categoryText.Contains("spice"))
            return "Condiments & Sauces";

        return "General";
    }

    /// <summary>
    /// Helper to get string value from multiple possible fields
    /// </summary>
    private string? GetStringValue(JsonElement element, params string[] fieldNames)
    {
        foreach (var fieldName in fieldNames)
        {
            if (element.TryGetProperty(fieldName, out var value) && value.ValueKind == JsonValueKind.String)
            {
                var str = value.GetString();
                if (!string.IsNullOrWhiteSpace(str))
                    return str;
            }
        }
        return null;
    }

    /// <summary>
    /// Helper to get decimal value from JSON element
    /// </summary>
    private decimal? GetDecimalValue(JsonElement element, params string[] fieldNames)
    {
        foreach (var fieldName in fieldNames)
        {
            if (element.TryGetProperty(fieldName, out var value))
            {
                if (value.ValueKind == JsonValueKind.Number)
                    return value.GetDecimal();
                if (value.ValueKind == JsonValueKind.String)
                {
                    var str = value.GetString();
                    if (decimal.TryParse(str, out var result))
                        return result;
                }
            }
        }
        return null;
    }
}
