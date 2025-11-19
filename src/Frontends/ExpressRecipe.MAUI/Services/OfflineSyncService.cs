using System.Text.Json;

namespace ExpressRecipe.MAUI.Services;

/// <summary>
/// Service for managing offline data synchronization
/// </summary>
public class OfflineSyncService : IOfflineSyncService
{
    private readonly ISQLiteDatabase _database;
    private readonly ILogger<OfflineSyncService> _logger;
    private readonly IConnectivity _connectivity;

    public bool IsOnline => _connectivity.NetworkAccess == NetworkAccess.Internet;

    public OfflineSyncService(
        ISQLiteDatabase database,
        ILogger<OfflineSyncService> logger,
        IConnectivity connectivity)
    {
        _database = database;
        _logger = logger;
        _connectivity = connectivity;
    }

    public async Task<bool> SyncAsync()
    {
        try
        {
            if (!IsOnline)
            {
                _logger.LogInformation("Cannot sync: device is offline");
                return false;
            }

            _logger.LogInformation("Starting sync...");

            var connection = _database.GetConnection();
            var pendingOperations = await connection.Table<OfflineSyncQueue>()
                .Where(x => !x.IsSynced && x.RetryCount < 3)
                .ToListAsync();

            if (!pendingOperations.Any())
            {
                _logger.LogInformation("No pending operations to sync");
                return true;
            }

            _logger.LogInformation("Syncing {Count} pending operations", pendingOperations.Count);

            var successCount = 0;
            foreach (var operation in pendingOperations.Take(50)) // Process in batches
            {
                try
                {
                    // Here you would call the appropriate API endpoint based on entityType and operation
                    // For now, just mark as synced
                    operation.IsSynced = true;
                    await connection.UpdateAsync(operation);
                    successCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error syncing operation {Id}", operation.Id);
                    operation.RetryCount++;
                    await connection.UpdateAsync(operation);
                }
            }

            _logger.LogInformation("Sync complete: {Success}/{Total} operations synced",
                successCount, pendingOperations.Count);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during sync");
            return false;
        }
    }

    public async Task EnqueueOperationAsync(string entityType, string entityId, string operation, object data)
    {
        try
        {
            var connection = _database.GetConnection();
            var syncItem = new OfflineSyncQueue
            {
                EntityType = entityType,
                EntityId = entityId,
                Operation = operation,
                Data = JsonSerializer.Serialize(data),
                IsSynced = false,
                CreatedAt = DateTime.UtcNow,
                RetryCount = 0
            };

            await connection.InsertAsync(syncItem);
            _logger.LogInformation("Enqueued {Operation} operation for {EntityType} {EntityId}",
                operation, entityType, entityId);

            // If online, try to sync immediately
            if (IsOnline)
            {
                _ = Task.Run(async () => await SyncAsync());
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enqueuing operation");
        }
    }

    public async Task<int> GetPendingOperationsCountAsync()
    {
        try
        {
            var connection = _database.GetConnection();
            return await connection.Table<OfflineSyncQueue>()
                .Where(x => !x.IsSynced)
                .CountAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending operations count");
            return 0;
        }
    }
}
