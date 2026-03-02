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

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetNotifications([FromQuery] bool unreadOnly = false, [FromQuery] int limit = 50)
    {
        var userId = GetUserId();
        var notifications = await _repository.GetUserNotificationsAsync(userId, unreadOnly, limit);
        return Ok(notifications);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetNotification(Guid id)
    {
        var notification = await _repository.GetNotificationAsync(id);
        if (notification == null) return NotFound();
        return Ok(notification);
    }

    [HttpPut("{id}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        await _repository.MarkAsReadAsync(id);
        return NoContent();
    }

    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var userId = GetUserId();
        await _repository.MarkAllAsReadAsync(userId);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteNotification(Guid id)
    {
        await _repository.DeleteNotificationAsync(id);
        return NoContent();
    }

    [HttpGet("unread/count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var userId = GetUserId();
        var count = await _repository.GetUnreadCountAsync(userId);
        return Ok(new { count });
    }

    [HttpGet("preferences")]
    public async Task<IActionResult> GetPreferences()
    {
        var userId = GetUserId();
        var prefs = await _repository.GetUserPreferencesAsync(userId);
        return Ok(prefs);
    }

    [HttpPost("preferences")]
    public async Task<IActionResult> SavePreference([FromBody] SavePreferenceRequest request)
    {
        var userId = GetUserId();
        var prefId = await _repository.SavePreferenceAsync(
            userId, request.NotificationType, request.EmailEnabled, request.PushEnabled, request.SmsEnabled, request.InAppEnabled);
        return Ok(new { id = prefId });
    }

    /// <summary>
    /// Send an email notification (internal service-to-service endpoint)
    /// </summary>
    [AllowAnonymous]  // Allow internal service calls
    [HttpPost("send-email")]
    public async Task<IActionResult> SendEmail([FromBody] SendEmailRequest request)
    {
        try
        {
            _logger.LogInformation("Email send request received for: {Email}", request.ToEmail);

            // TODO: Implement actual email sending logic using SMTP or email service provider
            // For now, just log the request
            _logger.LogInformation("Sending email to {Email} with subject '{Subject}' using template '{Template}'", 
                request.ToEmail, request.Subject, request.TemplateName);

            // Simulate email sending
            await Task.Delay(100);

            return Ok(new { message = "Email queued for sending", recipientEmail = request.ToEmail });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}", request.ToEmail);
            return StatusCode(500, new { message = "Failed to send email" });
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

public class SendEmailRequest
{
    public string ToEmail { get; set; } = string.Empty;
    public string ToName { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public object? TemplateData { get; set; }
}
