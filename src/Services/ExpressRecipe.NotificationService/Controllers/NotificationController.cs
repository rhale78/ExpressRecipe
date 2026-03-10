using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ExpressRecipe.NotificationService.Data;

namespace ExpressRecipe.NotificationService.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class NotificationController : ControllerBase
{
    private readonly ILogger<NotificationController> _logger;
    private readonly INotificationRepository _repository;

    private readonly IConfiguration _configuration;

    public NotificationController(ILogger<NotificationController> logger, INotificationRepository repository,
        IConfiguration configuration)
    {
        _logger        = logger;
        _repository    = repository;
        _configuration = configuration;
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

    // Allowlist of valid entity types for action URL construction.
    private static readonly HashSet<string> AllowedEntityTypes =
        new(StringComparer.OrdinalIgnoreCase) { "PlannedMeal", "InventoryItem", "Recipe", "CookingTimer" };

    /// <summary>
    /// Service-to-service endpoint for creating in-app notifications from other microservices.
    /// Protected by an API key header (X-Internal-Api-Key) when InternalApi:Key is configured.
    /// </summary>
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    [HttpPost("internal")]
    public async Task<IActionResult> CreateInternal([FromBody] InternalNotificationRequest request)
    {
        // Validate service-to-service API key when one is configured.
        string? configuredKey = _configuration["InternalApi:Key"];
        if (!string.IsNullOrEmpty(configuredKey))
        {
            string? providedKey = Request.Headers["X-Internal-Api-Key"].FirstOrDefault();
            if (!IsValidApiKey(providedKey, configuredKey))
            {
                return Unauthorized(new { error = "Invalid or missing X-Internal-Api-Key header" });
            }
        }

        try
        {
            if (request.UserId == Guid.Empty)
                return BadRequest(new { error = "UserId is required" });

            // Build action URL — prefer an explicit ActionUrl, fall back to entity-type routing.
            string? actionUrl = request.ActionUrl;
            Dictionary<string, string>? metadata = null;

            if (!string.IsNullOrEmpty(request.Priority)
                || !string.IsNullOrEmpty(request.RelatedEntityType)
                || request.RelatedEntityId.HasValue)
            {
                metadata = new Dictionary<string, string>();
                if (!string.IsNullOrEmpty(request.Priority))
                    metadata["priority"] = request.Priority;
                if (!string.IsNullOrEmpty(request.RelatedEntityType))
                {
                    if (!AllowedEntityTypes.Contains(request.RelatedEntityType))
                        return BadRequest(new { error = "Invalid RelatedEntityType" });
                    metadata["relatedEntityType"] = request.RelatedEntityType;
                    if (actionUrl == null && request.RelatedEntityId.HasValue)
                        actionUrl = $"/{request.RelatedEntityType.ToLowerInvariant()}/{request.RelatedEntityId}";
                }
                if (request.RelatedEntityId.HasValue)
                    metadata["relatedEntityId"] = request.RelatedEntityId.Value.ToString();
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


    // Constant-time comparison to guard against timing attacks when comparing API keys.
    private static bool IsValidApiKey(string? provided, string configured)
    {
        if (provided is null) { return false; }
        byte[] a = Encoding.UTF8.GetBytes(provided);
        byte[] b = Encoding.UTF8.GetBytes(configured);
        // Pad the shorter array so both buffers are equal length before comparing.
        if (a.Length != b.Length)
        {
            byte[] padded = new byte[Math.Max(a.Length, b.Length)];
            Buffer.BlockCopy(a.Length < b.Length ? a : b, 0, padded, 0, Math.Min(a.Length, b.Length));
            if (a.Length < b.Length) { a = padded; } else { b = padded; }
        }
        return CryptographicOperations.FixedTimeEquals(a, b);
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
    public string Priority { get; set; } = "Normal";
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? ActionUrl { get; set; }
    public string? RelatedEntityType { get; set; }
    public Guid? RelatedEntityId { get; set; }
}
