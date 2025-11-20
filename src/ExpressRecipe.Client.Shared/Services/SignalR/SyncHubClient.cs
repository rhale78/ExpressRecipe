using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace ExpressRecipe.Client.Shared.Services.SignalR;

/// <summary>
/// SignalR client for real-time sync updates
/// </summary>
public class SyncHubClient : IAsyncDisposable
{
    private readonly HubConnection _connection;
    private readonly ILogger<SyncHubClient> _logger;

    public event Action<SyncStatusUpdate>? OnSyncStarted;
    public event Action<SyncProgressUpdate>? OnSyncProgress;
    public event Action<SyncCompletionUpdate>? OnSyncCompleted;
    public event Action<SyncErrorUpdate>? OnSyncError;
    public event Action<SyncConflictUpdate>? OnConflictDetected;
    public event Action<Guid, string>? OnConflictResolved;
    public event Action<string, DateTime>? OnStatusChanged;

    public bool IsConnected => _connection.State == HubConnectionState.Connected;
    public HubConnectionState State => _connection.State;

    public SyncHubClient(string hubUrl, string? accessToken, ILogger<SyncHubClient> logger)
    {
        _logger = logger;

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
        // Handle sync started
        _connection.On<SyncStatusUpdate>("SyncStarted", update =>
        {
            _logger.LogInformation("Sync started: {SyncId}", update.SyncId);
            OnSyncStarted?.Invoke(update);
        });

        // Handle sync progress
        _connection.On<SyncProgressUpdate>("SyncProgress", update =>
        {
            _logger.LogDebug("Sync progress: {Percent}%", update.PercentComplete);
            OnSyncProgress?.Invoke(update);
        });

        // Handle sync completed
        _connection.On<SyncCompletionUpdate>("SyncCompleted", update =>
        {
            _logger.LogInformation("Sync completed: {SyncId}, Items: {ItemsSynced}", update.SyncId, update.ItemsSynced);
            OnSyncCompleted?.Invoke(update);
        });

        // Handle sync error
        _connection.On<SyncErrorUpdate>("SyncError", update =>
        {
            _logger.LogWarning("Sync error: {Error}", update.ErrorMessage);
            OnSyncError?.Invoke(update);
        });

        // Handle conflict detected
        _connection.On<SyncConflictUpdate>("ConflictDetected", update =>
        {
            _logger.LogWarning("Conflict detected: {EntityType}/{EntityId}", update.EntityType, update.EntityId);
            OnConflictDetected?.Invoke(update);
        });

        // Handle conflict resolved
        _connection.On<Guid, string>("ConflictResolved", (conflictId, resolution) =>
        {
            _logger.LogInformation("Conflict resolved: {ConflictId} with {Resolution}", conflictId, resolution);
            OnConflictResolved?.Invoke(conflictId, resolution);
        });

        // Handle status changed
        _connection.On<string, DateTime>("StatusChanged", (status, timestamp) =>
        {
            _logger.LogInformation("Sync status changed: {Status}", status);
            OnStatusChanged?.Invoke(status, timestamp);
        });

        // Handle reconnection
        _connection.Reconnecting += error =>
        {
            _logger.LogWarning("Sync hub reconnecting: {Error}", error?.Message);
            return Task.CompletedTask;
        };

        _connection.Reconnected += connectionId =>
        {
            _logger.LogInformation("Sync hub reconnected: {ConnectionId}", connectionId);
            return Task.CompletedTask;
        };

        _connection.Closed += error =>
        {
            _logger.LogError("Sync hub connection closed: {Error}", error?.Message);
            return Task.CompletedTask;
        };
    }

    /// <summary>
    /// Start connection to sync hub
    /// </summary>
    public async Task StartAsync()
    {
        try
        {
            if (_connection.State == HubConnectionState.Disconnected)
            {
                _logger.LogInformation("Starting sync hub connection");
                await _connection.StartAsync();
                _logger.LogInformation("Sync hub connected");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start sync hub connection");
            throw;
        }
    }

    /// <summary>
    /// Stop connection to sync hub
    /// </summary>
    public async Task StopAsync()
    {
        try
        {
            if (_connection.State == HubConnectionState.Connected)
            {
                _logger.LogInformation("Stopping sync hub connection");
                await _connection.StopAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop sync hub connection");
        }
    }

    /// <summary>
    /// Request current sync status
    /// </summary>
    public async Task RequestSyncStatusAsync()
    {
        try
        {
            await _connection.InvokeAsync("RequestSyncStatus");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to request sync status");
        }
    }

    /// <summary>
    /// Acknowledge sync update
    /// </summary>
    public async Task AcknowledgeSyncUpdateAsync(Guid syncId)
    {
        try
        {
            await _connection.InvokeAsync("AcknowledgeSyncUpdate", syncId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to acknowledge sync update");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        await _connection.DisposeAsync();
    }
}

/// <summary>
/// Sync status update
/// </summary>
public class SyncStatusUpdate
{
    public Guid SyncId { get; set; }
    public DateTime StartedAt { get; set; }
    public int TotalItems { get; set; }
    public List<string> SyncingEntities { get; set; } = new();
}

/// <summary>
/// Sync progress update
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
/// Sync completion update
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
/// Sync error update
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
/// Sync conflict update
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
