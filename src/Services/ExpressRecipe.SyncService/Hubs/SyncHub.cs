using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ExpressRecipe.SyncService.Hubs;

/// <summary>
/// SignalR hub for real-time sync status updates
/// Notifies clients about sync progress, conflicts, and completion
/// </summary>
[Authorize]
public class SyncHub : Hub
{
    private readonly ILogger<SyncHub> _logger;

    public SyncHub(ILogger<SyncHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Called when client connects
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;
        _logger.LogInformation("User {UserId} connected to sync hub with connection {ConnectionId}",
            userId, Context.ConnectionId);

        // Join user to their personal sync group
        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"sync_{userId}");
        }

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when client disconnects
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.UserIdentifier;
        _logger.LogInformation("User {UserId} disconnected from sync hub", userId);

        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"sync_{userId}");
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Client requests current sync status
    /// </summary>
    public async Task RequestSyncStatus()
    {
        var userId = Context.UserIdentifier;
        _logger.LogInformation("User {UserId} requested sync status", userId);

        // This would typically query the sync service for current status
        // For now, we'll just acknowledge the request
        await Clients.Caller.SendAsync("SyncStatusRequested");
    }

    /// <summary>
    /// Client acknowledges sync notification
    /// </summary>
    public async Task AcknowledgeSyncUpdate(Guid syncId)
    {
        var userId = Context.UserIdentifier;
        _logger.LogInformation("User {UserId} acknowledged sync update {SyncId}", userId, syncId);
    }
}

/// <summary>
/// Typed client interface for SyncHub
/// </summary>
public interface ISyncClient
{
    /// <summary>
    /// Notify client that sync has started
    /// </summary>
    Task SyncStarted(SyncStatusUpdate status);

    /// <summary>
    /// Notify client of sync progress
    /// </summary>
    Task SyncProgress(SyncProgressUpdate progress);

    /// <summary>
    /// Notify client that sync has completed
    /// </summary>
    Task SyncCompleted(SyncCompletionUpdate completion);

    /// <summary>
    /// Notify client of sync error
    /// </summary>
    Task SyncError(SyncErrorUpdate error);

    /// <summary>
    /// Notify client of new sync conflict
    /// </summary>
    Task ConflictDetected(SyncConflictUpdate conflict);

    /// <summary>
    /// Notify client that conflict was resolved
    /// </summary>
    Task ConflictResolved(Guid conflictId, string resolution);

    /// <summary>
    /// Update sync status
    /// </summary>
    Task StatusChanged(string status, DateTime timestamp);

    /// <summary>
    /// Acknowledge status request
    /// </summary>
    Task SyncStatusRequested();
}

/// <summary>
/// Sync status update message
/// </summary>
public class SyncStatusUpdate
{
    public Guid SyncId { get; set; }
    public DateTime StartedAt { get; set; }
    public int TotalItems { get; set; }
    public List<string> SyncingEntities { get; set; } = new();
}

/// <summary>
/// Sync progress update message
/// </summary>
public class SyncProgressUpdate
{
    public Guid SyncId { get; set; }
    public int ItemsCompleted { get; set; }
    public int TotalItems { get; set; }
    public decimal PercentComplete { get; set; }
    public string CurrentEntity { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Sync completion update message
/// </summary>
public class SyncCompletionUpdate
{
    public Guid SyncId { get; set; }
    public DateTime CompletedAt { get; set; }
    public int ItemsSynced { get; set; }
    public int ConflictsFound { get; set; }
    public int ConflictsResolved { get; set; }
    public TimeSpan Duration { get; set; }
    public bool Success { get; set; }
}

/// <summary>
/// Sync error update message
/// </summary>
public class SyncErrorUpdate
{
    public Guid SyncId { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public DateTime Timestamp { get; set; }
    public bool IsFatal { get; set; }
}

/// <summary>
/// Sync conflict update message
/// </summary>
public class SyncConflictUpdate
{
    public Guid ConflictId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Field { get; set; } = string.Empty;
    public object? LocalValue { get; set; }
    public object? ServerValue { get; set; }
    public DateTime LocalModifiedAt { get; set; }
    public DateTime ServerModifiedAt { get; set; }
    public DateTime DetectedAt { get; set; }
}

/// <summary>
/// Service for sending sync updates via SignalR
/// </summary>
public class SyncPushService
{
    private readonly IHubContext<SyncHub, ISyncClient> _hubContext;
    private readonly ILogger<SyncPushService> _logger;

    public SyncPushService(
        IHubContext<SyncHub, ISyncClient> hubContext,
        ILogger<SyncPushService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// Notify user that sync has started
    /// </summary>
    public async Task NotifySyncStartedAsync(Guid userId, SyncStatusUpdate status)
    {
        try
        {
            _logger.LogInformation("Notifying user {UserId} that sync {SyncId} started",
                userId, status.SyncId);

            await _hubContext.Clients
                .Group($"sync_{userId}")
                .SyncStarted(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to notify sync started for user {UserId}", userId);
        }
    }

    /// <summary>
    /// Send sync progress update to user
    /// </summary>
    public async Task NotifySyncProgressAsync(Guid userId, SyncProgressUpdate progress)
    {
        try
        {
            await _hubContext.Clients
                .Group($"sync_{userId}")
                .SyncProgress(progress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send sync progress for user {UserId}", userId);
        }
    }

    /// <summary>
    /// Notify user that sync has completed
    /// </summary>
    public async Task NotifySyncCompletedAsync(Guid userId, SyncCompletionUpdate completion)
    {
        try
        {
            _logger.LogInformation("Notifying user {UserId} that sync {SyncId} completed: {ItemsSynced} items",
                userId, completion.SyncId, completion.ItemsSynced);

            await _hubContext.Clients
                .Group($"sync_{userId}")
                .SyncCompleted(completion);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to notify sync completed for user {UserId}", userId);
        }
    }

    /// <summary>
    /// Notify user of sync error
    /// </summary>
    public async Task NotifySyncErrorAsync(Guid userId, SyncErrorUpdate error)
    {
        try
        {
            _logger.LogWarning("Notifying user {UserId} of sync error: {Error}",
                userId, error.ErrorMessage);

            await _hubContext.Clients
                .Group($"sync_{userId}")
                .SyncError(error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to notify sync error for user {UserId}", userId);
        }
    }

    /// <summary>
    /// Notify user of new sync conflict
    /// </summary>
    public async Task NotifyConflictDetectedAsync(Guid userId, SyncConflictUpdate conflict)
    {
        try
        {
            _logger.LogInformation("Notifying user {UserId} of sync conflict {ConflictId}",
                userId, conflict.ConflictId);

            await _hubContext.Clients
                .Group($"sync_{userId}")
                .ConflictDetected(conflict);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to notify conflict for user {UserId}", userId);
        }
    }

    /// <summary>
    /// Notify user that conflict was resolved
    /// </summary>
    public async Task NotifyConflictResolvedAsync(Guid userId, Guid conflictId, string resolution)
    {
        try
        {
            _logger.LogInformation("Notifying user {UserId} that conflict {ConflictId} was resolved with {Resolution}",
                userId, conflictId, resolution);

            await _hubContext.Clients
                .Group($"sync_{userId}")
                .ConflictResolved(conflictId, resolution);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to notify conflict resolved for user {UserId}", userId);
        }
    }

    /// <summary>
    /// Broadcast status change to user
    /// </summary>
    public async Task NotifyStatusChangedAsync(Guid userId, string status)
    {
        try
        {
            await _hubContext.Clients
                .Group($"sync_{userId}")
                .StatusChanged(status, DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to notify status change for user {UserId}", userId);
        }
    }
}
