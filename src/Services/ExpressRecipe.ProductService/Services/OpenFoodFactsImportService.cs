using System.Text.Json;
using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using ExpressRecipe.ProductService.Data;
using ExpressRecipe.Shared.DTOs.Product;

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
    private readonly IIngredientListParser _ingredientListParser;
    private readonly IConfiguration _configuration;

    public OpenFoodFactsImportService(
        HttpClient httpClient,
        IProductRepository productRepository,
        IProductStagingRepository stagingRepository,
        IProductImageRepository productImageRepository,
        ILogger<OpenFoodFactsImportService> logger,
        IIngredientListParser ingredientListParser,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _productRepository = productRepository;
        _stagingRepository = stagingRepository;
        _productImageRepository = productImageRepository;
        _logger = logger;
        _ingredientListParser = ingredientListParser;
        _configuration = configuration;

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
            _logger.LogInformation("========== IMPORTING OpenFoodFacts product: {Barcode} ==========", barcode);

            var response = await _httpClient.GetAsync($"api/v2/product/{barcode}.json");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("OpenFoodFacts API returned error for barcode {Barcode}: {StatusCode}", barcode, response.StatusCode);
                return new ImportResult
                {
                    Success = false,
                    ErrorMessage = $"Product not found or API error: {response.StatusCode}"
                };
            }

            var json = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("OpenFoodFacts API response for barcode {Barcode}: {Json}", barcode, json);

            var data = JsonDocument.Parse(json);

            if (!data.RootElement.TryGetProperty("product", out var product))
            {
                _logger.LogError("Invalid OpenFoodFacts response format for barcode {Barcode} - missing 'product' property", barcode);
                return new ImportResult
                {
                    Success = false,
                    ErrorMessage = "Invalid response format from OpenFoodFacts"
                };
            }

            _logger.LogDebug("Product JSON for barcode {Barcode}: {ProductJson}", barcode, product.GetRawText());

            // Log all image-related fields found in the response
            LogImageFieldsFromJson(product, barcode);

            var result = await ProcessProductAsync(product, barcode);

            _logger.LogInformation("========== Successfully imported product {Barcode}: {ProductName} ==========",
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
    /// Log all image-related fields found in the OpenFoodFacts JSON response
    /// </summary>
    private void LogImageFieldsFromJson(JsonElement product, string barcode)
    {
        try
        {
            _logger.LogInformation(">>> Extracting image URLs for barcode {Barcode}:", barcode);

            // Check top-level image fields
            var imageUrl = GetStringValue(product, "image_url");
            var imageFrontUrl = GetStringValue(product, "image_front_url");
            var imageFrontSmallUrl = GetStringValue(product, "image_front_small_url");
            var imageThumbUrl = GetStringValue(product, "image_thumb_url");
            var imageNutritionUrl = GetStringValue(product, "image_nutrition_url");
            var imageNutritionSmallUrl = GetStringValue(product, "image_nutrition_small_url");
            var imageIngredientsUrl = GetStringValue(product, "image_ingredients_url");
            var imageIngredientsSmallUrl = GetStringValue(product, "image_ingredients_small_url");

            _logger.LogInformation("  image_url: {Value}", imageUrl ?? "(null)");
            _logger.LogInformation("  image_front_url: {Value}", imageFrontUrl ?? "(null)");
            _logger.LogInformation("  image_front_small_url: {Value}", imageFrontSmallUrl ?? "(null)");
            _logger.LogInformation("  image_thumb_url: {Value}", imageThumbUrl ?? "(null)");
            _logger.LogInformation("  image_nutrition_url: {Value}", imageNutritionUrl ?? "(null)");
            _logger.LogInformation("  image_nutrition_small_url: {Value}", imageNutritionSmallUrl ?? "(null)");
            _logger.LogInformation("  image_ingredients_url: {Value}", imageIngredientsUrl ?? "(null)");
            _logger.LogInformation("  image_ingredients_small_url: {Value}", imageIngredientsSmallUrl ?? "(null)");

            // Check for selected_images structure
            if (product.TryGetProperty("selected_images", out var selectedImages))
            {
                _logger.LogInformation("  selected_images structure found:");
                _logger.LogDebug("    Full selected_images JSON: {Json}", selectedImages.GetRawText());

                // Check each image type in selected_images
                CheckNestedImage(selectedImages, "front", barcode);
                CheckNestedImage(selectedImages, "nutrition", barcode);
                CheckNestedImage(selectedImages, "ingredients", barcode);
                CheckNestedImage(selectedImages, "back", barcode);
            }
            else
            {
                _logger.LogWarning("  selected_images structure NOT found in response");
            }

            // Check for images structure
            if (product.TryGetProperty("images", out var images))
            {
                _logger.LogInformation("  images object found with keys: {Keys}",
                    string.Join(", ", images.EnumerateObject().Select(p => p.Name)));
                _logger.LogDebug("    Full images JSON: {Json}", images.GetRawText());
            }
            else
            {
                _logger.LogWarning("  images object NOT found in response");
            }

            _logger.LogInformation("<<< Finished extracting image URLs for barcode {Barcode}", barcode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging image fields for barcode {Barcode}", barcode);
        }
    }

    /// <summary>
    /// Check and log nested image structure
    /// </summary>
    private void CheckNestedImage(JsonElement selectedImages, string imageType, string barcode)
    {
        if (selectedImages.TryGetProperty(imageType, out var imageTypeElement))
        {
            _logger.LogInformation("    {ImageType} image found in selected_images", imageType);

            if (imageTypeElement.TryGetProperty("display", out var display))
            {
                _logger.LogInformation("      display property found with keys: {Keys}",
                    string.Join(", ", display.EnumerateObject().Select(p => p.Name)));

                foreach (var lang in display.EnumerateObject())
                {
                    _logger.LogInformation("        {Lang}: {Url}", lang.Name, lang.Value.GetString() ?? "(null)");
                }
            }
            else
            {
                _logger.LogWarning("      display property NOT found for {ImageType}", imageType);
            }
        }
        else
        {
            _logger.LogDebug("    {ImageType} image NOT found in selected_images", imageType);
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
                            _logger.LogInformation("========== Processing product {Count}/{MaxResults}: barcode {Barcode} ==========", count + 1, maxResults, barcode);

                            // Log product JSON for debugging
                            _logger.LogDebug("Product JSON from search for barcode {Barcode}: {ProductJson}", barcode, product.GetRawText());

                            // Log image fields
                            LogImageFieldsFromJson(product, barcode);

                            var importResult = await ProcessProductAsync(product, barcode);
                            result.Results.Add(importResult);

                            if (importResult.Success)
                                result.SuccessCount++;
                            else
                                result.FailureCount++;
                        }
                        else
                        {
                            _logger.LogWarning("Skipping product {Count} - no barcode found", count + 1);
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
            else
            {
                _logger.LogWarning("No products array found in search results for query: {Query}", query);
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
            var ingredientsText = GetStringValue(product, "ingredients_text", "ingredients_text_en");

            result.ProductName = productName ?? "Unknown Product";

            _logger.LogInformation(">>> Processing product: {ProductName} (Barcode: {Barcode}, Brand: {Brand})",
                result.ProductName, barcode, brand ?? "(no brand)");

            // Language detection - skip non-English products
            if (!LanguageDetector.ShouldImportProduct(productName ?? "", brand ?? "", ingredientsText ?? ""))
            {
                _logger.LogInformation("Skipping non-English product: {ProductName} (Barcode: {Barcode})",
                    result.ProductName, barcode);
                return new ImportResult
                {
                    Success = false,
                    ErrorMessage = "Product is not in English - skipped",
                    ProductName = result.ProductName,
                    ExternalId = barcode
                };
            }

            // Check if product already exists
            _logger.LogDebug("Checking if product with barcode {Barcode} already exists in database...", barcode);
            var existing = await _productRepository.GetProductByBarcodeAsync(barcode);

            if (existing != null)
            {
                _logger.LogInformation("!!! Product ALREADY EXISTS in database: {ProductName} (ID: {ProductId}, Barcode: {Barcode})",
                    existing.Name, existing.Id, barcode);
                _logger.LogInformation("    Will update existing product and save images");

                result.ProductId = existing.Id;
                result.Success = true;
                result.AlreadyExists = true;

                // Update with OpenFoodFacts data if we have more info
                await LinkToExternalDatabasesAsync(existing.Id, product);

                // Extract and save all available product images (even for existing products)
                await SaveProductImagesAsync(existing.Id, product, barcode);

                _logger.LogInformation("<<< Finished updating existing product {ProductId}", existing.Id);
                return result;
            }

            _logger.LogInformation(">>> Product NOT found in database. Creating NEW product for barcode {Barcode}", barcode);

            // Determine primary category
            var category = DetermineCategory(categories, productName);

            // Extract legacy image URL for backward compatibility
            var imageUrl = GetStringValue(product, "image_url", "image_front_url", "image_front_small_url", "image_thumb_url")
                ?? GetNestedImageUrl(product);

            _logger.LogInformation("    Creating product: Name={Name}, Brand={Brand}, Category={Category}, ImageUrl={ImageUrl}",
                productName ?? "Unknown", brand ?? "(none)", category ?? "(none)", imageUrl ?? "(none)");

            // Create product
            var productId = await _productRepository.CreateProductAsync(new Shared.DTOs.Product.CreateProductRequest {
                Name = productName ?? "Unknown Product",
                Brand = brand,
                Barcode = barcode,
                Category = category,
                ImageUrl = imageUrl
            });

            _logger.LogInformation("    ✓ Product created with ID: {ProductId}", productId);

            // Extract and save all available product images
            await SaveProductImagesAsync(productId, product, barcode);

            result.ProductId = productId;

            // Extract and save ingredients using advanced parser
            if (!string.IsNullOrWhiteSpace(ingredientsText))
            {
                var ingredients = _ingredientListParser.ParseIngredients(ingredientsText);
                int orderIndex = 0;
                foreach (var ingredient in ingredients.Take(50)) // Limit to 50 ingredients
                {
                    await _productRepository.AddIngredientToProductAsync(
                        productId, ingredient, orderIndex: orderIndex++);
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
    /// Extract and save all available product images from OpenFoodFacts
    /// Saves both display (400px) and small (200px) variants for better performance
    /// </summary>
    private async Task SaveProductImagesAsync(Guid productId, JsonElement product, string barcode)
    {
        var displayOrder = 0;
        var imageCount = 0;

        try
        {
            _logger.LogInformation("Starting image import for product {ProductId} from OpenFoodFacts barcode {Barcode}",
                productId, barcode);

            // Front image - display size (400px, primary)
            var frontUrl = GetStringValue(product, "image_front_url", "image_url")
                ?? GetNestedImageUrl(product, "front", "display");
            if (!string.IsNullOrWhiteSpace(frontUrl))
            {
                _logger.LogDebug("Found front image URL (display): {Url}", frontUrl);
                try
                {
                    await _productImageRepository.AddImageAsync(
                        productId: productId,
                        imageType: "Front",
                        imageUrl: frontUrl,
                        localFilePath: null,
                        fileName: null,
                        fileSize: null,
                        mimeType: "image/jpeg",
                        width: null,
                        height: null,
                        isPrimary: true,
                        displayOrder: displayOrder++,
                        isUserUploaded: false,
                        sourceSystem: "OpenFoodFacts",
                        sourceId: barcode,
                        userId: null
                    );
                    imageCount++;
                    _logger.LogInformation("🖼️ Imported front image (display, primary) for product {ProductId}", productId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to save front image for product {ProductId}. URL: {Url}", productId, frontUrl);
                    throw; // Re-throw to surface the error
                }
            }
            else
            {
                _logger.LogWarning("⚠️ No front image URL found for product {ProductId} barcode {Barcode}", productId, barcode);
            }

            // Front image - small size (200px, thumbnail)
            var frontSmallUrl = GetStringValue(product, "image_front_small_url")
                ?? GetNestedImageUrl(product, "front", "small");
            if (!string.IsNullOrWhiteSpace(frontSmallUrl) && frontSmallUrl != frontUrl)
            {
                _logger.LogDebug("Found front image URL (small): {Url}", frontSmallUrl);
                try
                {
                    await _productImageRepository.AddImageAsync(
                        productId: productId,
                        imageType: "Front",
                        imageUrl: frontSmallUrl,
                        localFilePath: null,
                        fileName: null,
                        fileSize: null,
                        mimeType: "image/jpeg",
                        width: null,
                        height: null,
                        isPrimary: false,
                        displayOrder: displayOrder++,
                        isUserUploaded: false,
                        sourceSystem: "OpenFoodFacts",
                        sourceId: barcode,
                        userId: null
                    );
                    imageCount++;
                    _logger.LogInformation("🖼️ Imported front image (small) for product {ProductId}", productId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to save front small image for product {ProductId}. Continuing...", productId);
                }
            }

            // Nutrition facts image - display size (400px)
            var nutritionUrl = GetStringValue(product, "image_nutrition_url")
                ?? GetNestedImageUrl(product, "nutrition", "display");
            if (!string.IsNullOrWhiteSpace(nutritionUrl))
            {
                _logger.LogDebug("Found nutrition image URL (display): {Url}", nutritionUrl);
                try
                {
                    await _productImageRepository.AddImageAsync(
                        productId: productId,
                        imageType: "Nutrition",
                        imageUrl: nutritionUrl,
                        localFilePath: null,
                        fileName: null,
                        fileSize: null,
                        mimeType: "image/jpeg",
                        width: null,
                        height: null,
                        isPrimary: false,
                        displayOrder: displayOrder++,
                        isUserUploaded: false,
                        sourceSystem: "OpenFoodFacts",
                        sourceId: barcode,
                        userId: null
                    );
                    imageCount++;
                    _logger.LogInformation("🖼️ Imported nutrition image (display) for product {ProductId}", productId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to save nutrition image for product {ProductId}. Continuing...", productId);
                }
            }

            // Nutrition facts image - small size (200px)
            var nutritionSmallUrl = GetStringValue(product, "image_nutrition_small_url")
                ?? GetNestedImageUrl(product, "nutrition", "small");
            if (!string.IsNullOrWhiteSpace(nutritionSmallUrl) && nutritionSmallUrl != nutritionUrl)
            {
                _logger.LogDebug("Found nutrition image URL (small): {Url}", nutritionSmallUrl);
                try
                {
                    await _productImageRepository.AddImageAsync(
                        productId: productId,
                        imageType: "Nutrition",
                        imageUrl: nutritionSmallUrl,
                        localFilePath: null,
                        fileName: null,
                        fileSize: null,
                        mimeType: "image/jpeg",
                        width: null,
                        height: null,
                        isPrimary: false,
                        displayOrder: displayOrder++,
                        isUserUploaded: false,
                        sourceSystem: "OpenFoodFacts",
                        sourceId: barcode,
                        userId: null
                    );
                    imageCount++;
                    _logger.LogInformation("🖼️ Imported nutrition image (small) for product {ProductId}", productId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to save nutrition small image for product {ProductId}. Continuing...", productId);
                }
            }

            // Ingredients list image - display size (400px)
            var ingredientsUrl = GetStringValue(product, "image_ingredients_url")
                ?? GetNestedImageUrl(product, "ingredients", "display");
            if (!string.IsNullOrWhiteSpace(ingredientsUrl))
            {
                _logger.LogDebug("Found ingredients image URL (display): {Url}", ingredientsUrl);
                try
                {
                    await _productImageRepository.AddImageAsync(
                        productId: productId,
                        imageType: "Ingredients",
                        imageUrl: ingredientsUrl,
                        localFilePath: null,
                        fileName: null,
                        fileSize: null,
                        mimeType: "image/jpeg",
                        width: null,
                        height: null,
                        isPrimary: false,
                        displayOrder: displayOrder++,
                        isUserUploaded: false,
                        sourceSystem: "OpenFoodFacts",
                        sourceId: barcode,
                        userId: null
                    );
                    imageCount++;
                    _logger.LogInformation("🖼️ Imported ingredients image (display) for product {ProductId}", productId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to save ingredients image for product {ProductId}. Continuing...", productId);
                }
            }

            // Ingredients list image - small size (200px)
            var ingredientsSmallUrl = GetStringValue(product, "image_ingredients_small_url")
                ?? GetNestedImageUrl(product, "ingredients", "small");
            if (!string.IsNullOrWhiteSpace(ingredientsSmallUrl) && ingredientsSmallUrl != ingredientsUrl)
            {
                _logger.LogDebug("Found ingredients image URL (small): {Url}", ingredientsSmallUrl);
                try
                {
                    await _productImageRepository.AddImageAsync(
                        productId: productId,
                        imageType: "Ingredients",
                        imageUrl: ingredientsSmallUrl,
                        localFilePath: null,
                        fileName: null,
                        fileSize: null,
                        mimeType: "image/jpeg",
                        width: null,
                        height: null,
                        isPrimary: false,
                        displayOrder: displayOrder++,
                        isUserUploaded: false,
                        sourceSystem: "OpenFoodFacts",
                        sourceId: barcode,
                        userId: null
                    );
                    imageCount++;
                    _logger.LogInformation("🖼️ Imported ingredients image (small) for product {ProductId}", productId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to save ingredients small image for product {ProductId}. Continuing...", productId);
                }
            }

            // Packaging image - display size (400px)
            // NOTE: Changed from "back" to "packaging" to match OpenFoodFacts terminology
            var packagingUrl = GetStringValue(product, "image_packaging_url")
                ?? GetNestedImageUrl(product, "packaging", "display");
            if (!string.IsNullOrWhiteSpace(packagingUrl))
            {
                _logger.LogDebug("Found packaging image URL (display): {Url}", packagingUrl);
                try
                {
                    await _productImageRepository.AddImageAsync(
                        productId: productId,
                        imageType: "Packaging",
                        imageUrl: packagingUrl,
                        localFilePath: null,
                        fileName: null,
                        fileSize: null,
                        mimeType: "image/jpeg",
                        width: null,
                        height: null,
                        isPrimary: false,
                        displayOrder: displayOrder++,
                        isUserUploaded: false,
                        sourceSystem: "OpenFoodFacts",
                        sourceId: barcode,
                        userId: null
                    );
                    imageCount++;
                    _logger.LogInformation("🖼️ Imported packaging image (display) for product {ProductId}", productId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to save packaging image for product {ProductId}. Continuing...", productId);
                }
            }

            // Packaging image - small size (200px)
            var packagingSmallUrl = GetStringValue(product, "image_packaging_small_url")
                ?? GetNestedImageUrl(product, "packaging", "small");
            if (!string.IsNullOrWhiteSpace(packagingSmallUrl) && packagingSmallUrl != packagingUrl)
            {
                _logger.LogDebug("Found packaging image URL (small): {Url}", packagingSmallUrl);
                try
                {
                    await _productImageRepository.AddImageAsync(
                        productId: productId,
                        imageType: "Packaging",
                        imageUrl: packagingSmallUrl,
                        localFilePath: null,
                        fileName: null,
                        fileSize: null,
                        mimeType: "image/jpeg",
                        width: null,
                        height: null,
                        isPrimary: false,
                        displayOrder: displayOrder++,
                        isUserUploaded: false,
                        sourceSystem: "OpenFoodFacts",
                        sourceId: barcode,
                        userId: null
                    );
                    imageCount++;
                    _logger.LogInformation("🖼️ Imported packaging image (small) for product {ProductId}", productId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to save packaging small image for product {ProductId}. Continuing...", productId);
                }
            }

            if (imageCount > 0)
            {
                _logger.LogInformation("✅ Successfully imported {Count} image(s) for product {ProductId} from OpenFoodFacts",
                    imageCount, productId);
            }
            else
            {
                _logger.LogWarning("⚠️⚠️ No images found for product {ProductId} barcode {Barcode} from OpenFoodFacts",
                    productId, barcode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ CRITICAL: Failed to save product images for {ProductId} from OpenFoodFacts. This likely means the ProductImage table doesn't exist or there's a database connection issue.", productId);
            throw; // Re-throw to bubble up the error
        }
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
    /// Extract image URL from nested selected_images structure
    /// OpenFoodFacts stores images in selected_images.{type}.{size}.{lang}
    /// </summary>
    /// <param name="product">Product JSON element</param>
    /// <param name="imageType">Image type: front, nutrition, ingredients, packaging</param>
    /// <param name="sizeKey">Size variant: display (400px), small (200px), thumb (100px)</param>
    /// <returns>Image URL or null if not found</returns>
    private string? GetNestedImageUrl(JsonElement product, string imageType = "front", string sizeKey = "display")
    {
        try
        {
            // Try selected_images.{imageType}.{sizeKey}.{lang}
            if (product.TryGetProperty("selected_images", out var selectedImages))
            {
                if (selectedImages.TryGetProperty(imageType, out var imageTypeElement))
                {
                    if (imageTypeElement.TryGetProperty(sizeKey, out var sizeElement))
                    {
                        // Try language-specific first
                        var lang = GetStringValue(product, "lang", "lc") ?? "en";
                        if (sizeElement.TryGetProperty(lang, out var langImage))
                        {
                            var url = langImage.GetString();
                            if (!string.IsNullOrWhiteSpace(url))
                                return url;
                        }

                        // Try "en" as fallback
                        if (sizeElement.TryGetProperty("en", out var enImage))
                        {
                            var url = enImage.GetString();
                            if (!string.IsNullOrWhiteSpace(url))
                                return url;
                        }

                        // Try any language as last resort
                        foreach (var prop in sizeElement.EnumerateObject())
                        {
                            var url = prop.Value.GetString();
                            if (!string.IsNullOrWhiteSpace(url))
                                return url;
                        }
                    }
                }
            }
        }
        catch
        {
            // If nested extraction fails, return null
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

    /// <summary>
    /// Import products from OpenFoodFacts bulk data export (JSONL format)
    /// Downloads and processes the en.openfoodfacts.org.products.jsonl.gz file
    /// </summary>
    public async Task<BatchImportResult> ImportFromBulkDataAsync(
        string? dataFileUrl = null,
        int maxProducts = 10000,
        CancellationToken cancellationToken = default,
        IProgress<ImportProgress>? progress = null)
    {
        var result = new BatchImportResult();
        dataFileUrl ??= "https://static.openfoodfacts.org/data/openfoodfacts-products.jsonl.gz";

        try
        {
            _logger.LogInformation("Starting bulk import from OpenFoodFacts: {Url}", dataFileUrl);
            progress?.Report(new ImportProgress { Message = "Downloading data file...", PercentComplete = 0 });

            // Download compressed file
            using var response = await _httpClient.GetAsync(dataFileUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var gzipStream = new System.IO.Compression.GZipStream(stream, System.IO.Compression.CompressionMode.Decompress);
            using var reader = new StreamReader(gzipStream);

            int lineNumber = 0;
            int processedCount = 0;
            int skippedCount = 0;
            var batchSize = 500;
            var stagingBatch = new List<StagedProduct>();

            while (!reader.EndOfStream && processedCount < maxProducts && !cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync();
                lineNumber++;

                if (string.IsNullOrWhiteSpace(line)) continue;

                try
                {
                    var product = JsonDocument.Parse(line).RootElement;

                    // Filter: English products only
                    var lang = GetStringValue(product, "lang", "lc");
                    if (lang != null && !lang.StartsWith("en", StringComparison.OrdinalIgnoreCase))
                    {
                        skippedCount++;
                        continue;
                    }

                    // Filter: Food products only (exclude beauty, pet food, etc.)
                    var categories = GetStringValue(product, "categories", "categories_tags")?.ToLower() ?? "";
                    var productName = GetStringValue(product, "product_name", "product_name_en")?.ToLower() ?? "";

                    // Skip non-food products
                    if (categories.Contains("beauty") || categories.Contains("pet-food") ||
                        categories.Contains("cosmetic") || productName.Contains("shampoo") ||
                        productName.Contains("lotion"))
                    {
                        skippedCount++;
                        continue;
                    }

                    // Skip products without barcode or name
                    var barcode = GetStringValue(product, "code");
                    if (string.IsNullOrWhiteSpace(barcode) || string.IsNullOrWhiteSpace(productName))
                    {
                        skippedCount++;
                        continue;
                    }

                    // Map to staging product
                    var stagedProduct = MapToStagedProduct(product, barcode);
                    stagingBatch.Add(stagedProduct);

                    // Bulk insert when batch is full
                    if (stagingBatch.Count >= batchSize)
                    {
                        var enableAugmentation = _configuration.GetValue<bool>("ProductImport:EnableDataAugmentation", false);
                        var diagnoseImages = _configuration.GetValue<bool>("ProductImport:DiagnoseJsonImages", false);

                        if (enableAugmentation)
                        {
                            var augmented = await _stagingRepository.BulkAugmentStagingProductsAsync(stagingBatch, "JSON");
                            var inserted = await _stagingRepository.BulkInsertStagingProductsAsync(stagingBatch);
                            result.SuccessCount += inserted;
                            result.FailureCount += (stagingBatch.Count - inserted - augmented);
                            processedCount += inserted;
                            result.TotalProcessed = processedCount;

                            if (augmented > 0)
                            {
                                _logger.LogInformation("[JSON] Augmented {Count} existing records", augmented);
                            }

                            // JSON image diagnostics - log products that have images
                            if (diagnoseImages && inserted > 0)
                            {
                                var withImages = stagingBatch.Where(p => !string.IsNullOrWhiteSpace(p.ImageUrl)).ToList();
                                if (withImages.Any())
                                {
                                    _logger.LogWarning("[JSON-IMAGES] Found {Count} products with images in this batch", withImages.Count);
                                    // Log a few examples with their image URLs
                                    foreach (var p in withImages.Take(3))
                                    {
                                        _logger.LogWarning("[JSON-IMAGES] Barcode: {Barcode}, Name: {Name}, ImageUrl: {Url}",
                                            p.Barcode, p.ProductName, p.ImageUrl);
                                    }
                                }
                            }
                        }
                        else
                        {
                            var inserted = await _stagingRepository.BulkInsertStagingProductsAsync(stagingBatch);
                            result.SuccessCount += inserted;
                            result.FailureCount += (stagingBatch.Count - inserted);
                            processedCount += inserted;
                            result.TotalProcessed = processedCount;
                        }

                        stagingBatch.Clear();

                        var percentComplete = (int)((processedCount / (double)maxProducts) * 100);
                        progress?.Report(new ImportProgress
                        {
                            Message = $"Imported {processedCount} products to staging (skipped {skippedCount})...",
                            PercentComplete = percentComplete,
                            ProductsImported = processedCount
                        });

                        _logger.LogInformation("[JSON] Bulk import progress: {Processed} imported, {Skipped} skipped",
                            processedCount, skippedCount);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to process line {LineNumber}", lineNumber);
                    result.FailureCount++;
                    result.Errors.Add($"Line {lineNumber}: {ex.Message}");
                }
            }

            // Insert remaining batch
            if (stagingBatch.Any())
            {
                var enableAugmentation = _configuration.GetValue<bool>("ProductImport:EnableDataAugmentation", false);
                var diagnoseImages = _configuration.GetValue<bool>("ProductImport:DiagnoseJsonImages", false);

                if (enableAugmentation)
                {
                    var augmented = await _stagingRepository.BulkAugmentStagingProductsAsync(stagingBatch, "JSON");
                    var inserted = await _stagingRepository.BulkInsertStagingProductsAsync(stagingBatch);
                    result.SuccessCount += inserted;
                    result.FailureCount += (stagingBatch.Count - inserted - augmented);
                    processedCount += inserted;
                    result.TotalProcessed = processedCount;

                    if (augmented > 0)
                    {
                        _logger.LogInformation("[JSON] Final batch augmented {Count} existing records", augmented);
                    }

                    // JSON image diagnostics - log products that have images
                    if (diagnoseImages && inserted > 0)
                    {
                        var withImages = stagingBatch.Where(p => !string.IsNullOrWhiteSpace(p.ImageUrl)).ToList();
                        if (withImages.Any())
                        {
                            _logger.LogWarning("[JSON-IMAGES] Found {Count} products with images in final batch", withImages.Count);
                            foreach (var p in withImages.Take(3))
                            {
                                _logger.LogWarning("[JSON-IMAGES] Barcode: {Barcode}, Name: {Name}, ImageUrl: {Url}",
                                    p.Barcode, p.ProductName, p.ImageUrl);
                            }
                        }
                    }
                }
                else
                {
                    var inserted = await _stagingRepository.BulkInsertStagingProductsAsync(stagingBatch);
                    result.SuccessCount += inserted;
                    result.FailureCount += (stagingBatch.Count - inserted);
                    processedCount += inserted;
                    result.TotalProcessed = processedCount;
                }
            }

            progress?.Report(new ImportProgress
            {
                Message = $"Import complete! Imported {processedCount} products.",
                PercentComplete = 100,
                ProductsImported = processedCount
            });

            _logger.LogInformation("Bulk import completed: {Success} successful, {Failed} failed, {Skipped} skipped",
                result.SuccessCount, result.FailureCount, skippedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import from bulk data");
            result.Errors.Add($"Bulk import error: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Import delta updates from OpenFoodFacts (products modified in last N days)
    /// Downloads the most recent delta files from the OpenFoodFacts delta index
    /// </summary>
    public async Task<BatchImportResult> ImportDeltaUpdatesAsync(
        int days = 14,
        int maxProducts = 5000,
        CancellationToken cancellationToken = default)
    {
        var result = new BatchImportResult();

        try
        {
            _logger.LogInformation("Importing delta updates from last {Days} days", days);

            // Fetch the delta index to get the list of available delta files
            var indexUrl = "https://static.openfoodfacts.org/data/delta/index.txt";
            var indexContent = await _httpClient.GetStringAsync(indexUrl, cancellationToken);
            var deltaFiles = indexContent.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Where(f => f.EndsWith(".json.gz"))
                .OrderByDescending(f => f) // Most recent files first (higher timestamps)
                .Take(20) // Take last ~14 days worth of delta files
                .ToList();

            if (!deltaFiles.Any())
            {
                _logger.LogWarning("No delta files found in index");
                result.Errors.Add("No delta files available");
                return result;
            }

            _logger.LogInformation("Found {Count} recent delta files to process", deltaFiles.Count);

            int totalImported = 0;
            foreach (var deltaFile in deltaFiles)
            {
                if (totalImported >= maxProducts || cancellationToken.IsCancellationRequested)
                    break;

                var deltaUrl = $"https://static.openfoodfacts.org/data/delta/{deltaFile}";
                var remainingQuota = maxProducts - totalImported;

                _logger.LogInformation("Processing delta file: {File} (max {Max} products)", deltaFile, remainingQuota);

                var deltaResult = await ImportFromBulkDataAsync(deltaUrl, remainingQuota, cancellationToken);

                result.TotalProcessed += deltaResult.TotalProcessed;
                result.SuccessCount += deltaResult.SuccessCount;
                result.FailureCount += deltaResult.FailureCount;
                result.Errors.AddRange(deltaResult.Errors);

                totalImported += deltaResult.SuccessCount;
            }

            _logger.LogInformation("Delta import completed: {Success} successful, {Failed} failed from {Files} files",
                result.SuccessCount, result.FailureCount, deltaFiles.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import delta updates");
            result.Errors.Add($"Delta import error: {ex.Message}");
        }

            return result;
        }

        /// <summary>
        /// Import products from OpenFoodFacts CSV export
        /// Downloads and processes the CSV.gz file which may contain more complete image data
        /// CSV format: https://world.openfoodfacts.org/data
        /// </summary>
        public async Task<BatchImportResult> ImportFromCsvDataAsync(
            string? dataFileUrl = null,
            int maxProducts = 10000,
            CancellationToken cancellationToken = default,
            IProgress<ImportProgress>? progress = null)
        {
            var result = new BatchImportResult();
            dataFileUrl ??= "https://static.openfoodfacts.org/data/en.openfoodfacts.org.products.csv.gz";

            try
            {
                _logger.LogInformation("Starting CSV bulk import from OpenFoodFacts: {Url}", dataFileUrl);
                progress?.Report(new ImportProgress { Message = "Downloading CSV data file...", PercentComplete = 0 });

                // Download compressed CSV file
                using var response = await _httpClient.GetAsync(dataFileUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();

                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var gzipStream = new System.IO.Compression.GZipStream(stream, System.IO.Compression.CompressionMode.Decompress);
                using var reader = new StreamReader(gzipStream);

                var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = true,
                    MissingFieldFound = null, // Ignore missing fields
                    BadDataFound = null, // Ignore bad data
                    Delimiter = "\t" // OpenFoodFacts CSV uses tab delimiter
                };

                using var csv = new CsvReader(reader, csvConfig);

                int recordNumber = 0;
                int processedCount = 0;
                int skippedCount = 0;
                var batchSize = 500;
                var stagingBatch = new List<StagedProduct>();

                // Read CSV records
                await csv.ReadAsync();
                csv.ReadHeader();

                _logger.LogInformation("CSV Headers found: {Headers}", string.Join(", ", csv.HeaderRecord ?? Array.Empty<string>()));

                while (await csv.ReadAsync() && processedCount < maxProducts && !cancellationToken.IsCancellationRequested)
                {
                    recordNumber++;

                    try
                    {
                        // Extract core fields
                        var barcode = csv.GetField<string>("code");
                        var lang = csv.GetField<string>("lang");
                        var productName = csv.GetField<string>("product_name");

                        // Debug: Log first 10 records to see what lang values look like
                        if (recordNumber <= 10)
                        {
                            _logger.LogInformation(
                                "CSV Debug Record {RecordNumber}: code={Code}, lang={Lang}, product_name={ProductName}",
                                recordNumber,
                                barcode ?? "(null)",
                                lang ?? "(null)",
                                productName ?? "(null)");
                        }

                        // Skip products without barcode or name (primary filter)
                        if (string.IsNullOrWhiteSpace(barcode) || string.IsNullOrWhiteSpace(productName))
                        {
                            skippedCount++;
                            continue;
                        }

                        // Language filter: Since we're downloading from en.openfoodfacts.org,
                        // we accept products with no lang specified OR lang starting with "en"
                        // This is more permissive than before
                        if (!string.IsNullOrWhiteSpace(lang) &&
                            !lang.StartsWith("en", StringComparison.OrdinalIgnoreCase))
                        {
                            // Only skip if lang is explicitly set to non-English
                            skippedCount++;
                            continue;
                        }

                        // Content-based language detection - check actual product data
                        var ingredientsText = csv.GetField<string>("ingredients_text");
                        var ingredientsTextEn = csv.GetField<string>("ingredients_text_en");
                        var brand = csv.GetField<string>("brands");

                        if (!LanguageDetector.ShouldImportProduct(productName ?? "", brand ?? "", ingredientsTextEn ?? ingredientsText ?? ""))
                        {
                            skippedCount++;
                            if (recordNumber <= 20) // Debug first 20 skips
                            {
                                _logger.LogInformation("Skipping non-English product (content-based): {ProductName} (Barcode: {Barcode})",
                                    productName, barcode);
                            }
                            continue;
                        }

                        // Filter: Food products only
                        var categories = csv.GetField<string>("categories")?.ToLower() ?? "";
                        if (categories.Contains("beauty") || categories.Contains("pet-food") ||
                            categories.Contains("cosmetic") || productName.ToLower().Contains("shampoo") ||
                            productName.ToLower().Contains("lotion"))
                        {
                            skippedCount++;
                            continue;
                        }

                        // Map CSV record to StagedProduct
                        var stagedProduct = new StagedProduct
                        {
                            ExternalId = barcode,
                            Barcode = barcode,
                            ProductName = productName,
                            GenericName = csv.GetField<string>("generic_name"),
                            Brands = brand,
                            Lang = lang,
                            Countries = csv.GetField<string>("countries"),
                            NutriScore = csv.GetField<string>("nutriscore_grade"),
                            EcoScore = csv.GetField<string>("ecoscore_grade"),

                            // CSV has explicit image URL columns - check all of them
                            ImageUrl = csv.GetField<string>("image_url")
                                ?? csv.GetField<string>("image_front_url")
                                ?? csv.GetField<string>("image_small_url"),
                            ImageSmallUrl = csv.GetField<string>("image_small_url")
                                ?? csv.GetField<string>("image_thumb_url"),

                            IngredientsText = ingredientsText,
                            IngredientsTextEn = ingredientsTextEn,
                            Allergens = csv.GetField<string>("allergens"),
                            Categories = csv.GetField<string>("categories")
                        };

                        // Try to get Nova group
                        var novaStr = csv.GetField<string>("nova_group");
                        if (int.TryParse(novaStr, out var novaGroup))
                        {
                            stagedProduct.NovaGroup = novaGroup;
                        }

                        // Log image URLs for debugging (first 100 products only)
                        if (recordNumber <= 100)
                        {
                            var imageUrl = stagedProduct.ImageUrl;
                            var imageFrontUrl = csv.GetField<string>("image_front_url");
                            var imageNutritionUrl = csv.GetField<string>("image_nutrition_url");
                            var imageIngredientsUrl = csv.GetField<string>("image_ingredients_url");

                            _logger.LogInformation(
                                "CSV Record {RecordNumber} - Barcode: {Barcode}, Product: {ProductName}\n" +
                                "  ImageUrl: {ImageUrl}\n" +
                                "  image_front_url: {FrontUrl}\n" +
                                "  image_nutrition_url: {NutritionUrl}\n" +
                                "  image_ingredients_url: {IngredientsUrl}",
                                recordNumber, barcode, productName,
                                imageUrl ?? "(null)",
                                imageFrontUrl ?? "(null)",
                                imageNutritionUrl ?? "(null)",
                                imageIngredientsUrl ?? "(null)");
                        }

                        stagingBatch.Add(stagedProduct);

                        // Bulk insert when batch is full
                        if (stagingBatch.Count >= batchSize)
                        {
                            var enableAugmentation = _configuration.GetValue<bool>("ProductImport:EnableDataAugmentation", false);

                            if (enableAugmentation)
                            {
                                // Try to augment existing records first
                                var augmented = await _stagingRepository.BulkAugmentStagingProductsAsync(stagingBatch, "CSV");

                                // Then insert new records (duplicates will be skipped)
                                var inserted = await _stagingRepository.BulkInsertStagingProductsAsync(stagingBatch);

                                result.SuccessCount += inserted;
                                result.FailureCount += (stagingBatch.Count - inserted - augmented);
                                processedCount += inserted;
                                result.TotalProcessed = processedCount;

                                if (augmented > 0)
                                {
                                    _logger.LogInformation("[CSV] Augmented {Count} existing records with additional data", augmented);
                                }
                            }
                            else
                            {
                                var inserted = await _stagingRepository.BulkInsertStagingProductsAsync(stagingBatch);
                                result.SuccessCount += inserted;
                                result.FailureCount += (stagingBatch.Count - inserted); // Duplicates
                                processedCount += inserted;
                                result.TotalProcessed = processedCount;
                            }

                            stagingBatch.Clear();

                            // Only log progress every 5000 products to reduce noise
                            if (processedCount % 5000 == 0)
                            {
                                var percentComplete = (int)((processedCount / (double)maxProducts) * 100);
                                progress?.Report(new ImportProgress
                                {
                                    Message = $"Imported {processedCount} products from CSV (skipped {skippedCount})...",
                                    PercentComplete = percentComplete,
                                    ProductsImported = processedCount
                                });

                                _logger.LogInformation("CSV bulk import progress: {Processed} imported, {Skipped} skipped",
                                    processedCount, skippedCount);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to process CSV record {RecordNumber}", recordNumber);
                        result.FailureCount++;
                        result.Errors.Add($"Record {recordNumber}: {ex.Message}");
                    }
                }

                // Insert remaining batch
                if (stagingBatch.Any())
                {
                    var inserted = await _stagingRepository.BulkInsertStagingProductsAsync(stagingBatch);
                    result.SuccessCount += inserted;
                    result.FailureCount += (stagingBatch.Count - inserted);
                    processedCount += inserted;
                    result.TotalProcessed = processedCount;
                }

                progress?.Report(new ImportProgress
                {
                    Message = $"CSV import complete! Imported {processedCount} products.",
                    PercentComplete = 100,
                    ProductsImported = processedCount
                });

                _logger.LogInformation("CSV bulk import completed: {Success} successful, {Failed} failed, {Skipped} skipped",
                    result.SuccessCount, result.FailureCount, skippedCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to import from CSV bulk data");
                result.Errors.Add($"CSV bulk import error: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Map OpenFoodFacts JSON to StagedProduct for bulk insert
        /// </summary>
    private StagedProduct MapToStagedProduct(JsonElement product, string barcode)
    {
        var stagedProduct = new StagedProduct
        {
            ExternalId = GetStringValue(product, "code", "_id") ?? barcode,
            Barcode = barcode,
            ProductName = GetStringValue(product, "product_name", "product_name_en"),
            GenericName = GetStringValue(product, "generic_name", "generic_name_en"),
            Brands = GetStringValue(product, "brands"),
            Lang = GetStringValue(product, "lang", "lc"),
            Countries = GetStringValue(product, "countries"),
            NutriScore = GetStringValue(product, "nutriscore_grade"),
            EcoScore = GetStringValue(product, "ecoscore_grade"),
            // Try multiple image URL fields - OpenFoodFacts uses different fields
            ImageUrl = GetStringValue(product, "image_url", "image_front_url", "image_front_small_url", "image_thumb_url")
                ?? GetNestedImageUrl(product),
            ImageSmallUrl = GetStringValue(product, "image_small_url", "image_front_small_url", "image_thumb_url")
        };

        // Ingredients text
        stagedProduct.IngredientsText = GetStringValue(product, "ingredients_text");
        stagedProduct.IngredientsTextEn = GetStringValue(product, "ingredients_text_en");

        // Allergens
        stagedProduct.Allergens = GetStringValue(product, "allergens");
        if (product.TryGetProperty("allergens_hierarchy", out var allergensHierarchy))
        {
            stagedProduct.AllergensHierarchy = allergensHierarchy.GetRawText();
        }

        // Categories
        stagedProduct.Categories = GetStringValue(product, "categories");
        if (product.TryGetProperty("categories_hierarchy", out var categoriesHierarchy))
        {
            stagedProduct.CategoriesHierarchy = categoriesHierarchy.GetRawText();
        }

        // Nova group
        if (product.TryGetProperty("nova_group", out var novaGroup) && novaGroup.ValueKind == JsonValueKind.Number)
        {
            stagedProduct.NovaGroup = novaGroup.GetInt32();
        }

        // Nutrition data (store as JSON)
        if (product.TryGetProperty("nutriments", out var nutriments))
        {
            stagedProduct.NutritionData = nutriments.GetRawText();
        }

        // Store full JSON for reference
        stagedProduct.RawJson = product.GetRawText();

        return stagedProduct;
    }
}

public class ImportProgress
{
    public string Message { get; set; } = "";
    public int PercentComplete { get; set; }
    public int ProductsImported { get; set; }
}

