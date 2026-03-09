using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ExpressRecipe.NotificationService.Data;

namespace ExpressRecipe.NotificationService.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class NotificationController : ControllerBase
{
    private readonly ILogger<NotificationController> _logger;
    private readonly INotificationRepository _repository;

    public NotificationController(ILogger<NotificationController> logger, INotificationRepository repository)
    {
        _logger = logger;
        _repository = repository;
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    [HttpGet]
    public async Task<IActionResult> GetNotifications([FromQuery] bool unreadOnly = false, [FromQuery] int limit = 50)
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();
            var notifications = await _repository.GetUserNotificationsAsync(userId.Value, unreadOnly, limit);
            return Ok(notifications);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving notifications");
            return StatusCode(500, new { message = "An error occurred while retrieving notifications" });
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetNotification(Guid id)
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();
            var notification = await _repository.GetNotificationAsync(id);
            if (notification == null) return NotFound();
            if (notification.UserId != userId.Value) return Forbid();
            return Ok(notification);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving notification {NotificationId}", id);
            return StatusCode(500, new { message = "An error occurred while retrieving the notification" });
        }
    }

    [HttpPut("{id}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();
            var notification = await _repository.GetNotificationAsync(id);
            if (notification == null) return NotFound();
            if (notification.UserId != userId.Value) return Forbid();
            await _repository.MarkAsReadAsync(id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking notification {NotificationId} as read", id);
            return StatusCode(500, new { message = "An error occurred while marking the notification as read" });
        }
    }

    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();
            await _repository.MarkAllAsReadAsync(userId.Value);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking all notifications as read");
            return StatusCode(500, new { message = "An error occurred while marking notifications as read" });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteNotification(Guid id)
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();
            var notification = await _repository.GetNotificationAsync(id);
            if (notification == null) return NotFound();
            if (notification.UserId != userId.Value) return Forbid();
            await _repository.DeleteNotificationAsync(id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting notification {NotificationId}", id);
            return StatusCode(500, new { message = "An error occurred while deleting the notification" });
        }
    }

    [HttpGet("unread/count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();
            var count = await _repository.GetUnreadCountAsync(userId.Value);
            return Ok(new { count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving unread notification count");
            return StatusCode(500, new { message = "An error occurred while retrieving the unread count" });
        }
    }

    [HttpGet("preferences")]
    public async Task<IActionResult> GetPreferences()
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();
            var prefs = await _repository.GetUserPreferencesAsync(userId.Value);
            return Ok(prefs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving notification preferences");
            return StatusCode(500, new { message = "An error occurred while retrieving preferences" });
        }
    }

    [HttpPost("preferences")]
    public async Task<IActionResult> SavePreference([FromBody] SavePreferenceRequest request)
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();
            var prefId = await _repository.SavePreferenceAsync(
                userId.Value, request.NotificationType, request.EmailEnabled, request.PushEnabled, request.SmsEnabled, request.InAppEnabled);
            return Ok(new { id = prefId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving notification preference");
            return StatusCode(500, new { message = "An error occurred while saving the preference" });
        }
    }

    /// <summary>
    /// Service-to-service endpoint for creating in-app notifications from other microservices.
    /// Does not require end-user authentication.
    /// </summary>
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    [HttpPost("internal")]
    public async Task<IActionResult> CreateInternal([FromBody] InternalNotificationRequest request)
    {
        try
        {
            if (request.UserId == Guid.Empty)
                return BadRequest(new { error = "UserId is required" });

            string? actionUrl = request.RelatedEntityId.HasValue
                ? $"/{request.RelatedEntityType?.ToLowerInvariant()}/{request.RelatedEntityId}"
                : null;

            Dictionary<string, string>? metadata = null;
            if (request.RelatedEntityType is not null && request.RelatedEntityId.HasValue)
            {
                metadata = new Dictionary<string, string>
                {
                    ["relatedEntityType"] = request.RelatedEntityType,
                    ["relatedEntityId"]   = request.RelatedEntityId.Value.ToString()
                };
            }

            Guid notificationId = await _repository.CreateNotificationAsync(
                request.UserId, request.Type, request.Title, request.Message, actionUrl, metadata);

            return Ok(new { id = notificationId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating internal notification for user {UserId}", request.UserId);
            return StatusCode(500, new { message = "An error occurred while creating the notification" });
        }
    }
}

public class SavePreferenceRequest
{
    public string NotificationType { get; set; } = string.Empty;
    public bool EmailEnabled { get; set; }
    public bool PushEnabled { get; set; }
    public bool SmsEnabled { get; set; }
    public bool InAppEnabled { get; set; }
}

/// <summary>Request body for the service-to-service internal notification endpoint.</summary>
public class InternalNotificationRequest
{
    public Guid UserId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? RelatedEntityType { get; set; }
    public Guid? RelatedEntityId { get; set; }
}
