using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ExpressRecipe.ShoppingService.Data;

namespace ExpressRecipe.ShoppingService.Controllers;

[Authorize]
[ApiController]
[Route("api/shopping/[controller]")]
public class StoresController : ControllerBase
{
    private readonly ILogger<StoresController> _logger;
    private readonly IShoppingRepository _repository;

    public StoresController(ILogger<StoresController> logger, IShoppingRepository repository)
    {
        _logger = logger;
        _repository = repository;
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    /// <summary>
    /// Get all stores
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetStores()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var stores = await _repository.GetUserStoresAsync(userId.Value);
        return Ok(stores);
    }

    /// <summary>
    /// Get store by ID
    /// </summary>
    [HttpGet("{storeId}")]
    public async Task<IActionResult> GetStore(Guid storeId)
    {
        var store = await _repository.GetStoreByIdAsync(storeId);
        if (store == null)
            return NotFound();

        return Ok(store);
    }

    /// <summary>
    /// Create new store
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateStore([FromBody] CreateStoreRequestDto request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var storeId = await _repository.CreateStoreAsync(
            userId.Value,
            request.Name,
            request.Chain,
            request.Address,
            request.City,
            request.State,
            request.ZipCode,
            request.Latitude,
            request.Longitude
        );

        _logger.LogInformation("User {UserId} created store {StoreId}", userId, storeId);
        return CreatedAtAction(nameof(GetStore), new { storeId }, new { id = storeId });
    }

    /// <summary>
    /// Update store
    /// </summary>
    [HttpPut("{storeId}")]
    public async Task<IActionResult> UpdateStore(Guid storeId, [FromBody] UpdateStoreRequestDto request)
    {
        await _repository.UpdateStoreAsync(storeId, request.Name, request.Address, request.Latitude, request.Longitude);
        _logger.LogInformation("Updated store {StoreId}", storeId);
        return NoContent();
    }

    /// <summary>
    /// Find nearby stores using GPS coordinates
    /// </summary>
    [HttpPost("nearby")]
    public async Task<IActionResult> FindNearbyStores([FromBody] NearbyStoresRequest request)
    {
        var stores = await _repository.GetNearbyStoresAsync(
            request.Latitude,
            request.Longitude,
            request.MaxDistanceKm ?? 10.0
        );

        _logger.LogInformation("Found {Count} stores near ({Lat}, {Lng})", 
            stores.Count, request.Latitude, request.Longitude);
        
        return Ok(stores);
    }

    /// <summary>
    /// Set preferred store for user
    /// </summary>
    [HttpPut("{storeId}/preferred")]
    public async Task<IActionResult> SetPreferredStore(Guid storeId)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        await _repository.SetPreferredStoreAsync(userId.Value, storeId);
        _logger.LogInformation("User {UserId} set preferred store {StoreId}", userId, storeId);
        return NoContent();
    }

    /// <summary>
    /// Get store layout
    /// </summary>
    [HttpGet("{storeId}/layout")]
    public async Task<IActionResult> GetStoreLayout(Guid storeId)
    {
        var layout = await _repository.GetStoreLayoutAsync(storeId);
        return Ok(layout);
    }

    /// <summary>
    /// Create store layout entry
    /// </summary>
    [HttpPost("{storeId}/layout")]
    public async Task<IActionResult> CreateStoreLayout(Guid storeId, [FromBody] CreateStoreLayoutRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var layoutId = await _repository.CreateStoreLayoutAsync(
            userId.Value,
            storeId,
            request.CategoryName,
            request.Aisle,
            request.OrderIndex
        );

        _logger.LogInformation("User {UserId} created layout {LayoutId} for store {StoreId}", 
            userId, layoutId, storeId);
        
        return CreatedAtAction(nameof(GetStoreLayout), new { storeId }, new { id = layoutId });
    }

    /// <summary>
    /// Update store layout entry
    /// </summary>
    [HttpPut("layout/{layoutId}")]
    public async Task<IActionResult> UpdateStoreLayout(Guid layoutId, [FromBody] UpdateStoreLayoutRequest request)
    {
        await _repository.UpdateStoreLayoutAsync(layoutId, request.Aisle, request.OrderIndex);
        _logger.LogInformation("Updated store layout {LayoutId}", layoutId);
        return NoContent();
    }

    /// <summary>
    /// Record price comparison for a shopping list item
    /// </summary>
    [HttpPost("items/{itemId}/prices")]
    public async Task<IActionResult> RecordPrice(Guid itemId, [FromBody] RecordPriceRequest request)
    {
        var comparisonId = await _repository.RecordPriceComparisonAsync(
            itemId,
            request.ProductId,
            request.StoreId,
            request.Price,
            request.UnitPrice,
            request.Size,
            request.Unit,
            request.HasDeal,
            request.DealType,
            request.DealEndDate
        );

        // Update best price for the item
        await _repository.UpdateBestPriceForItemAsync(itemId);

        _logger.LogInformation("Recorded price comparison {ComparisonId} for item {ItemId}", 
            comparisonId, itemId);
        
        return Ok(new { id = comparisonId });
    }

    /// <summary>
    /// Get price comparisons for a shopping list item
    /// </summary>
    [HttpGet("items/{itemId}/prices")]
    public async Task<IActionResult> GetPriceComparisons(Guid itemId)
    {
        var comparisons = await _repository.GetPriceComparisonsAsync(itemId);
        return Ok(comparisons);
    }

    /// <summary>
    /// Get best prices for a product across stores
    /// </summary>
    [HttpGet("products/{productId}/best-prices")]
    public async Task<IActionResult> GetBestPrices(Guid productId, [FromQuery] Guid? preferredStoreId = null)
    {
        var prices = await _repository.GetBestPricesAsync(productId, preferredStoreId);
        return Ok(prices);
    }
}

