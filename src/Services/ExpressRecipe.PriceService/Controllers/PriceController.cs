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

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("stores")]
    public async Task<IActionResult> GetStores([FromQuery] string? city, [FromQuery] string? state, [FromQuery] string? chain)
    {
        var stores = await _repository.GetStoresAsync(city, state, chain);
        return Ok(stores);
    }

    [HttpPost("stores")]
    public async Task<IActionResult> AddStore([FromBody] AddStoreRequest request)
    {
        var storeId = await _repository.AddStoreAsync(
            request.Name, request.Address, request.City, request.State, request.ZipCode, request.Chain);
        return Ok(new { id = storeId });
    }

    [HttpPost("prices")]
    public async Task<IActionResult> RecordPrice([FromBody] RecordPriceRequest request)
    {
        var userId = GetUserId();
        var priceId = await _repository.RecordPriceAsync(
            request.ProductId, request.StoreId, request.Price, userId, request.ObservedAt);
        return Ok(new { id = priceId });
    }

    [HttpGet("products/{productId}/prices")]
    public async Task<IActionResult> GetProductPrices(Guid productId, [FromQuery] Guid? storeId, [FromQuery] int daysBack = 90)
    {
        var prices = await _repository.GetProductPricesAsync(productId, storeId, daysBack);
        return Ok(prices);
    }

    [HttpGet("products/{productId}/trend")]
    public async Task<IActionResult> GetPriceTrend(Guid productId, [FromQuery] Guid? storeId)
    {
        var trend = await _repository.GetPriceTrendAsync(productId, storeId);
        return Ok(trend);
    }

    [HttpGet("deals")]
    public async Task<IActionResult> GetDeals([FromQuery] Guid? storeId, [FromQuery] Guid? productId)
    {
        var deals = await _repository.GetActiveDealsAsync(storeId, productId);
        return Ok(deals);
    }

    [HttpPost("deals")]
    public async Task<IActionResult> CreateDeal([FromBody] CreateDealRequest request)
    {
        var dealId = await _repository.CreateDealAsync(
            request.ProductId, request.StoreId, request.DealType,
            request.OriginalPrice, request.SalePrice, request.StartDate, request.EndDate);
        return Ok(new { id = dealId });
    }

    [HttpPost("compare")]
    public async Task<IActionResult> ComparePrices([FromBody] ComparePricesRequest request)
    {
        var comparison = await _repository.ComparePricesAsync(request.ProductIds, request.StoreIds);
        return Ok(comparison);
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
