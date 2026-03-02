using ExpressRecipe.PriceService.Data;
using ExpressRecipe.PriceService.Services;
using ExpressRecipe.PriceService.Workers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpressRecipe.PriceService.Controllers;

[ApiController]
[Route("api/prices")]
public class PricesController : ControllerBase
{
    private readonly ILogger<PricesController> _logger;
    private readonly IPriceRepository _repository;
    private readonly PriceDataImportWorker? _importWorker;
    private readonly IConfiguration _configuration;

    public PricesController(
        ILogger<PricesController> logger,
        IPriceRepository repository,
        IConfiguration configuration,
        PriceDataImportWorker? importWorker = null)
    {
        _logger = logger;
        _repository = repository;
        _importWorker = importWorker;
        _configuration = configuration;
    }

    /// <summary>GET /api/prices/search — Search prices with filters</summary>
    [AllowAnonymous]
    [HttpGet("search")]
    public async Task<IActionResult> SearchPrices([FromQuery] PriceSearchRequest request)
    {
        var prices = await _repository.SearchPricesAsync(request);
        var total = await _repository.GetSearchCountAsync(request);
        return Ok(new { data = prices, total, page = request.Page, pageSize = request.PageSize });
    }

    /// <summary>GET /api/prices/product/{productId} — Get prices for a specific product</summary>
    [AllowAnonymous]
    [HttpGet("product/{productId:guid}")]
    public async Task<IActionResult> GetPricesByProduct(Guid productId, [FromQuery] int daysBack = 90)
    {
        var request = new PriceSearchRequest { ProductId = productId, DaysBack = daysBack, PageSize = 100 };
        var prices = await _repository.SearchPricesAsync(request);
        return Ok(prices);
    }

    /// <summary>GET /api/prices/upc/{upc} — Get prices by UPC/barcode</summary>
    [AllowAnonymous]
    [HttpGet("upc/{upc}")]
    public async Task<IActionResult> GetPricesByUpc(string upc, [FromQuery] int limit = 50)
    {
        if (string.IsNullOrWhiteSpace(upc)) return BadRequest("UPC is required");
        var prices = await _repository.GetPricesByUpcAsync(upc, limit);
        return Ok(prices);
    }

    /// <summary>GET /api/prices/search/name — Search prices by product name</summary>
    [AllowAnonymous]
    [HttpGet("search/name")]
    public async Task<IActionResult> GetPricesByName([FromQuery] string name, [FromQuery] int limit = 50)
    {
        if (string.IsNullOrWhiteSpace(name)) return BadRequest("name is required");
        var prices = await _repository.GetPricesByProductNameAsync(name, limit);
        return Ok(prices);
    }

    /// <summary>POST /api/prices/batch — Get prices for multiple product IDs</summary>
    [AllowAnonymous]
    [HttpPost("batch")]
    public async Task<IActionResult> GetBatchPrices([FromBody] BatchPriceRequest request)
    {
        if (request.ProductIds == null || request.ProductIds.Count == 0)
            return BadRequest("productIds is required");

        var prices = await _repository.GetBatchPricesAsync(request.ProductIds);
        return Ok(prices);
    }

    /// <summary>GET /api/prices/best/{productId} — Get best (lowest) prices for a product</summary>
    [AllowAnonymous]
    [HttpGet("best/{productId:guid}")]
    public async Task<IActionResult> GetBestPrices(Guid productId, [FromQuery] int limit = 10)
    {
        var prices = await _repository.GetBestPricesAsync(productId, limit);
        return Ok(prices);
    }

    /// <summary>GET /api/prices/import/status — Get import status for each data source</summary>
    [AllowAnonymous]
    [HttpGet("import/status")]
    public async Task<IActionResult> GetImportStatus()
    {
        var sources = new[] { "OpenPrices", "GroceryDB", "USDA", "Manual" };
        var statuses = new List<object>();

        foreach (var source in sources)
        {
            var last = await _repository.GetLastImportAsync(source);
            statuses.Add(new
            {
                dataSource = source,
                lastImport = last?.ImportedAt,
                lastSuccess = last?.Success,
                lastRecordsImported = last?.RecordsImported
            });
        }

        var totalPrices = await _repository.GetProductPriceCountAsync();
        return Ok(new { totalPrices, sources = statuses });
    }

    /// <summary>POST /api/prices/import/trigger — Trigger a manual import (admin only)</summary>
    [Authorize]
    [HttpPost("import/trigger")]
    public async Task<IActionResult> TriggerImport([FromQuery] string source = "OpenPrices")
    {
        _logger.LogInformation("Manual import triggered for source: {Source}", source);

        if (_importWorker == null)
            return StatusCode(503, "Import worker is not available");

        // Delegate to the hosted PriceDataImportWorker so the work is tracked
        // and will be cancelled cleanly on application shutdown.
        _ = _importWorker.RunImportAsync(HttpContext.RequestAborted);

        return Accepted(new { message = "Import triggered", source });
    }
}

public class BatchPriceRequest
{
    public List<Guid> ProductIds { get; set; } = new();
}
