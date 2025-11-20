using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace ExpressRecipe.SyncService.Data;

public class SyncRepository : ISyncRepository
{
    private readonly string _connectionString;
    private readonly ILogger<SyncRepository> _logger;

    public SyncRepository(string connectionString, ILogger<SyncRepository> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task<Guid> CreateSyncMetadataAsync(Guid userId, Guid deviceId, string entityType, Guid entityId, int version, string operation, string data, DateTime clientTimestamp)
    {
        const string sql = @"
            INSERT INTO SyncMetadata (UserId, DeviceId, EntityType, EntityId, Version, Operation, Data, ClientTimestamp, ServerTimestamp, IsSynced)
            OUTPUT INSERTED.Id
            VALUES (@UserId, @DeviceId, @EntityType, @EntityId, @Version, @Operation, @Data, @ClientTimestamp, GETUTCDATE(), 0)";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@DeviceId", deviceId);
        command.Parameters.AddWithValue("@EntityType", entityType);
        command.Parameters.AddWithValue("@EntityId", entityId);
        command.Parameters.AddWithValue("@Version", version);
        command.Parameters.AddWithValue("@Operation", operation);
        command.Parameters.AddWithValue("@Data", data);
        command.Parameters.AddWithValue("@ClientTimestamp", clientTimestamp);

        return (Guid)await command.ExecuteScalarAsync()!;
    }

    public async Task<List<SyncMetadataDto>> GetPendingSyncsAsync(Guid userId, Guid deviceId, DateTime since)
    {
        const string sql = @"
            SELECT Id, UserId, DeviceId, EntityType, EntityId, Version, Operation, Data, ClientTimestamp, ServerTimestamp, IsSynced
            FROM SyncMetadata
            WHERE UserId = @UserId AND DeviceId != @DeviceId AND ServerTimestamp > @Since
            ORDER BY ServerTimestamp ASC";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@DeviceId", deviceId);
        command.Parameters.AddWithValue("@Since", since);

        var syncs = new List<SyncMetadataDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            syncs.Add(new SyncMetadataDto
            {
                Id = reader.GetGuid(0),
                UserId = reader.GetGuid(1),
                DeviceId = reader.GetGuid(2),
                EntityType = reader.GetString(3),
                EntityId = reader.GetGuid(4),
                Version = reader.GetInt32(5),
                Operation = reader.GetString(6),
                Data = reader.GetString(7),
                ClientTimestamp = reader.GetDateTime(8),
                ServerTimestamp = reader.GetDateTime(9),
                IsSynced = reader.GetBoolean(10)
            });
        }

        return syncs;
    }

    public async Task<List<SyncMetadataDto>> GetEntityHistoryAsync(string entityType, Guid entityId)
    {
        const string sql = @"
            SELECT Id, UserId, DeviceId, EntityType, EntityId, Version, Operation, Data, ClientTimestamp, ServerTimestamp, IsSynced
            FROM SyncMetadata
            WHERE EntityType = @EntityType AND EntityId = @EntityId
            ORDER BY Version ASC";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@EntityType", entityType);
        command.Parameters.AddWithValue("@EntityId", entityId);

        var syncs = new List<SyncMetadataDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            syncs.Add(new SyncMetadataDto
            {
                Id = reader.GetGuid(0),
                UserId = reader.GetGuid(1),
                DeviceId = reader.GetGuid(2),
                EntityType = reader.GetString(3),
                EntityId = reader.GetGuid(4),
                Version = reader.GetInt32(5),
                Operation = reader.GetString(6),
                Data = reader.GetString(7),
                ClientTimestamp = reader.GetDateTime(8),
                ServerTimestamp = reader.GetDateTime(9),
                IsSynced = reader.GetBoolean(10)
            });
        }

        return syncs;
    }

    public async Task<SyncMetadataDto?> GetLatestVersionAsync(string entityType, Guid entityId)
    {
        const string sql = @"
            SELECT TOP 1 Id, UserId, DeviceId, EntityType, EntityId, Version, Operation, Data, ClientTimestamp, ServerTimestamp, IsSynced
            FROM SyncMetadata
            WHERE EntityType = @EntityType AND EntityId = @EntityId
            ORDER BY Version DESC";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@EntityType", entityType);
        command.Parameters.AddWithValue("@EntityId", entityId);

        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new SyncMetadataDto
            {
                Id = reader.GetGuid(0),
                UserId = reader.GetGuid(1),
                DeviceId = reader.GetGuid(2),
                EntityType = reader.GetString(3),
                EntityId = reader.GetGuid(4),
                Version = reader.GetInt32(5),
                Operation = reader.GetString(6),
                Data = reader.GetString(7),
                ClientTimestamp = reader.GetDateTime(8),
                ServerTimestamp = reader.GetDateTime(9),
                IsSynced = reader.GetBoolean(10)
            };
        }

        return null;
    }

    public async Task MarkAsSyncedAsync(Guid syncId, Guid deviceId, DateTime syncedAt)
    {
        const string sql = "UPDATE SyncMetadata SET IsSynced = 1 WHERE Id = @SyncId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@SyncId", syncId);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<Guid> CreateConflictAsync(Guid userId, string entityType, Guid entityId, Guid device1Id, Guid device2Id, string serverData, string device1Data, string device2Data)
    {
        const string sql = @"
            INSERT INTO SyncConflict (UserId, EntityType, EntityId, Device1Id, Device2Id, ServerData, Device1Data, Device2Data, Status, DetectedAt)
            OUTPUT INSERTED.Id
            VALUES (@UserId, @EntityType, @EntityId, @Device1Id, @Device2Id, @ServerData, @Device1Data, @Device2Data, 'Unresolved', GETUTCDATE())";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@EntityType", entityType);
        command.Parameters.AddWithValue("@EntityId", entityId);
        command.Parameters.AddWithValue("@Device1Id", device1Id);
        command.Parameters.AddWithValue("@Device2Id", device2Id);
        command.Parameters.AddWithValue("@ServerData", serverData);
        command.Parameters.AddWithValue("@Device1Data", device1Data);
        command.Parameters.AddWithValue("@Device2Data", device2Data);

        return (Guid)await command.ExecuteScalarAsync()!;
    }

    public async Task<List<SyncConflictDto>> GetUnresolvedConflictsAsync(Guid userId)
    {
        const string sql = @"
            SELECT Id, UserId, EntityType, EntityId, Device1Id, Device2Id, ServerData, Device1Data, Device2Data, Status, Resolution, ResolvedData, DetectedAt, ResolvedAt
            FROM SyncConflict
            WHERE UserId = @UserId AND Status = 'Unresolved'
            ORDER BY DetectedAt ASC";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);

        return await ReadSyncConflicts(reader => reader);
    }

    public async Task<List<SyncConflictDto>> GetEntityConflictsAsync(string entityType, Guid entityId)
    {
        const string sql = @"
            SELECT Id, UserId, EntityType, EntityId, Device1Id, Device2Id, ServerData, Device1Data, Device2Data, Status, Resolution, ResolvedData, DetectedAt, ResolvedAt
            FROM SyncConflict
            WHERE EntityType = @EntityType AND EntityId = @EntityId
            ORDER BY DetectedAt DESC";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@EntityType", entityType);
        command.Parameters.AddWithValue("@EntityId", entityId);

        return await ReadSyncConflicts(reader => reader);
    }

    public async Task ResolveConflictAsync(Guid conflictId, string resolution, string resolvedData, Guid resolvedBy)
    {
        const string sql = @"
            UPDATE SyncConflict
            SET Status = 'Resolved', Resolution = @Resolution, ResolvedData = @ResolvedData, ResolvedBy = @ResolvedBy, ResolvedAt = GETUTCDATE()
            WHERE Id = @ConflictId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ConflictId", conflictId);
        command.Parameters.AddWithValue("@Resolution", resolution);
        command.Parameters.AddWithValue("@ResolvedData", resolvedData);
        command.Parameters.AddWithValue("@ResolvedBy", resolvedBy);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<Guid> EnqueueSyncAsync(Guid userId, Guid deviceId, string entityType, Guid entityId, string operation, string data, int priority)
    {
        const string sql = @"
            INSERT INTO SyncQueue (UserId, DeviceId, EntityType, EntityId, Operation, Data, Priority, Status, RetryCount, QueuedAt)
            OUTPUT INSERTED.Id
            VALUES (@UserId, @DeviceId, @EntityType, @EntityId, @Operation, @Data, @Priority, 'Queued', 0, GETUTCDATE())";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@DeviceId", deviceId);
        command.Parameters.AddWithValue("@EntityType", entityType);
        command.Parameters.AddWithValue("@EntityId", entityId);
        command.Parameters.AddWithValue("@Operation", operation);
        command.Parameters.AddWithValue("@Data", data);
        command.Parameters.AddWithValue("@Priority", priority);

        return (Guid)await command.ExecuteScalarAsync()!;
    }

    public async Task<List<SyncQueueDto>> GetQueuedSyncsAsync(Guid userId, Guid deviceId, int limit = 100)
    {
        const string sql = @"
            SELECT TOP (@Limit) Id, UserId, DeviceId, EntityType, EntityId, Operation, Data, Priority, Status, RetryCount, ErrorMessage, QueuedAt
            FROM SyncQueue
            WHERE UserId = @UserId AND DeviceId = @DeviceId AND Status = 'Queued'
            ORDER BY Priority DESC, QueuedAt ASC";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Limit", limit);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@DeviceId", deviceId);

        return await ReadSyncQueue(reader => reader);
    }

    public async Task<List<SyncQueueDto>> GetFailedSyncsAsync(Guid userId, Guid deviceId)
    {
        const string sql = @"
            SELECT Id, UserId, DeviceId, EntityType, EntityId, Operation, Data, Priority, Status, RetryCount, ErrorMessage, QueuedAt
            FROM SyncQueue
            WHERE UserId = @UserId AND DeviceId = @DeviceId AND Status = 'Failed'
            ORDER BY QueuedAt DESC";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@DeviceId", deviceId);

        return await ReadSyncQueue(reader => reader);
    }

    public async Task UpdateSyncStatusAsync(Guid queueId, string status, string? errorMessage, int retryCount)
    {
        const string sql = @"
            UPDATE SyncQueue
            SET Status = @Status, ErrorMessage = @ErrorMessage, RetryCount = @RetryCount
            WHERE Id = @QueueId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@QueueId", queueId);
        command.Parameters.AddWithValue("@Status", status);
        command.Parameters.AddWithValue("@ErrorMessage", errorMessage ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@RetryCount", retryCount);

        await command.ExecuteNonQueryAsync();
    }

    public async Task RemoveFromQueueAsync(Guid queueId)
    {
        const string sql = "DELETE FROM SyncQueue WHERE Id = @QueueId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@QueueId", queueId);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<Guid> RegisterDeviceAsync(Guid userId, string deviceName, string deviceType, string osVersion, string appVersion)
    {
        const string sql = @"
            INSERT INTO DeviceRegistration (UserId, DeviceName, DeviceType, OsVersion, AppVersion, RegisteredAt, IsActive)
            OUTPUT INSERTED.Id
            VALUES (@UserId, @DeviceName, @DeviceType, @OsVersion, @AppVersion, GETUTCDATE(), 1)";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@DeviceName", deviceName);
        command.Parameters.AddWithValue("@DeviceType", deviceType);
        command.Parameters.AddWithValue("@OsVersion", osVersion);
        command.Parameters.AddWithValue("@AppVersion", appVersion);

        return (Guid)await command.ExecuteScalarAsync()!;
    }

    public async Task<List<DeviceRegistrationDto>> GetUserDevicesAsync(Guid userId)
    {
        const string sql = @"
            SELECT Id, UserId, DeviceName, DeviceType, OsVersion, AppVersion, RegisteredAt, LastSyncAt, IsActive
            FROM DeviceRegistration
            WHERE UserId = @UserId
            ORDER BY LastSyncAt DESC NULLS LAST, RegisteredAt DESC";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);

        var devices = new List<DeviceRegistrationDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            devices.Add(new DeviceRegistrationDto
            {
                Id = reader.GetGuid(0),
                UserId = reader.GetGuid(1),
                DeviceName = reader.GetString(2),
                DeviceType = reader.GetString(3),
                OsVersion = reader.GetString(4),
                AppVersion = reader.GetString(5),
                RegisteredAt = reader.GetDateTime(6),
                LastSyncAt = reader.IsDBNull(7) ? null : reader.GetDateTime(7),
                IsActive = reader.GetBoolean(8)
            });
        }

        return devices;
    }

    public async Task<DeviceRegistrationDto?> GetDeviceAsync(Guid deviceId)
    {
        const string sql = @"
            SELECT Id, UserId, DeviceName, DeviceType, OsVersion, AppVersion, RegisteredAt, LastSyncAt, IsActive
            FROM DeviceRegistration
            WHERE Id = @DeviceId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@DeviceId", deviceId);

        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new DeviceRegistrationDto
            {
                Id = reader.GetGuid(0),
                UserId = reader.GetGuid(1),
                DeviceName = reader.GetString(2),
                DeviceType = reader.GetString(3),
                OsVersion = reader.GetString(4),
                AppVersion = reader.GetString(5),
                RegisteredAt = reader.GetDateTime(6),
                LastSyncAt = reader.IsDBNull(7) ? null : reader.GetDateTime(7),
                IsActive = reader.GetBoolean(8)
            };
        }

        return null;
    }

    public async Task UpdateDeviceLastSyncAsync(Guid deviceId, DateTime lastSyncAt)
    {
        const string sql = "UPDATE DeviceRegistration SET LastSyncAt = @LastSyncAt WHERE Id = @DeviceId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@DeviceId", deviceId);
        command.Parameters.AddWithValue("@LastSyncAt", lastSyncAt);

        await command.ExecuteNonQueryAsync();
    }

    public async Task UnregisterDeviceAsync(Guid deviceId)
    {
        const string sql = "UPDATE DeviceRegistration SET IsActive = 0 WHERE Id = @DeviceId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@DeviceId", deviceId);

        await command.ExecuteNonQueryAsync();
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

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        if (deviceId.HasValue)
            command.Parameters.AddWithValue("@DeviceId", deviceId.Value);

        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new SyncStatsDto
            {
                UserId = userId,
                DeviceId = deviceId,
                TotalSyncs = 0,
                PendingSyncs = reader.GetInt32(0),
                FailedSyncs = reader.GetInt32(1),
                Conflicts = reader.GetInt32(2),
                LastSyncAt = reader.IsDBNull(3) ? null : reader.GetDateTime(3)
            };
        }

        return new SyncStatsDto { UserId = userId, DeviceId = deviceId };
    }

    private async Task<List<SyncConflictDto>> ReadSyncConflicts(Func<SqlDataReader, SqlDataReader> readerFunc)
    {
        var conflicts = new List<SyncConflictDto>();
        // Would need actual reader implementation
        return conflicts;
    }

    private async Task<List<SyncQueueDto>> ReadSyncQueue(Func<SqlDataReader, SqlDataReader> readerFunc)
    {
        var queue = new List<SyncQueueDto>();
        // Would need actual reader implementation
        return queue;
    }
}
