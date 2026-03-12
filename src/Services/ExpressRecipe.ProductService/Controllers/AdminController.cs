using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ExpressRecipe.ProductService.Services;
using ExpressRecipe.ProductService.Data;
using ExpressRecipe.Shared.DTOs.Product;
using System.Collections.Concurrent;
using System.Text.Json;

namespace ExpressRecipe.ProductService.Controllers;

/// <summary>
/// Admin controller for data import operations
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")] // Requires admin role
public class AdminController : ControllerBase
{
    private readonly USDAFoodDataImportService _usdaImportService;
    private readonly OpenFoodFactsImportService _openFoodFactsImportService;
    private readonly IProductRepository _productRepository;
    private readonly UsdaPortionImportService _usdaPortionImportService;
    private readonly ILogger<AdminController> _logger;

    // In-memory tracking of import jobs (in production, use database or distributed cache)
    private static readonly ConcurrentDictionary<Guid, ImportJobStatus> _importJobs = new();

    public AdminController(
        USDAFoodDataImportService usdaImportService,
        OpenFoodFactsImportService openFoodFactsImportService,
        IProductRepository productRepository,
        UsdaPortionImportService usdaPortionImportService,
        ILogger<AdminController> logger)
    {
        _usdaImportService = usdaImportService;
        _openFoodFactsImportService = openFoodFactsImportService;
        _productRepository = productRepository;
        _usdaPortionImportService = usdaPortionImportService;
        _logger = logger;
    }

    /// <summary>
    /// Start USDA database import
    /// </summary>
    [HttpPost("import/usda")]
    public async Task<ActionResult<ImportStatusDto>> ImportUSDADatabase(
        [FromBody] ImportRequest? request = null)
    {
        var importId = Guid.NewGuid();
        var jobStatus = new ImportJobStatus
        {
            ImportId = importId,
            Source = "USDA",
            Status = "InProgress",
            StartedAt = DateTime.UtcNow
        };

        _importJobs[importId] = jobStatus;

        _logger.LogInformation("Starting USDA import job {ImportId}", importId);

        // Run import in background
        _ = Task.Run(async () =>
        {
            try
            {
                var query = request?.Query ?? "food"; // Default broad search
                var maxResults = request?.MaxResults ?? 500;

                var result = await _usdaImportService.SearchAndImportAsync(query, pageSize: 50, maxResults: maxResults);

                jobStatus.Status = "Completed";
                jobStatus.CompletedAt = DateTime.UtcNow;
                jobStatus.TotalRecords = result.TotalProcessed;
                jobStatus.ProcessedRecords = result.TotalProcessed;
                jobStatus.SuccessCount = result.SuccessCount;
                jobStatus.ErrorCount = result.FailureCount;

                _logger.LogInformation("USDA import {ImportId} completed: {Success} successful, {Failed} failed",
                    importId, result.SuccessCount, result.FailureCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "USDA import {ImportId} failed", importId);
                jobStatus.Status = "Failed";
                jobStatus.CompletedAt = DateTime.UtcNow;
                jobStatus.ErrorMessage = ex.Message;
            }
        });

        return Ok(MapToDto(jobStatus));
    }

    /// <summary>
    /// Start OpenFoodFacts database import
    /// </summary>
    [HttpPost("import/openfoodfacts")]
    public async Task<ActionResult<ImportStatusDto>> ImportOpenFoodFacts(
        [FromBody] ImportRequest? request = null)
    {
        var importId = Guid.NewGuid();
        var jobStatus = new ImportJobStatus
        {
            ImportId = importId,
            Source = "OpenFoodFacts",
            Status = "InProgress",
            StartedAt = DateTime.UtcNow
        };

        _importJobs[importId] = jobStatus;

        _logger.LogInformation("Starting OpenFoodFacts import job {ImportId}", importId);

        // Run import in background
        _ = Task.Run(async () =>
        {
            try
            {
                var query = request?.Query ?? "united-states"; // Default to US products
                var maxResults = request?.MaxResults ?? 1000;

                var result = await _openFoodFactsImportService.SearchAndImportAsync(query, pageSize: 100, maxResults: maxResults);

                jobStatus.Status = "Completed";
                jobStatus.CompletedAt = DateTime.UtcNow;
                jobStatus.TotalRecords = result.TotalProcessed;
                jobStatus.ProcessedRecords = result.TotalProcessed;
                jobStatus.SuccessCount = result.SuccessCount;
                jobStatus.ErrorCount = result.FailureCount;

                _logger.LogInformation("OpenFoodFacts import {ImportId} completed: {Success} successful, {Failed} failed",
                    importId, result.SuccessCount, result.FailureCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OpenFoodFacts import {ImportId} failed", importId);
                jobStatus.Status = "Failed";
                jobStatus.CompletedAt = DateTime.UtcNow;
                jobStatus.ErrorMessage = ex.Message;
            }
        });

        return Ok(MapToDto(jobStatus));
    }

    /// <summary>
    /// Backfill product images for existing products from OpenFoodFacts
    /// Re-imports products to populate the ProductImage table
    /// </summary>
    [HttpPost("import/openfoodfacts/backfill-images")]
    public async Task<ActionResult<ImportStatusDto>> BackfillProductImages(
        [FromBody] BackfillImagesRequest? request = null)
    {
        var importId = Guid.NewGuid();
        var jobStatus = new ImportJobStatus
        {
            ImportId = importId,
            Source = "OpenFoodFacts-ImageBackfill",
            Status = "InProgress",
            StartedAt = DateTime.UtcNow
        };

        _importJobs[importId] = jobStatus;

        _logger.LogInformation("Starting OpenFoodFacts image backfill job {ImportId}", importId);

        // Run backfill in background
        _ = Task.Run(async () =>
        {
            try
            {
                var maxProducts = request?.MaxProducts ?? 100;
                var successCount = 0;
                var errorCount = 0;

                // Get products that have barcodes but no images in ProductImage table
                var products = await GetProductsNeedingImages(maxProducts);

                jobStatus.TotalRecords = products.Count;

                foreach (var product in products)
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(product.Barcode))
                        {
                            // Re-import the product - this will now save images
                            var result = await _openFoodFactsImportService.ImportProductByBarcodeAsync(product.Barcode);

                            if (result.Success)
                                successCount++;
                            else
                                errorCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to backfill images for product {ProductId}", product.Id);
                        errorCount++;
                    }

                    jobStatus.ProcessedRecords++;
                }

                jobStatus.Status = "Completed";
                jobStatus.CompletedAt = DateTime.UtcNow;
                jobStatus.SuccessCount = successCount;
                jobStatus.ErrorCount = errorCount;

                _logger.LogInformation("Image backfill {ImportId} completed: {Success} successful, {Failed} failed",
                    importId, successCount, errorCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Image backfill {ImportId} failed", importId);
                jobStatus.Status = "Failed";
                jobStatus.CompletedAt = DateTime.UtcNow;
                jobStatus.ErrorMessage = ex.Message;
            }
        });

        return Ok(MapToDto(jobStatus));
    }

    private async Task<List<ProductDto>> GetProductsNeedingImages(int maxProducts)
    {
        // Simple implementation - get products with barcodes
        // In a real implementation, you'd query for products without images in ProductImage table
        var searchRequest = new ProductSearchRequest
        {
            PageSize = maxProducts,
            OnlyApproved = true
        };

        return await _productRepository.SearchAsync(searchRequest);
    }

    /// <summary>
    /// Get import job status
    /// </summary>
    [HttpGet("import/status/{importId}")]
    public ActionResult<ImportStatusDto> GetImportStatus(Guid importId)
    {
        if (!_importJobs.TryGetValue(importId, out var jobStatus))
        {
            return NotFound(new { message = "Import job not found" });
        }

        return Ok(MapToDto(jobStatus));
    }

    /// <summary>
    /// Diagnostic: Test OpenFoodFacts API response for a specific barcode
    /// </summary>
    [HttpGet("debug/openfoodfacts/{barcode}")]
    public async Task<ActionResult> DebugOpenFoodFacts(string barcode)
    {
        try
        {
            var httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri("https://world.openfoodfacts.org/");
            httpClient.DefaultRequestHeaders.Add("User-Agent", "ExpressRecipe/1.0 (Dietary Management Platform)");

            var response = await httpClient.GetAsync($"api/v2/product/{barcode}.json");
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return BadRequest(new { error = "Product not found", statusCode = response.StatusCode, response = json });
            }

            var data = JsonDocument.Parse(json);

            if (!data.RootElement.TryGetProperty("product", out var product))
            {
                return BadRequest(new { error = "Invalid response format", response = json });
            }

            // Extract all possible image fields
            var imageDebug = new
            {
                barcode = barcode,
                productName = product.TryGetProperty("product_name", out var pn) ? pn.GetString() : null,

                // Top-level image fields
                image_url = product.TryGetProperty("image_url", out var iu) ? iu.GetString() : null,
                image_front_url = product.TryGetProperty("image_front_url", out var ifu) ? ifu.GetString() : null,
                image_front_small_url = product.TryGetProperty("image_front_small_url", out var ifsu) ? ifsu.GetString() : null,
                image_thumb_url = product.TryGetProperty("image_thumb_url", out var itu) ? itu.GetString() : null,
                image_nutrition_url = product.TryGetProperty("image_nutrition_url", out var inu) ? inu.GetString() : null,
                image_nutrition_small_url = product.TryGetProperty("image_nutrition_small_url", out var insu) ? insu.GetString() : null,
                image_ingredients_url = product.TryGetProperty("image_ingredients_url", out var iinu) ? iinu.GetString() : null,
                image_ingredients_small_url = product.TryGetProperty("image_ingredients_small_url", out var iinsu) ? iinsu.GetString() : null,

                // Check for selected_images structure
                has_selected_images = product.TryGetProperty("selected_images", out var si),
                selected_images_raw = product.TryGetProperty("selected_images", out var si2) ? si2.GetRawText() : null,

                // Check for images structure
                has_images = product.TryGetProperty("images", out var imgs),
                images_keys = product.TryGetProperty("images", out var imgs2) ?
                    string.Join(", ", imgs2.EnumerateObject().Select(p => p.Name)) : null,

                full_product_json = product.GetRawText()
            };

            return Ok(imageDebug);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to debug OpenFoodFacts for barcode {Barcode}", barcode);
            return StatusCode(500, new { error = ex.Message, stackTrace = ex.StackTrace });
        }
    }

    /// <summary>
    /// Get import history
    /// </summary>
    [HttpGet("import/history")]
    public ActionResult<List<ImportHistoryDto>> GetImportHistory()
    {
        var history = _importJobs.Values
            .OrderByDescending(j => j.StartedAt)
            .Take(50)
            .Select(j => new ImportHistoryDto
            {
                Id = j.ImportId,
                Source = j.Source,
                Status = j.Status,
                RecordsImported = j.SuccessCount,
                StartedAt = j.StartedAt,
                CompletedAt = j.CompletedAt
            })
            .ToList();

        return Ok(history);
    }

    /// <summary>
    /// Clear all import jobs (for testing)
    /// </summary>
    [HttpDelete("import/clear")]
    public IActionResult ClearImportHistory()
    {
        _importJobs.Clear();
        _logger.LogInformation("Import history cleared");
        return Ok(new { message = "Import history cleared" });
    }

    private ImportStatusDto MapToDto(ImportJobStatus job)
    {
        return new ImportStatusDto
        {
            ImportId = job.ImportId,
            Source = job.Source,
            Status = job.Status,
            TotalRecords = job.TotalRecords,
            ProcessedRecords = job.ProcessedRecords,
            SuccessCount = job.SuccessCount,
            ErrorCount = job.ErrorCount,
            ErrorMessage = job.ErrorMessage,
            StartedAt = job.StartedAt,
            CompletedAt = job.CompletedAt
        };
    }

    /// <summary>
    /// Import USDA food_portion.csv into the IngredientUnitDensity table.
    /// Safe to re-run (UPSERT is idempotent). Requires UsdaImport:PortionCsvPath in configuration.
    /// </summary>
    [HttpPost("import/usda-portions")]
    public async Task<IActionResult> ImportUsdaPortions(CancellationToken ct)
    {
        try
        {
            int count = await _usdaPortionImportService.RunImportAsync(ct);
            return Ok(new { message = $"USDA portion import complete. Upserted {count} rows.", rowsUpserted = count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during USDA portion import");
            return StatusCode(500, new { message = "An error occurred during USDA portion import." });
        }
    }

    /// <summary>
    /// Force-import a product, bypassing the approval queue.
    /// Sets ApprovalStatus='Approved' immediately. Admin only.
    /// </summary>
    [HttpPost("products/force-import")]
    public async Task<IActionResult> ForceImportProduct(
        [FromBody] ManualProductImportRequest req,
        CancellationToken ct)
    {
        var adminIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(adminIdClaim, out var adminId))
            return Unauthorized();

        var productId = await _productRepository.ForceImportAsync(req, adminId);

        _logger.LogInformation("Admin {AdminId} force-imported product '{Name}' → {ProductId}",
            adminId, req.ProductName, productId);

        return Ok(new { productId });
    }
}

/// <summary>
/// Internal tracking of import job status
/// </summary>
internal class ImportJobStatus
{
    public Guid ImportId { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public int TotalRecords { get; set; }
    public int ProcessedRecords { get; set; }
    public int SuccessCount { get; set; }
    public int ErrorCount { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

/// <summary>
/// DTO for import requests
/// </summary>
public class ImportRequest
{
    public string? Query { get; set; }
    public int? MaxResults { get; set; }
}

/// <summary>
/// DTO for import status
/// </summary>
public class ImportStatusDto
{
    public Guid ImportId { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public int TotalRecords { get; set; }
    public int ProcessedRecords { get; set; }
    public int SuccessCount { get; set; }
    public int ErrorCount { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int ProgressPercentage => TotalRecords > 0 ? (int)((ProcessedRecords / (double)TotalRecords) * 100) : 0;
}

/// <summary>
/// DTO for import history
/// </summary>
public class ImportHistoryDto
{
    public Guid Id { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int RecordsImported { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public TimeSpan? Duration => CompletedAt.HasValue ? CompletedAt.Value - StartedAt : null;
}

/// <summary>
/// Request for backfilling images
/// </summary>
public class BackfillImagesRequest
{
    public int MaxProducts { get; set; } = 100;
}
