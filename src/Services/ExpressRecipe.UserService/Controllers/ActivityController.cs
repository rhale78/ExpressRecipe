using ExpressRecipe.Shared.DTOs.User;
using ExpressRecipe.UserService.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ExpressRecipe.UserService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ActivityController : ControllerBase
{
    private readonly IActivityRepository _activityRepository;
    private readonly ILogger<ActivityController> _logger;

    public ActivityController(
        IActivityRepository activityRepository,
        ILogger<ActivityController> logger)
    {
        _activityRepository = activityRepository;
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
    /// Get user's activity history
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<UserActivityDto>>> GetActivity(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            if (pageNumber < 1 || pageSize < 1 || pageSize > 100)
            {
                return BadRequest(new { message = "Invalid pagination parameters" });
            }

            var activities = await _activityRepository.GetUserActivityAsync(userId.Value, pageNumber, pageSize);
            return Ok(activities);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user activity");
            return StatusCode(500, new { message = "An error occurred while retrieving activity" });
        }
    }

    /// <summary>
    /// Get recent activity (last N days)
    /// </summary>
    [HttpGet("recent")]
    public async Task<ActionResult<List<UserActivityDto>>> GetRecentActivity([FromQuery] int days = 7)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            if (days < 1 || days > 365)
            {
                return BadRequest(new { message = "Days must be between 1 and 365" });
            }

            var activities = await _activityRepository.GetRecentActivityAsync(userId.Value, days);
            return Ok(activities);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving recent activity");
            return StatusCode(500, new { message = "An error occurred while retrieving recent activity" });
        }
    }

    /// <summary>
    /// Get activity by type
    /// </summary>
    [HttpGet("type/{activityType}")]
    public async Task<ActionResult<List<UserActivityDto>>> GetActivityByType(string activityType)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var activities = await _activityRepository.GetActivityByTypeAsync(userId.Value, activityType);
            return Ok(activities);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving activity by type {ActivityType}", activityType);
            return StatusCode(500, new { message = "An error occurred while retrieving activity" });
        }
    }

    /// <summary>
    /// Get activity summary
    /// </summary>
    [HttpGet("summary")]
    public async Task<ActionResult<UserActivitySummaryDto>> GetSummary(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var summary = await _activityRepository.GetActivitySummaryAsync(userId.Value, startDate, endDate);
            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving activity summary");
            return StatusCode(500, new { message = "An error occurred while retrieving activity summary" });
        }
    }

    /// <summary>
    /// Get activity counts by type
    /// </summary>
    [HttpGet("counts")]
    public async Task<ActionResult<Dictionary<string, int>>> GetActivityCounts([FromQuery] int days = 30)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            if (days < 1 || days > 365)
            {
                return BadRequest(new { message = "Days must be between 1 and 365" });
            }

            var counts = await _activityRepository.GetActivityCountsByTypeAsync(userId.Value, days);
            return Ok(counts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving activity counts");
            return StatusCode(500, new { message = "An error occurred while retrieving activity counts" });
        }
    }

    /// <summary>
    /// Get current streak
    /// </summary>
    [HttpGet("streak/current")]
    public async Task<ActionResult<int>> GetCurrentStreak()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var streak = await _activityRepository.GetCurrentStreakAsync(userId.Value);
            return Ok(new { currentStreak = streak });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving current streak");
            return StatusCode(500, new { message = "An error occurred while retrieving streak" });
        }
    }

    /// <summary>
    /// Get longest streak
    /// </summary>
    [HttpGet("streak/longest")]
    public async Task<ActionResult<int>> GetLongestStreak()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var streak = await _activityRepository.GetLongestStreakAsync(userId.Value);
            return Ok(new { longestStreak = streak });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving longest streak");
            return StatusCode(500, new { message = "An error occurred while retrieving streak" });
        }
    }

    /// <summary>
    /// Check if user has activity today
    /// </summary>
    [HttpGet("today")]
    public async Task<ActionResult<bool>> HasActivityToday()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var hasActivity = await _activityRepository.HasActivityTodayAsync(userId.Value);
            return Ok(new { hasActivityToday = hasActivity });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking today's activity");
            return StatusCode(500, new { message = "An error occurred while checking activity" });
        }
    }

    /// <summary>
    /// Log user activity
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<Guid>> LogActivity([FromBody] LogActivityRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            // Capture device/IP from request if not provided
            if (string.IsNullOrEmpty(request.DeviceType))
            {
                request.DeviceType = Request.Headers.UserAgent.ToString();
            }

            if (string.IsNullOrEmpty(request.IPAddress))
            {
                request.IPAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            }

            var activityId = await _activityRepository.LogActivityAsync(userId.Value, request);

            return Ok(new { id = activityId, message = "Activity logged successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging activity");
            return StatusCode(500, new { message = "An error occurred while logging activity" });
        }
    }
}
