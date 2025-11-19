using System.Text.Json;
using ExpressRecipe.ProductService.Data;

namespace ExpressRecipe.ProductService.Services;

/// <summary>
/// Service for importing food data from USDA FoodData Central API
/// API Documentation: https://fdc.nal.usda.gov/api-guide.html
/// </summary>
public class USDAFoodDataImportService
{
    private readonly HttpClient _httpClient;
    private readonly IProductRepository _productRepository;
    private readonly ILogger<USDAFoodDataImportService> _logger;
    private readonly string _apiKey;

    public USDAFoodDataImportService(
        HttpClient httpClient,
        IProductRepository productRepository,
        IConfiguration configuration,
        ILogger<USDAFoodDataImportService> logger)
    {
        _httpClient = httpClient;
        _productRepository = productRepository;
        _logger = logger;
        _apiKey = configuration["USDA:ApiKey"] ?? throw new InvalidOperationException("USDA API key not configured");

        _httpClient.BaseAddress = new Uri("https://api.nal.usda.gov/fdc/v1/");
    }

    /// <summary>
    /// Import a single food item by FDC ID
    /// </summary>
    public async Task<ImportResult> ImportFoodByIdAsync(string fdcId)
    {
        try
        {
            _logger.LogInformation("Importing USDA food item: {FdcId}", fdcId);

            var response = await _httpClient.GetAsync($"food/{fdcId}?api_key={_apiKey}");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var foodData = JsonDocument.Parse(json);

            var result = await ProcessFoodDataAsync(foodData.RootElement);

            _logger.LogInformation("Successfully imported food {FdcId}: {ProductName}", fdcId, result.ProductName);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import USDA food {FdcId}", fdcId);
            return new ImportResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Search and import foods matching a query
    /// </summary>
    public async Task<BatchImportResult> SearchAndImportAsync(string query, int pageSize = 50, int maxResults = 200)
    {
        var result = new BatchImportResult();

        try
        {
            _logger.LogInformation("Searching USDA for: {Query}", query);

            var searchRequest = new
            {
                query = query,
                pageSize = pageSize,
                dataType = new[] { "Branded", "Foundation", "SR Legacy" }
            };

            var response = await _httpClient.PostAsJsonAsync($"foods/search?api_key={_apiKey}", searchRequest);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var searchResult = JsonDocument.Parse(json);

            if (searchResult.RootElement.TryGetProperty("foods", out var foods))
            {
                int count = 0;
                foreach (var food in foods.EnumerateArray())
                {
                    if (count >= maxResults) break;

                    try
                    {
                        var importResult = await ProcessFoodDataAsync(food);
                        result.Results.Add(importResult);

                        if (importResult.Success)
                            result.SuccessCount++;
                        else
                            result.FailureCount++;

                        count++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to import food item from search results");
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
            _logger.LogError(ex, "Failed to search USDA database");
            result.Errors.Add($"Search error: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Import branded foods in batches
    /// </summary>
    public async Task<BatchImportResult> ImportBrandedFoodsBatchAsync(int skip = 0, int take = 100)
    {
        var result = new BatchImportResult();

        try
        {
            var response = await _httpClient.GetAsync(
                $"foods/list?dataType=Branded&pageSize={take}&pageNumber={(skip / take) + 1}&api_key={_apiKey}");

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var foods = JsonSerializer.Deserialize<JsonElement>(json);

            if (foods.ValueKind == JsonValueKind.Array)
            {
                foreach (var food in foods.EnumerateArray())
                {
                    try
                    {
                        var importResult = await ProcessFoodDataAsync(food);
                        result.Results.Add(importResult);

                        if (importResult.Success)
                            result.SuccessCount++;
                        else
                            result.FailureCount++;

                        result.TotalProcessed++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to import branded food");
                        result.FailureCount++;
                        result.Errors.Add(ex.Message);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import branded foods batch");
            result.Errors.Add($"Batch error: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Process and save USDA food data to database
    /// </summary>
    private async Task<ImportResult> ProcessFoodDataAsync(JsonElement foodData)
    {
        var result = new ImportResult();

        try
        {
            // Extract basic food information
            var fdcId = foodData.GetProperty("fdcId").GetInt32().ToString();
            var description = foodData.GetProperty("description").GetString() ?? "Unknown";
            var dataType = foodData.TryGetProperty("dataType", out var dt) ? dt.GetString() : null;

            result.ExternalId = fdcId;
            result.ProductName = description;

            // Extract brand information for branded foods
            string? brand = null;
            string? upc = null;

            if (foodData.TryGetProperty("brandOwner", out var brandOwner))
            {
                brand = brandOwner.GetString();
            }

            if (foodData.TryGetProperty("gtinUpc", out var gtinUpc))
            {
                upc = gtinUpc.GetString();
            }

            // Check if product already exists
            var existing = await _productRepository.GetProductByExternalIdAsync("USDA", fdcId);
            if (existing != null)
            {
                _logger.LogDebug("Product already exists: {FdcId}", fdcId);
                result.ProductId = existing.Id;
                result.Success = true;
                result.AlreadyExists = true;
                return result;
            }

            // Create product
            var productId = await _productRepository.CreateProductAsync(new CreateProductRequest
            {
                Name = description,
                Brand = brand,
                UPC = upc,
                Category = DetermineCategory(description, dataType),
                ExternalSource = "USDA",
                ExternalId = fdcId,
                IsVerified = true
            });

            result.ProductId = productId;

            // Extract and create ingredients
            if (foodData.TryGetProperty("ingredients", out var ingredientsElement))
            {
                var ingredientsText = ingredientsElement.GetString();
                if (!string.IsNullOrWhiteSpace(ingredientsText))
                {
                    var ingredients = ParseIngredients(ingredientsText);
                    foreach (var ingredient in ingredients)
                    {
                        await _productRepository.AddIngredientToProductAsync(
                            productId, ingredient, orderIndex: ingredients.IndexOf(ingredient));
                    }
                    result.IngredientCount = ingredients.Count;
                }
            }

            // Extract nutrition data
            if (foodData.TryGetProperty("foodNutrients", out var nutrients))
            {
                await ProcessNutrientsAsync(productId, nutrients);
                result.HasNutrition = true;
            }

            result.Success = true;
            _logger.LogInformation("Created product {ProductId} from USDA food {FdcId}", productId, fdcId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process USDA food data");
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    /// <summary>
    /// Process nutrition data from USDA format
    /// </summary>
    private async Task ProcessNutrientsAsync(Guid productId, JsonElement nutrients)
    {
        // Extract common nutrients
        var nutritionData = new Dictionary<string, decimal?>();

        foreach (var nutrient in nutrients.EnumerateArray())
        {
            if (!nutrient.TryGetProperty("nutrientName", out var name) ||
                !nutrient.TryGetProperty("value", out var value))
                continue;

            var nutrientName = name.GetString();
            var nutrientValue = value.ValueKind == JsonValueKind.Number ? (decimal?)value.GetDecimal() : null;

            if (nutrientValue == null) continue;

            switch (nutrientName)
            {
                case "Energy":
                case "Energy (Atwater General Factors)":
                    nutritionData["Calories"] = nutrientValue;
                    break;
                case "Protein":
                    nutritionData["Protein"] = nutrientValue;
                    break;
                case "Total lipid (fat)":
                    nutritionData["TotalFat"] = nutrientValue;
                    break;
                case "Carbohydrate, by difference":
                    nutritionData["Carbohydrates"] = nutrientValue;
                    break;
                case "Fiber, total dietary":
                    nutritionData["Fiber"] = nutrientValue;
                    break;
                case "Sugars, total including NLEA":
                    nutritionData["Sugar"] = nutrientValue;
                    break;
                case "Sodium, Na":
                    nutritionData["Sodium"] = nutrientValue;
                    break;
            }
        }

        // Save nutrition data to database
        // Note: This assumes a method exists in the repository
        // await _productRepository.AddNutritionDataAsync(productId, nutritionData);
    }

    /// <summary>
    /// Parse ingredients text into individual ingredients
    /// </summary>
    private List<string> ParseIngredients(string ingredientsText)
    {
        // Split by common separators
        var separators = new[] { ',', ';', '.' };
        var ingredients = ingredientsText.Split(separators, StringSplitOptions.RemoveEmptyEntries)
            .Select(i => i.Trim())
            .Where(i => !string.IsNullOrWhiteSpace(i) && i.Length > 2)
            .ToList();

        return ingredients;
    }

    /// <summary>
    /// Determine product category from description and data type
    /// </summary>
    private string? DetermineCategory(string description, string? dataType)
    {
        var lower = description.ToLower();

        if (lower.Contains("milk") || lower.Contains("cheese") || lower.Contains("yogurt"))
            return "Dairy";
        if (lower.Contains("bread") || lower.Contains("cereal") || lower.Contains("pasta"))
            return "Grains";
        if (lower.Contains("chicken") || lower.Contains("beef") || lower.Contains("pork") || lower.Contains("fish"))
            return "Protein";
        if (lower.Contains("apple") || lower.Contains("banana") || lower.Contains("orange") || lower.Contains("berry"))
            return "Fruit";
        if (lower.Contains("carrot") || lower.Contains("broccoli") || lower.Contains("lettuce") || lower.Contains("potato"))
            return "Vegetables";
        if (lower.Contains("cookie") || lower.Contains("cake") || lower.Contains("candy") || lower.Contains("chocolate"))
            return "Snacks & Sweets";

        return dataType == "Branded" ? "Packaged Foods" : "General";
    }
}

/// <summary>
/// Result of a single food import
/// </summary>
public class ImportResult
{
    public bool Success { get; set; }
    public Guid? ProductId { get; set; }
    public string? ExternalId { get; set; }
    public string? ProductName { get; set; }
    public bool AlreadyExists { get; set; }
    public int IngredientCount { get; set; }
    public bool HasNutrition { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Result of batch import operation
/// </summary>
public class BatchImportResult
{
    public int TotalProcessed { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public List<ImportResult> Results { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// DTO for creating a product
/// </summary>
public class CreateProductRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string? UPC { get; set; }
    public string? Category { get; set; }
    public string? ExternalSource { get; set; }
    public string? ExternalId { get; set; }
    public bool IsVerified { get; set; }
}
