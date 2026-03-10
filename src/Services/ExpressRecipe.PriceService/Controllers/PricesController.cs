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
    private readonly IPriceUnitNormalizer _unitNormalizer;
    private readonly PriceDataImportWorker? _importWorker;
    private readonly IConfiguration _configuration;

    public PricesController(
        ILogger<PricesController> logger,
        IPriceRepository repository,
        IPriceUnitNormalizer unitNormalizer,
        IConfiguration configuration,
        PriceDataImportWorker? importWorker = null)
    {
        _logger = logger;
        _repository = repository;
        _unitNormalizer = unitNormalizer;
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

    /// <summary>POST /api/prices/search — Search prices with filters (body-based)</summary>
    [AllowAnonymous]
    [HttpPost("search")]
    public async Task<IActionResult> SearchPricesPost([FromBody] PriceSearchRequest request)
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

    /// <summary>GET /api/prices/{productId}/history — Append-only price history for a product</summary>
    [AllowAnonymous]
    [HttpGet("{productId:guid}/history")]
    public async Task<IActionResult> GetPriceHistory(Guid productId, [FromQuery] Guid? storeId, [FromQuery] int daysBack = 90)
    {
        var history = await _repository.GetPriceHistoryAsync(productId, storeId, daysBack);
        return Ok(history);
    }

    /// <summary>GET /api/prices/{productId}/stats — Price statistics for a product</summary>
    [AllowAnonymous]
    [HttpGet("{productId:guid}/stats")]
    public async Task<IActionResult> GetPriceStats(Guid productId, [FromQuery] Guid? storeId, [FromQuery] int daysBack = 90)
    {
        var stats = await _repository.GetPriceStatsAsync(productId, storeId, daysBack);
        return Ok(stats);
    }

    /// <summary>GET /api/prices/{productId}/unit-compare — Compare price per unit across stores/products</summary>
    [AllowAnonymous]
    [HttpGet("{productId:guid}/unit-compare")]
    public async Task<IActionResult> CompareByUnit(Guid productId, [FromQuery] string unit = "oz")
    {
        if (string.IsNullOrWhiteSpace(unit)) { return BadRequest("unit is required"); }
        var results = await _repository.CompareByUnitAsync(new[] { productId }, unit);
        return Ok(results);
    }

    /// <summary>GET /api/prices/store/{storeId}/products — Products linked to a store (paged)</summary>
    [AllowAnonymous]
    [HttpGet("store/{storeId:guid}/products")]
    public async Task<IActionResult> GetProductsForStore(Guid storeId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        if (page < 1) { page = 1; }
        if (pageSize is < 1 or > 200) { pageSize = 50; }
        var products = await _repository.GetProductsForStoreAsync(storeId, page, pageSize);
        return Ok(products);
    }

    /// <summary>GET /api/prices/product/{productId}/stores — Stores that carry a product</summary>
    [AllowAnonymous]
    [HttpGet("product/{productId:guid}/stores")]
    public async Task<IActionResult> GetStoresForProduct(Guid productId)
    {
        var stores = await _repository.GetStoresForProductAsync(productId);
        return Ok(stores);
    }

    /// <summary>POST /api/prices/user-entry — Manual user price entry (writes to PriceHistory)</summary>
    [Authorize]
    [HttpPost("user-entry")]
    public async Task<IActionResult> RecordUserPriceEntry([FromBody] UserPriceEntryRequest request)
    {
        if (request.ProductId == Guid.Empty) { return BadRequest("productId is required"); }
        if (request.FinalPrice <= 0) { return BadRequest("finalPrice must be greater than zero"); }

        var unitMetrics = _unitNormalizer.ComputeUnitPrices(request.FinalPrice, request.Unit, request.Quantity);

        var record = new PriceHistoryRecord
        {
            ProductId = request.ProductId,
            Upc = request.Upc,
            ProductName = request.ProductName ?? string.Empty,
            StoreId = request.StoreId,
            StoreName = request.StoreName,
            IsOnline = request.IsOnline,
            BasePrice = request.BasePrice > 0 ? request.BasePrice : request.FinalPrice,
            FinalPrice = request.FinalPrice,
            Currency = request.Currency ?? "USD",
            Unit = unitMetrics.NormalizedUnit ?? request.Unit,
            Quantity = request.Quantity,
            PricePerOz = unitMetrics.PricePerOz,
            PricePerHundredG = unitMetrics.PricePerHundredG,
            DataSource = "Manual",
            ObservedAt = request.ObservedAt.HasValue
                ? new DateTimeOffset(request.ObservedAt.Value, TimeSpan.Zero)
                : DateTimeOffset.UtcNow
        };

        await _repository.RecordPriceHistoryAsync(record);
        _logger.LogInformation("Manual price entry recorded for product {ProductId}", request.ProductId);
        return Ok(new { message = "Price entry recorded" });
    }

    /// <summary>GET /api/prices/import/status — Get import status for each data source</summary>
    [AllowAnonymous]
    [HttpGet("import/status")]
    public async Task<IActionResult> GetImportStatus()
    {
        var sources = new[] { "OpenPrices", "GroceryDB", "USDA", "Manual", "USDA_FMAP", "BLS_CPI", "KAGGLE_WALMART", "KAGGLE_COSTCO" };
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
    [Authorize(Roles = "Admin")]
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

public class UserPriceEntryRequest
{
    public Guid ProductId { get; set; }
    public string? Upc { get; set; }
    public string? ProductName { get; set; }
    public Guid? StoreId { get; set; }
    public string? StoreName { get; set; }
    public bool IsOnline { get; set; }
    public decimal BasePrice { get; set; }
    public decimal FinalPrice { get; set; }
    public string? Currency { get; set; }
    public string? Unit { get; set; }
    public decimal? Quantity { get; set; }
    public DateTime? ObservedAt { get; set; }
}
