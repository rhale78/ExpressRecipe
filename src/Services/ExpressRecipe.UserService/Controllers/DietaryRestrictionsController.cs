using ExpressRecipe.Shared.DTOs.User;
using ExpressRecipe.UserService.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ExpressRecipe.UserService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DietaryRestrictionsController : ControllerBase
{
    private readonly IDietaryRestrictionRepository _repository;
    private readonly ILogger<DietaryRestrictionsController> _logger;

    public DietaryRestrictionsController(
        IDietaryRestrictionRepository repository,
        ILogger<DietaryRestrictionsController> logger)
    {
        _repository = repository;
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

    /// <summary>
    /// Get all available dietary restrictions
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<DietaryRestrictionDto>>> GetAll()
    {
        try
        {
            var restrictions = await _repository.GetAllRestrictionsAsync();
            return Ok(restrictions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving dietary restrictions");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Get dietary restriction by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DietaryRestrictionDto>> GetById(Guid id)
    {
        try
        {
            var restriction = await _repository.GetByIdAsync(id);

            if (restriction == null)
            {
                return NotFound(new { message = "Dietary restriction not found" });
            }

            return Ok(restriction);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving dietary restriction {RestrictionId}", id);
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Get dietary restrictions by type
    /// </summary>
    [HttpGet("type/{type}")]
    public async Task<ActionResult<List<DietaryRestrictionDto>>> GetByType(string type)
    {
        try
        {
            var restrictions = await _repository.GetByTypeAsync(type);
            return Ok(restrictions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving dietary restrictions by type {Type}", type);
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Get current user's dietary restrictions
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<List<UserDietaryRestrictionDto>>> GetMyRestrictions()
    {
        try
        {
            var userId = GetCurrentUserId();
            var restrictions = await _repository.GetUserRestrictionsAsync(userId);
            return Ok(restrictions);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user dietary restrictions");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Add dietary restriction to current user
    /// </summary>
    [HttpPost("me")]
    [Authorize]
    public async Task<ActionResult<UserDietaryRestrictionDto>> AddMyRestriction([FromBody] AddUserDietaryRestrictionRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();

            // Verify restriction exists
            var restriction = await _repository.GetByIdAsync(request.DietaryRestrictionId);
            if (restriction == null)
            {
                return NotFound(new { message = "Dietary restriction not found" });
            }

            var userRestrictionId = await _repository.AddUserRestrictionAsync(userId, request, userId);
            var userRestrictions = await _repository.GetUserRestrictionsAsync(userId);
            var addedRestriction = userRestrictions.FirstOrDefault(ur => ur.Id == userRestrictionId);

            _logger.LogInformation("Dietary restriction {RestrictionId} added to user {UserId}", request.DietaryRestrictionId, userId);

            return CreatedAtAction(nameof(GetMyRestrictions), addedRestriction);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding user dietary restriction");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Update user dietary restriction
    /// </summary>
    [HttpPut("me/{userRestrictionId:guid}")]
    [Authorize]
    public async Task<ActionResult> UpdateMyRestriction(Guid userRestrictionId, [FromBody] UpdateUserDietaryRestrictionRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var success = await _repository.UpdateUserRestrictionAsync(userRestrictionId, request, userId);

            if (!success)
            {
                return NotFound(new { message = "User dietary restriction not found" });
            }

            _logger.LogInformation("User dietary restriction {UserRestrictionId} updated", userRestrictionId);

            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user dietary restriction");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Remove dietary restriction from current user
    /// </summary>
    [HttpDelete("me/{userRestrictionId:guid}")]
    [Authorize]
    public async Task<ActionResult> RemoveMyRestriction(Guid userRestrictionId)
    {
        try
        {
            var userId = GetCurrentUserId();
            var success = await _repository.RemoveUserRestrictionAsync(userRestrictionId, userId);

            if (!success)
            {
                return NotFound(new { message = "User dietary restriction not found" });
            }

            _logger.LogInformation("User dietary restriction {UserRestrictionId} removed", userRestrictionId);

            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing user dietary restriction");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }
}
