using ExpressRecipe.Shared.DTOs.Recipe;
using ExpressRecipe.RecipeService.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ExpressRecipe.RecipeService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RatingsController : ControllerBase
{
    private readonly IRatingRepository _ratingRepository;
    private readonly ILogger<RatingsController> _logger;

    public RatingsController(
        IRatingRepository ratingRepository,
        ILogger<RatingsController> logger)
    {
        _ratingRepository = ratingRepository;
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

    #region Family Member Endpoints

    /// <summary>
    /// Get all family members for current user
    /// </summary>
    [HttpGet("family-members")]
    public async Task<ActionResult<List<FamilyMemberDto>>> GetFamilyMembers()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var members = await _ratingRepository.GetUserFamilyMembersAsync(userId.Value);
            return Ok(members);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving family members");
            return StatusCode(500, new { message = "An error occurred while retrieving family members" });
        }
    }

    /// <summary>
    /// Get family member by ID
    /// </summary>
    [HttpGet("family-members/{id:guid}")]
    public async Task<ActionResult<FamilyMemberDto>> GetFamilyMember(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var member = await _ratingRepository.GetFamilyMemberAsync(id, userId.Value);
            if (member == null)
            {
                return NotFound(new { message = "Family member not found" });
            }

            return Ok(member);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving family member {FamilyMemberId}", id);
            return StatusCode(500, new { message = "An error occurred while retrieving the family member" });
        }
    }

    /// <summary>
    /// Create a family member
    /// </summary>
    [HttpPost("family-members")]
    public async Task<ActionResult<Guid>> CreateFamilyMember([FromBody] CreateFamilyMemberRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var id = await _ratingRepository.CreateFamilyMemberAsync(userId.Value, request);

            _logger.LogInformation("Family member {FamilyMemberId} created for user {UserId}", id, userId.Value);

            return CreatedAtAction(nameof(GetFamilyMember), new { id }, new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating family member");
            return StatusCode(500, new { message = "An error occurred while creating the family member" });
        }
    }

    /// <summary>
    /// Update a family member
    /// </summary>
    [HttpPut("family-members/{id:guid}")]
    public async Task<ActionResult> UpdateFamilyMember(Guid id, [FromBody] CreateFamilyMemberRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            // Verify ownership
            var existing = await _ratingRepository.GetFamilyMemberAsync(id, userId.Value);
            if (existing == null)
            {
                return NotFound(new { message = "Family member not found" });
            }

            await _ratingRepository.UpdateFamilyMemberAsync(id, userId.Value, request);

            _logger.LogInformation("Family member {FamilyMemberId} updated by user {UserId}", id, userId.Value);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating family member {FamilyMemberId}", id);
            return StatusCode(500, new { message = "An error occurred while updating the family member" });
        }
    }

    /// <summary>
    /// Delete a family member
    /// </summary>
    [HttpDelete("family-members/{id:guid}")]
    public async Task<ActionResult> DeleteFamilyMember(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            // Verify ownership
            var existing = await _ratingRepository.GetFamilyMemberAsync(id, userId.Value);
            if (existing == null)
            {
                return NotFound(new { message = "Family member not found" });
            }

            await _ratingRepository.DeleteFamilyMemberAsync(id, userId.Value);

            _logger.LogInformation("Family member {FamilyMemberId} deleted by user {UserId}", id, userId.Value);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting family member {FamilyMemberId}", id);
            return StatusCode(500, new { message = "An error occurred while deleting the family member" });
        }
    }

    #endregion

    #region Recipe Rating Endpoints

    /// <summary>
    /// Get rating summary for a recipe (includes all user's family ratings)
    /// </summary>
    [HttpGet("recipes/{recipeId:guid}/summary")]
    [AllowAnonymous]
    public async Task<ActionResult<RecipeRatingSummaryDto>> GetRecipeRatingSummary(Guid recipeId)
    {
        try
        {
            var userId = GetCurrentUserId();
            var summary = await _ratingRepository.GetRecipeRatingSummaryAsync(recipeId, userId);

            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving rating summary for recipe {RecipeId}", recipeId);
            return StatusCode(500, new { message = "An error occurred while retrieving the rating summary" });
        }
    }

    /// <summary>
    /// Get all ratings for a recipe (optionally filtered to current user's ratings)
    /// </summary>
    [HttpGet("recipes/{recipeId:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<List<UserRecipeFamilyRatingDto>>> GetRecipeRatings(
        Guid recipeId,
        [FromQuery] bool myRatingsOnly = false)
    {
        try
        {
            Guid? userId = null;
            if (myRatingsOnly)
            {
                userId = GetCurrentUserId();
                if (!userId.HasValue)
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }
            }

            var ratings = await _ratingRepository.GetRecipeRatingsAsync(recipeId, userId);
            return Ok(ratings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving ratings for recipe {RecipeId}", recipeId);
            return StatusCode(500, new { message = "An error occurred while retrieving ratings" });
        }
    }

    /// <summary>
    /// Create or update a recipe rating
    /// </summary>
    [HttpPost("recipes")]
    public async Task<ActionResult<Guid>> CreateOrUpdateRecipeRating([FromBody] CreateRecipeRatingRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            // Validate rating value
            if (request.Rating < 0 || request.Rating > 5 || request.Rating % 0.5m != 0)
            {
                return BadRequest(new { message = "Rating must be between 0 and 5 in 0.5 increments (e.g., 0, 0.5, 1.0, ..., 5.0)" });
            }

            // If family member is specified, verify ownership
            if (request.FamilyMemberId.HasValue)
            {
                var member = await _ratingRepository.GetFamilyMemberAsync(request.FamilyMemberId.Value, userId.Value);
                if (member == null)
                {
                    return BadRequest(new { message = "Invalid family member" });
                }
            }

            var id = await _ratingRepository.CreateOrUpdateRatingAsync(userId.Value, request);

            _logger.LogInformation("Recipe rating {RatingId} created/updated for recipe {RecipeId} by user {UserId}",
                id, request.RecipeId, userId.Value);

            return Ok(new { id });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating/updating recipe rating");
            return StatusCode(500, new { message = "An error occurred while saving the rating" });
        }
    }

    /// <summary>
    /// Get a specific rating by ID
    /// </summary>
    [HttpGet("recipes/rating/{id:guid}")]
    public async Task<ActionResult<UserRecipeFamilyRatingDto>> GetRating(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var rating = await _ratingRepository.GetRatingAsync(id, userId.Value);
            if (rating == null)
            {
                return NotFound(new { message = "Rating not found" });
            }

            return Ok(rating);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving rating {RatingId}", id);
            return StatusCode(500, new { message = "An error occurred while retrieving the rating" });
        }
    }

    /// <summary>
    /// Delete a recipe rating
    /// </summary>
    [HttpDelete("recipes/rating/{id:guid}")]
    public async Task<ActionResult> DeleteRating(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            // Verify ownership
            var existing = await _ratingRepository.GetRatingAsync(id, userId.Value);
            if (existing == null)
            {
                return NotFound(new { message = "Rating not found" });
            }

            await _ratingRepository.DeleteRatingAsync(id, userId.Value);

            _logger.LogInformation("Recipe rating {RatingId} deleted by user {UserId}", id, userId.Value);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting rating {RatingId}", id);
            return StatusCode(500, new { message = "An error occurred while deleting the rating" });
        }
    }

    #endregion
}
