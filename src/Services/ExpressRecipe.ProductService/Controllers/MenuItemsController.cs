using ExpressRecipe.Shared.DTOs.Product;
using ExpressRecipe.ProductService.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ExpressRecipe.ProductService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MenuItemsController : ControllerBase
    {
        private readonly IMenuItemRepository _repository;
        private readonly ILogger<MenuItemsController> _logger;

        public MenuItemsController(
            IMenuItemRepository repository,
            ILogger<MenuItemsController> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        private Guid? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId) ? null : userId;
        }

        /// <summary>
        /// Search for menu items
        /// </summary>
        [HttpGet("search")]
        public async Task<ActionResult<List<MenuItemDto>>> Search([FromQuery] MenuItemSearchRequest request)
        {
            try
            {
                List<MenuItemDto> menuItems = await _repository.SearchAsync(request);
                return Ok(menuItems);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching menu items");
                return StatusCode(500, new { message = "An error occurred" });
            }
        }

        /// <summary>
        /// Get menu item by ID
        /// </summary>
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<MenuItemDto>> GetById(Guid id)
        {
            try
            {
                MenuItemDto? menuItem = await _repository.GetByIdAsync(id);

                if (menuItem == null)
                {
                    return NotFound(new { message = "Menu item not found" });
                }

                // Load ingredients
                menuItem.Ingredients = await _repository.GetMenuItemIngredientsAsync(id);

                // Load nutrition
                menuItem.Nutrition = await _repository.GetMenuItemNutritionAsync(id);

                return Ok(menuItem);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving menu item {MenuItemId}", id);
                return StatusCode(500, new { message = "An error occurred" });
            }
        }

        /// <summary>
        /// Create a new menu item
        /// </summary>
        [HttpPost]
        [Authorize]
        public async Task<ActionResult<MenuItemDto>> Create([FromBody] CreateMenuItemRequest request)
        {
            try
            {
                Guid? userId = GetCurrentUserId();
                if (userId == null)
                {
                    return Unauthorized();
                }

                Guid menuItemId = await _repository.CreateAsync(request, userId.Value);

                // Add ingredients if provided
                if (request.IngredientIds != null && request.IngredientIds.Count != 0)
                {
                    for (int i = 0; i < request.IngredientIds.Count; i++)
                    {
                        await _repository.AddMenuItemIngredientAsync(
                            menuItemId,
                            request.IngredientIds[i],
                            i,
                            userId.Value);
                    }
                }

                MenuItemDto? menuItem = await _repository.GetByIdAsync(menuItemId);
                if (menuItem != null)
                {
                    menuItem.Ingredients = await _repository.GetMenuItemIngredientsAsync(menuItemId);
                }

                _logger.LogInformation("Menu item {MenuItemId} created by user {UserId}", menuItemId, userId);

                return CreatedAtAction(nameof(GetById), new { id = menuItemId }, menuItem);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating menu item");
                return StatusCode(500, new { message = "An error occurred" });
            }
        }

        /// <summary>
        /// Update a menu item
        /// </summary>
        [HttpPut("{id:guid}")]
        [Authorize]
        public async Task<ActionResult<MenuItemDto>> Update(Guid id, [FromBody] UpdateMenuItemRequest request)
        {
            try
            {
                Guid? userId = GetCurrentUserId();
                if (userId == null)
                {
                    return Unauthorized();
                }

                var success = await _repository.UpdateAsync(id, request, userId.Value);

                if (!success)
                {
                    return NotFound(new { message = "Menu item not found" });
                }

                MenuItemDto? menuItem = await _repository.GetByIdAsync(id);
                if (menuItem != null)
                {
                    menuItem.Ingredients = await _repository.GetMenuItemIngredientsAsync(id);
                    menuItem.Nutrition = await _repository.GetMenuItemNutritionAsync(id);
                }

                _logger.LogInformation("Menu item {MenuItemId} updated by user {UserId}", id, userId);

                return Ok(menuItem);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating menu item {MenuItemId}", id);
                return StatusCode(500, new { message = "An error occurred" });
            }
        }

        /// <summary>
        /// Delete a menu item
        /// </summary>
        [HttpDelete("{id:guid}")]
        [Authorize]
        public async Task<ActionResult> Delete(Guid id)
        {
            try
            {
                Guid? userId = GetCurrentUserId();
                if (userId == null)
                {
                    return Unauthorized();
                }

                var success = await _repository.DeleteAsync(id, userId.Value);

                if (!success)
                {
                    return NotFound(new { message = "Menu item not found" });
                }

                _logger.LogInformation("Menu item {MenuItemId} deleted by user {UserId}", id, userId);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting menu item {MenuItemId}", id);
                return StatusCode(500, new { message = "An error occurred" });
            }
        }

        #region Menu Item Ingredients

        /// <summary>
        /// Get ingredients for a menu item
        /// </summary>
        [HttpGet("{id:guid}/ingredients")]
        public async Task<ActionResult<List<MenuItemIngredientDto>>> GetIngredients(Guid id)
        {
            try
            {
                List<MenuItemIngredientDto> ingredients = await _repository.GetMenuItemIngredientsAsync(id);
                return Ok(ingredients);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving ingredients for menu item {MenuItemId}", id);
                return StatusCode(500, new { message = "An error occurred" });
            }
        }

        /// <summary>
        /// Add ingredient to menu item
        /// </summary>
        [HttpPost("{id:guid}/ingredients/{ingredientId:guid}")]
        [Authorize]
        public async Task<ActionResult> AddIngredient(Guid id, Guid ingredientId, [FromQuery] int orderIndex = 0)
        {
            try
            {
                Guid? userId = GetCurrentUserId();
                if (userId == null)
                {
                    return Unauthorized();
                }

                // Verify menu item exists
                if (!await _repository.MenuItemExistsAsync(id))
                {
                    return NotFound(new { message = "Menu item not found" });
                }

                Guid ingredientMappingId = await _repository.AddMenuItemIngredientAsync(id, ingredientId, orderIndex, userId.Value);

                _logger.LogInformation("Ingredient {IngredientId} added to menu item {MenuItemId}", ingredientId, id);

                return CreatedAtAction(nameof(GetIngredients), new { id }, new { ingredientId = ingredientMappingId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding ingredient to menu item {MenuItemId}", id);
                return StatusCode(500, new { message = "An error occurred" });
            }
        }

        /// <summary>
        /// Remove ingredient from menu item
        /// </summary>
        [HttpDelete("{id:guid}/ingredients/{ingredientMappingId:guid}")]
        [Authorize]
        public async Task<ActionResult> RemoveIngredient(Guid id, Guid ingredientMappingId)
        {
            try
            {
                Guid? userId = GetCurrentUserId();
                if (userId == null)
                {
                    return Unauthorized();
                }

                var success = await _repository.RemoveMenuItemIngredientAsync(ingredientMappingId, userId.Value);

                if (!success)
                {
                    return NotFound(new { message = "Menu item ingredient not found" });
                }

                _logger.LogInformation("Ingredient mapping {MappingId} removed from menu item {MenuItemId}", ingredientMappingId, id);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing ingredient from menu item {MenuItemId}", id);
                return StatusCode(500, new { message = "An error occurred" });
            }
        }

        #endregion

        #region Menu Item Nutrition

        /// <summary>
        /// Get nutrition for a menu item
        /// </summary>
        [HttpGet("{id:guid}/nutrition")]
        public async Task<ActionResult<MenuItemNutritionDto>> GetNutrition(Guid id)
        {
            try
            {
                MenuItemNutritionDto? nutrition = await _repository.GetMenuItemNutritionAsync(id);

                return nutrition == null ? (ActionResult<MenuItemNutritionDto>)NotFound(new { message = "Nutrition information not found" }) : (ActionResult<MenuItemNutritionDto>)Ok(nutrition);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving nutrition for menu item {MenuItemId}", id);
                return StatusCode(500, new { message = "An error occurred" });
            }
        }

        /// <summary>
        /// Add or update nutrition for a menu item
        /// </summary>
        [HttpPut("{id:guid}/nutrition")]
        [Authorize]
        public async Task<ActionResult> AddOrUpdateNutrition(Guid id, [FromBody] MenuItemNutritionDto nutrition)
        {
            try
            {
                Guid? userId = GetCurrentUserId();
                if (userId == null)
                {
                    return Unauthorized();
                }

                // Verify menu item exists
                if (!await _repository.MenuItemExistsAsync(id))
                {
                    return NotFound(new { message = "Menu item not found" });
                }

                await _repository.AddOrUpdateMenuItemNutritionAsync(id, nutrition, userId.Value);

                _logger.LogInformation("Nutrition updated for menu item {MenuItemId}", id);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating nutrition for menu item {MenuItemId}", id);
                return StatusCode(500, new { message = "An error occurred" });
            }
        }

        #endregion

        #region Menu Item Ratings

        /// <summary>
        /// Get all ratings for a menu item
        /// </summary>
        [HttpGet("{id:guid}/ratings")]
        public async Task<ActionResult<List<UserMenuItemRatingDto>>> GetRatings(Guid id)
        {
            try
            {
                List<UserMenuItemRatingDto> ratings = await _repository.GetMenuItemRatingsAsync(id);
                return Ok(ratings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving ratings for menu item {MenuItemId}", id);
                return StatusCode(500, new { message = "An error occurred" });
            }
        }

        /// <summary>
        /// Get current user's rating for a menu item
        /// </summary>
        [HttpGet("{id:guid}/ratings/me")]
        [Authorize]
        public async Task<ActionResult<UserMenuItemRatingDto>> GetMyRating(Guid id)
        {
            try
            {
                Guid? userId = GetCurrentUserId();
                if (userId == null)
                {
                    return Unauthorized();
                }

                UserMenuItemRatingDto? rating = await _repository.GetUserRatingAsync(id, userId.Value);

                return rating == null ? (ActionResult<UserMenuItemRatingDto>)NotFound(new { message = "Rating not found" }) : (ActionResult<UserMenuItemRatingDto>)Ok(rating);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user rating for menu item {MenuItemId}", id);
                return StatusCode(500, new { message = "An error occurred" });
            }
        }

        /// <summary>
        /// Rate a menu item
        /// </summary>
        [HttpPost("{id:guid}/ratings")]
        [Authorize]
        public async Task<ActionResult> RateMenuItem(Guid id, [FromBody] RateMenuItemRequest request)
        {
            try
            {
                Guid? userId = GetCurrentUserId();
                if (userId == null)
                {
                    return Unauthorized();
                }

                // Verify menu item exists
                if (!await _repository.MenuItemExistsAsync(id))
                {
                    return NotFound(new { message = "Menu item not found" });
                }

                await _repository.AddOrUpdateRatingAsync(id, userId.Value, request);

                _logger.LogInformation("User {UserId} rated menu item {MenuItemId}", userId, id);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rating menu item {MenuItemId}", id);
                return StatusCode(500, new { message = "An error occurred" });
            }
        }

        /// <summary>
        /// Delete a menu item rating
        /// </summary>
        [HttpDelete("{id:guid}/ratings")]
        [Authorize]
        public async Task<ActionResult> DeleteRating(Guid id)
        {
            try
            {
                Guid? userId = GetCurrentUserId();
                if (userId == null)
                {
                    return Unauthorized();
                }

                var success = await _repository.DeleteRatingAsync(id, userId.Value);

                if (!success)
                {
                    return NotFound(new { message = "Rating not found" });
                }

                _logger.LogInformation("User {UserId} deleted rating for menu item {MenuItemId}", userId, id);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting rating for menu item {MenuItemId}", id);
                return StatusCode(500, new { message = "An error occurred" });
            }
        }

        #endregion
    }
}
