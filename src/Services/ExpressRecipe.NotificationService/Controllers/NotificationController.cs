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

    [HttpPut("preferences")]
    public async Task<IActionResult> UpdatePreferences([FromBody] UpdatePreferencesRequest request)
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            // Save each notification type preference
            foreach (var pref in request.Preferences)
            {
                await _repository.SavePreferenceAsync(
                    userId.Value, pref.NotificationType, pref.EmailEnabled, pref.PushEnabled, pref.SmsEnabled, pref.InAppEnabled);
            }
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating notification preferences");
            return StatusCode(500, new { message = "An error occurred while updating preferences" });
        }
    }

    [HttpPost("search")]
    public async Task<IActionResult> SearchNotifications([FromBody] NotificationSearchRequest request)
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var notifications = await _repository.GetUserNotificationsAsync(
                userId.Value, request.IsRead.HasValue && !request.IsRead.Value, request.PageSize);

            // Apply type filter in memory (simple approach until a DB-level filter is warranted)
            if (!string.IsNullOrEmpty(request.Type))
                notifications = notifications.Where(n => n.Type == request.Type).ToList();

            var total = notifications.Count;
            var page = request.Page > 0 ? request.Page : 1;
            var pageSize = request.PageSize > 0 ? request.PageSize : 50;
            var paged = notifications.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            var unreadCount = notifications.Count(n => !n.IsRead);

            return Ok(new { Notifications = paged, TotalCount = total, UnreadCount = unreadCount, Page = page, PageSize = pageSize });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching notifications");
            return StatusCode(500, new { message = "An error occurred while searching notifications" });
        }
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var notifications = await _repository.GetUserNotificationsAsync(userId.Value, false, 500);
            var unreadCount = await _repository.GetUnreadCountAsync(userId.Value);
            var byType = notifications.GroupBy(n => n.Type)
                                      .ToDictionary(g => g.Key, g => g.Count());

            return Ok(new
            {
                TotalNotifications = notifications.Count,
                UnreadNotifications = unreadCount,
                HighPriorityUnread = 0, // Priority not currently in the data model
                NotificationsByType = byType
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving notification summary");
            return StatusCode(500, new { message = "An error occurred while retrieving the summary" });
        }
    }

    [HttpDelete("delete-all-read")]
    public async Task<IActionResult> DeleteAllRead()
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();
            await _repository.DeleteAllReadAsync(userId.Value);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting read notifications");
            return StatusCode(500, new { message = "An error occurred while deleting read notifications" });
        }
    }

    [HttpPost("mark-read")]
    public async Task<IActionResult> MarkNotificationRead([FromBody] MarkNotificationReadRequest request)
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();
            var notification = await _repository.GetNotificationAsync(request.NotificationId);
            if (notification == null) return NotFound();
            if (notification.UserId != userId.Value) return Forbid();
            if (request.IsRead)
                await _repository.MarkAsReadAsync(request.NotificationId);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking notification as read");
            return StatusCode(500, new { message = "An error occurred while marking the notification" });
        }
    }

    [HttpPost("mark-all-read")]
    public async Task<IActionResult> MarkAllNotificationsRead()
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
            return StatusCode(500, new { message = "An error occurred while marking all notifications as read" });
        }
    }

    [HttpPost("generate-expiring")]
    public async Task<IActionResult> GenerateExpiringItemNotifications()
    {
        // Expiring item notifications are generated automatically by the ExpirationAlertWorker
        // This endpoint allows manual triggering for testing purposes
        _logger.LogInformation("Manual expiring item notification generation requested");
        return Accepted(new { message = "Expiring item notification generation is handled automatically by the background worker" });
    }

    [HttpPost("generate-low-stock")]
    public async Task<IActionResult> GenerateLowStockNotifications()
    {
        // Low stock notifications are generated by the InventoryService or triggered by events
        _logger.LogInformation("Manual low-stock notification generation requested");
        return Accepted(new { message = "Low stock notification generation is handled automatically by background workers" });
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

public class UpdatePreferencesRequest
{
    public List<SavePreferenceRequest> Preferences { get; set; } = new();
}

public class NotificationSearchRequest
{
    public string? Type { get; set; }
    public bool? IsRead { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public class MarkNotificationReadRequest
{
    public Guid NotificationId { get; set; }
    public bool IsRead { get; set; } = true;
}
