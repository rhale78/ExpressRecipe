using Blazored.LocalStorage;

namespace ExpressRecipe.Client.Shared.Services.LocalStorage;

/// <summary>
/// Service for managing sync queue of pending changes
/// </summary>
public class SyncQueueService
{
    private readonly ILocalStorageService _localStorage;
    private readonly ISyncApiClient _syncClient;
    private const string QueueKey = "expressrecipe_sync_queue";

    public SyncQueueService(ILocalStorageService localStorage, ISyncApiClient syncClient)
    {
        _localStorage = localStorage;
        _syncClient = syncClient;
    }

    /// <summary>
    /// Add item to sync queue
    /// </summary>
    public async Task EnqueueAsync(SyncQueueItem item)
    {
        var queue = await GetQueueAsync();

        // Check if item already exists
        var existing = queue.FirstOrDefault(x =>
            x.EntityType == item.EntityType &&
            x.EntityId == item.EntityId &&
            !x.IsSynced);

        if (existing != null)
        {
            // Update existing item
            existing.Operation = item.Operation;
            existing.Data = item.Data;
            existing.QueuedAt = DateTime.UtcNow;
        }
        else
        {
            queue.Add(item);
        }

        await _localStorage.SetItemAsync(QueueKey, queue);
    }

    /// <summary>
    /// Get all pending sync items
    /// </summary>
    public async Task<List<SyncQueueItem>> GetPendingAsync()
    {
        var queue = await GetQueueAsync();
        return queue.Where(x => !x.IsSynced).OrderBy(x => x.QueuedAt).ToList();
    }

    /// <summary>
    /// Get sync queue count
    /// </summary>
    public async Task<int> GetPendingCountAsync()
    {
        var queue = await GetQueueAsync();
        return queue.Count(x => !x.IsSynced);
    }

    /// <summary>
    /// Mark item as synced
    /// </summary>
    public async Task MarkAsSyncedAsync(Guid itemId)
    {
        var queue = await GetQueueAsync();
        var item = queue.FirstOrDefault(x => x.Id == itemId);

        if (item != null)
        {
            item.IsSynced = true;
            item.SyncedAt = DateTime.UtcNow;
            await _localStorage.SetItemAsync(QueueKey, queue);
        }
    }

    /// <summary>
    /// Remove synced items from queue
    /// </summary>
    public async Task CleanupSyncedItemsAsync()
    {
        var queue = await GetQueueAsync();
        var pending = queue.Where(x => !x.IsSynced).ToList();
        await _localStorage.SetItemAsync(QueueKey, pending);
    }

    /// <summary>
    /// Clear entire sync queue
    /// </summary>
    public async Task ClearQueueAsync()
    {
        await _localStorage.RemoveItemAsync(QueueKey);
    }

    /// <summary>
    /// Process sync queue - upload pending changes to server
    /// </summary>
    public async Task<SyncResult> ProcessQueueAsync()
    {
        var pending = await GetPendingAsync();

        if (!pending.Any())
        {
            return new SyncResult
            {
                Success = true,
                ItemsSynced = 0,
                Message = "No items to sync"
            };
        }

        var syncedCount = 0;
        var errors = new List<string>();

        foreach (var item in pending.Take(100)) // Process in batches of 100
        {
            try
            {
                // Call sync service for this item
                var result = await _syncClient.SyncEntityAsync(item.EntityType, item.EntityId);

                if (result.Success)
                {
                    await MarkAsSyncedAsync(item.Id);
                    syncedCount++;
                }
                else
                {
                    item.RetryCount++;
                    errors.Add($"{item.EntityType}/{item.EntityId}: {result.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                item.RetryCount++;
                errors.Add($"{item.EntityType}/{item.EntityId}: {ex.Message}");
            }
        }

        // Update queue with retry counts
        var queue = await GetQueueAsync();
        await _localStorage.SetItemAsync(QueueKey, queue);

        return new SyncResult
        {
            Success = syncedCount > 0,
            ItemsSynced = syncedCount,
            ConflictsRemaining = pending.Count - syncedCount,
            Message = $"Synced {syncedCount}/{pending.Count} items",
            ErrorMessage = errors.Any() ? string.Join("; ", errors) : null
        };
    }

    private async Task<List<SyncQueueItem>> GetQueueAsync()
    {
        try
        {
            var queue = await _localStorage.GetItemAsync<List<SyncQueueItem>>(QueueKey);
            return queue ?? new List<SyncQueueItem>();
        }
        catch
        {
            return new List<SyncQueueItem>();
        }
    }
}

/// <summary>
/// Item in the sync queue
/// </summary>
public class SyncQueueItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Operation { get; set; } = string.Empty; // Create, Update, Delete
    public string Data { get; set; } = string.Empty; // JSON serialized entity
    public DateTime QueuedAt { get; set; } = DateTime.UtcNow;
    public int RetryCount { get; set; }
    public bool IsSynced { get; set; }
    public DateTime? SyncedAt { get; set; }
}

/// <summary>
/// Result of sync operation
/// </summary>
public class SyncResult
{
    public bool Success { get; set; }
    public int ItemsSynced { get; set; }
    public int ConflictsRemaining { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}
