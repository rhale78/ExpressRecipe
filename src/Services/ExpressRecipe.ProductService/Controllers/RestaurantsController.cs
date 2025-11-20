using ExpressRecipe.Shared.DTOs.Product;
using ExpressRecipe.ProductService.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ExpressRecipe.ProductService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RestaurantsController : ControllerBase
{
    private readonly IRestaurantRepository _repository;
    private readonly ILogger<RestaurantsController> _logger;

    public RestaurantsController(
        IRestaurantRepository repository,
        ILogger<RestaurantsController> logger)
    {
        _repository = repository;
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
    /// Search for restaurants
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<List<RestaurantDto>>> Search([FromQuery] RestaurantSearchRequest request)
    {
        try
        {
            var restaurants = await _repository.SearchAsync(request);
            return Ok(restaurants);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching restaurants");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Get restaurant by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RestaurantDto>> GetById(Guid id)
    {
        try
        {
            var restaurant = await _repository.GetByIdAsync(id);

            if (restaurant == null)
            {
                return NotFound(new { message = "Restaurant not found" });
            }

            return Ok(restaurant);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving restaurant {RestaurantId}", id);
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Submit a new restaurant
    /// </summary>
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<RestaurantDto>> Create([FromBody] CreateRestaurantRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            var restaurantId = await _repository.CreateAsync(request, userId.Value);
            var restaurant = await _repository.GetByIdAsync(restaurantId);

            _logger.LogInformation("Restaurant {RestaurantId} submitted by user {UserId}", restaurantId, userId);

            return CreatedAtAction(nameof(GetById), new { id = restaurantId }, restaurant);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating restaurant");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Update a restaurant
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize]
    public async Task<ActionResult<RestaurantDto>> Update(Guid id, [FromBody] UpdateRestaurantRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            var success = await _repository.UpdateAsync(id, request, userId.Value);

            if (!success)
            {
                return NotFound(new { message = "Restaurant not found" });
            }

            var restaurant = await _repository.GetByIdAsync(id);

            _logger.LogInformation("Restaurant {RestaurantId} updated by user {UserId}", id, userId);

            return Ok(restaurant);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating restaurant {RestaurantId}", id);
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Delete a restaurant
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize]
    public async Task<ActionResult> Delete(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            var success = await _repository.DeleteAsync(id, userId.Value);

            if (!success)
            {
                return NotFound(new { message = "Restaurant not found" });
            }

            _logger.LogInformation("Restaurant {RestaurantId} deleted by user {UserId}", id, userId);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting restaurant {RestaurantId}", id);
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Approve or reject a restaurant (admin only)
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    [Authorize] // TODO: Add admin role check
    public async Task<ActionResult> Approve(Guid id, [FromQuery] bool approve, [FromQuery] string? rejectionReason = null)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            var success = await _repository.ApproveAsync(id, approve, userId.Value, rejectionReason);

            if (!success)
            {
                return NotFound(new { message = "Restaurant not found" });
            }

            _logger.LogInformation(
                "Restaurant {RestaurantId} {Action} by user {UserId}",
                id,
                approve ? "approved" : "rejected",
                userId);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving restaurant {RestaurantId}", id);
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    #region Restaurant Ratings

    /// <summary>
    /// Get all ratings for a restaurant
    /// </summary>
    [HttpGet("{id:guid}/ratings")]
    public async Task<ActionResult<List<UserRestaurantRatingDto>>> GetRatings(Guid id)
    {
        try
        {
            var ratings = await _repository.GetRestaurantRatingsAsync(id);
            return Ok(ratings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving ratings for restaurant {RestaurantId}", id);
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Get current user's rating for a restaurant
    /// </summary>
    [HttpGet("{id:guid}/ratings/me")]
    [Authorize]
    public async Task<ActionResult<UserRestaurantRatingDto>> GetMyRating(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            var rating = await _repository.GetUserRatingAsync(id, userId.Value);

            if (rating == null)
            {
                return NotFound(new { message = "Rating not found" });
            }

            return Ok(rating);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user rating for restaurant {RestaurantId}", id);
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Rate a restaurant
    /// </summary>
    [HttpPost("{id:guid}/ratings")]
    [Authorize]
    public async Task<ActionResult> RateRestaurant(Guid id, [FromBody] RateRestaurantRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            // Verify restaurant exists
            if (!await _repository.RestaurantExistsAsync(id))
            {
                return NotFound(new { message = "Restaurant not found" });
            }

            await _repository.AddOrUpdateRatingAsync(id, userId.Value, request);

            _logger.LogInformation("User {UserId} rated restaurant {RestaurantId}", userId, id);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rating restaurant {RestaurantId}", id);
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Delete a restaurant rating
    /// </summary>
    [HttpDelete("{id:guid}/ratings")]
    [Authorize]
    public async Task<ActionResult> DeleteRating(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            var success = await _repository.DeleteRatingAsync(id, userId.Value);

            if (!success)
            {
                return NotFound(new { message = "Rating not found" });
            }

            _logger.LogInformation("User {UserId} deleted rating for restaurant {RestaurantId}", userId, id);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting rating for restaurant {RestaurantId}", id);
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    #endregion
}
