using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ExpressRecipe.NotificationService.Hubs;

/// <summary>
/// SignalR hub for real-time notifications
/// Allows clients to receive instant notifications without polling
/// </summary>
[Authorize]
public class NotificationHub : Hub
{
    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(ILogger<NotificationHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Called when client connects
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;
        _logger.LogInformation("User {UserId} connected to notification hub with connection {ConnectionId}",
            userId, Context.ConnectionId);

        // Join user to their personal group for targeted notifications
        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
            _logger.LogInformation("Added connection {ConnectionId} to group user_{UserId}",
                Context.ConnectionId, userId);
        }

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when client disconnects
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.UserIdentifier;
        _logger.LogInformation("User {UserId} disconnected from notification hub. Exception: {Exception}",
            userId, exception?.Message);

        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userId}");
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Client requests to join a notification channel
    /// </summary>
    public async Task JoinChannel(string channelName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"channel_{channelName}");
        _logger.LogInformation("Connection {ConnectionId} joined channel {Channel}",
            Context.ConnectionId, channelName);
    }

    /// <summary>
    /// Client requests to leave a notification channel
    /// </summary>
    public async Task LeaveChannel(string channelName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"channel_{channelName}");
        _logger.LogInformation("Connection {ConnectionId} left channel {Channel}",
            Context.ConnectionId, channelName);
    }

    /// <summary>
    /// Client marks notification as read
    /// </summary>
    public async Task MarkAsRead(Guid notificationId)
    {
        var userId = Context.UserIdentifier;
        _logger.LogInformation("User {UserId} marked notification {NotificationId} as read",
            userId, notificationId);

        // Notify other connected clients of same user
        await Clients.User(userId!).SendAsync("NotificationRead", notificationId);
    }

    /// <summary>
    /// Client marks all notifications as read
    /// </summary>
    public async Task MarkAllAsRead()
    {
        var userId = Context.UserIdentifier;
        _logger.LogInformation("User {UserId} marked all notifications as read", userId);

        // Notify other connected clients of same user
        await Clients.User(userId!).SendAsync("AllNotificationsRead");
    }

    /// <summary>
    /// Get connection status
    /// </summary>
    public async Task Ping()
    {
        await Clients.Caller.SendAsync("Pong", DateTime.UtcNow);
    }
}

/// <summary>
/// Typed client interface for NotificationHub
/// Use this for sending notifications from server side
/// </summary>
public interface INotificationClient
{
    /// <summary>
    /// Send new notification to client
    /// </summary>
    Task ReceiveNotification(NotificationMessage notification);

    /// <summary>
    /// Notify client that a notification was read
    /// </summary>
    Task NotificationRead(Guid notificationId);

    /// <summary>
    /// Notify client that all notifications were read
    /// </summary>
    Task AllNotificationsRead();

    /// <summary>
    /// Send unread count update
    /// </summary>
    Task UnreadCountUpdated(int count);

    /// <summary>
    /// Respond to ping
    /// </summary>
    Task Pong(DateTime serverTime);
}

/// <summary>
/// Notification message structure for real-time push
/// </summary>
public class NotificationMessage
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = "Info"; // Info, Warning, Error, Success
    public Dictionary<string, string> Data { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public string? ActionUrl { get; set; }
    public string? ActionText { get; set; }
    public string? ImageUrl { get; set; }
}

/// <summary>
/// Service for sending notifications via SignalR
/// </summary>
public class NotificationPushService
{
    private readonly IHubContext<NotificationHub, INotificationClient> _hubContext;
    private readonly ILogger<NotificationPushService> _logger;

    public NotificationPushService(
        IHubContext<NotificationHub, INotificationClient> hubContext,
        ILogger<NotificationPushService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// Send notification to specific user
    /// </summary>
    public async Task SendToUserAsync(Guid userId, NotificationMessage notification)
    {
        try
        {
            _logger.LogInformation("Sending notification {NotificationId} to user {UserId}",
                notification.Id, userId);

            await _hubContext.Clients
                .Group($"user_{userId}")
                .ReceiveNotification(notification);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send notification to user {UserId}", userId);
        }
    }

    /// <summary>
    /// Send notification to all users in a channel
    /// </summary>
    public async Task SendToChannelAsync(string channelName, NotificationMessage notification)
    {
        try
        {
            _logger.LogInformation("Sending notification {NotificationId} to channel {Channel}",
                notification.Id, channelName);

            await _hubContext.Clients
                .Group($"channel_{channelName}")
                .ReceiveNotification(notification);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send notification to channel {Channel}", channelName);
        }
    }

    /// <summary>
    /// Send notification to all connected users (broadcast)
    /// </summary>
    public async Task BroadcastAsync(NotificationMessage notification)
    {
        try
        {
            _logger.LogInformation("Broadcasting notification {NotificationId} to all users",
                notification.Id);

            await _hubContext.Clients.All.ReceiveNotification(notification);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast notification");
        }
    }

    /// <summary>
    /// Update unread count for user
    /// </summary>
    public async Task UpdateUnreadCountAsync(Guid userId, int count)
    {
        try
        {
            await _hubContext.Clients
                .Group($"user_{userId}")
                .UnreadCountUpdated(count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update unread count for user {UserId}", userId);
        }
    }
}
