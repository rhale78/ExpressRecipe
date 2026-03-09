using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ExpressRecipe.InventoryService.Data;

namespace ExpressRecipe.InventoryService.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class InventoryController : ControllerBase
{
    private readonly ILogger<InventoryController> _logger;
    private readonly IInventoryRepository _repository;
    private readonly IInventorySaleRepository _saleRepository;

    public InventoryController(
        ILogger<InventoryController> logger,
        IInventoryRepository repository,
        IInventorySaleRepository saleRepository)
    {
        _logger = logger;
        _repository = repository;
        _saleRepository = saleRepository;
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    /// <summary>
    /// Get all inventory items for the authenticated user
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetInventory()
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();
            _logger.LogInformation("Getting inventory for user {UserId}", userId);
            var items = await _repository.GetUserInventoryAsync(userId.Value);
            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving inventory");
            return StatusCode(500, new { message = "An error occurred while retrieving inventory" });
        }
    }

    /// <summary>
    /// Add item to inventory
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> AddItem([FromBody] AddInventoryItemRequest request)
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();
            _logger.LogInformation("Adding inventory item for user {UserId}", userId);
            var itemId = await _repository.AddInventoryItemAsync(
                userId.Value,
                request.HouseholdId,
                request.ProductId,
                request.CustomName,
                request.StorageLocationId,
                request.Quantity,
                request.Unit,
                request.ExpirationDate,
                request.Barcode,
                request.Price,
                request.PreferredStore,
                request.StoreLocation);
            var item = await _repository.GetInventoryItemAsync(itemId, userId.Value);
            return CreatedAtAction(nameof(GetItem), new { id = itemId }, item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding inventory item");
            return StatusCode(500, new { message = "An error occurred while adding the inventory item" });
        }
    }

    /// <summary>
    /// Get single inventory item
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetItem(Guid id)
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();
            _logger.LogInformation("Getting inventory item {ItemId} for user {UserId}", id, userId);
            var item = await _repository.GetInventoryItemAsync(id, userId.Value);
            if (item == null)
                return NotFound();
            return Ok(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving inventory item {ItemId}", id);
            return StatusCode(500, new { message = "An error occurred while retrieving the inventory item" });
        }
    }

    /// <summary>
    /// Update inventory item quantity
    /// </summary>
    [HttpPut("{id}/quantity")]
    public async Task<IActionResult> UpdateQuantity(Guid id, [FromBody] UpdateQuantityRequest request)
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();
            _logger.LogInformation("Updating quantity for item {ItemId}", id);
            await _repository.UpdateInventoryQuantityAsync(id, request.Quantity, request.ActionType, userId.Value, request.Reason, request.DisposalReason, request.AllergenDetected);
            var item = await _repository.GetInventoryItemAsync(id, userId.Value);
            return Ok(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating quantity for inventory item {ItemId}", id);
            return StatusCode(500, new { message = "An error occurred while updating the item quantity" });
        }
    }

    /// <summary>
    /// Delete inventory item
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteItem(Guid id)
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();
            _logger.LogInformation("Deleting inventory item {ItemId}", id);
            await _repository.DeleteInventoryItemAsync(id, userId.Value);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting inventory item {ItemId}", id);
            return StatusCode(500, new { message = "An error occurred while deleting the inventory item" });
        }
    }

    /// <summary>
    /// Get items expiring soon
    /// </summary>
    [HttpGet("expiring")]
    public async Task<IActionResult> GetExpiringItems([FromQuery] int daysAhead = 7)
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();
            _logger.LogInformation("Getting expiring items for user {UserId}", userId);
            var items = await _repository.GetExpiringItemsAsync(userId.Value, daysAhead);
            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving expiring items");
            return StatusCode(500, new { message = "An error occurred while retrieving expiring items" });
        }
    }

    /// <summary>
    /// Get storage locations
    /// </summary>
    [HttpGet("locations")]
    public async Task<IActionResult> GetStorageLocations()
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();
            _logger.LogInformation("Getting storage locations for user {UserId}", userId);
            var locations = await _repository.GetStorageLocationsAsync(userId.Value);
            return Ok(locations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving storage locations");
            return StatusCode(500, new { message = "An error occurred while retrieving storage locations" });
        }
    }

    /// <summary>
    /// Create storage location
    /// </summary>
    [HttpPost("locations")]
    public async Task<IActionResult> CreateStorageLocation([FromBody] CreateStorageLocationRequest request)
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();
            _logger.LogInformation("Creating storage location for user {UserId}", userId);
            var locationId = await _repository.CreateStorageLocationAsync(userId.Value, request.HouseholdId, request.AddressId, request.Name, request.Description, request.Temperature);
            var locations = await _repository.GetStorageLocationsAsync(userId.Value);
            return CreatedAtAction(nameof(GetStorageLocations), new { id = locationId }, locations.First(l => l.Id == locationId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating storage location");
            return StatusCode(500, new { message = "An error occurred while creating the storage location" });
        }
    }

    /// <summary>
    /// Get storage locations by address
    /// </summary>
    [HttpGet("addresses/{addressId}/locations")]
    public async Task<IActionResult> GetLocationsByAddress(Guid addressId)
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();
            _logger.LogInformation("Getting storage locations for address {AddressId}", addressId);
            var address = await _repository.GetAddressByIdAsync(addressId);
            if (address == null) return NotFound();
            if (!await _repository.IsUserMemberOfHouseholdAsync(address.HouseholdId, userId.Value))
                return Forbid();
            var locations = await _repository.GetStorageLocationsByAddressAsync(addressId);
            return Ok(locations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving storage locations for address {AddressId}", addressId);
            return StatusCode(500, new { message = "An error occurred while retrieving storage locations" });
        }
    }

    /// <summary>
    /// Get storage locations by household
    /// </summary>
    [HttpGet("households/{householdId}/locations")]
    public async Task<IActionResult> GetLocationsByHousehold(Guid householdId)
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();
            _logger.LogInformation("Getting storage locations for household {HouseholdId}", householdId);
            if (!await _repository.IsUserMemberOfHouseholdAsync(householdId, userId.Value))
                return Forbid();
            var locations = await _repository.GetStorageLocationsByHouseholdAsync(householdId);
            return Ok(locations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving storage locations for household {HouseholdId}", householdId);
            return StatusCode(500, new { message = "An error occurred while retrieving storage locations" });
        }
    }

    /// <summary>
    /// Update storage location
    /// </summary>
    [HttpPut("locations/{id}")]
    public async Task<IActionResult> UpdateStorageLocation(Guid id, [FromBody] UpdateStorageLocationRequest request)
    {
        try
        {
            await _repository.UpdateStorageLocationAsync(id, request.Name, request.Description, request.Temperature, request.AddressId);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating storage location {LocationId}", id);
            return StatusCode(500, new { message = "An error occurred while updating the storage location" });
        }
    }

    /// <summary>
    /// Delete storage location
    /// </summary>
    [HttpDelete("locations/{id}")]
    public async Task<IActionResult> DeleteStorageLocation(Guid id)
    {
        try
        {
            await _repository.DeleteStorageLocationAsync(id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting storage location {LocationId}", id);
            return StatusCode(500, new { message = "An error occurred while deleting the storage location" });
        }
    }

    /// <summary>
    /// Get household inventory
    /// </summary>
    [HttpGet("households/{householdId}")]
    public async Task<IActionResult> GetHouseholdInventory(Guid householdId)
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();
            _logger.LogInformation("Getting inventory for household {HouseholdId}", householdId);
            if (!await _repository.IsUserMemberOfHouseholdAsync(householdId, userId.Value))
                return Forbid();
            var items = await _repository.GetHouseholdInventoryAsync(householdId);
            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving inventory for household {HouseholdId}", householdId);
            return StatusCode(500, new { message = "An error occurred while retrieving household inventory" });
        }
    }

    /// <summary>
    /// Get inventory by address
    /// </summary>
    [HttpGet("addresses/{addressId}")]
    public async Task<IActionResult> GetInventoryByAddress(Guid addressId)
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();
            _logger.LogInformation("Getting inventory for address {AddressId}", addressId);
            var address = await _repository.GetAddressByIdAsync(addressId);
            if (address == null) return NotFound();
            if (!await _repository.IsUserMemberOfHouseholdAsync(address.HouseholdId, userId.Value))
                return Forbid();
            var items = await _repository.GetInventoryByAddressAsync(addressId);
            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving inventory for address {AddressId}", addressId);
            return StatusCode(500, new { message = "An error occurred while retrieving inventory by address" });
        }
    }

    /// <summary>
    /// Get inventory by storage location
    /// </summary>
    [HttpGet("locations/{locationId}/items")]
    public async Task<IActionResult> GetInventoryByLocation(Guid locationId)
    {
        try
        {
            _logger.LogInformation("Getting inventory for location {LocationId}", locationId);
            var items = await _repository.GetInventoryByStorageLocationAsync(locationId);
            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving inventory for location {LocationId}", locationId);
            return StatusCode(500, new { message = "An error occurred while retrieving inventory by location" });
        }
    }

    /// <summary>
    /// Get low stock items
    /// </summary>
    [HttpGet("low-stock")]
    public async Task<IActionResult> GetLowStockItems([FromQuery] decimal threshold = 2.0m)
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();
            _logger.LogInformation("Getting low stock items for user {UserId}", userId);
            var items = await _repository.GetLowStockItemsAsync(userId.Value, threshold);
            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving low stock items");
            return StatusCode(500, new { message = "An error occurred while retrieving low stock items" });
        }
    }

    /// <summary>
    /// Get items running out within specified days
    /// </summary>
    [HttpGet("running-out")]
    public async Task<IActionResult> GetItemsRunningOut([FromQuery] int withinDays = 7)
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();
            _logger.LogInformation("Getting items running out for user {UserId} within {Days} days", userId, withinDays);
            var items = await _repository.GetItemsRunningOutAsync(userId.Value, withinDays);
            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving items running out");
            return StatusCode(500, new { message = "An error occurred while retrieving items running out" });
        }
    }

    /// <summary>
    /// Get items about to expire
    /// </summary>
    [HttpGet("about-to-expire")]
    public async Task<IActionResult> GetItemsAboutToExpire([FromQuery] int daysAhead = 3)
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();
            _logger.LogInformation("Getting items about to expire for user {UserId} in {Days} days", userId, daysAhead);
            var items = await _repository.GetItemsAboutToExpireAsync(userId.Value, daysAhead);
            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving items about to expire");
            return StatusCode(500, new { message = "An error occurred while retrieving items about to expire" });
        }
    }

    /// <summary>
    /// Get inventory report with statistics
    /// </summary>
    [HttpGet("report")]
    public async Task<IActionResult> GetInventoryReport([FromQuery] Guid? householdId = null)
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();
            _logger.LogInformation("Getting inventory report for user {UserId}", userId);
            var report = await _repository.GetInventoryReportAsync(userId.Value, householdId);
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving inventory report");
            return StatusCode(500, new { message = "An error occurred while retrieving the inventory report" });
        }
    }

    /// <summary>
    /// Get usage history for item
    /// </summary>
    [HttpGet("{id}/history")]
    public async Task<IActionResult> GetUsageHistory(Guid id, [FromQuery] int limit = 50)
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();
            _logger.LogInformation("Getting usage history for item {ItemId}", id);
            var history = await _repository.GetUsageHistoryAsync(id, limit);
            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving usage history for item {ItemId}", id);
            return StatusCode(500, new { message = "An error occurred while retrieving usage history" });
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Sales
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Record a sale of an inventory item and deduct the quantity.
    /// Returns 422 if the item has insufficient quantity.
    /// </summary>
    [HttpPost("{id}/sell")]
    public async Task<IActionResult> SellItem(Guid id, [FromBody] SellItemRequest request)
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) { return Unauthorized(); }

            var item = await _repository.GetInventoryItemAsync(id, userId.Value);
            if (item == null) { return NotFound(); }

            Guid householdId = item.HouseholdId ?? Guid.Empty;
            if (householdId == Guid.Empty)
            {
                return UnprocessableEntity(new { message = "Item is not associated with a household." });
            }

            if (!await _repository.IsUserMemberOfHouseholdAsync(householdId, userId.Value))
            {
                return Forbid();
            }

            _logger.LogInformation(
                "Recording sale of {Quantity} {Unit} from item {ItemId}", request.Quantity, request.Unit, id);

            Guid saleId = await _saleRepository.RecordSaleAsync(
                householdId,
                id,
                item.ProductName ?? item.CustomName ?? "Unknown",
                request.Quantity,
                request.Unit,
                request.SaleDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
                request.Buyer,
                request.Notes,
                request.AutoRemoveOnZero);

            return Ok(new { id = saleId });
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("Insufficient quantity") || ex.Message.StartsWith("Insufficient quantity or item not found"))
        {
            return UnprocessableEntity(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Inventory item {ItemId} not found during sell", id);
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording sale for item {ItemId}", id);
            return StatusCode(500, new { message = "An error occurred while recording the sale." });
        }
    }

    /// <summary>
    /// Get sales history for a household, with optional date range and item filters.
    /// </summary>
    [HttpGet("sales")]
    public async Task<IActionResult> GetSales(
        [FromQuery] Guid householdId,
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        [FromQuery] Guid? itemId = null)
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) { return Unauthorized(); }

            if (!await _repository.IsUserMemberOfHouseholdAsync(householdId, userId.Value))
            {
                return Forbid();
            }

            if (itemId.HasValue)
            {
                var salesByItem = await _saleRepository.GetSalesByItemAsync(householdId, itemId.Value);
                return Ok(salesByItem);
            }

            DateOnly effectiveFrom = from ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
            DateOnly effectiveTo   = to   ?? DateOnly.FromDateTime(DateTime.UtcNow);

            var sales = await _saleRepository.GetSalesAsync(householdId, effectiveFrom, effectiveTo);
            return Ok(sales);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving sales for household {HouseholdId}", householdId);
            return StatusCode(500, new { message = "An error occurred while retrieving sales." });
        }
    }
}

public class SellItemRequest
{
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public DateOnly? SaleDate { get; set; }
    public string? Buyer { get; set; }
    public string? Notes { get; set; }
    public bool AutoRemoveOnZero { get; set; } = true;
}

public class AddInventoryItemRequest
{
    public Guid? HouseholdId { get; set; }
    public Guid? ProductId { get; set; }
    public string? CustomName { get; set; }
    public Guid StorageLocationId { get; set; }
    public decimal Quantity { get; set; }
    public string? Unit { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public string? Barcode { get; set; }
    public decimal? Price { get; set; }
    public string? Store { get; set; }
    public string? PreferredStore { get; set; }
    public string? StoreLocation { get; set; }
}

public class UpdateQuantityRequest
{
    public decimal Quantity { get; set; }
    public string ActionType { get; set; } = "Updated";
    public string? Reason { get; set; }
    public string? DisposalReason { get; set; }
    public string? AllergenDetected { get; set; }
}

public class CreateStorageLocationRequest
{
    public Guid? HouseholdId { get; set; }
    public Guid? AddressId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Temperature { get; set; }
}

public class UpdateStorageLocationRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Temperature { get; set; }
    public Guid? AddressId { get; set; }
}
