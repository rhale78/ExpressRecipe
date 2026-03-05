using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ExpressRecipe.PriceService.Data;
using ExpressRecipe.PriceService.Services;

namespace ExpressRecipe.PriceService.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class PriceController : ControllerBase
{
    private readonly ILogger<PriceController> _logger;
    private readonly IPriceRepository _repository;
    private readonly IPriceEventPublisher _events;
    private readonly IPriceBatchChannel _batchChannel;

    public PriceController(
        ILogger<PriceController> logger,
        IPriceRepository repository,
        IPriceEventPublisher events,
        IPriceBatchChannel batchChannel)
    {
        _logger = logger;
        _repository = repository;
        _events = events;
        _batchChannel = batchChannel;
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    [HttpGet("stores")]
    public async Task<IActionResult> GetStores([FromQuery] string? city, [FromQuery] string? state, [FromQuery] string? chain)
    {
        try
        {
            var stores = await _repository.GetStoresAsync(city, state, chain);
            return Ok(stores);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving stores");
            return StatusCode(500, new { message = "An error occurred while retrieving stores" });
        }
    }

    /// <summary>
    /// Add a store – synchronous REST path. Fires a StoreAdded event for downstream consumers.
    /// </summary>
    [HttpPost("stores")]
    public async Task<IActionResult> AddStore([FromBody] AddStoreRequest request)
    {
        try
        {
            var storeId = await _repository.AddStoreAsync(
                request.Name, request.Address, request.City, request.State, request.ZipCode, request.Chain);

            _logger.LogInformation("[PriceController] Store {StoreId} '{Name}' added", storeId, request.Name);

            // Fire event – works when messaging is on; NullPriceEventPublisher skips silently when off
            await _events.PublishStoreAddedAsync(
                storeId, request.Name, request.City, request.State, request.Chain,
                HttpContext.RequestAborted);

            return Ok(new { id = storeId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding store");
            return StatusCode(500, new { message = "An error occurred while adding the store" });
        }
    }

    /// <summary>
    /// Record a single price observation – synchronous path.
    /// Writes directly to the database and fires a PriceRecorded event.
    /// For bulk submissions prefer POST /api/price/prices/batch (async channel path).
    /// </summary>
    [HttpPost("prices")]
    public async Task<IActionResult> RecordPrice([FromBody] RecordPriceRequest request)
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var priceId = await _repository.RecordPriceAsync(
                request.ProductId, request.StoreId, request.Price, userId.Value, request.ObservedAt);

            _logger.LogInformation(
                "[PriceController] Price {PriceId} recorded for product {ProductId} at store {StoreId} = ${Price} by user {UserId}",
                priceId, request.ProductId, request.StoreId, request.Price, userId.Value);

            // Fire event – same event whether this came via REST (sync) or the channel worker (async)
            await _events.PublishPriceRecordedAsync(
                priceId, request.ProductId, request.StoreId,
                request.Price, userId.Value, HttpContext.RequestAborted);

            return Ok(new { id = priceId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording price for product {ProductId}", request.ProductId);
            return StatusCode(500, new { message = "An error occurred while recording the price" });
        }
    }

    /// <summary>
    /// Submit multiple price observations in one call – asynchronous channel path.
    /// Items are written to the <see cref="IPriceBatchChannel"/> and processed by
    /// <see cref="PriceBatchChannelWorker"/> in the background. Returns immediately
    /// with a session ID that clients can use to correlate the batch.
    /// </summary>
    [HttpPost("prices/batch")]
    public async Task<IActionResult> RecordPricesBatch([FromBody] BatchRecordPriceRequest request)
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            if (request.Prices == null || request.Prices.Count == 0)
                return BadRequest(new { message = "prices list cannot be empty" });

            const int maxBatch = 1000;
            if (request.Prices.Count > maxBatch)
                return BadRequest(new { message = $"Batch exceeds maximum of {maxBatch} items" });

            var sessionId = Guid.NewGuid().ToString("N");
            var accepted  = 0;

            foreach (var item in request.Prices)
            {
                var batchItem = new PriceBatchItem
                {
                    ProductId   = item.ProductId,
                    StoreId     = item.StoreId,
                    Price       = item.Price,
                    ObservedAt  = item.ObservedAt,
                    SubmittedBy = userId.Value,
                    SessionId   = sessionId
                };

                if (!_batchChannel.TryWrite(batchItem))
                {
                    // Channel momentarily full – apply backpressure and wait for space
                    await _batchChannel.WriteAsync(batchItem, HttpContext.RequestAborted);
                }
                accepted++;
            }

            _logger.LogInformation(
                "[PriceController] Batch submitted: session={SessionId} submitted={Submitted} accepted={Accepted} by user {UserId}",
                sessionId, request.Prices.Count, accepted, userId.Value);

            // Fire a single batch-submitted event (accepted count may differ from submitted when channel is full)
            await _events.PublishPriceBatchSubmittedAsync(
                sessionId, accepted, userId.Value, HttpContext.RequestAborted);

            return Accepted(new
            {
                sessionId,
                submitted = request.Prices.Count,
                accepted,
                message   = "Price batch queued for async processing"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting price batch");
            return StatusCode(500, new { message = "An error occurred while submitting the price batch" });
        }
    }

    [HttpGet("products/{productId}/prices")]
    public async Task<IActionResult> GetProductPrices(Guid productId, [FromQuery] Guid? storeId, [FromQuery] int daysBack = 90)
    {
        try
        {
            var prices = await _repository.GetProductPricesAsync(productId, storeId, daysBack);
            return Ok(prices);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving prices for product {ProductId}", productId);
            return StatusCode(500, new { message = "An error occurred while retrieving product prices" });
        }
    }

    [HttpGet("products/{productId}/trend")]
    public async Task<IActionResult> GetPriceTrend(Guid productId, [FromQuery] Guid? storeId)
    {
        try
        {
            var trend = await _repository.GetPriceTrendAsync(productId, storeId);
            return Ok(trend);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving price trend for product {ProductId}", productId);
            return StatusCode(500, new { message = "An error occurred while retrieving the price trend" });
        }
    }

    [HttpGet("deals")]
    public async Task<IActionResult> GetDeals([FromQuery] Guid? storeId, [FromQuery] Guid? productId)
    {
        try
        {
            var deals = await _repository.GetActiveDealsAsync(storeId, productId);
            return Ok(deals);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving deals");
            return StatusCode(500, new { message = "An error occurred while retrieving deals" });
        }
    }

    /// <summary>
    /// Create a deal – synchronous path. Fires a DealCreated event.
    /// </summary>
    [HttpPost("deals")]
    public async Task<IActionResult> CreateDeal([FromBody] CreateDealRequest request)
    {
        try
        {
            var dealId = await _repository.CreateDealAsync(
                request.ProductId, request.StoreId, request.DealType,
                request.OriginalPrice, request.SalePrice, request.StartDate, request.EndDate);

            _logger.LogInformation(
                "[PriceController] Deal {DealId} created for product {ProductId} at store {StoreId} type={DealType}",
                dealId, request.ProductId, request.StoreId, request.DealType);

            await _events.PublishDealCreatedAsync(
                dealId, request.ProductId, request.StoreId, request.DealType,
                request.OriginalPrice, request.SalePrice,
                request.StartDate, request.EndDate, HttpContext.RequestAborted);

            return Ok(new { id = dealId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating deal for product {ProductId}", request.ProductId);
            return StatusCode(500, new { message = "An error occurred while creating the deal" });
        }
    }

    [HttpPost("compare")]
    public async Task<IActionResult> ComparePrices([FromBody] ComparePricesRequest request)
    {
        try
        {
            var comparison = await _repository.ComparePricesAsync(request.ProductIds, request.StoreIds);
            return Ok(comparison);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error comparing prices");
            return StatusCode(500, new { message = "An error occurred while comparing prices" });
        }
    }
}

// ── Request models ──────────────────────────────────────────────────────────

public class AddStoreRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
    public string? Chain { get; set; }
}

public class RecordPriceRequest
{
    public Guid ProductId { get; set; }
    public Guid StoreId { get; set; }
    public decimal Price { get; set; }
    public DateTime? ObservedAt { get; set; }
}

/// <summary>
/// A single item in a batch price submission. Identical fields to <see cref="RecordPriceRequest"/>.
/// </summary>
public class BatchRecordPriceItem
{
    public Guid ProductId   { get; set; }
    public Guid StoreId     { get; set; }
    public decimal Price    { get; set; }
    public DateTime? ObservedAt { get; set; }
}

/// <summary>
/// Request body for <c>POST /api/price/prices/batch</c> (async channel path).
/// </summary>
public class BatchRecordPriceRequest
{
    public List<BatchRecordPriceItem> Prices { get; set; } = new();
}

public class CreateDealRequest
{
    public Guid ProductId { get; set; }
    public Guid StoreId { get; set; }
    public string DealType { get; set; } = string.Empty;
    public decimal OriginalPrice { get; set; }
    public decimal SalePrice { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

public class ComparePricesRequest
{
    public List<Guid> ProductIds { get; set; } = new();
    public List<Guid> StoreIds { get; set; } = new();
}

