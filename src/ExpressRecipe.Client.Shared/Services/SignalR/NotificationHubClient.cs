using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace ExpressRecipe.Client.Shared.Services.SignalR;

/// <summary>
/// SignalR client for real-time notifications
/// </summary>
public class NotificationHubClient : IAsyncDisposable
{
    private readonly HubConnection _connection;
    private readonly ILogger<NotificationHubClient> _logger;
    private readonly IToastService _toastService;

    public event Action<NotificationMessage>? OnNotificationReceived;
    public event Action<Guid>? OnNotificationRead;
    public event Action? OnAllNotificationsRead;
    public event Action<int>? OnUnreadCountUpdated;

    public bool IsConnected => _connection.State == HubConnectionState.Connected;
    public HubConnectionState State => _connection.State;

    public NotificationHubClient(
        string hubUrl,
        string? accessToken,
        ILogger<NotificationHubClient> logger,
        IToastService toastService)
    {
        _logger = logger;
        _toastService = toastService;

        _connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                if (!string.IsNullOrEmpty(accessToken))
                {
                    options.AccessTokenProvider = () => Task.FromResult(accessToken)!;
                }
            })
            .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10) })
            .ConfigureLogging(logging =>
            {
                logging.SetMinimumLevel(LogLevel.Information);
            })
            .Build();

        ConfigureHandlers();
    }

    private void ConfigureHandlers()
    {
        // Handle incoming notifications
        _connection.On<NotificationMessage>("ReceiveNotification", notification =>
        {
            _logger.LogInformation("Received notification: {Title}", notification.Title);

            OnNotificationReceived?.Invoke(notification);

            // Show toast based on severity
            switch (notification.Severity.ToLower())
            {
                case "error":
                    _toastService.ShowError(notification.Title, notification.Message);
                    break;
                case "warning":
                    _toastService.ShowWarning(notification.Title, notification.Message);
                    break;
                case "success":
                    _toastService.ShowSuccess(notification.Title, notification.Message);
                    break;
                default:
                    _toastService.ShowInfo(notification.Title, notification.Message);
                    break;
            }
        });

        // Handle notification read
        _connection.On<Guid>("NotificationRead", notificationId =>
        {
            _logger.LogInformation("Notification marked as read: {NotificationId}", notificationId);
            OnNotificationRead?.Invoke(notificationId);
        });

        // Handle all notifications read
        _connection.On("AllNotificationsRead", () =>
        {
            _logger.LogInformation("All notifications marked as read");
            OnAllNotificationsRead?.Invoke();
        });

        // Handle unread count update
        _connection.On<int>("UnreadCountUpdated", count =>
        {
            _logger.LogInformation("Unread count updated: {Count}", count);
            OnUnreadCountUpdated?.Invoke(count);
        });

        // Handle reconnection
        _connection.Reconnecting += error =>
        {
            _logger.LogWarning("Notification hub reconnecting: {Error}", error?.Message);
            return Task.CompletedTask;
        };

        _connection.Reconnected += connectionId =>
        {
            _logger.LogInformation("Notification hub reconnected: {ConnectionId}", connectionId);
            return Task.CompletedTask;
        };

        _connection.Closed += error =>
        {
            _logger.LogError("Notification hub connection closed: {Error}", error?.Message);
            return Task.CompletedTask;
        };
    }

    /// <summary>
    /// Start connection to notification hub
    /// </summary>
    public async Task StartAsync()
    {
        try
        {
            if (_connection.State == HubConnectionState.Disconnected)
            {
                _logger.LogInformation("Starting notification hub connection");
                await _connection.StartAsync();
                _logger.LogInformation("Notification hub connected");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start notification hub connection");
            throw;
        }
    }

    /// <summary>
    /// Stop connection to notification hub
    /// </summary>
    public async Task StopAsync()
    {
        try
        {
            if (_connection.State == HubConnectionState.Connected)
            {
                _logger.LogInformation("Stopping notification hub connection");
                await _connection.StopAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop notification hub connection");
        }
    }

    /// <summary>
    /// Join a notification channel
    /// </summary>
    public async Task JoinChannelAsync(string channelName)
    {
        try
        {
            await _connection.InvokeAsync("JoinChannel", channelName);
            _logger.LogInformation("Joined channel: {Channel}", channelName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to join channel: {Channel}", channelName);
        }
    }

    /// <summary>
    /// Leave a notification channel
    /// </summary>
    public async Task LeaveChannelAsync(string channelName)
    {
        try
        {
            await _connection.InvokeAsync("LeaveChannel", channelName);
            _logger.LogInformation("Left channel: {Channel}", channelName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to leave channel: {Channel}", channelName);
        }
    }

    /// <summary>
    /// Mark notification as read
    /// </summary>
    public async Task MarkAsReadAsync(Guid notificationId)
    {
        try
        {
            await _connection.InvokeAsync("MarkAsRead", notificationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark notification as read: {NotificationId}", notificationId);
        }
    }

    /// <summary>
    /// Mark all notifications as read
    /// </summary>
    public async Task MarkAllAsReadAsync()
    {
        try
        {
            await _connection.InvokeAsync("MarkAllAsRead");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark all notifications as read");
        }
    }

    /// <summary>
    /// Ping the hub to check connection
    /// </summary>
    public async Task PingAsync()
    {
        try
        {
            await _connection.InvokeAsync("Ping");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ping notification hub");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        await _connection.DisposeAsync();
    }
}

/// <summary>
/// Notification message from SignalR
/// </summary>
public class NotificationMessage
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = "Info";
    public Dictionary<string, string> Data { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public string? ActionUrl { get; set; }
    public string? ActionText { get; set; }
    public string? ImageUrl { get; set; }
}
