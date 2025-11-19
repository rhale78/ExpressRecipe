using ExpressRecipe.Shared.DTOs.User;
using ExpressRecipe.UserService.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ExpressRecipe.UserService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UserProfileController : ControllerBase
{
    private readonly IUserProfileRepository _repository;
    private readonly ILogger<UserProfileController> _logger;

    public UserProfileController(
        IUserProfileRepository repository,
        ILogger<UserProfileController> logger)
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
    /// Get current user's profile
    /// </summary>
    [HttpGet("me")]
    public async Task<ActionResult<UserProfileDto>> GetMyProfile()
    {
        try
        {
            var userId = GetCurrentUserId();
            var profile = await _repository.GetByUserIdAsync(userId);

            if (profile == null)
            {
                return NotFound(new { message = "Profile not found" });
            }

            return Ok(profile);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user profile");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Get user profile by user ID
    /// </summary>
    [HttpGet("user/{userId:guid}")]
    public async Task<ActionResult<UserProfileDto>> GetByUserId(Guid userId)
    {
        try
        {
            var profile = await _repository.GetByUserIdAsync(userId);

            if (profile == null)
            {
                return NotFound(new { message = "Profile not found" });
            }

            return Ok(profile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user profile for user {UserId}", userId);
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Create a new user profile
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<UserProfileDto>> Create([FromBody] CreateUserProfileRequest request)
    {
        try
        {
            var currentUserId = GetCurrentUserId();

            // Users can only create their own profile
            if (request.UserId != currentUserId)
            {
                return Forbid();
            }

            // Check if profile already exists
            if (await _repository.UserProfileExistsAsync(request.UserId))
            {
                return Conflict(new { message = "Profile already exists for this user" });
            }

            var profileId = await _repository.CreateAsync(request, currentUserId);
            var profile = await _repository.GetByUserIdAsync(request.UserId);

            _logger.LogInformation("User profile created for user {UserId}", request.UserId);

            return CreatedAtAction(nameof(GetByUserId), new { userId = request.UserId }, profile);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user profile");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Update current user's profile
    /// </summary>
    [HttpPut("me")]
    public async Task<ActionResult<UserProfileDto>> UpdateMyProfile([FromBody] UpdateUserProfileRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var success = await _repository.UpdateAsync(userId, request, userId);

            if (!success)
            {
                return NotFound(new { message = "Profile not found" });
            }

            var profile = await _repository.GetByUserIdAsync(userId);
            _logger.LogInformation("User profile updated for user {UserId}", userId);

            return Ok(profile);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user profile");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Delete current user's profile
    /// </summary>
    [HttpDelete("me")]
    public async Task<ActionResult> DeleteMyProfile()
    {
        try
        {
            var userId = GetCurrentUserId();
            var success = await _repository.DeleteAsync(userId, userId);

            if (!success)
            {
                return NotFound(new { message = "Profile not found" });
            }

            _logger.LogInformation("User profile deleted for user {UserId}", userId);

            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user profile");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }
}
