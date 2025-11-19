namespace ExpressRecipe.MAUI.Services;

public interface IOfflineSyncService
{
    Task<bool> SyncAsync();
    Task EnqueueOperationAsync(string entityType, string entityId, string operation, object data);
    Task<int> GetPendingOperationsCountAsync();
    bool IsOnline { get; }
}
