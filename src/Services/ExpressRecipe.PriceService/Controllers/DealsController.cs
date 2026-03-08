using ExpressRecipe.PriceService.Data;
using ExpressRecipe.PriceService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpressRecipe.PriceService.Controllers;

[ApiController]
[Route("api/deals")]
public class DealsController : ControllerBase
{
    private readonly ILogger<DealsController> _logger;
    private readonly IPriceRepository _repository;
    private readonly IEffectivePriceCalculator _effectivePriceCalculator;

    public DealsController(
        ILogger<DealsController> logger,
        IPriceRepository repository,
        IEffectivePriceCalculator effectivePriceCalculator)
    {
        _logger = logger;
        _repository = repository;
        _effectivePriceCalculator = effectivePriceCalculator;
    }

    /// <summary>POST /api/deals/enhanced — Create an enhanced deal with full discount metadata</summary>
    [Authorize]
    [HttpPost("enhanced")]
    public async Task<IActionResult> CreateEnhancedDeal([FromBody] CreateEnhancedDealRequest request)
    {
        try
        {
            var dealId = await _repository.CreateEnhancedDealAsync(request, HttpContext.RequestAborted);
            _logger.LogInformation("[DealsController] Enhanced deal {DealId} created for product {ProductId}", dealId, request.ProductId);
            return Ok(new { id = dealId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating enhanced deal for product {ProductId}", request.ProductId);
            return StatusCode(500, new { message = "An error occurred while creating the deal" });
        }
    }

    /// <summary>GET /api/deals/{productId}/effective-price — Compute effective price after applying best deal</summary>
    [AllowAnonymous]
    [HttpGet("{productId:guid}/effective-price")]
    public async Task<IActionResult> GetEffectivePrice(Guid productId, [FromQuery] Guid storeId, [FromQuery] int qty = 1)
    {
        try
        {
            if (storeId == Guid.Empty) { return BadRequest("storeId is required"); }
            if (qty < 1) { qty = 1; }

            var result = await _repository.CalculateEffectivePriceAsync(productId, storeId, qty, HttpContext.RequestAborted);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating effective price for product {ProductId} at store {StoreId}", productId, storeId);
            return StatusCode(500, new { message = "An error occurred while calculating the effective price" });
        }
    }

    /// <summary>GET /api/deals — Get active deals, optionally filtered by store/product</summary>
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> GetActiveDeals([FromQuery] Guid? storeId, [FromQuery] Guid? productId)
    {
        try
        {
            var deals = await _repository.GetActiveDealsAsync(storeId, productId);
            return Ok(deals);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active deals");
            return StatusCode(500, new { message = "An error occurred while retrieving deals" });
        }
    }
}
