using ExpressRecipe.PriceService.Data;
using ExpressRecipe.PriceService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpressRecipe.PriceService.Controllers;

/// <summary>
/// Admin endpoints for managing price imports and batch operations
/// </summary>
[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    private readonly ILogger<AdminController> _logger;
    private readonly IOpenPricesImportService _importService;
    private readonly DataflowOpenPricesImportService? _dataflowImportService;
    private readonly IBatchProductLookupService? _batchProductLookup;
    private readonly IBatchPriceInsertService? _batchPriceInsert;
    private readonly IPriceRepository _repository;

    public AdminController(
        ILogger<AdminController> logger,
        IOpenPricesImportService importService,
        IPriceRepository repository,
        DataflowOpenPricesImportService? dataflowImportService = null,
        IBatchProductLookupService? batchProductLookup = null,
        IBatchPriceInsertService? batchPriceInsert = null)
    {
        _logger = logger;
        _importService = importService;
        _repository = repository;
        _dataflowImportService = dataflowImportService;
        _batchProductLookup = batchProductLookup;
        _batchPriceInsert = batchPriceInsert;
    }

    /// <summary>
    /// Trigger manual price import (standard implementation)
    /// </summary>
    [HttpPost("import/standard")]
    public async Task<IActionResult> TriggerStandardImport([FromQuery] string? url, [FromQuery] string? format)
    {
        try
        {
            _logger.LogInformation("Manual standard import triggered by admin");
            
            var importUrl = url ?? "https://huggingface.co/datasets/openfoodfacts/openprices/resolve/main/data/prices.parquet?download=1";
            var importFormat = format ?? "parquet";
            
            var result = await _importService.ImportFromUrlAsync(importUrl, importFormat);
            
            return Ok(new
            {
                Success = result.Success,
                Source = result.DataSource,
                Processed = result.Processed,
                Imported = result.Imported,
                Skipped = result.Skipped,
                Errors = result.Errors
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Standard import failed");
            return StatusCode(500, new { message = ex.Message });
        }
    }

    /// <summary>
    /// Trigger manual price import using dataflow pipeline (optimized for performance)
    /// </summary>
    [HttpPost("import/dataflow")]
    public async Task<IActionResult> TriggerDataflowImport([FromQuery] string? url, [FromQuery] string? format)
    {
        try
        {
            if (_dataflowImportService == null)
            {
                return BadRequest(new { message = "Dataflow import service not configured" });
            }

            _logger.LogInformation("Manual dataflow import triggered by admin");
            
            var importUrl = url ?? "https://huggingface.co/datasets/openfoodfacts/openprices/resolve/main/data/prices.parquet?download=1";
            var importFormat = format ?? "parquet";
            
            var result = await _dataflowImportService.ImportFromUrlAsync(importUrl, importFormat);
            
            return Ok(new
            {
                Success = result.Success,
                Source = result.DataSource,
                Processed = result.Processed,
                Imported = result.Imported,
                Skipped = result.Skipped,
                Errors = result.Errors,
                Note = "Used optimized dataflow pipeline with batched product lookups"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dataflow import failed");
            return StatusCode(500, new { message = ex.Message });
        }
    }

    /// <summary>
    /// Get last import statistics
    /// </summary>
    [HttpGet("import/last")]
    public async Task<IActionResult> GetLastImport([FromQuery] string? source)
    {
        try
        {
            var dataSource = source ?? "OpenPrices-Parquet";
            var lastImport = await _repository.GetLastImportAsync(dataSource);
            
            if (lastImport == null)
            {
                return NotFound(new { message = $"No imports found for source '{dataSource}'" });
            }
            
            return Ok(lastImport);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving last import");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Test batch product lookup performance
    /// </summary>
    [HttpPost("test/batch-lookup")]
    public async Task<IActionResult> TestBatchLookup([FromBody] TestBatchLookupRequest request)
    {
        try
        {
            if (_batchProductLookup == null)
            {
                return BadRequest(new { message = "Batch product lookup service not configured" });
            }

            if (request?.Barcodes == null || !request.Barcodes.Any())
            {
                return BadRequest(new { message = "Barcodes required" });
            }

            _logger.LogInformation("Testing batch product lookup for {Count} barcodes", request.Barcodes.Count);
            
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var products = await _batchProductLookup.GetProductsByBarcodesAsync(request.Barcodes);
            sw.Stop();
            
            return Ok(new
            {
                RequestedCount = request.Barcodes.Count,
                FoundCount = products.Count,
                ElapsedMs = sw.ElapsedMilliseconds,
                RatePerSecond = request.Barcodes.Count / sw.Elapsed.TotalSeconds,
                Products = products
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Batch lookup test failed");
            return StatusCode(500, new { message = ex.Message });
        }
    }

    /// <summary>
    /// Get price data statistics
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        try
        {
            var totalPrices = await _repository.GetProductPriceCountAsync();
            
            return Ok(new
            {
                TotalPrices = totalPrices,
                Note = "Additional statistics coming soon"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving stats");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }
}

public class TestBatchLookupRequest
{
    public List<string> Barcodes { get; set; } = new();
}
