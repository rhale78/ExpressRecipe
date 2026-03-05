using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ExpressRecipe.PriceService.Data;

namespace ExpressRecipe.PriceService.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class PriceController : ControllerBase
{
    private readonly ILogger<PriceController> _logger;
    private readonly IPriceRepository _repository;

    public PriceController(ILogger<PriceController> logger, IPriceRepository repository)
    {
        _logger = logger;
        _repository = repository;
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

    [HttpPost("stores")]
    public async Task<IActionResult> AddStore([FromBody] AddStoreRequest request)
    {
        try
        {
            var storeId = await _repository.AddStoreAsync(
                request.Name, request.Address, request.City, request.State, request.ZipCode, request.Chain);
            return Ok(new { id = storeId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding store");
            return StatusCode(500, new { message = "An error occurred while adding the store" });
        }
    }

    [HttpPost("prices")]
    public async Task<IActionResult> RecordPrice([FromBody] RecordPriceRequest request)
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();
            var priceId = await _repository.RecordPriceAsync(
                request.ProductId, request.StoreId, request.Price, userId.Value, request.ObservedAt);
            return Ok(new { id = priceId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording price for product {ProductId}", request.ProductId);
            return StatusCode(500, new { message = "An error occurred while recording the price" });
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

    [HttpPost("deals")]
    public async Task<IActionResult> CreateDeal([FromBody] CreateDealRequest request)
    {
        try
        {
            var dealId = await _repository.CreateDealAsync(
                request.ProductId, request.StoreId, request.DealType,
                request.OriginalPrice, request.SalePrice, request.StartDate, request.EndDate);
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
