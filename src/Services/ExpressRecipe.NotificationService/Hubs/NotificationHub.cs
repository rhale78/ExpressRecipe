using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace ExpressRecipe.NotificationService.Hubs;

/// <summary>
/// SignalR hub for real-time notification delivery
/// </summary>
[Authorize]
public class NotificationHub : Hub
{
    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(ILogger<NotificationHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!string.IsNullOrEmpty(userId))
        {
            // Add user to their personal notification group
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");
            _logger.LogInformation("User {UserId} connected to NotificationHub with connection {ConnectionId}",
                userId, Context.ConnectionId);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!string.IsNullOrEmpty(userId))
        {
            _logger.LogInformation("User {UserId} disconnected from NotificationHub", userId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Client can mark a notification as read
    /// </summary>
    public async Task MarkAsRead(Guid notificationId)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        _logger.LogInformation("User {UserId} marked notification {NotificationId} as read",
            userId, notificationId);

        // Broadcast to all client connections for this user
        if (!string.IsNullOrEmpty(userId))
        {
            await Clients.Group($"user:{userId}").SendAsync("NotificationRead", notificationId);
        }
    }

    /// <summary>
    /// Client can request unread count
    /// </summary>
    public async Task RequestUnreadCount()
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        _logger.LogDebug("User {UserId} requested unread count", userId);

        // The caller would need to inject INotificationRepository to get actual count
        // For now, acknowledge the request
        await Clients.Caller.SendAsync("UnreadCountRequested");
    }
}

/// <summary>
/// Strongly-typed hub client interface
/// </summary>
public interface INotificationClient
{
    /// <summary>
    /// Receive a new notification
    /// </summary>
    Task ReceiveNotification(NotificationDto notification);

    /// <summary>
    /// Notification was marked as read
    /// </summary>
    Task NotificationRead(Guid notificationId);

    /// <summary>
    /// Update unread notification count
    /// </summary>
    Task UnreadCountUpdated(int count);
}

/// <summary>
/// DTO for real-time notification delivery
/// </summary>
public class NotificationDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? ActionUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsRead { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}
