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

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// Get all shopping lists for user
    /// </summary>
    [HttpGet("lists")]
    public async Task<IActionResult> GetLists()
    {
        var userId = GetUserId();
        var lists = await _repository.GetUserListsAsync(userId);
        return Ok(lists);
    }

    /// <summary>
    /// Create new shopping list
    /// </summary>
    [HttpPost("lists")]
    public async Task<IActionResult> CreateList([FromBody] CreateListRequest request)
    {
        var userId = GetUserId();
        var listId = await _repository.CreateShoppingListAsync(userId, request.Name, request.Description);
        var list = await _repository.GetShoppingListAsync(listId, userId);
        return CreatedAtAction(nameof(GetList), new { id = listId }, list);
    }

    /// <summary>
    /// Get shopping list by ID
    /// </summary>
    [HttpGet("lists/{id}")]
    public async Task<IActionResult> GetList(Guid id)
    {
        var userId = GetUserId();
        var list = await _repository.GetShoppingListAsync(id, userId);
        if (list == null)
            return NotFound();

        return Ok(list);
    }

    /// <summary>
    /// Update shopping list
    /// </summary>
    [HttpPut("lists/{id}")]
    public async Task<IActionResult> UpdateList(Guid id, [FromBody] UpdateListRequest request)
    {
        await _repository.UpdateShoppingListAsync(id, request.Name, request.Description);
        return NoContent();
    }

    /// <summary>
    /// Delete shopping list
    /// </summary>
    [HttpDelete("lists/{id}")]
    public async Task<IActionResult> DeleteList(Guid id)
    {
        var userId = GetUserId();
        await _repository.DeleteShoppingListAsync(id, userId);
        return NoContent();
    }

    /// <summary>
    /// Get items in shopping list
    /// </summary>
    [HttpGet("lists/{id}/items")]
    public async Task<IActionResult> GetListItems(Guid id)
    {
        var userId = GetUserId();
        var items = await _repository.GetListItemsAsync(id, userId);
        return Ok(items);
    }

    /// <summary>
    /// Add item to shopping list
    /// </summary>
    [HttpPost("lists/{id}/items")]
    public async Task<IActionResult> AddItem(Guid id, [FromBody] AddItemRequest request)
    {
        var userId = GetUserId();
        var itemId = await _repository.AddItemToListAsync(
            id, userId, request.ProductId, request.CustomName, request.Quantity, request.Unit, request.Category);

        return Ok(new { id = itemId });
    }

    /// <summary>
    /// Update item quantity
    /// </summary>
    [HttpPut("items/{id}/quantity")]
    public async Task<IActionResult> UpdateQuantity(Guid id, [FromBody] UpdateQuantityRequest request)
    {
        await _repository.UpdateItemQuantityAsync(id, request.Quantity);
        return NoContent();
    }

    /// <summary>
    /// Toggle item checked status
    /// </summary>
    [HttpPut("items/{id}/toggle")]
    public async Task<IActionResult> ToggleChecked(Guid id)
    {
        await _repository.ToggleItemCheckedAsync(id);
        return NoContent();
    }

    /// <summary>
    /// Remove item from list
    /// </summary>
    [HttpDelete("items/{id}")]
    public async Task<IActionResult> RemoveItem(Guid id)
    {
        await _repository.RemoveItemFromListAsync(id);
        return NoContent();
    }

    /// <summary>
    /// Share list with another user
    /// </summary>
    [HttpPost("lists/{id}/share")]
    public async Task<IActionResult> ShareList(Guid id, [FromBody] ShareListRequest request)
    {
        var userId = GetUserId();
        var shareId = await _repository.ShareListAsync(id, userId, request.SharedWithUserId, request.Permission);
        return Ok(new { id = shareId });
    }

    /// <summary>
    /// Get shared lists
    /// </summary>
    [HttpGet("shared")]
    public async Task<IActionResult> GetSharedLists()
    {
        var userId = GetUserId();
        var lists = await _repository.GetSharedListsAsync(userId);
        return Ok(lists);
    }

    /// <summary>
    /// Get stores
    /// </summary>
    [HttpGet("stores")]
    public async Task<IActionResult> GetStores()
    {
        var userId = GetUserId();
        var stores = await _repository.GetUserStoresAsync(userId);
        return Ok(stores);
    }

    /// <summary>
    /// Create store layout
    /// </summary>
    [HttpPost("stores")]
    public async Task<IActionResult> CreateStore([FromBody] CreateStoreRequest request)
    {
        var userId = GetUserId();
        var storeId = await _repository.CreateStoreLayoutAsync(userId, request.StoreName, request.Address);
        return Ok(new { id = storeId });
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
