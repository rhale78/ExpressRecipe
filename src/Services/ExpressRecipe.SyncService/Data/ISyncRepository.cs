namespace ExpressRecipe.SyncService.Data;

public interface ISyncRepository
{
    // Sync Metadata
    Task<Guid> CreateSyncMetadataAsync(Guid userId, Guid deviceId, string entityType, Guid entityId, int version, string operation, string data, DateTime clientTimestamp);
    Task<List<SyncMetadataDto>> GetPendingSyncsAsync(Guid userId, Guid deviceId, DateTime since);
    Task<List<SyncMetadataDto>> GetEntityHistoryAsync(string entityType, Guid entityId);
    Task<SyncMetadataDto?> GetLatestVersionAsync(string entityType, Guid entityId);
    Task MarkAsSyncedAsync(Guid syncId, Guid deviceId, DateTime syncedAt);

    // Sync Conflicts
    Task<Guid> CreateConflictAsync(Guid userId, string entityType, Guid entityId, Guid device1Id, Guid device2Id, string serverData, string device1Data, string device2Data);
    Task<List<SyncConflictDto>> GetUnresolvedConflictsAsync(Guid userId);
    Task<List<SyncConflictDto>> GetEntityConflictsAsync(string entityType, Guid entityId);
    Task ResolveConflictAsync(Guid conflictId, string resolution, string resolvedData, Guid resolvedBy);

    // Sync Queue
    Task<Guid> EnqueueSyncAsync(Guid userId, Guid deviceId, string entityType, Guid entityId, string operation, string data, int priority);
    Task<List<SyncQueueDto>> GetQueuedSyncsAsync(Guid userId, Guid deviceId, int limit = 100);
    Task<List<SyncQueueDto>> GetFailedSyncsAsync(Guid userId, Guid deviceId);
    Task UpdateSyncStatusAsync(Guid queueId, string status, string? errorMessage, int retryCount);
    Task RemoveFromQueueAsync(Guid queueId);

    // Device Registration
    Task<Guid> RegisterDeviceAsync(Guid userId, string deviceName, string deviceType, string osVersion, string appVersion);
    Task<List<DeviceRegistrationDto>> GetUserDevicesAsync(Guid userId);
    Task<DeviceRegistrationDto?> GetDeviceAsync(Guid deviceId);
    Task UpdateDeviceLastSyncAsync(Guid deviceId, DateTime lastSyncAt);
    Task UnregisterDeviceAsync(Guid deviceId);

    // Sync Statistics
    Task<SyncStatsDto> GetSyncStatsAsync(Guid userId, Guid? deviceId = null);
}

public class SyncMetadataDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid DeviceId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public int Version { get; set; }
    public string Operation { get; set; } = string.Empty;
    public string Data { get; set; } = string.Empty;
    public DateTime ClientTimestamp { get; set; }
    public DateTime ServerTimestamp { get; set; }
    public bool IsSynced { get; set; }
}

public class SyncConflictDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public Guid Device1Id { get; set; }
    public Guid Device2Id { get; set; }
    public string ServerData { get; set; } = string.Empty;
    public string Device1Data { get; set; } = string.Empty;
    public string Device2Data { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Resolution { get; set; }
    public string? ResolvedData { get; set; }
    public DateTime DetectedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}

public class SyncQueueDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid DeviceId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Operation { get; set; } = string.Empty;
    public string Data { get; set; } = string.Empty;
    public int Priority { get; set; }
    public string Status { get; set; } = string.Empty;
    public int RetryCount { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime QueuedAt { get; set; }
}

public class DeviceRegistrationDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public string DeviceType { get; set; } = string.Empty;
    public string OsVersion { get; set; } = string.Empty;
    public string AppVersion { get; set; } = string.Empty;
    public DateTime RegisteredAt { get; set; }
    public DateTime? LastSyncAt { get; set; }
    public bool IsActive { get; set; }
}

public class SyncStatsDto
{
    public Guid UserId { get; set; }
    public Guid? DeviceId { get; set; }
    public int TotalSyncs { get; set; }
    public int PendingSyncs { get; set; }
    public int FailedSyncs { get; set; }
    public int Conflicts { get; set; }
    public DateTime? LastSyncAt { get; set; }
}
