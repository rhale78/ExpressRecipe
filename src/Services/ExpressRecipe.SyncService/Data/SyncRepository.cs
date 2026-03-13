using ExpressRecipe.Data.Common;
using System.Text.Json;

namespace ExpressRecipe.SyncService.Data;

public class SyncRepository : SqlHelper, ISyncRepository
{
    private readonly ILogger<SyncRepository> _logger;

    public SyncRepository(string connectionString, ILogger<SyncRepository> logger) : base(connectionString)
    {
        _logger = logger;
    }

    public async Task<Guid> CreateSyncMetadataAsync(Guid userId, Guid deviceId, string entityType, Guid entityId, int version, string operation, string data, DateTime clientTimestamp)
    {
        const string sql = @"
            INSERT INTO SyncMetadata (UserId, DeviceId, EntityType, EntityId, Version, Operation, Data, ClientTimestamp, ServerTimestamp, IsSynced)
            OUTPUT INSERTED.Id
            VALUES (@UserId, @DeviceId, @EntityType, @EntityId, @Version, @Operation, @Data, @ClientTimestamp, GETUTCDATE(), 0)";

        return (await ExecuteScalarAsync<Guid>(sql,
            CreateParameter("@UserId", userId),
            CreateParameter("@DeviceId", deviceId),
            CreateParameter("@EntityType", entityType),
            CreateParameter("@EntityId", entityId),
            CreateParameter("@Version", version),
            CreateParameter("@Operation", operation),
            CreateParameter("@Data", data),
            CreateParameter("@ClientTimestamp", clientTimestamp)))!;
    }

    private static SyncMetadataDto MapSyncMetadata(System.Data.IDataRecord reader) => new SyncMetadataDto
    {
        Id = SqlHelper.GetGuid(reader, "Id"),
        UserId = SqlHelper.GetGuid(reader, "UserId"),
        DeviceId = SqlHelper.GetGuid(reader, "DeviceId"),
        EntityType = SqlHelper.GetString(reader, "EntityType")!,
        EntityId = SqlHelper.GetGuid(reader, "EntityId"),
        Version = SqlHelper.GetInt32(reader, "Version"),
        Operation = SqlHelper.GetString(reader, "Operation")!,
        Data = SqlHelper.GetString(reader, "Data")!,
        ClientTimestamp = SqlHelper.GetDateTime(reader, "ClientTimestamp"),
        ServerTimestamp = SqlHelper.GetDateTime(reader, "ServerTimestamp"),
        IsSynced = SqlHelper.GetBoolean(reader, "IsSynced")
    };

    public async Task<List<SyncMetadataDto>> GetPendingSyncsAsync(Guid userId, Guid deviceId, DateTime since)
    {
        const string sql = @"
            SELECT Id, UserId, DeviceId, EntityType, EntityId, Version, Operation, Data, ClientTimestamp, ServerTimestamp, IsSynced
            FROM SyncMetadata
            WHERE UserId = @UserId AND DeviceId != @DeviceId AND ServerTimestamp > @Since AND IsDeleted = 0
            ORDER BY ServerTimestamp ASC";

        return await ExecuteReaderAsync<SyncMetadataDto>(sql, MapSyncMetadata,
            CreateParameter("@UserId", userId),
            CreateParameter("@DeviceId", deviceId),
            CreateParameter("@Since", since));
    }

    public async Task<List<SyncMetadataDto>> GetEntityHistoryAsync(string entityType, Guid entityId)
    {
        const string sql = @"
            SELECT Id, UserId, DeviceId, EntityType, EntityId, Version, Operation, Data, ClientTimestamp, ServerTimestamp, IsSynced
            FROM SyncMetadata
            WHERE EntityType = @EntityType AND EntityId = @EntityId AND IsDeleted = 0
            ORDER BY Version ASC";

        return await ExecuteReaderAsync<SyncMetadataDto>(sql, MapSyncMetadata,
            CreateParameter("@EntityType", entityType),
            CreateParameter("@EntityId", entityId));
    }

    public async Task<SyncMetadataDto?> GetLatestVersionAsync(string entityType, Guid entityId)
    {
        const string sql = @"
            SELECT TOP 1 Id, UserId, DeviceId, EntityType, EntityId, Version, Operation, Data, ClientTimestamp, ServerTimestamp, IsSynced
            FROM SyncMetadata
            WHERE EntityType = @EntityType AND EntityId = @EntityId AND IsDeleted = 0
            ORDER BY Version DESC";

        var results = await ExecuteReaderAsync<SyncMetadataDto>(sql, MapSyncMetadata,
            CreateParameter("@EntityType", entityType),
            CreateParameter("@EntityId", entityId));

        return results.FirstOrDefault();
    }

    public async Task MarkAsSyncedAsync(Guid syncId, Guid deviceId, DateTime syncedAt)
    {
        const string sql = "UPDATE SyncMetadata SET IsSynced = 1 WHERE Id = @SyncId";
        await ExecuteNonQueryAsync(sql, CreateParameter("@SyncId", syncId));
    }

    public async Task<Guid> CreateConflictAsync(Guid userId, string entityType, Guid entityId, Guid device1Id, Guid device2Id, string serverData, string device1Data, string device2Data)
    {
        const string sql = @"
            INSERT INTO SyncConflict (UserId, EntityType, EntityId, Device1Id, Device2Id, ServerData, Device1Data, Device2Data, Status, DetectedAt)
            OUTPUT INSERTED.Id
            VALUES (@UserId, @EntityType, @EntityId, @Device1Id, @Device2Id, @ServerData, @Device1Data, @Device2Data, 'Unresolved', GETUTCDATE())";

        return (await ExecuteScalarAsync<Guid>(sql,
            CreateParameter("@UserId", userId),
            CreateParameter("@EntityType", entityType),
            CreateParameter("@EntityId", entityId),
            CreateParameter("@Device1Id", device1Id),
            CreateParameter("@Device2Id", device2Id),
            CreateParameter("@ServerData", serverData),
            CreateParameter("@Device1Data", device1Data),
            CreateParameter("@Device2Data", device2Data)))!;
    }

    public async Task<List<SyncConflictDto>> GetUnresolvedConflictsAsync(Guid userId)
    {
        const string sql = @"
            SELECT Id, UserId, EntityType, EntityId, Device1Id, Device2Id, ServerData, Device1Data, Device2Data, Status, Resolution, ResolvedData, DetectedAt, ResolvedAt
            FROM SyncConflict
            WHERE UserId = @UserId AND Status = 'Unresolved'
            ORDER BY DetectedAt ASC";

        return await ExecuteReaderAsync<SyncConflictDto>(sql, MapSyncConflict, CreateParameter("@UserId", userId));
    }

    public async Task<List<SyncConflictDto>> GetEntityConflictsAsync(string entityType, Guid entityId)
    {
        const string sql = @"
            SELECT Id, UserId, EntityType, EntityId, Device1Id, Device2Id, ServerData, Device1Data, Device2Data, Status, Resolution, ResolvedData, DetectedAt, ResolvedAt
            FROM SyncConflict
            WHERE EntityType = @EntityType AND EntityId = @EntityId
            ORDER BY DetectedAt DESC";

        return await ExecuteReaderAsync<SyncConflictDto>(sql, MapSyncConflict,
            CreateParameter("@EntityType", entityType),
            CreateParameter("@EntityId", entityId));
    }

    public async Task ResolveConflictAsync(Guid conflictId, string resolution, string resolvedData, Guid resolvedBy)
    {
        const string sql = @"
            UPDATE SyncConflict
            SET Status = 'Resolved', Resolution = @Resolution, ResolvedData = @ResolvedData, ResolvedBy = @ResolvedBy, ResolvedAt = GETUTCDATE()
            WHERE Id = @ConflictId";

        await ExecuteNonQueryAsync(sql,
            CreateParameter("@ConflictId", conflictId),
            CreateParameter("@Resolution", resolution),
            CreateParameter("@ResolvedData", resolvedData),
            CreateParameter("@ResolvedBy", resolvedBy));
    }

    public async Task<Guid> EnqueueSyncAsync(Guid userId, Guid deviceId, string entityType, Guid entityId, string operation, string data, int priority)
    {
        const string sql = @"
            INSERT INTO SyncQueue (UserId, DeviceId, EntityType, EntityId, Operation, Data, Priority, Status, RetryCount, QueuedAt)
            OUTPUT INSERTED.Id
            VALUES (@UserId, @DeviceId, @EntityType, @EntityId, @Operation, @Data, @Priority, 'Queued', 0, GETUTCDATE())";

        return (await ExecuteScalarAsync<Guid>(sql,
            CreateParameter("@UserId", userId),
            CreateParameter("@DeviceId", deviceId),
            CreateParameter("@EntityType", entityType),
            CreateParameter("@EntityId", entityId),
            CreateParameter("@Operation", operation),
            CreateParameter("@Data", data),
            CreateParameter("@Priority", priority)))!;
    }

    public async Task<List<SyncQueueDto>> GetQueuedSyncsAsync(Guid userId, Guid deviceId, int limit = 100)
    {
        const string sql = @"
            SELECT TOP (@Limit) Id, UserId, DeviceId, EntityType, EntityId, Operation, Data, Priority, Status, RetryCount, ErrorMessage, QueuedAt
            FROM SyncQueue
            WHERE UserId = @UserId AND DeviceId = @DeviceId AND Status = 'Queued'
            ORDER BY Priority DESC, QueuedAt ASC";

        return await ExecuteReaderAsync<SyncQueueDto>(sql, MapSyncQueue,
            CreateParameter("@Limit", limit),
            CreateParameter("@UserId", userId),
            CreateParameter("@DeviceId", deviceId));
    }

    public async Task<List<SyncQueueDto>> GetFailedSyncsAsync(Guid userId, Guid deviceId)
    {
        const string sql = @"
            SELECT Id, UserId, DeviceId, EntityType, EntityId, Operation, Data, Priority, Status, RetryCount, ErrorMessage, QueuedAt
            FROM SyncQueue
            WHERE UserId = @UserId AND DeviceId = @DeviceId AND Status = 'Failed'
            ORDER BY QueuedAt DESC";

        return await ExecuteReaderAsync<SyncQueueDto>(sql, MapSyncQueue,
            CreateParameter("@UserId", userId),
            CreateParameter("@DeviceId", deviceId));
    }

    public async Task UpdateSyncStatusAsync(Guid queueId, string status, string? errorMessage, int retryCount)
    {
        const string sql = @"
            UPDATE SyncQueue
            SET Status = @Status, ErrorMessage = @ErrorMessage, RetryCount = @RetryCount
            WHERE Id = @QueueId";

        await ExecuteNonQueryAsync(sql,
            CreateParameter("@QueueId", queueId),
            CreateParameter("@Status", status),
            CreateParameter("@ErrorMessage", errorMessage ?? (object)DBNull.Value),
            CreateParameter("@RetryCount", retryCount));
    }

    public async Task RemoveFromQueueAsync(Guid queueId)
    {
        const string sql = "DELETE FROM SyncQueue WHERE Id = @QueueId";
        await ExecuteNonQueryAsync(sql, CreateParameter("@QueueId", queueId));
    }

    public async Task<Guid> RegisterDeviceAsync(Guid userId, string deviceName, string deviceType, string osVersion, string appVersion)
    {
        const string sql = @"
            INSERT INTO DeviceRegistration (UserId, DeviceName, DeviceType, OsVersion, AppVersion, RegisteredAt, IsActive)
            OUTPUT INSERTED.Id
            VALUES (@UserId, @DeviceName, @DeviceType, @OsVersion, @AppVersion, GETUTCDATE(), 1)";

        return (await ExecuteScalarAsync<Guid>(sql,
            CreateParameter("@UserId", userId),
            CreateParameter("@DeviceName", deviceName),
            CreateParameter("@DeviceType", deviceType),
            CreateParameter("@OsVersion", osVersion),
            CreateParameter("@AppVersion", appVersion)))!;
    }

    private static DeviceRegistrationDto MapDevice(System.Data.IDataRecord reader) => new DeviceRegistrationDto
    {
        Id = SqlHelper.GetGuid(reader, "Id"),
        UserId = SqlHelper.GetGuid(reader, "UserId"),
        DeviceName = SqlHelper.GetString(reader, "DeviceName")!,
        DeviceType = SqlHelper.GetString(reader, "DeviceType")!,
        OsVersion = SqlHelper.GetString(reader, "OsVersion")!,
        AppVersion = SqlHelper.GetString(reader, "AppVersion")!,
        RegisteredAt = SqlHelper.GetDateTime(reader, "RegisteredAt"),
        LastSyncAt = SqlHelper.GetNullableDateTime(reader, "LastSyncAt"),
        IsActive = SqlHelper.GetBoolean(reader, "IsActive")
    };

    public async Task<List<DeviceRegistrationDto>> GetUserDevicesAsync(Guid userId)
    {
        const string sql = @"
            SELECT Id, UserId, DeviceName, DeviceType, OsVersion, AppVersion, RegisteredAt, LastSyncAt, IsActive
            FROM DeviceRegistration
            WHERE UserId = @UserId
            ORDER BY LastSyncAt DESC NULLS LAST, RegisteredAt DESC";

        return await ExecuteReaderAsync<DeviceRegistrationDto>(sql, MapDevice, CreateParameter("@UserId", userId));
    }

    public async Task<DeviceRegistrationDto?> GetDeviceAsync(Guid deviceId)
    {
        const string sql = @"
            SELECT Id, UserId, DeviceName, DeviceType, OsVersion, AppVersion, RegisteredAt, LastSyncAt, IsActive
            FROM DeviceRegistration
            WHERE Id = @DeviceId";

        var results = await ExecuteReaderAsync<DeviceRegistrationDto>(sql, MapDevice, CreateParameter("@DeviceId", deviceId));
        return results.FirstOrDefault();
    }

    public async Task UpdateDeviceLastSyncAsync(Guid deviceId, DateTime lastSyncAt)
    {
        const string sql = "UPDATE DeviceRegistration SET LastSyncAt = @LastSyncAt WHERE Id = @DeviceId";
        await ExecuteNonQueryAsync(sql,
            CreateParameter("@DeviceId", deviceId),
            CreateParameter("@LastSyncAt", lastSyncAt));
    }

    public async Task UnregisterDeviceAsync(Guid deviceId)
    {
        const string sql = "UPDATE DeviceRegistration SET IsActive = 0 WHERE Id = @DeviceId";
        await ExecuteNonQueryAsync(sql, CreateParameter("@DeviceId", deviceId));
    }

    public async Task<SyncStatsDto> GetSyncStatsAsync(Guid userId, Guid? deviceId = null)
    {
        var sql = @"
            SELECT
                COUNT(CASE WHEN Status = 'Queued' THEN 1 END) AS PendingSyncs,
                COUNT(CASE WHEN Status = 'Failed' THEN 1 END) AS FailedSyncs,
                (SELECT COUNT(*) FROM SyncConflict WHERE UserId = @UserId AND Status = 'Unresolved') AS Conflicts,
                MAX(QueuedAt) AS LastSyncAt
            FROM SyncQueue
            WHERE UserId = @UserId";

        if (deviceId.HasValue)
            sql += " AND DeviceId = @DeviceId";

        var paramList = new List<System.Data.Common.DbParameter> { CreateParameter("@UserId", userId) };
        if (deviceId.HasValue)
            paramList.Add(CreateParameter("@DeviceId", deviceId.Value));

        var results = await ExecuteReaderAsync<SyncStatsDto>(sql, reader => new SyncStatsDto
        {
            UserId = userId,
            DeviceId = deviceId,
            TotalSyncs = 0,
            PendingSyncs = GetInt32(reader, "PendingSyncs"),
            FailedSyncs = GetInt32(reader, "FailedSyncs"),
            Conflicts = GetInt32(reader, "Conflicts"),
            LastSyncAt = GetNullableDateTime(reader, "LastSyncAt")
        }, paramList.ToArray());

        return results.FirstOrDefault() ?? new SyncStatsDto { UserId = userId, DeviceId = deviceId };
    }

    private static SyncConflictDto MapSyncConflict(System.Data.IDataRecord reader) => new SyncConflictDto
    {
        Id = SqlHelper.GetGuid(reader, "Id"),
        UserId = SqlHelper.GetGuid(reader, "UserId"),
        EntityType = SqlHelper.GetString(reader, "EntityType")!,
        EntityId = SqlHelper.GetGuid(reader, "EntityId"),
        Device1Id = SqlHelper.GetGuid(reader, "Device1Id"),
        Device2Id = SqlHelper.GetGuid(reader, "Device2Id"),
        ServerData = SqlHelper.GetString(reader, "ServerData")!,
        Device1Data = SqlHelper.GetString(reader, "Device1Data")!,
        Device2Data = SqlHelper.GetString(reader, "Device2Data")!,
        Status = SqlHelper.GetString(reader, "Status")!,
        Resolution = SqlHelper.GetString(reader, "Resolution"),
        ResolvedData = SqlHelper.GetString(reader, "ResolvedData"),
        DetectedAt = SqlHelper.GetDateTime(reader, "DetectedAt"),
        ResolvedAt = SqlHelper.GetNullableDateTime(reader, "ResolvedAt")
    };

    private static SyncQueueDto MapSyncQueue(System.Data.IDataRecord reader) => new SyncQueueDto
    {
        Id = SqlHelper.GetGuid(reader, "Id"),
        UserId = SqlHelper.GetGuid(reader, "UserId"),
        DeviceId = SqlHelper.GetGuid(reader, "DeviceId"),
        EntityType = SqlHelper.GetString(reader, "EntityType")!,
        EntityId = SqlHelper.GetGuid(reader, "EntityId"),
        Operation = SqlHelper.GetString(reader, "Operation")!,
        Data = SqlHelper.GetString(reader, "Data")!,
        Priority = SqlHelper.GetInt32(reader, "Priority"),
        Status = SqlHelper.GetString(reader, "Status")!,
        RetryCount = SqlHelper.GetInt32(reader, "RetryCount"),
        ErrorMessage = SqlHelper.GetString(reader, "ErrorMessage"),
        QueuedAt = SqlHelper.GetDateTime(reader, "QueuedAt")
    };

    public async Task DeleteUserDataAsync(Guid userId, CancellationToken ct = default)
    {
        const string sql = @"
DELETE FROM SyncQueue          WHERE UserId = @UserId;
DELETE FROM DeviceRegistration WHERE UserId = @UserId;
DELETE FROM SyncConflict       WHERE UserId = @UserId;";

        await ExecuteNonQueryAsync(sql, ct, CreateParameter("@UserId", userId));
    }
}
