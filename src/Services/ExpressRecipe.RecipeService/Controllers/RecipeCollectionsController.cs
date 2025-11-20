using ExpressRecipe.Shared.DTOs.Recipe;
using ExpressRecipe.RecipeService.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ExpressRecipe.RecipeService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RecipeCollectionsController : ControllerBase
{
    private readonly IRecipeImportRepository _importRepository;
    private readonly ILogger<RecipeCollectionsController> _logger;

    public RecipeCollectionsController(
        IRecipeImportRepository importRepository,
        ILogger<RecipeCollectionsController> logger)
    {
        _importRepository = importRepository;
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

    /// <summary>
    /// Get collection summary for user
    /// </summary>
    [HttpGet("summary")]
    public async Task<ActionResult<CollectionSummaryDto>> GetSummary()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var summary = await _importRepository.GetCollectionSummaryAsync(userId.Value);
            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving collection summary");
            return StatusCode(500, new { message = "An error occurred while retrieving collection summary" });
        }
    }

    /// <summary>
    /// Get user's recipe collections
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<RecipeCollectionDto>>> GetCollections()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var collections = await _importRepository.GetUserCollectionsAsync(userId.Value, true);
            return Ok(collections);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving collections");
            return StatusCode(500, new { message = "An error occurred while retrieving your collections" });
        }
    }

    /// <summary>
    /// Get collection by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RecipeCollectionDto>> GetCollection(Guid id, [FromQuery] bool includeItems = true)
    {
        try
        {
            var collection = await _importRepository.GetCollectionByIdAsync(id, includeItems);

            if (collection == null)
            {
                return NotFound(new { message = "Collection not found" });
            }

            // Verify ownership or public access
            var userId = GetCurrentUserId();
            if (collection.UserId != userId && !collection.IsPublic)
            {
                return Forbid();
            }

            return Ok(collection);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving collection {CollectionId}", id);
            return StatusCode(500, new { message = "An error occurred while retrieving the collection" });
        }
    }

    /// <summary>
    /// Create a new collection
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<Guid>> CreateCollection([FromBody] CreateRecipeCollectionRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var collectionId = await _importRepository.CreateCollectionAsync(userId.Value, request);

            return CreatedAtAction(nameof(GetCollection), new { id = collectionId }, new { id = collectionId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating collection");
            return StatusCode(500, new { message = "An error occurred while creating the collection" });
        }
    }

    /// <summary>
    /// Update a collection
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult> UpdateCollection(Guid id, [FromBody] UpdateRecipeCollectionRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var success = await _importRepository.UpdateCollectionAsync(id, userId.Value, request);

            if (!success)
            {
                return NotFound(new { message = "Collection not found or could not be updated" });
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating collection {CollectionId}", id);
            return StatusCode(500, new { message = "An error occurred while updating the collection" });
        }
    }

    /// <summary>
    /// Delete a collection
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteCollection(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var success = await _importRepository.DeleteCollectionAsync(id, userId.Value);

            if (!success)
            {
                return NotFound(new { message = "Collection not found" });
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting collection {CollectionId}", id);
            return StatusCode(500, new { message = "An error occurred while deleting the collection" });
        }
    }

    /// <summary>
    /// Add recipe to collection
    /// </summary>
    [HttpPost("{id:guid}/recipes")]
    public async Task<ActionResult<Guid>> AddRecipe(Guid id, [FromBody] AddRecipeToCollectionRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var itemId = await _importRepository.AddRecipeToCollectionAsync(id, userId.Value, request);

            return Ok(new { id = itemId, message = "Recipe added to collection" });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding recipe to collection {CollectionId}", id);
            return StatusCode(500, new { message = "An error occurred while adding the recipe to the collection" });
        }
    }

    /// <summary>
    /// Remove recipe from collection
    /// </summary>
    [HttpDelete("{collectionId:guid}/recipes/{recipeId:guid}")]
    public async Task<ActionResult> RemoveRecipe(Guid collectionId, Guid recipeId)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var success = await _importRepository.RemoveRecipeFromCollectionAsync(collectionId, recipeId, userId.Value);

            if (!success)
            {
                return NotFound(new { message = "Recipe not found in collection" });
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing recipe {RecipeId} from collection {CollectionId}", recipeId, collectionId);
            return StatusCode(500, new { message = "An error occurred while removing the recipe from the collection" });
        }
    }

    /// <summary>
    /// Update collection item (order, notes)
    /// </summary>
    [HttpPut("items/{itemId:guid}")]
    public async Task<ActionResult> UpdateCollectionItem(Guid itemId, [FromBody] UpdateCollectionItemRequest request)
    {
        try
        {
            var success = await _importRepository.UpdateCollectionItemAsync(itemId, request);

            if (!success)
            {
                return NotFound(new { message = "Collection item not found" });
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating collection item {ItemId}", itemId);
            return StatusCode(500, new { message = "An error occurred while updating the collection item" });
        }
    }
}
