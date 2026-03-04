using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ExpressRecipe.ShoppingService.Data;

namespace ExpressRecipe.ShoppingService.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ShoppingController : ControllerBase
{
    private readonly ILogger<ShoppingController> _logger;
    private readonly IShoppingRepository _repository;

    public ShoppingController(ILogger<ShoppingController> logger, IShoppingRepository repository)
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
    /// Get all shopping lists for user
    /// </summary>
    [HttpGet("lists")]
    public async Task<IActionResult> GetLists()
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();
            var lists = await _repository.GetUserListsAsync(userId.Value);
            return Ok(lists);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving shopping lists");
            return StatusCode(500, new { message = "An error occurred while retrieving shopping lists" });
        }
    }

    /// <summary>
    /// Create new shopping list
    /// </summary>
    [HttpPost("lists")]
    public async Task<IActionResult> CreateList([FromBody] CreateListRequest request)
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();
            var listId = await _repository.CreateShoppingListAsync(userId.Value, null, request.Name, request.Description);
            var list = await _repository.GetShoppingListAsync(listId, userId.Value);
            return CreatedAtAction(nameof(GetList), new { id = listId }, list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating shopping list");
            return StatusCode(500, new { message = "An error occurred while creating the shopping list" });
        }
    }

    /// <summary>
    /// Get shopping list by ID
    /// </summary>
    [HttpGet("lists/{id}")]
    public async Task<IActionResult> GetList(Guid id)
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();
            var list = await _repository.GetShoppingListAsync(id, userId.Value);
            if (list == null)
                return NotFound();
            return Ok(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving shopping list {ListId}", id);
            return StatusCode(500, new { message = "An error occurred while retrieving the shopping list" });
        }
    }

    /// <summary>
    /// Update shopping list
    /// </summary>
    [HttpPut("lists/{id}")]
    public async Task<IActionResult> UpdateList(Guid id, [FromBody] UpdateListRequest request)
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            // Verify ownership: only proceed if the list belongs to this user
            var existing = await _repository.GetShoppingListAsync(id, userId.Value);
            if (existing == null)
                return NotFound();

            await _repository.UpdateShoppingListAsync(id, request.Name, request.Description);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating shopping list {ListId}", id);
            return StatusCode(500, new { message = "An error occurred while updating the shopping list" });
        }
    }

    /// <summary>
    /// Delete shopping list
    /// </summary>
    [HttpDelete("lists/{id}")]
    public async Task<IActionResult> DeleteList(Guid id)
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();
            await _repository.DeleteShoppingListAsync(id, userId.Value);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting shopping list {ListId}", id);
            return StatusCode(500, new { message = "An error occurred while deleting the shopping list" });
        }
    }

    /// <summary>
    /// Get items in shopping list
    /// </summary>
    [HttpGet("lists/{id}/items")]
    public async Task<IActionResult> GetListItems(Guid id)
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();
            var items = await _repository.GetListItemsAsync(id, userId.Value);
            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving items for list {ListId}", id);
            return StatusCode(500, new { message = "An error occurred while retrieving list items" });
        }
    }

    /// <summary>
    /// Add item to shopping list
    /// </summary>
    [HttpPost("lists/{id}/items")]
    public async Task<IActionResult> AddItem(Guid id, [FromBody] AddItemRequest request)
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();
            var itemId = await _repository.AddItemToListAsync(
                id, userId.Value, request.ProductId, request.CustomName, request.Quantity, request.Unit, request.Category);
            return Ok(new { id = itemId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding item to list {ListId}", id);
            return StatusCode(500, new { message = "An error occurred while adding the item" });
        }
    }

    /// <summary>
    /// Update item quantity
    /// </summary>
    [HttpPut("items/{id}/quantity")]
    public async Task<IActionResult> UpdateQuantity(Guid id, [FromBody] UpdateQuantityRequest request)
    {
        try
        {
            await _repository.UpdateItemQuantityAsync(id, request.Quantity);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating quantity for item {ItemId}", id);
            return StatusCode(500, new { message = "An error occurred while updating the item quantity" });
        }
    }

    /// <summary>
    /// Toggle item checked status
    /// </summary>
    [HttpPut("items/{id}/toggle")]
    public async Task<IActionResult> ToggleChecked(Guid id)
    {
        try
        {
            await _repository.ToggleItemCheckedAsync(id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling checked status for item {ItemId}", id);
            return StatusCode(500, new { message = "An error occurred while updating the item" });
        }
    }

    /// <summary>
    /// Remove item from list
    /// </summary>
    [HttpDelete("items/{id}")]
    public async Task<IActionResult> RemoveItem(Guid id)
    {
        try
        {
            await _repository.RemoveItemFromListAsync(id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing item {ItemId}", id);
            return StatusCode(500, new { message = "An error occurred while removing the item" });
        }
    }

    /// <summary>
    /// Share list with another user
    /// </summary>
    [HttpPost("lists/{id}/share")]
    public async Task<IActionResult> ShareList(Guid id, [FromBody] ShareListRequest request)
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();
            var shareId = await _repository.ShareListAsync(id, userId.Value, request.SharedWithUserId, request.Permission);
            return Ok(new { id = shareId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sharing list {ListId}", id);
            return StatusCode(500, new { message = "An error occurred while sharing the list" });
        }
    }

    /// <summary>
    /// Get shared lists
    /// </summary>
    [HttpGet("shared")]
    public async Task<IActionResult> GetSharedLists()
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();
            var lists = await _repository.GetSharedListsAsync(userId.Value);
            return Ok(lists);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving shared lists");
            return StatusCode(500, new { message = "An error occurred while retrieving shared lists" });
        }
    }

    /// <summary>
    /// Get stores
    /// </summary>
    [HttpGet("stores")]
    public async Task<IActionResult> GetStores()
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();
            var stores = await _repository.GetUserStoresAsync(userId.Value);
            return Ok(stores);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving stores");
            return StatusCode(500, new { message = "An error occurred while retrieving stores" });
        }
    }

    /// <summary>
    /// Create store layout
    /// </summary>
    [HttpPost("stores")]
    public async Task<IActionResult> CreateStore([FromBody] CreateStoreRequest request)
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();
            var storeId = await _repository.CreateStoreLayoutAsync(userId.Value, Guid.Empty, request.StoreName, request.Address, 0);
            return Ok(new { id = storeId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating store");
            return StatusCode(500, new { message = "An error occurred while creating the store" });
        }
    }
}

public class CreateListRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class UpdateListRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class AddItemRequest
{
    public Guid? ProductId { get; set; }
    public string? CustomName { get; set; }
    public decimal Quantity { get; set; }
    public string? Unit { get; set; }
    public string? Category { get; set; }
}

public class UpdateQuantityRequest
{
    public decimal Quantity { get; set; }
}

public class ShareListRequest
{
    public Guid SharedWithUserId { get; set; }
    public string Permission { get; set; } = "View";
}

public class CreateStoreRequest
{
    public string StoreName { get; set; } = string.Empty;
    public string? Address { get; set; }
}
