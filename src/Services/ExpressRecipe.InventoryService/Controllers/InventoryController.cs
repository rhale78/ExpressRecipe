using ExpressRecipe.InventoryService.Logging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ExpressRecipe.InventoryService.Data;

namespace ExpressRecipe.InventoryService.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class InventoryController : ControllerBase
{
    private readonly ILogger<InventoryController> _logger;
    private readonly IInventoryRepository _repository;
    private readonly IInventorySaleRepository? _saleRepository;
    private readonly IConfiguration? _configuration;

    public InventoryController(ILogger<InventoryController> logger, IInventoryRepository repository,
        IInventorySaleRepository? saleRepository = null, IConfiguration? configuration = null)
    {
        _logger         = logger;
        _repository     = repository;
        _saleRepository = saleRepository;
        _configuration  = configuration;
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
            _logger.LogGettingInventory(userId.Value);
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
            _logger.LogInventoryItemAdded(userId.Value, itemId);
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
            _logger.LogInventoryItemDeleted(userId.Value, id);
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
            _logger.LogGettingExpiringItems(userId.Value, daysAhead);
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
            _logger.LogStorageLocationCreated(userId.Value, locationId);
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
            var userId = GetUserId();
            if (userId == null) return Unauthorized();
            await _repository.DeleteStorageLocationAsync(id);
            _logger.LogStorageLocationDeleted(userId.Value, id);
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
    /// Get frozen inventory items matching a recipe's ingredients (service-to-service, used by ThawTaskGeneratorService).
    /// Protected by an API key header (X-Internal-Api-Key) when InternalApi:Key is configured.
    /// </summary>
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    [HttpGet("frozen-for-recipe/{householdId}/{recipeId}")]
    public async Task<IActionResult> GetFrozenIngredientsForRecipe(
        Guid householdId, Guid recipeId, CancellationToken ct)
    {
        // Validate service-to-service API key when one is configured.
        string? configuredKey = _configuration?["InternalApi:Key"];
        if (!string.IsNullOrEmpty(configuredKey))
        {
            string? providedKey = Request.Headers["X-Internal-Api-Key"].FirstOrDefault();
            if (!IsValidApiKey(providedKey, configuredKey))
            {
                return Unauthorized(new { error = "Invalid or missing X-Internal-Api-Key header" });
            }
        }

        try
        {
            _logger.LogInformation(
                "Getting frozen ingredients for household {HouseholdId} recipe {RecipeId}",
                householdId, recipeId);
            List<FrozenIngredientResult> items =
                await _repository.GetFrozenIngredientsForRecipeAsync(householdId, recipeId, ct);
            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting frozen ingredients for recipe {RecipeId}", recipeId);
            return StatusCode(500, new { message = "An error occurred while retrieving frozen ingredients" });
        }
    }

    // Constant-time comparison to guard against timing attacks when comparing API keys.
    private static bool IsValidApiKey(string? provided, string configured)
    {
        if (provided is null) { return false; }
        byte[] a = Encoding.UTF8.GetBytes(provided);
        byte[] b = Encoding.UTF8.GetBytes(configured);
        if (a.Length != b.Length)
        {
            byte[] padded = new byte[Math.Max(a.Length, b.Length)];
            Buffer.BlockCopy(a.Length < b.Length ? a : b, 0, padded, 0, Math.Min(a.Length, b.Length));
            if (a.Length < b.Length) { a = padded; } else { b = padded; }
        }
        return CryptographicOperations.FixedTimeEquals(a, b);
    }

    /// <summary>
    /// Get products safely consumed by this household in the last 180 days (service-to-service).
    /// Used by the allergy differential analysis engine.
    /// </summary>
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    [HttpGet("safe-product-history/{householdId}")]
    public async Task<IActionResult> GetSafeProductHistory(
        Guid householdId, [FromQuery] int minCount = 3, CancellationToken ct = default)
    {
        // Validate service-to-service API key when one is configured.
        string? configuredKey = _configuration?["InternalApi:Key"];
        if (!string.IsNullOrEmpty(configuredKey))
        {
            string? providedKey = Request.Headers["X-Internal-Api-Key"].FirstOrDefault();
            if (!IsValidApiKey(providedKey, configuredKey))
            {
                return Unauthorized(new { error = "Invalid or missing X-Internal-Api-Key header" });
            }
        }

        try
        {
            List<SafeProductUsageResult> results =
                await _repository.GetSafeProductHistoryAsync(householdId, minCount, ct);
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error fetching safe product history for household {HouseholdId}", householdId);
            return StatusCode(500, new { message = "An error occurred" });
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
            Guid? userId = GetUserId();
            if (userId == null) return Unauthorized();
            _logger.LogInformation("Getting usage history for item {ItemId}", id);
            List<InventoryHistoryDto> history = await _repository.GetUsageHistoryAsync(id, limit);
            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving usage history for item {ItemId}", id);
            return StatusCode(500, new { message = "An error occurred while retrieving usage history" });
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  PURCHASE EVENTS
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Record a purchase event (called by ShoppingService on list complete)
    /// </summary>
    [HttpPost("purchase-event")]
    public async Task<IActionResult> RecordPurchaseEvent([FromBody] RecordPurchaseEventRequest request)
    {
        try
        {
            Guid? userId = GetUserId();
            if (userId == null) return Unauthorized();
            _logger.LogInformation("Recording purchase event for user {UserId}", userId);
            PurchaseEventRecord record = new PurchaseEventRecord
            {
                UserId = userId.Value,
                HouseholdId = request.HouseholdId,
                ProductId = request.ProductId,
                IngredientId = request.IngredientId,
                CustomName = request.CustomName,
                Barcode = request.Barcode,
                Quantity = request.Quantity,
                Unit = request.Unit,
                Price = request.Price,
                StoreId = request.StoreId,
                StoreName = request.StoreName,
                PurchasedAt = request.PurchasedAt ?? DateTime.UtcNow,
                Source = request.Source ?? "ManualAdd"
            };
            Guid eventId = await _repository.RecordPurchaseEventAsync(record);
            _logger.LogPurchaseEventRecorded(userId.Value, record.ProductId, record.HouseholdId);
            return Ok(new { id = eventId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording purchase event");
            return StatusCode(500, new { message = "An error occurred while recording the purchase event" });
        }
    }

    /// <summary>
    /// Add a long-term storage item (canned, freeze-dried, frozen, etc.)
    /// </summary>
    [HttpPost("long-term")]
    public async Task<IActionResult> AddLongTermItem([FromBody] AddLongTermStorageRequest request, CancellationToken ct)
    {
        try
        {
            Guid? userId = GetUserId();
            if (userId == null) { return Unauthorized(); }
            Guid? householdId = GetHouseholdId();
            if (!householdId.HasValue) { return Unauthorized(); }
            Guid id = await _repository.AddItemAsync(new AddItemRequest
            {
                UserId              = userId.Value,
                HouseholdId         = householdId,
                Name                = request.Name,
                Quantity            = request.Quantity,
                Unit                = request.Unit,
                StorageMethod       = request.StorageMethod,
                IsLongTermStorage   = true,
                BatchLabel          = request.BatchLabel,
                StorageCapacityUnit = request.StorageCapacityUnit,
                ExpirationDate      = request.ExpirationDate ?? ComputeExpiration(request.StorageMethod, request.ProcessDate),
                Temperature         = request.StorageMethod == "FrozenMeal" ? "Frozen" : "Room",
                Source              = request.Source ?? "Store"
            }, ct);
            return Ok(new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding long-term storage item");
            return StatusCode(500, new { message = "An error occurred while adding the long-term storage item" });
        }
    }

    /// <summary>
    /// Get purchase history for user
    /// </summary>
    [HttpGet("purchase-history")]
    public async Task<IActionResult> GetPurchaseHistory([FromQuery] Guid? productId = null, [FromQuery] int daysBack = 90)
    {
        try
        {
            Guid? userId = GetUserId();
            if (userId == null) return Unauthorized();
            List<PurchaseEventDto> events = await _repository.GetPurchaseHistoryAsync(userId.Value, productId, daysBack);
            return Ok(events);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving purchase history");
            return StatusCode(500, new { message = "An error occurred while retrieving purchase history" });
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  CONSUMPTION PATTERNS
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Get consumption patterns for user
    /// </summary>
    [HttpGet("patterns")]
    public async Task<IActionResult> GetConsumptionPatterns()
    {
        try
        {
            Guid? userId = GetUserId();
            if (userId == null) return Unauthorized();
            _logger.LogGettingPatterns(userId.Value, null);
            List<ProductConsumptionPatternDto> patterns = await _repository.GetConsumptionPatternsAsync(userId.Value);
            return Ok(patterns);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving consumption patterns");
            return StatusCode(500, new { message = "An error occurred while retrieving consumption patterns" });
        }
    }

    /// <summary>
    /// Get abandoned products
    /// </summary>
    [HttpGet("patterns/abandoned")]
    public async Task<IActionResult> GetAbandonedProducts()
    {
        try
        {
            Guid? userId = GetUserId();
            if (userId == null) return Unauthorized();
            _logger.LogGettingAbandonedProducts(userId.Value, null);
            List<ProductConsumptionPatternDto> abandoned = await _repository.GetAbandonedProductsAsync(userId.Value);
            return Ok(abandoned);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving abandoned products");
            return StatusCode(500, new { message = "An error occurred while retrieving abandoned products" });
        }
    }

    /// <summary>
    /// Get low stock items predicted by consumption patterns
    /// </summary>
    [HttpGet("patterns/low-stock")]
    public async Task<IActionResult> GetLowStockByPrediction([FromQuery] int daysAhead = 3)
    {
        try
        {
            Guid? userId = GetUserId();
            if (userId == null) return Unauthorized();
            _logger.LogGettingLowStockPredictions(userId.Value, null);
            List<ProductConsumptionPatternDto> lowStock = await _repository.GetLowStockByPredictionAsync(userId.Value, daysAhead);
            return Ok(lowStock);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving low stock predictions");
            return StatusCode(500, new { message = "An error occurred while retrieving low stock predictions" });
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  PRICE WATCH
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Get active price watch alerts for user
    /// </summary>
    [HttpGet("price-watch")]
    public async Task<IActionResult> GetPriceWatchAlerts()
    {
        try
        {
            Guid? userId = GetUserId();
            if (userId == null) return Unauthorized();
            _logger.LogGettingPriceWatchAlerts(userId.Value, null);
            List<PriceWatchAlertDto> alerts = await _repository.GetActiveWatchAlertsByUserAsync(userId.Value);
            return Ok(alerts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving price watch alerts");
            return StatusCode(500, new { message = "An error occurred while retrieving price watch alerts" });
        }
    }

    /// <summary>
    /// Set target price on a price watch alert
    /// </summary>
    [HttpPost("price-watch/{id}/target")]
    public async Task<IActionResult> SetPriceWatchTarget(Guid id, [FromBody] SetTargetPriceRequest request)
    {
        try
        {
            Guid? userId = GetUserId();
            if (userId == null) return Unauthorized();
            await _repository.SetPriceWatchTargetPriceAsync(userId.Value, id, request.TargetPrice);
            _logger.LogPriceWatchAlertSet(userId.Value, id, request.TargetPrice);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting target price for alert {AlertId}", id);
            return StatusCode(500, new { message = "An error occurred while setting the target price" });
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  ABANDONED PRODUCT INQUIRY
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Respond to an abandoned product inquiry
    /// </summary>
    [HttpPost("inquiry/{id}/respond")]
    public async Task<IActionResult> RespondToInquiry(Guid id, [FromBody] InquiryResponseRequest request)
    {
        try
        {
            Guid? userId = GetUserId();
            if (userId == null) return Unauthorized();
            await _repository.RecordInquiryResponseAsync(userId.Value, id, request.Response, request.Note);
            _logger.LogInquiryResponseRecorded(userId.Value, id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording inquiry response for {InquiryId}", id);
            return StatusCode(500, new { message = "An error occurred while recording the inquiry response" });
        }
    }

    /// <summary>
    /// Get pending abandoned product inquiries for user
    /// </summary>
    [HttpGet("inquiry/pending")]
    public async Task<IActionResult> GetPendingInquiries()
    {
        try
        {
            Guid? userId = GetUserId();
            if (userId == null) return Unauthorized();
            _logger.LogGettingPendingInquiries(userId.Value);
            List<AbandonedProductInquiryDto> inquiries = await _repository.GetPendingInquiriesAsync(userId.Value);
            return Ok(inquiries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pending inquiries");
            return StatusCode(500, new { message = "An error occurred while retrieving pending inquiries" });
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  WASTE REPORT
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Get waste analytics grouped by month
    /// </summary>
    [HttpGet("waste-report")]
    public async Task<IActionResult> GetWasteReport([FromQuery] Guid? householdId = null)
    {
        try
        {
            Guid? userId = GetUserId();
            if (userId == null) return Unauthorized();
            _logger.LogGettingWasteReport(userId.Value, householdId);
            List<WasteReportMonthDto> report = await _repository.GetWasteReportAsync(userId.Value, householdId);
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving waste report");
            return StatusCode(500, new { message = "An error occurred while retrieving waste report" });
        }
    }

    /// <summary>
    /// Get long-term storage items for the household, optionally filtered by storage method
    /// </summary>
    [HttpGet("long-term")]
    public async Task<IActionResult> GetLongTermItems([FromQuery] string? storageMethod, CancellationToken ct)
    {
        try
        {
            Guid? householdId = GetHouseholdId();
            if (!householdId.HasValue) return Unauthorized();
            return Ok(await _repository.GetItemsAsync(householdId.Value, isLongTermOnly: true, storageMethod: storageMethod, ct: ct));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving long-term storage items");
            return StatusCode(500, new { message = "An error occurred while retrieving long-term storage items" });
        }
    }

    private static Guid? ExtractHouseholdId(System.Security.Claims.ClaimsPrincipal user)
    {
        string? value = user.FindFirstValue("household_id");
        return Guid.TryParse(value, out Guid id) ? id : null;
    }

    private Guid? GetHouseholdId() => ExtractHouseholdId(User);

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
            Guid? userId = GetUserId();
            if (userId == null) { return Unauthorized(); }

            InventoryItemDto? item = await _repository.GetInventoryItemAsync(id, userId.Value);
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

            if (_saleRepository is null)
                return StatusCode(503, new { message = "Sales service is not available." });

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
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("Insufficient quantity"))
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
            Guid? userId = GetUserId();
            if (userId == null) { return Unauthorized(); }

            if (!await _repository.IsUserMemberOfHouseholdAsync(householdId, userId.Value))
            {
                return Forbid();
            }

            if (_saleRepository is null)
                return StatusCode(503, new { message = "Sales service is not available." });

            if (itemId.HasValue)
            {
                List<InventorySaleDto> salesByItem = await _saleRepository.GetSalesByItemAsync(householdId, itemId.Value);
                return Ok(salesByItem);
            }

            DateOnly effectiveFrom = from ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
            DateOnly effectiveTo   = to   ?? DateOnly.FromDateTime(DateTime.UtcNow);

            List<InventorySaleDto> sales = await _saleRepository.GetSalesAsync(householdId, effectiveFrom, effectiveTo);
            return Ok(sales);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving sales for household {HouseholdId}", householdId);
            return StatusCode(500, new { message = "An error occurred while retrieving sales." });
        }
    }

    private static DateTime ComputeExpiration(string? storageMethod, DateTime? processDate)
    {
        DateTime from = processDate ?? DateTime.UtcNow.Date;
        return storageMethod switch
        {
            "Canned"         => from.AddMonths(18),
            "CannedPressure" => from.AddMonths(24),
            "FreezeDried"    => from.AddYears(25),
            "Dehydrated"     => from.AddMonths(12),
            "FrozenMeal"     => from.AddMonths(6),
            "Pickled"        => from.AddMonths(12),
            _                => from.AddMonths(12)
        };
    }
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

public class RecordPurchaseEventRequest
{
    public Guid? HouseholdId { get; set; }
    public Guid? ProductId { get; set; }
    public Guid? IngredientId { get; set; }
    public string? CustomName { get; set; }
    public string? Barcode { get; set; }
    public decimal Quantity { get; set; } = 1;
    public string? Unit { get; set; }
    public decimal? Price { get; set; }
    public Guid? StoreId { get; set; }
    public string? StoreName { get; set; }
    public DateTime? PurchasedAt { get; set; }
    public string? Source { get; set; }
}

public class SetTargetPriceRequest
{
    public decimal TargetPrice { get; set; }
}

public class InquiryResponseRequest
{
    public string Response { get; set; } = string.Empty;
    public string? Note { get; set; }
}

public class AddLongTermStorageRequest
{
    public string Name { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string? Unit { get; set; }
    public string? StorageMethod { get; set; }
    public string? BatchLabel { get; set; }
    public string? StorageCapacityUnit { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public DateTime? ProcessDate { get; set; }
    public string? Source { get; set; }
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
