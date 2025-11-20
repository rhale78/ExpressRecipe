using ExpressRecipe.Shared.DTOs.User;
using ExpressRecipe.UserService.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ExpressRecipe.UserService.Controllers;

[ApiController]
[Route("api/users/{userId:guid}/preferences")]
[Authorize]
public class UserPreferencesController : ControllerBase
{
    private readonly IUserPreferenceRepository _repository;
    private readonly ILogger<UserPreferencesController> _logger;

    public UserPreferencesController(
        IUserPreferenceRepository repository,
        ILogger<UserPreferencesController> logger)
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

    private bool IsAuthorizedForUser(Guid userId)
    {
        var currentUserId = GetCurrentUserId();
        return currentUserId.HasValue && currentUserId.Value == userId;
    }

    #region Preferred Cuisines

    /// <summary>
    /// Get user's preferred cuisines
    /// </summary>
    [HttpGet("cuisines")]
    public async Task<ActionResult<List<UserPreferredCuisineDto>>> GetPreferredCuisines(Guid userId)
    {
        try
        {
            if (!IsAuthorizedForUser(userId))
            {
                return Forbid();
            }

            var cuisines = await _repository.GetUserPreferredCuisinesAsync(userId);
            return Ok(cuisines);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving preferred cuisines for user {UserId}", userId);
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Add preferred cuisine
    /// </summary>
    [HttpPost("cuisines")]
    public async Task<ActionResult> AddPreferredCuisine(Guid userId, [FromBody] AddUserPreferredCuisineRequest request)
    {
        try
        {
            if (!IsAuthorizedForUser(userId))
            {
                return Forbid();
            }

            var id = await _repository.AddPreferredCuisineAsync(userId, request);
            _logger.LogInformation("User {UserId} added preferred cuisine {CuisineId}", userId, request.CuisineId);

            return CreatedAtAction(nameof(GetPreferredCuisines), new { userId }, new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding preferred cuisine for user {UserId}", userId);
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Update preferred cuisine
    /// </summary>
    [HttpPut("cuisines/{cuisineId:guid}")]
    public async Task<ActionResult> UpdatePreferredCuisine(Guid userId, Guid cuisineId, [FromBody] UpdateUserPreferredCuisineRequest request)
    {
        try
        {
            if (!IsAuthorizedForUser(userId))
            {
                return Forbid();
            }

            var success = await _repository.UpdatePreferredCuisineAsync(userId, cuisineId, request);

            if (!success)
            {
                return NotFound(new { message = "Preferred cuisine not found" });
            }

            _logger.LogInformation("User {UserId} updated preferred cuisine {CuisineId}", userId, cuisineId);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating preferred cuisine for user {UserId}", userId);
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Remove preferred cuisine
    /// </summary>
    [HttpDelete("cuisines/{cuisineId:guid}")]
    public async Task<ActionResult> RemovePreferredCuisine(Guid userId, Guid cuisineId)
    {
        try
        {
            if (!IsAuthorizedForUser(userId))
            {
                return Forbid();
            }

            var success = await _repository.RemovePreferredCuisineAsync(userId, cuisineId);

            if (!success)
            {
                return NotFound(new { message = "Preferred cuisine not found" });
            }

            _logger.LogInformation("User {UserId} removed preferred cuisine {CuisineId}", userId, cuisineId);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing preferred cuisine for user {UserId}", userId);
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    #endregion

    #region Health Goals

    /// <summary>
    /// Get user's health goals
    /// </summary>
    [HttpGet("health-goals")]
    public async Task<ActionResult<List<UserHealthGoalDto>>> GetHealthGoals(Guid userId)
    {
        try
        {
            if (!IsAuthorizedForUser(userId))
            {
                return Forbid();
            }

            var goals = await _repository.GetUserHealthGoalsAsync(userId);
            return Ok(goals);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving health goals for user {UserId}", userId);
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Add health goal
    /// </summary>
    [HttpPost("health-goals")]
    public async Task<ActionResult> AddHealthGoal(Guid userId, [FromBody] AddUserHealthGoalRequest request)
    {
        try
        {
            if (!IsAuthorizedForUser(userId))
            {
                return Forbid();
            }

            var id = await _repository.AddHealthGoalAsync(userId, request);
            _logger.LogInformation("User {UserId} added health goal {GoalId}", userId, request.HealthGoalId);

            return CreatedAtAction(nameof(GetHealthGoals), new { userId }, new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding health goal for user {UserId}", userId);
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Update health goal
    /// </summary>
    [HttpPut("health-goals/{goalId:guid}")]
    public async Task<ActionResult> UpdateHealthGoal(Guid userId, Guid goalId, [FromBody] UpdateUserHealthGoalRequest request)
    {
        try
        {
            if (!IsAuthorizedForUser(userId))
            {
                return Forbid();
            }

            var success = await _repository.UpdateHealthGoalAsync(userId, goalId, request);

            if (!success)
            {
                return NotFound(new { message = "Health goal not found" });
            }

            _logger.LogInformation("User {UserId} updated health goal {GoalId}", userId, goalId);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating health goal for user {UserId}", userId);
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Remove health goal
    /// </summary>
    [HttpDelete("health-goals/{goalId:guid}")]
    public async Task<ActionResult> RemoveHealthGoal(Guid userId, Guid goalId)
    {
        try
        {
            if (!IsAuthorizedForUser(userId))
            {
                return Forbid();
            }

            var success = await _repository.RemoveHealthGoalAsync(userId, goalId);

            if (!success)
            {
                return NotFound(new { message = "Health goal not found" });
            }

            _logger.LogInformation("User {UserId} removed health goal {GoalId}", userId, goalId);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing health goal for user {UserId}", userId);
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    #endregion

    #region Favorite Ingredients

    /// <summary>
    /// Get user's favorite ingredients
    /// </summary>
    [HttpGet("favorite-ingredients")]
    public async Task<ActionResult<List<UserFavoriteIngredientDto>>> GetFavoriteIngredients(Guid userId)
    {
        try
        {
            if (!IsAuthorizedForUser(userId))
            {
                return Forbid();
            }

            var ingredients = await _repository.GetUserFavoriteIngredientsAsync(userId);
            return Ok(ingredients);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving favorite ingredients for user {UserId}", userId);
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Add favorite ingredient
    /// </summary>
    [HttpPost("favorite-ingredients")]
    public async Task<ActionResult> AddFavoriteIngredient(Guid userId, [FromBody] AddUserFavoriteIngredientRequest request)
    {
        try
        {
            if (!IsAuthorizedForUser(userId))
            {
                return Forbid();
            }

            var id = await _repository.AddFavoriteIngredientAsync(userId, request);
            _logger.LogInformation("User {UserId} added favorite ingredient {IngredientId}", userId, request.IngredientId);

            return CreatedAtAction(nameof(GetFavoriteIngredients), new { userId }, new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding favorite ingredient for user {UserId}", userId);
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Update favorite ingredient
    /// </summary>
    [HttpPut("favorite-ingredients/{ingredientId:guid}")]
    public async Task<ActionResult> UpdateFavoriteIngredient(Guid userId, Guid ingredientId, [FromBody] UpdateUserFavoriteIngredientRequest request)
    {
        try
        {
            if (!IsAuthorizedForUser(userId))
            {
                return Forbid();
            }

            var success = await _repository.UpdateFavoriteIngredientAsync(userId, ingredientId, request);

            if (!success)
            {
                return NotFound(new { message = "Favorite ingredient not found" });
            }

            _logger.LogInformation("User {UserId} updated favorite ingredient {IngredientId}", userId, ingredientId);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating favorite ingredient for user {UserId}", userId);
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Remove favorite ingredient
    /// </summary>
    [HttpDelete("favorite-ingredients/{ingredientId:guid}")]
    public async Task<ActionResult> RemoveFavoriteIngredient(Guid userId, Guid ingredientId)
    {
        try
        {
            if (!IsAuthorizedForUser(userId))
            {
                return Forbid();
            }

            var success = await _repository.RemoveFavoriteIngredientAsync(userId, ingredientId);

            if (!success)
            {
                return NotFound(new { message = "Favorite ingredient not found" });
            }

            _logger.LogInformation("User {UserId} removed favorite ingredient {IngredientId}", userId, ingredientId);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing favorite ingredient for user {UserId}", userId);
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    #endregion

    #region Disliked Ingredients

    /// <summary>
    /// Get user's disliked ingredients
    /// </summary>
    [HttpGet("disliked-ingredients")]
    public async Task<ActionResult<List<UserDislikedIngredientDto>>> GetDislikedIngredients(Guid userId)
    {
        try
        {
            if (!IsAuthorizedForUser(userId))
            {
                return Forbid();
            }

            var ingredients = await _repository.GetUserDislikedIngredientsAsync(userId);
            return Ok(ingredients);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving disliked ingredients for user {UserId}", userId);
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Add disliked ingredient
    /// </summary>
    [HttpPost("disliked-ingredients")]
    public async Task<ActionResult> AddDislikedIngredient(Guid userId, [FromBody] AddUserDislikedIngredientRequest request)
    {
        try
        {
            if (!IsAuthorizedForUser(userId))
            {
                return Forbid();
            }

            var id = await _repository.AddDislikedIngredientAsync(userId, request);
            _logger.LogInformation("User {UserId} added disliked ingredient {IngredientId}", userId, request.IngredientId);

            return CreatedAtAction(nameof(GetDislikedIngredients), new { userId }, new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding disliked ingredient for user {UserId}", userId);
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Update disliked ingredient
    /// </summary>
    [HttpPut("disliked-ingredients/{ingredientId:guid}")]
    public async Task<ActionResult> UpdateDislikedIngredient(Guid userId, Guid ingredientId, [FromBody] UpdateUserDislikedIngredientRequest request)
    {
        try
        {
            if (!IsAuthorizedForUser(userId))
            {
                return Forbid();
            }

            var success = await _repository.UpdateDislikedIngredientAsync(userId, ingredientId, request);

            if (!success)
            {
                return NotFound(new { message = "Disliked ingredient not found" });
            }

            _logger.LogInformation("User {UserId} updated disliked ingredient {IngredientId}", userId, ingredientId);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating disliked ingredient for user {UserId}", userId);
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Remove disliked ingredient
    /// </summary>
    [HttpDelete("disliked-ingredients/{ingredientId:guid}")]
    public async Task<ActionResult> RemoveDislikedIngredient(Guid userId, Guid ingredientId)
    {
        try
        {
            if (!IsAuthorizedForUser(userId))
            {
                return Forbid();
            }

            var success = await _repository.RemoveDislikedIngredientAsync(userId, ingredientId);

            if (!success)
            {
                return NotFound(new { message = "Disliked ingredient not found" });
            }

            _logger.LogInformation("User {UserId} removed disliked ingredient {IngredientId}", userId, ingredientId);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing disliked ingredient for user {UserId}", userId);
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    #endregion
}
