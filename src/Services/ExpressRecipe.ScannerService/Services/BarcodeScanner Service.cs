using ExpressRecipe.ScannerService.Data;

namespace ExpressRecipe.ScannerService.Services;

/// <summary>
/// Service for scanning barcodes and retrieving product information
/// Uses multiple data sources with fallback strategy
/// </summary>
public class BarcodeScannerService
{
    private readonly IScannerRepository _repository;
    private readonly OpenFoodFactsApiClient _openFoodFactsClient;
    private readonly UPCDatabaseApiClient _upcDatabaseClient;
    private readonly ILogger<BarcodeScannerService> _logger;

    public BarcodeScannerService(
        IScannerRepository repository,
        OpenFoodFactsApiClient openFoodFactsClient,
        UPCDatabaseApiClient upcDatabaseClient,
        ILogger<BarcodeScannerService> logger)
    {
        _repository = repository;
        _openFoodFactsClient = openFoodFactsClient;
        _upcDatabaseClient = upcDatabaseClient;
        _logger = logger;
    }

    /// <summary>
    /// Scan barcode and get product information
    /// Strategy: Local DB → OpenFoodFacts → UPC Database → Create placeholder
    /// </summary>
    public async Task<ScanResult> ScanBarcodeAsync(string barcode, Guid? userId = null)
    {
        try
        {
            _logger.LogInformation("Scanning barcode: {Barcode} for user {UserId}", barcode, userId);

            var result = new ScanResult
            {
                Barcode = barcode,
                ScannedAt = DateTime.UtcNow,
                UserId = userId
            };

            // Step 1: Check local database first
            var localProduct = await _repository.GetProductByBarcodeAsync(barcode);
            if (localProduct != null)
            {
                _logger.LogInformation("Product found in local database: {ProductId}", localProduct.ProductId);
                result.Found = true;
                result.Product = localProduct;
                result.Source = "Local";
                await SaveScanHistoryAsync(result);
                return result;
            }

            // Step 2: Try OpenFoodFacts
            var offProduct = await _openFoodFactsClient.GetProductByBarcodeAsync(barcode);
            if (offProduct != null && !string.IsNullOrEmpty(offProduct.ProductName))
            {
                _logger.LogInformation("Product found in OpenFoodFacts: {ProductName}", offProduct.ProductName);
                result.Found = true;
                result.Product = await ConvertAndSaveOpenFoodFactsProductAsync(offProduct, barcode);
                result.Source = "OpenFoodFacts";
                await SaveScanHistoryAsync(result);
                return result;
            }

            // Step 3: Try UPC Database
            var upcProduct = await _upcDatabaseClient.GetProductByBarcodeAsync(barcode);
            if (upcProduct != null && !string.IsNullOrEmpty(upcProduct.Title))
            {
                _logger.LogInformation("Product found in UPC Database: {Title}", upcProduct.Title);
                result.Found = true;
                result.Product = await ConvertAndSaveUPCProductAsync(upcProduct, barcode);
                result.Source = "UPCDatabase";
                await SaveScanHistoryAsync(result);
                return result;
            }

            // Step 4: Product not found - create placeholder
            _logger.LogInformation("Product not found in any source: {Barcode}", barcode);
            result.Found = false;
            result.Source = "None";

            // Still save scan history for analytics
            await SaveScanHistoryAsync(result);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scan barcode: {Barcode}", barcode);
            return new ScanResult
            {
                Barcode = barcode,
                Found = false,
                ErrorMessage = ex.Message,
                ScannedAt = DateTime.UtcNow,
                UserId = userId
            };
        }
    }

    /// <summary>
    /// Convert OpenFoodFacts product to our domain model and save to database
    /// </summary>
    private async Task<ScannedProduct> ConvertAndSaveOpenFoodFactsProductAsync(
        OpenFoodFactsProduct offProduct, string barcode)
    {
        var product = new ScannedProduct
        {
            ProductId = Guid.NewGuid(),
            Barcode = barcode,
            Name = offProduct.ProductName ?? "Unknown Product",
            Brand = offProduct.Brands?.Split(',').FirstOrDefault()?.Trim(),
            Description = offProduct.GenericName,
            ImageUrl = offProduct.ImageFrontUrl ?? offProduct.ImageUrl,
            Category = offProduct.Categories?.Split(',').FirstOrDefault()?.Trim(),
            Ingredients = ParseIngredients(offProduct.IngredientsText),
            Allergens = ParseAllergens(offProduct.AllergensTags),
            Traces = ParseAllergens(offProduct.TracesTags),
            Nutrition = offProduct.Nutriments != null ? new NutritionInfo
            {
                Calories = (int?)offProduct.Nutriments.EnergyKcal100g ?? 0,
                Fat = offProduct.Nutriments.Fat100g ?? 0,
                SaturatedFat = offProduct.Nutriments.SaturatedFat100g ?? 0,
                Carbohydrates = offProduct.Nutriments.Carbohydrates100g ?? 0,
                Sugar = offProduct.Nutriments.Sugars100g ?? 0,
                Fiber = offProduct.Nutriments.Fiber100g ?? 0,
                Protein = offProduct.Nutriments.Proteins100g ?? 0,
                Salt = offProduct.Nutriments.Salt100g ?? 0,
                Sodium = offProduct.Nutriments.Sodium100g ?? 0
            } : null,
            ServingSize = offProduct.ServingSize,
            Quantity = offProduct.Quantity,
            Source = "OpenFoodFacts",
            ImportedAt = DateTime.UtcNow
        };

        // Save to database for future lookups
        await _repository.SaveProductAsync(product);

        return product;
    }

    /// <summary>
    /// Convert UPC Database product to our domain model and save to database
    /// </summary>
    private async Task<ScannedProduct> ConvertAndSaveUPCProductAsync(
        UPCDatabaseProduct upcProduct, string barcode)
    {
        var product = new ScannedProduct
        {
            ProductId = Guid.NewGuid(),
            Barcode = barcode,
            Name = upcProduct.Title ?? "Unknown Product",
            Brand = upcProduct.Brand,
            Description = upcProduct.Description,
            ImageUrl = upcProduct.Images?.FirstOrDefault(),
            Category = upcProduct.Category,
            Ingredients = new List<string>(), // UPC Database doesn't provide ingredient info
            Allergens = new List<string>(), // Not available
            Traces = new List<string>(),
            Nutrition = null, // Not available from UPC Database
            Source = "UPCDatabase",
            ImportedAt = DateTime.UtcNow
        };

        // Save to database
        await _repository.SaveProductAsync(product);

        return product;
    }

    /// <summary>
    /// Save scan history for analytics
    /// </summary>
    private async Task SaveScanHistoryAsync(ScanResult result)
    {
        if (result.UserId == null) return;

        try
        {
            await _repository.SaveScanHistoryAsync(new ScanHistoryRecord
            {
                Id = Guid.NewGuid(),
                UserId = result.UserId.Value,
                Barcode = result.Barcode,
                ProductId = result.Product?.ProductId,
                ProductName = result.Product?.Name,
                Found = result.Found,
                Source = result.Source ?? "Unknown",
                ScannedAt = result.ScannedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save scan history");
        }
    }

    /// <summary>
    /// Parse ingredients text into list
    /// </summary>
    private List<string> ParseIngredients(string? ingredientsText)
    {
        if (string.IsNullOrWhiteSpace(ingredientsText))
            return new List<string>();

        return ingredientsText
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(i => i.Trim())
            .Where(i => !string.IsNullOrWhiteSpace(i))
            .Take(50) // Limit to 50 ingredients
            .ToList();
    }

    /// <summary>
    /// Parse allergen tags into readable list
    /// </summary>
    private List<string> ParseAllergens(List<string>? allergenTags)
    {
        if (allergenTags == null || allergenTags.Count == 0)
            return new List<string>();

        return allergenTags
            .Select(tag => tag.Replace("en:", "").Replace("-", " ").Trim())
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Select(a => char.ToUpper(a[0]) + a.Substring(1)) // Capitalize
            .ToList();
    }

    /// <summary>
    /// Get scan history for a user
    /// </summary>
    public async Task<List<ScanHistoryRecord>> GetScanHistoryAsync(Guid userId, int limit = 50)
    {
        return await _repository.GetScanHistoryAsync(userId, limit);
    }

    /// <summary>
    /// Report a missing product
    /// </summary>
    public async Task<bool> ReportMissingProductAsync(string barcode, Guid userId, string? productName = null)
    {
        try
        {
            await _repository.SaveMissingProductReportAsync(new MissingProductReport
            {
                Id = Guid.NewGuid(),
                Barcode = barcode,
                UserId = userId,
                ProductName = productName,
                ReportedAt = DateTime.UtcNow,
                Status = "Pending"
            });

            _logger.LogInformation("Missing product reported: {Barcode} by user {UserId}", barcode, userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to report missing product: {Barcode}", barcode);
            return false;
        }
    }
}

/// <summary>
/// Result of barcode scan operation
/// </summary>
public class ScanResult
{
    public string Barcode { get; set; } = string.Empty;
    public bool Found { get; set; }
    public ScannedProduct? Product { get; set; }
    public string? Source { get; set; }
    public DateTime ScannedAt { get; set; }
    public Guid? UserId { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Scanned product information
/// </summary>
public class ScannedProduct
{
    public Guid ProductId { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public string? Category { get; set; }
    public List<string> Ingredients { get; set; } = new();
    public List<string> Allergens { get; set; } = new();
    public List<string> Traces { get; set; } = new();
    public NutritionInfo? Nutrition { get; set; }
    public string? ServingSize { get; set; }
    public string? Quantity { get; set; }
    public string Source { get; set; } = "Unknown";
    public DateTime ImportedAt { get; set; }
}

/// <summary>
/// Nutrition information per 100g
/// </summary>
public class NutritionInfo
{
    public int Calories { get; set; }
    public decimal Fat { get; set; }
    public decimal SaturatedFat { get; set; }
    public decimal Carbohydrates { get; set; }
    public decimal Sugar { get; set; }
    public decimal Fiber { get; set; }
    public decimal Protein { get; set; }
    public decimal Salt { get; set; }
    public decimal Sodium { get; set; }
}

/// <summary>
/// Scan history record
/// </summary>
public class ScanHistoryRecord
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public Guid? ProductId { get; set; }
    public string? ProductName { get; set; }
    public bool Found { get; set; }
    public string Source { get; set; } = string.Empty;
    public DateTime ScannedAt { get; set; }
}

/// <summary>
/// Missing product report
/// </summary>
public class MissingProductReport
{
    public Guid Id { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public string? ProductName { get; set; }
    public DateTime ReportedAt { get; set; }
    public string Status { get; set; } = "Pending";
}
