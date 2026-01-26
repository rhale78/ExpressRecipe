using ExpressRecipe.Shared.DTOs.Recipe;
using ExpressRecipe.RecipeService.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ExpressRecipe.RecipeService.Controllers
{
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
            return string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId) ? null : userId;
        }

        /// <summary>
        /// Get collection summary for user
        /// </summary>
        [HttpGet("summary")]
        public async Task<ActionResult<CollectionSummaryDto>> GetSummary()
        {
            try
            {
                Guid? userId = GetCurrentUserId();
                if (!userId.HasValue)
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                CollectionSummaryDto summary = await _importRepository.GetCollectionSummaryAsync(userId.Value);
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
                Guid? userId = GetCurrentUserId();
                if (!userId.HasValue)
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                List<RecipeCollectionDto> collections = await _importRepository.GetUserCollectionsAsync(userId.Value, true);
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
                RecipeCollectionDto? collection = await _importRepository.GetCollectionByIdAsync(id, includeItems);

                if (collection == null)
                {
                    return NotFound(new { message = "Collection not found" });
                }

                // Verify ownership or public access
                Guid? userId = GetCurrentUserId();
                return collection.UserId != userId && !collection.IsPublic ? (ActionResult<RecipeCollectionDto>)Forbid() : (ActionResult<RecipeCollectionDto>)Ok(collection);
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
                Guid? userId = GetCurrentUserId();
                if (!userId.HasValue)
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                Guid collectionId = await _importRepository.CreateCollectionAsync(userId.Value, request);

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
                Guid? userId = GetCurrentUserId();
                if (!userId.HasValue)
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                var success = await _importRepository.UpdateCollectionAsync(id, userId.Value, request);

                return !success ? NotFound(new { message = "Collection not found or could not be updated" }) : NoContent();
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
                Guid? userId = GetCurrentUserId();
                if (!userId.HasValue)
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                var success = await _importRepository.DeleteCollectionAsync(id, userId.Value);

                return !success ? NotFound(new { message = "Collection not found" }) : NoContent();
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
                Guid? userId = GetCurrentUserId();
                if (!userId.HasValue)
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                Guid itemId = await _importRepository.AddRecipeToCollectionAsync(id, userId.Value, request);

                return Ok(new { id = itemId, message = "Recipe added to collection" });
            }
            catch (UnauthorizedAccessException)
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
                Guid? userId = GetCurrentUserId();
                if (!userId.HasValue)
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                var success = await _importRepository.RemoveRecipeFromCollectionAsync(collectionId, recipeId, userId.Value);

                return !success ? NotFound(new { message = "Recipe not found in collection" }) : NoContent();
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

                return !success ? NotFound(new { message = "Collection item not found" }) : NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating collection item {ItemId}", itemId);
                return StatusCode(500, new { message = "An error occurred while updating the collection item" });
            }
        }
    }
}
