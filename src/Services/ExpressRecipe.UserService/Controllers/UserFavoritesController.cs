using ExpressRecipe.Shared.DTOs.User;
using ExpressRecipe.UserService.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ExpressRecipe.UserService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UserFavoritesController : ControllerBase
{
    private readonly IUserFavoritesRepository _repository;
    private readonly IUserProductRatingRepository _ratingRepository;
    private readonly ILogger<UserFavoritesController> _logger;

    public UserFavoritesController(
        IUserFavoritesRepository repository,
        IUserProductRatingRepository ratingRepository,
        ILogger<UserFavoritesController> logger)
    {
        _repository = repository;
        _ratingRepository = ratingRepository;
        _logger = logger;
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("Invalid user ID in token");
        }
        return userId;
    }

    #region Recipe Favorites

    /// <summary>
    /// Get all favorite recipes for current user
    /// </summary>
    [HttpGet("recipes")]
    public async Task<ActionResult<List<UserFavoriteRecipeDto>>> GetFavoriteRecipes()
    {
        try
        {
            var userId = GetCurrentUserId();
            var favorites = await _repository.GetFavoriteRecipesByUserIdAsync(userId);
            return Ok(favorites);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving favorite recipes");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Add a recipe to favorites
    /// </summary>
    [HttpPost("recipes/{recipeId:guid}")]
    public async Task<ActionResult<UserFavoriteRecipeDto>> AddFavoriteRecipe(Guid recipeId, [FromBody] AddFavoriteRequest? request = null)
    {
        try
        {
            var userId = GetCurrentUserId();

            // Check if already exists
            var existing = await _repository.GetFavoriteRecipeAsync(userId, recipeId);
            if (existing != null)
            {
                return Conflict(new { message = "Recipe already in favorites" });
            }

            var favoriteId = await _repository.AddFavoriteRecipeAsync(userId, recipeId, request?.Notes, userId);
            var favorite = await _repository.GetFavoriteRecipeAsync(userId, recipeId);

            _logger.LogInformation("Recipe {RecipeId} added to favorites for user {UserId}", recipeId, userId);

            return CreatedAtAction(nameof(GetFavoriteRecipes), favorite);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding recipe to favorites");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Remove a recipe from favorites
    /// </summary>
    [HttpDelete("recipes/{recipeId:guid}")]
    public async Task<ActionResult> RemoveFavoriteRecipe(Guid recipeId)
    {
        try
        {
            var userId = GetCurrentUserId();
            var success = await _repository.RemoveFavoriteRecipeAsync(userId, recipeId);

            if (!success)
            {
                return NotFound(new { message = "Favorite recipe not found" });
            }

            _logger.LogInformation("Recipe {RecipeId} removed from favorites for user {UserId}", recipeId, userId);

            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing recipe from favorites");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    #endregion

    #region Product Favorites

    /// <summary>
    /// Get all favorite products for current user
    /// </summary>
    [HttpGet("products")]
    public async Task<ActionResult<List<UserFavoriteProductDto>>> GetFavoriteProducts()
    {
        try
        {
            var userId = GetCurrentUserId();
            var favorites = await _repository.GetFavoriteProductsByUserIdAsync(userId);
            return Ok(favorites);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving favorite products");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Add a product to favorites
    /// </summary>
    [HttpPost("products/{productId:guid}")]
    public async Task<ActionResult<UserFavoriteProductDto>> AddFavoriteProduct(Guid productId, [FromBody] AddFavoriteRequest? request = null)
    {
        try
        {
            var userId = GetCurrentUserId();

            // Check if already exists
            var existing = await _repository.GetFavoriteProductAsync(userId, productId);
            if (existing != null)
            {
                return Conflict(new { message = "Product already in favorites" });
            }

            var favoriteId = await _repository.AddFavoriteProductAsync(userId, productId, request?.Notes, userId);
            var favorite = await _repository.GetFavoriteProductAsync(userId, productId);

            _logger.LogInformation("Product {ProductId} added to favorites for user {UserId}", productId, userId);

            return CreatedAtAction(nameof(GetFavoriteProducts), favorite);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding product to favorites");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Remove a product from favorites
    /// </summary>
    [HttpDelete("products/{productId:guid}")]
    public async Task<ActionResult> RemoveFavoriteProduct(Guid productId)
    {
        try
        {
            var userId = GetCurrentUserId();
            var success = await _repository.RemoveFavoriteProductAsync(userId, productId);

            if (!success)
            {
                return NotFound(new { message = "Favorite product not found" });
            }

            _logger.LogInformation("Product {ProductId} removed from favorites for user {UserId}", productId, userId);

            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing product from favorites");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    #endregion

    #region Product Ratings

    /// <summary>
    /// Get all product ratings by current user
    /// </summary>
    [HttpGet("ratings")]
    public async Task<ActionResult<List<UserProductRatingDto>>> GetMyRatings()
    {
        try
        {
            var userId = GetCurrentUserId();
            var ratings = await _ratingRepository.GetRatingsByUserIdAsync(userId);
            return Ok(ratings);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving ratings");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Get user's rating for a specific product
    /// </summary>
    [HttpGet("ratings/products/{productId:guid}")]
    public async Task<ActionResult<UserProductRatingDto>> GetProductRating(Guid productId)
    {
        try
        {
            var userId = GetCurrentUserId();
            var rating = await _ratingRepository.GetRatingAsync(userId, productId);

            if (rating == null)
            {
                return NotFound(new { message = "Rating not found" });
            }

            return Ok(rating);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving product rating");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Rate a product (create or update rating)
    /// </summary>
    [HttpPost("ratings/products/{productId:guid}")]
    [HttpPut("ratings/products/{productId:guid}")]
    public async Task<ActionResult<UserProductRatingDto>> RateProduct(Guid productId, [FromBody] CreateUserProductRatingRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();

            // Override the productId from route
            request.ProductId = productId;

            var ratingId = await _ratingRepository.CreateOrUpdateRatingAsync(userId, request, userId);
            var rating = await _ratingRepository.GetRatingAsync(userId, productId);

            _logger.LogInformation("Product {ProductId} rated by user {UserId}", productId, userId);

            return Ok(rating);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rating product");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Delete a product rating
    /// </summary>
    [HttpDelete("ratings/products/{productId:guid}")]
    public async Task<ActionResult> DeleteProductRating(Guid productId)
    {
        try
        {
            var userId = GetCurrentUserId();
            var success = await _ratingRepository.DeleteRatingAsync(userId, productId);

            if (!success)
            {
                return NotFound(new { message = "Rating not found" });
            }

            _logger.LogInformation("Product {ProductId} rating deleted for user {UserId}", productId, userId);

            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting product rating");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Get rating statistics for a product (public endpoint)
    /// </summary>
    [HttpGet("ratings/products/{productId:guid}/stats")]
    [AllowAnonymous]
    public async Task<ActionResult<object>> GetProductRatingStats(Guid productId)
    {
        try
        {
            var (averageRating, totalRatings) = await _ratingRepository.GetProductRatingStatsAsync(productId);
            return Ok(new { averageRating, totalRatings });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving product rating stats");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    #endregion
}

public class AddFavoriteRequest
{
    public string? Notes { get; set; }
}
