using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ExpressRecipe.InventoryService.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class InventoryController : ControllerBase
{
    private readonly ILogger<InventoryController> _logger;
    private readonly IInventoryRepository _repository;

    public InventoryController(ILogger<InventoryController> logger, IInventoryRepository repository)
    {
        _logger = logger;
        _repository = repository;
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// Get all inventory items for the authenticated user
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetInventory()
    {
        var userId = GetUserId();
        _logger.LogInformation("Getting inventory for user {UserId}", userId);

        var items = await _repository.GetUserInventoryAsync(userId);
        return Ok(items);
    }

    /// <summary>
    /// Add item to inventory
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> AddItem([FromBody] AddInventoryItemRequest request)
    {
        var userId = GetUserId();
        _logger.LogInformation("Adding inventory item for user {UserId}", userId);

        var itemId = await _repository.AddInventoryItemAsync(
            userId,
            request.ProductId,
            request.CustomName,
            request.StorageLocationId,
            request.Quantity,
            request.Unit,
            request.ExpirationDate,
            request.Barcode,
            request.Price,
            request.Store);

        var item = await _repository.GetInventoryItemAsync(itemId, userId);
        return CreatedAtAction(nameof(GetItem), new { id = itemId }, item);
    }

    /// <summary>
    /// Get single inventory item
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetItem(Guid id)
    {
        var userId = GetUserId();
        _logger.LogInformation("Getting inventory item {ItemId} for user {UserId}", id, userId);

        var item = await _repository.GetInventoryItemAsync(id, userId);
        if (item == null)
            return NotFound();

        return Ok(item);
    }

    /// <summary>
    /// Update inventory item quantity
    /// </summary>
    [HttpPut("{id}/quantity")]
    public async Task<IActionResult> UpdateQuantity(Guid id, [FromBody] UpdateQuantityRequest request)
    {
        var userId = GetUserId();
        _logger.LogInformation("Updating quantity for item {ItemId}", id);

        await _repository.UpdateInventoryQuantityAsync(id, request.Quantity, request.ActionType, request.Reason);
        var item = await _repository.GetInventoryItemAsync(id, userId);
        return Ok(item);
    }

    /// <summary>
    /// Delete inventory item
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteItem(Guid id)
    {
        var userId = GetUserId();
        _logger.LogInformation("Deleting inventory item {ItemId}", id);

        await _repository.DeleteInventoryItemAsync(id, userId);
        return NoContent();
    }

    /// <summary>
    /// Get items expiring soon
    /// </summary>
    [HttpGet("expiring")]
    public async Task<IActionResult> GetExpiringItems([FromQuery] int daysAhead = 7)
    {
        var userId = GetUserId();
        _logger.LogInformation("Getting expiring items for user {UserId}", userId);

        var items = await _repository.GetExpiringItemsAsync(userId, daysAhead);
        return Ok(items);
    }

    /// <summary>
    /// Get storage locations
    /// </summary>
    [HttpGet("locations")]
    public async Task<IActionResult> GetStorageLocations()
    {
        var userId = GetUserId();
        _logger.LogInformation("Getting storage locations for user {UserId}", userId);

        var locations = await _repository.GetStorageLocationsAsync(userId);
        return Ok(locations);
    }

    /// <summary>
    /// Create storage location
    /// </summary>
    [HttpPost("locations")]
    public async Task<IActionResult> CreateStorageLocation([FromBody] CreateStorageLocationRequest request)
    {
        var userId = GetUserId();
        _logger.LogInformation("Creating storage location for user {UserId}", userId);

        var locationId = await _repository.CreateStorageLocationAsync(userId, request.Name, request.Description, request.Temperature);
        var locations = await _repository.GetStorageLocationsAsync(userId);
        return CreatedAtAction(nameof(GetStorageLocations), new { id = locationId }, locations.First(l => l.Id == locationId));
    }

    /// <summary>
    /// Get usage history for item
    /// </summary>
    [HttpGet("{id}/history")]
    public async Task<IActionResult> GetUsageHistory(Guid id, [FromQuery] int limit = 50)
    {
        var userId = GetUserId();
        _logger.LogInformation("Getting usage history for item {ItemId}", id);

        var history = await _repository.GetItemHistoryAsync(id, limit);
        return Ok(history);
    }
}

public class AddInventoryItemRequest
{
    public Guid? ProductId { get; set; }
    public string? CustomName { get; set; }
    public Guid StorageLocationId { get; set; }
    public decimal Quantity { get; set; }
    public string? Unit { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public string? Barcode { get; set; }
    public decimal? Price { get; set; }
    public string? Store { get; set; }
}

public class UpdateQuantityRequest
{
    public decimal Quantity { get; set; }
    public string ActionType { get; set; } = "Updated";
    public string? Reason { get; set; }
}

public class CreateStorageLocationRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Temperature { get; set; }
}
