using ExpressRecipe.Shared.DTOs.User;
using ExpressRecipe.UserService.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ExpressRecipe.UserService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ListsController : ControllerBase
{
    private readonly IReportsRepository _reportsRepository;
    private readonly ILogger<ListsController> _logger;

    public ListsController(
        IReportsRepository reportsRepository,
        ILogger<ListsController> logger)
    {
        _reportsRepository = reportsRepository;
        _logger = logger;
    }

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return null;
        }
        return userId;
    }

    #region Lists

    /// <summary>
    /// Get user's lists
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<UserListDto>>> GetLists([FromQuery] string? listType = null)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var lists = await _reportsRepository.GetUserListsAsync(userId.Value, listType);
            return Ok(lists);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving lists");
            return StatusCode(500, new { message = "An error occurred while retrieving your lists" });
        }
    }

    /// <summary>
    /// Get list by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UserListDto>> GetList(Guid id, [FromQuery] bool includeItems = true)
    {
        try
        {
            var list = await _reportsRepository.GetListByIdAsync(id, includeItems);

            if (list == null)
            {
                return NotFound(new { message = "List not found" });
            }

            // Check if user owns the list or has access via sharing
            var userId = GetCurrentUserId();
            if (list.UserId != userId)
            {
                // Check if list is shared with user
                var sharedLists = await _reportsRepository.GetSharedListsAsync(userId!.Value);
                if (!sharedLists.Any(l => l.Id == id))
                {
                    return Forbid();
                }
            }

            return Ok(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving list {ListId}", id);
            return StatusCode(500, new { message = "An error occurred while retrieving the list" });
        }
    }

    /// <summary>
    /// Get lists shared with user
    /// </summary>
    [HttpGet("shared")]
    public async Task<ActionResult<List<UserListDto>>> GetSharedLists()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var sharedLists = await _reportsRepository.GetSharedListsAsync(userId.Value);
            return Ok(sharedLists);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving shared lists");
            return StatusCode(500, new { message = "An error occurred while retrieving shared lists" });
        }
    }

    /// <summary>
    /// Create a new list
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<Guid>> CreateList([FromBody] CreateUserListRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var listId = await _reportsRepository.CreateListAsync(userId.Value, request);

            return CreatedAtAction(nameof(GetList), new { id = listId }, new { id = listId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating list");
            return StatusCode(500, new { message = "An error occurred while creating the list" });
        }
    }

    /// <summary>
    /// Update a list
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult> UpdateList(Guid id, [FromBody] UpdateUserListRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var success = await _reportsRepository.UpdateListAsync(id, userId.Value, request);

            if (!success)
            {
                return NotFound(new { message = "List not found" });
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating list {ListId}", id);
            return StatusCode(500, new { message = "An error occurred while updating the list" });
        }
    }

    /// <summary>
    /// Delete a list
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteList(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var success = await _reportsRepository.DeleteListAsync(id, userId.Value);

            if (!success)
            {
                return NotFound(new { message = "List not found" });
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting list {ListId}", id);
            return StatusCode(500, new { message = "An error occurred while deleting the list" });
        }
    }

    #endregion

    #region List Items

    /// <summary>
    /// Get items in a list
    /// </summary>
    [HttpGet("{id:guid}/items")]
    public async Task<ActionResult<List<UserListItemDto>>> GetListItems(Guid id)
    {
        try
        {
            var items = await _reportsRepository.GetListItemsAsync(id);
            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving items for list {ListId}", id);
            return StatusCode(500, new { message = "An error occurred while retrieving list items" });
        }
    }

    /// <summary>
    /// Add item to list
    /// </summary>
    [HttpPost("{id:guid}/items")]
    public async Task<ActionResult<Guid>> AddListItem(Guid id, [FromBody] AddListItemRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var itemId = await _reportsRepository.AddListItemAsync(id, userId.Value, request);

            return Ok(new { id = itemId, message = "Item added to list" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding item to list {ListId}", id);
            return StatusCode(500, new { message = "An error occurred while adding the item to the list" });
        }
    }

    /// <summary>
    /// Update a list item
    /// </summary>
    [HttpPut("items/{itemId:guid}")]
    public async Task<ActionResult> UpdateListItem(Guid itemId, [FromBody] UpdateListItemRequest request)
    {
        try
        {
            var success = await _reportsRepository.UpdateListItemAsync(itemId, request);

            if (!success)
            {
                return NotFound(new { message = "List item not found" });
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating list item {ItemId}", itemId);
            return StatusCode(500, new { message = "An error occurred while updating the list item" });
        }
    }

    /// <summary>
    /// Delete a list item
    /// </summary>
    [HttpDelete("items/{itemId:guid}")]
    public async Task<ActionResult> DeleteListItem(Guid itemId)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var success = await _reportsRepository.DeleteListItemAsync(itemId, userId.Value);

            if (!success)
            {
                return NotFound(new { message = "List item not found" });
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting list item {ItemId}", itemId);
            return StatusCode(500, new { message = "An error occurred while deleting the list item" });
        }
    }

    /// <summary>
    /// Check or uncheck a list item
    /// </summary>
    [HttpPatch("items/{itemId:guid}/check")]
    public async Task<ActionResult> CheckListItem(Guid itemId, [FromBody] CheckListItemRequest request)
    {
        try
        {
            var success = await _reportsRepository.CheckListItemAsync(itemId, request.IsChecked);

            if (!success)
            {
                return NotFound(new { message = "List item not found" });
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking list item {ItemId}", itemId);
            return StatusCode(500, new { message = "An error occurred while checking the list item" });
        }
    }

    #endregion

    #region List Sharing

    /// <summary>
    /// Get sharing details for a list
    /// </summary>
    [HttpGet("{id:guid}/sharing")]
    public async Task<ActionResult<List<ListSharingDto>>> GetListSharing(Guid id)
    {
        try
        {
            var sharing = await _reportsRepository.GetListSharingAsync(id);
            return Ok(sharing);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving sharing for list {ListId}", id);
            return StatusCode(500, new { message = "An error occurred while retrieving list sharing" });
        }
    }

    /// <summary>
    /// Share a list with another user
    /// </summary>
    [HttpPost("{id:guid}/share")]
    public async Task<ActionResult<Guid>> ShareList(Guid id, [FromBody] ShareListRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var sharingId = await _reportsRepository.ShareListAsync(id, userId.Value, request);

            return Ok(new { id = sharingId, message = "List shared successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sharing list {ListId}", id);
            return StatusCode(500, new { message = "An error occurred while sharing the list" });
        }
    }

    /// <summary>
    /// Update list sharing permissions
    /// </summary>
    [HttpPut("sharing/{sharingId:guid}")]
    public async Task<ActionResult> UpdateListSharing(Guid sharingId, [FromBody] UpdateListSharingRequest request)
    {
        try
        {
            var success = await _reportsRepository.UpdateListSharingAsync(sharingId, request);

            if (!success)
            {
                return NotFound(new { message = "List sharing not found" });
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating list sharing {SharingId}", sharingId);
            return StatusCode(500, new { message = "An error occurred while updating list sharing" });
        }
    }

    /// <summary>
    /// Remove list sharing
    /// </summary>
    [HttpDelete("sharing/{sharingId:guid}")]
    public async Task<ActionResult> RemoveListSharing(Guid sharingId)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var success = await _reportsRepository.RemoveListSharingAsync(sharingId, userId.Value);

            if (!success)
            {
                return NotFound(new { message = "List sharing not found" });
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing list sharing {SharingId}", sharingId);
            return StatusCode(500, new { message = "An error occurred while removing list sharing" });
        }
    }

    #endregion
}
