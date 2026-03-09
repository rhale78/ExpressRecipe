using ExpressRecipe.UserService.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ExpressRecipe.UserService.Controllers;

[ApiController]
[Route("api/users/settings")]
[Authorize]
public class UserSettingsController : ControllerBase
{
    private readonly IUserSettingsRepository _settingsRepo;
    private readonly ILogger<UserSettingsController> _logger;

    public UserSettingsController(
        IUserSettingsRepository settingsRepo,
        ILogger<UserSettingsController> logger)
    {
        _settingsRepo = settingsRepo;
        _logger = logger;
    }

    private Guid GetCurrentUserId()
    {
        string? userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
        {
            throw new UnauthorizedAccessException("Invalid user ID in token");
        }
        return userId;
    }

    /// <summary>
    /// Gets settings values for the current user and group.
    /// </summary>
    [HttpGet("{group}")]
    public async Task<IActionResult> GetSettings(string group, CancellationToken ct)
    {
        try
        {
            Guid userId = GetCurrentUserId();
            Dictionary<string, object?> values = await _settingsRepo.GetAsync(userId, group, ct);
            return Ok(values);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving settings for group {Group}", group);
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Updates settings values for the current user and group.
    /// </summary>
    [HttpPut("{group}")]
    public async Task<IActionResult> UpdateSettings(
        string group,
        [FromBody] Dictionary<string, object?> values,
        CancellationToken ct)
    {
        try
        {
            Guid userId = GetCurrentUserId();
            List<string> errors = await _settingsRepo.ValidateAsync(group, values, ct);
            if (errors.Count > 0) { return BadRequest(new { errors }); }
            await _settingsRepo.UpsertAsync(userId, group, values, ct);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating settings for group {Group}", group);
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Resets settings for the current user and group to schema defaults.
    /// </summary>
    [HttpDelete("{group}")]
    public async Task<IActionResult> ResetSettings(string group, CancellationToken ct)
    {
        try
        {
            Guid userId = GetCurrentUserId();
            await _settingsRepo.DeleteAsync(userId, group, ct);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting settings for group {Group}", group);
            return StatusCode(500, new { message = "An error occurred" });
        }
    }
}
