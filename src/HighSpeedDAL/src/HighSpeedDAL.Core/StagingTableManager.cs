using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using HighSpeedDAL.Core.Attributes;

namespace HighSpeedDAL.Core.Staging
{
    /// <summary>
    /// Manages staging table sync operations for high-write scenarios.
    /// 
    /// Responsibilities:
    /// - Create staging tables matching main table schema
    /// - Schedule periodic syncs based on configured intervals
    /// - Handle conflict resolution during sync
    /// - Provide sync statistics and monitoring
    /// - Support multiple concurrent staging tables
    /// 
    /// Thread-safe for concurrent operations.
    /// 
    /// HighSpeedDAL Framework v0.1 - Phase 3
    /// </summary>
    public sealed class StagingTableManager : IDisposable
    {
        private readonly ILogger<StagingTableManager> _logger;
        private readonly ConcurrentDictionary<string, StagingTableConfig> _stagingTables;
        private readonly ConcurrentDictionary<string, Timer> _syncTimers;
        private readonly SemaphoreSlim _syncLock;
        private bool _disposed;

        /// <summary>
        /// Event raised when sync completes for a table.
        /// Provides statistics about the sync operation.
        /// </summary>
        public event EventHandler<StagingSyncCompletedEventArgs>? SyncCompleted;

        /// <summary>
        /// Event raised when sync fails for a table.
        /// </summary>
        public event EventHandler<StagingSyncFailedEventArgs>? SyncFailed;

        public StagingTableManager(ILogger<StagingTableManager> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _stagingTables = new ConcurrentDictionary<string, StagingTableConfig>(StringComparer.OrdinalIgnoreCase);
            _syncTimers = new ConcurrentDictionary<string, Timer>(StringComparer.OrdinalIgnoreCase);
            _syncLock = new SemaphoreSlim(1, 1);
            _disposed = false;
        }

        /// <summary>
        /// Registers a table for staging pattern and starts sync timer.
        /// </summary>
        public async Task RegisterStagingTableAsync<T>(
            DbConnection connection,
            StagingTableAttribute stagingConfig,
            CancellationToken cancellationToken = default)
        {
            string tableName = typeof(T).Name;
            string stagingTableName = $"{tableName}_Staging";

            StagingTableConfig config = new StagingTableConfig
            {
                MainTableName = tableName,
                StagingTableName = stagingTableName,
                SyncIntervalSeconds = stagingConfig.SyncIntervalSeconds,
                ConflictResolution = stagingConfig.ConflictResolution,
                BatchSize = stagingConfig.BatchSize,
                UseTransaction = stagingConfig.UseTransaction,
                SyncPriority = stagingConfig.SyncPriority,
                RetainStagingHistory = stagingConfig.RetainStagingHistory,
                MirrorIndexes = stagingConfig.MirrorIndexes,
                Connection = connection
            };

            // Add to tracking dictionary
            if (!_stagingTables.TryAdd(tableName, config))
            {
                _logger.LogWarning("Staging table {TableName} already registered", tableName);
                return;
            }

            // Create staging table if configured
            if (stagingConfig.AutoCreateStagingTable)
            {
                await CreateStagingTableAsync(config, cancellationToken).ConfigureAwait(false);
            }

            // Start sync timer
            Timer syncTimer = new Timer(
                async state => await SyncTableAsync(tableName, CancellationToken.None).ConfigureAwait(false),
                null,
                TimeSpan.FromSeconds(config.SyncIntervalSeconds),
                TimeSpan.FromSeconds(config.SyncIntervalSeconds));

            _syncTimers.TryAdd(tableName, syncTimer);

            _logger.LogInformation(
                "Registered staging table {StagingTable} for {MainTable} with {Interval}s sync interval",
                stagingTableName,
                tableName,
                config.SyncIntervalSeconds);
        }

        /// <summary>
        /// Creates staging table matching main table schema.
        /// </summary>
        private async Task CreateStagingTableAsync(
            StagingTableConfig config,
            CancellationToken cancellationToken)
        {
            string createScript = $@"
            -- Create staging table if not exists
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = '{config.StagingTableName}')
            BEGIN
                -- Get main table schema
                SELECT TOP 0 *
                INTO {config.StagingTableName}
                FROM {config.MainTableName};

                -- Add staging metadata columns
                ALTER TABLE {config.StagingTableName}
                ADD 
                    StagingOperation CHAR(1) NOT NULL DEFAULT 'I', -- I=Insert, U=Update, D=Delete
                    StagedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    SyncedAt DATETIME2 NULL,
                    StagingBatchId UNIQUEIDENTIFIER NULL;

                -- Create index on staging metadata
                CREATE NONCLUSTERED INDEX IX_{config.StagingTableName}_Sync
                ON {config.StagingTableName}(SyncedAt, StagingBatchId)
                WHERE SyncedAt IS NULL;
            END";

            DbCommand command = config.Connection.CreateCommand();
            command.CommandText = createScript;
            command.CommandTimeout = 120;

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Created staging table {StagingTable}", config.StagingTableName);
        }

        /// <summary>
        /// Executes sync operation for specified table.
        /// Merges staging data into main table based on conflict resolution strategy.
        /// </summary>
        public async Task SyncTableAsync(string tableName, CancellationToken cancellationToken)
        {
            if (!_stagingTables.TryGetValue(tableName, out StagingTableConfig? config))
            {
                _logger.LogWarning("Attempted to sync unregistered table {TableName}", tableName);
                return;
            }

            await _syncLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                DateTime syncStartTime = DateTime.UtcNow;
                Guid batchId = Guid.NewGuid();
                int recordsProcessed = 0;
                int insertCount = 0;
                int updateCount = 0;
                int deleteCount = 0;
                int conflictCount = 0;

                _logger.LogDebug("Starting sync for {TableName} (Batch: {BatchId})", tableName, batchId);

                DbTransaction? transaction = null;
                if (config.UseTransaction && config.Connection.State == ConnectionState.Open)
                {
                    transaction = await config.Connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
                }

                try
                {
                    // Get staging records to process
                    string selectStagingQuery = $@"
                    SELECT TOP {config.BatchSize} *
                    FROM {config.StagingTableName}
                    WHERE SyncedAt IS NULL
                    ORDER BY StagedAt ASC";

                    DbCommand selectCommand = config.Connection.CreateCommand();
                    selectCommand.CommandText = selectStagingQuery;
                    if (transaction != null)
                    {
                        selectCommand.Transaction = transaction;
                    }

                    List<Dictionary<string, object?>> stagingRecords = [];
                    DbDataReader reader = await selectCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        Dictionary<string, object?> record = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            record[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                        }
                        stagingRecords.Add(record);
                    }
                    await reader.CloseAsync().ConfigureAwait(false);

                    recordsProcessed = stagingRecords.Count;

                    if (recordsProcessed == 0)
                    {
                        _logger.LogDebug("No staging records to sync for {TableName}", tableName);
                        if (transaction != null)
                        {
                            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                        }
                        return;
                    }

                    // Process each staging record
                    foreach (Dictionary<string, object?> stagingRecord in stagingRecords)
                    {
                        string operation = stagingRecord["StagingOperation"]?.ToString() ?? "I";
                        object? primaryKeyValue = stagingRecord["Id"];

                        switch (operation)
                        {
                            case "I": // Insert
                                {
                                    string mergeInsertQuery = GenerateInsertQuery(config.MainTableName, stagingRecord);
                                    DbCommand insertCommand = config.Connection.CreateCommand();
                                    insertCommand.CommandText = mergeInsertQuery;
                                    if (transaction != null)
                                    {
                                        insertCommand.Transaction = transaction;
                                    }

                                    int rowsAffected = await insertCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                                    if (rowsAffected > 0)
                                    {
                                        insertCount++;
                                    }
                                    break;
                                }

                            case "U": // Update
                                {
                                    // Check for conflicts based on strategy
                                    bool shouldUpdate = await ShouldApplyUpdateAsync(
                                        config,
                                        primaryKeyValue,
                                        stagingRecord,
                                        transaction,
                                        cancellationToken).ConfigureAwait(false);

                                    if (shouldUpdate)
                                    {
                                        string mergeUpdateQuery = GenerateUpdateQuery(config.MainTableName, stagingRecord);
                                        DbCommand updateCommand = config.Connection.CreateCommand();
                                        updateCommand.CommandText = mergeUpdateQuery;
                                        if (transaction != null)
                                        {
                                            updateCommand.Transaction = transaction;
                                        }

                                        int rowsAffected = await updateCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                                        if (rowsAffected > 0)
                                        {
                                            updateCount++;
                                        }
                                    }
                                    else
                                    {
                                        conflictCount++;
                                    }
                                    break;
                                }

                            case "D": // Delete
                                {
                                    string deleteQuery = $"DELETE FROM {config.MainTableName} WHERE Id = {primaryKeyValue}";
                                    DbCommand deleteCommand = config.Connection.CreateCommand();
                                    deleteCommand.CommandText = deleteQuery;
                                    if (transaction != null)
                                    {
                                        deleteCommand.Transaction = transaction;
                                    }

                                    int rowsAffected = await deleteCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                                    if (rowsAffected > 0)
                                    {
                                        deleteCount++;
                                    }
                                    break;
                                }
                        }
                    }

                    // Mark staging records as synced or delete them
                    if (config.RetainStagingHistory)
                    {
                        string updateStagingQuery = $@"
                        UPDATE {config.StagingTableName}
                        SET SyncedAt = GETUTCDATE(), StagingBatchId = '{batchId}'
                        WHERE SyncedAt IS NULL";

                        DbCommand updateStagingCommand = config.Connection.CreateCommand();
                        updateStagingCommand.CommandText = updateStagingQuery;
                        if (transaction != null)
                        {
                            updateStagingCommand.Transaction = transaction;
                        }
                        await updateStagingCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        string deleteStagingQuery = $@"
                        DELETE FROM {config.StagingTableName}
                        WHERE SyncedAt IS NULL";

                        DbCommand deleteStagingCommand = config.Connection.CreateCommand();
                        deleteStagingCommand.CommandText = deleteStagingQuery;
                        if (transaction != null)
                        {
                            deleteStagingCommand.Transaction = transaction;
                        }
                        await deleteStagingCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                    }

                    if (transaction != null)
                    {
                        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                    }

                    TimeSpan duration = DateTime.UtcNow - syncStartTime;

                    _logger.LogInformation(
                        "Sync completed for {TableName}: {Records} records ({Inserts}I/{Updates}U/{Deletes}D, {Conflicts} conflicts) in {Duration}ms",
                        tableName,
                        recordsProcessed,
                        insertCount,
                        updateCount,
                        deleteCount,
                        conflictCount,
                        duration.TotalMilliseconds);

                    // Raise sync completed event
                    SyncCompleted?.Invoke(this, new StagingSyncCompletedEventArgs
                    {
                        TableName = tableName,
                        BatchId = batchId,
                        RecordsProcessed = recordsProcessed,
                        InsertCount = insertCount,
                        UpdateCount = updateCount,
                        DeleteCount = deleteCount,
                        ConflictCount = conflictCount,
                        Duration = duration,
                        SyncTime = syncStartTime
                    });
                }
                catch (Exception ex)
                {
                    if (transaction != null)
                    {
                        await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    }

                    _logger.LogError(ex, "Sync failed for {TableName}", tableName);

                    SyncFailed?.Invoke(this, new StagingSyncFailedEventArgs
                    {
                        TableName = tableName,
                        Exception = ex,
                        SyncTime = syncStartTime
                    });

                    throw;
                }
                finally
                {
                    if (transaction != null)
                    {
                        await transaction.DisposeAsync().ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                _syncLock.Release();
            }
        }

        private async Task<bool> ShouldApplyUpdateAsync(
            StagingTableConfig config,
            object? primaryKeyValue,
            Dictionary<string, object?> stagingRecord,
            DbTransaction? transaction,
            CancellationToken cancellationToken)
        {
            if (config.ConflictResolution == ConflictResolution.StagingWins)
            {
                return true;
            }

            if (config.ConflictResolution == ConflictResolution.MainTableWins)
            {
                return false;
            }

            // For LastWriteWins and ThrowOnConflict, check ModifiedDate
            string checkQuery = $@"
            SELECT ModifiedDate 
            FROM {config.MainTableName}
            WHERE Id = {primaryKeyValue}";

            DbCommand checkCommand = config.Connection.CreateCommand();
            checkCommand.CommandText = checkQuery;
            if (transaction != null)
            {
                checkCommand.Transaction = transaction;
            }

            object? mainTableModifiedDate = await checkCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            object? stagingModifiedDate = stagingRecord.ContainsKey("ModifiedDate") ? stagingRecord["ModifiedDate"] : null;

            if (mainTableModifiedDate != null && stagingModifiedDate != null)
            {
                DateTime mainDate = Convert.ToDateTime(mainTableModifiedDate);
                DateTime stagingDate = Convert.ToDateTime(stagingModifiedDate);

                if (mainDate > stagingDate)
                {
                    if (config.ConflictResolution == ConflictResolution.ThrowOnConflict)
                    {
                        throw new InvalidOperationException(
                            $"Conflict detected for {config.MainTableName} Id={primaryKeyValue}: " +
                            $"Main table has newer data ({mainDate}) than staging ({stagingDate})");
                    }
                    return false; // Main table is newer
                }
            }

            return true;
        }

        private string GenerateInsertQuery(string tableName, Dictionary<string, object?> record)
        {
            // Remove staging metadata columns
            List<string> columns = record.Keys
                .Where(k => k != "StagingOperation" && k != "StagedAt" && k != "SyncedAt" && k != "StagingBatchId")
                .ToList();

            string columnList = string.Join(", ", columns);
            string valueList = string.Join(", ", columns.Select(c => FormatValue(record[c])));

            return $"INSERT INTO {tableName} ({columnList}) VALUES ({valueList})";
        }

        private string GenerateUpdateQuery(string tableName, Dictionary<string, object?> record)
        {
            object? idValue = record["Id"];

            // Remove staging metadata columns and Id
            List<string> columns = record.Keys
                .Where(k => k != "Id" && k != "StagingOperation" && k != "StagedAt" && k != "SyncedAt" && k != "StagingBatchId")
                .ToList();

            string setClause = string.Join(", ", columns.Select(c => $"{c} = {FormatValue(record[c])}"));

            return $"UPDATE {tableName} SET {setClause} WHERE Id = {idValue}";
        }

        private string FormatValue(object? value)
        {
            if (value == null || value == DBNull.Value)
            {
                return "NULL";
            }

            return value is string stringValue
                ? $"'{stringValue.Replace("'", "''")}'"
                : value is DateTime dateTime
                ? $"'{dateTime:yyyy-MM-dd HH:mm:ss.fff}'"
                : value is bool boolValue ? boolValue ? "1" : "0" : value.ToString() ?? "NULL";
        }

        /// <summary>
        /// Forces immediate sync for specified table (bypasses timer).
        /// </summary>
        public Task ForceSyncAsync(string tableName, CancellationToken cancellationToken = default)
        {
            return SyncTableAsync(tableName, cancellationToken);
        }

        /// <summary>
        /// Forces immediate sync for all registered staging tables.
        /// </summary>
        public async Task ForceSyncAllAsync(CancellationToken cancellationToken = default)
        {
            foreach (string tableName in _stagingTables.Keys)
            {
                await SyncTableAsync(tableName, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Gets staging statistics for specified table.
        /// </summary>
        public async Task<StagingTableStats> GetStatsAsync(string tableName, CancellationToken cancellationToken = default)
        {
            if (!_stagingTables.TryGetValue(tableName, out StagingTableConfig? config))
            {
                throw new ArgumentException($"Table {tableName} is not registered for staging", nameof(tableName));
            }

            string statsQuery = $@"
            SELECT 
                COUNT(*) as TotalRecords,
                SUM(CASE WHEN SyncedAt IS NULL THEN 1 ELSE 0 END) as PendingRecords,
                SUM(CASE WHEN SyncedAt IS NOT NULL THEN 1 ELSE 0 END) as SyncedRecords,
                MIN(StagedAt) as OldestStagedAt,
                MAX(StagedAt) as NewestStagedAt
            FROM {config.StagingTableName}";

            DbCommand command = config.Connection.CreateCommand();
            command.CommandText = statsQuery;

            DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        
            StagingTableStats stats = new StagingTableStats
            {
                TableName = tableName,
                StagingTableName = config.StagingTableName
            };

            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                stats.TotalRecords = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                stats.PendingRecords = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                stats.SyncedRecords = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
                stats.OldestStagedAt = reader.IsDBNull(3) ? null : reader.GetDateTime(3);
                stats.NewestStagedAt = reader.IsDBNull(4) ? null : reader.GetDateTime(4);
            }

            await reader.CloseAsync().ConfigureAwait(false);

            return stats;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            foreach (Timer timer in _syncTimers.Values)
            {
                timer?.Dispose();
            }

            _syncTimers.Clear();
            _stagingTables.Clear();
            _syncLock?.Dispose();

            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    internal sealed class StagingTableConfig
    {
        public string MainTableName { get; set; } = string.Empty;
        public string StagingTableName { get; set; } = string.Empty;
        public int SyncIntervalSeconds { get; set; }
        public ConflictResolution ConflictResolution { get; set; }
        public int BatchSize { get; set; }
        public bool UseTransaction { get; set; }
        public int SyncPriority { get; set; }
        public bool RetainStagingHistory { get; set; }
        public bool MirrorIndexes { get; set; }
        public DbConnection Connection { get; set; } = null!;
    }

    public sealed class StagingSyncCompletedEventArgs : EventArgs
    {
        public string TableName { get; set; } = string.Empty;
        public Guid BatchId { get; set; }
        public int RecordsProcessed { get; set; }
        public int InsertCount { get; set; }
        public int UpdateCount { get; set; }
        public int DeleteCount { get; set; }
        public int ConflictCount { get; set; }
        public TimeSpan Duration { get; set; }
        public DateTime SyncTime { get; set; }
    }

    public sealed class StagingSyncFailedEventArgs : EventArgs
    {
        public string TableName { get; set; } = string.Empty;
        public Exception Exception { get; set; } = null!;
        public DateTime SyncTime { get; set; }
    }

    public sealed class StagingTableStats
    {
        public string TableName { get; set; } = string.Empty;
        public string StagingTableName { get; set; } = string.Empty;
        public int TotalRecords { get; set; }
        public int PendingRecords { get; set; }
        public int SyncedRecords { get; set; }
        public DateTime? OldestStagedAt { get; set; }
        public DateTime? NewestStagedAt { get; set; }
    }
}
