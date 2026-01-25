using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace HighSpeedDAL.Core.InMemoryTable
{
    /// <summary>
    /// Implements atomic table swap flush strategy:
    /// 1. Create temp table with same schema as original
    /// 2. Bulk insert all entities into temp table
    /// 3. Lock original table
    /// 4. Drop original table
    /// 5. Rename temp table to original name
    ///
    /// This achieves atomic all-or-nothing semantics with minimal locking duration.
    /// </summary>
    public sealed class TableSwapFlushStrategy<TEntity> : IFlushStrategy<TEntity> where TEntity : class, new()
    {
        private readonly TableSwapFlushStrategyConfig _config;
        private readonly ILogger _logger;
        private readonly Func<List<TEntity>, string, Task<int>> _bulkInsertToTempTable;

        public string StrategyName => "TableSwap";

        public TableSwapFlushStrategy(
            TableSwapFlushStrategyConfig config,
            ILogger logger,
            Func<List<TEntity>, string, Task<int>> bulkInsertToTempTable)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _bulkInsertToTempTable = bulkInsertToTempTable ?? throw new ArgumentNullException(nameof(bulkInsertToTempTable));
        }

        public async Task<int> FlushAsync(List<TEntity> entities, CancellationToken cancellationToken = default)
        {
            if (entities == null || entities.Count == 0)
            {
                _logger.LogDebug("No entities to flush");
                return 0;
            }

            _logger.LogInformation("Starting table swap flush for {Count} entities", entities.Count);

            return _config.DatabaseProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase)
                ? await FlushSqlServerAsync(entities, cancellationToken)
                : _config.DatabaseProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase)
                    ? await FlushSqliteAsync(entities, cancellationToken)
                    : throw new NotSupportedException($"Database provider '{_config.DatabaseProvider}' is not supported");
        }

        private async Task<int> FlushSqlServerAsync(List<TEntity> entities, CancellationToken cancellationToken)
        {
            var tempTableName = $"temp_{Guid.NewGuid():N}";
            var originalTableName = typeof(TEntity).Name;

            using SqlConnection connection = new SqlConnection(_config.ConnectionString);
            await connection.OpenAsync(cancellationToken);

            using SqlTransaction transaction = connection.BeginTransaction(
                ParseIsolationLevel(_config.TransactionIsolationLevel));

            try
            {
                _logger.LogDebug("Creating temporary table: {TempTableName}", tempTableName);

                // Step 1: Create temp table
                var createTempSql = _config.CreateTempTableSql
                    .Replace("{TempTableName}", tempTableName)
                    .Replace("{OriginalTableName}", originalTableName);

                using SqlCommand createCmd = new SqlCommand(createTempSql, connection, transaction);
                createCmd.CommandTimeout = _config.CommandTimeoutSeconds;
                await createCmd.ExecuteNonQueryAsync(cancellationToken);

                _logger.LogDebug("Bulk inserting {Count} entities to temporary table", entities.Count);

                // Step 2: Bulk insert all entities to temp table
                int insertedCount = await _bulkInsertToTempTable(entities, tempTableName);

                if (insertedCount != entities.Count)
                {
                    _logger.LogWarning("Inserted {InsertedCount} entities but expected {ExpectedCount}",
                        insertedCount, entities.Count);
                }

                _logger.LogDebug("Dropping original table: {OriginalTableName}", originalTableName);

                // Step 3: Drop original table (with lock)
                var dropSql = _config.DropOriginalTableSql
                    .Replace("{OriginalTableName}", originalTableName);

                using SqlCommand dropCmd = new SqlCommand(dropSql, connection, transaction);
                dropCmd.CommandTimeout = _config.CommandTimeoutSeconds;
                await dropCmd.ExecuteNonQueryAsync(cancellationToken);

                _logger.LogDebug("Renaming temporary table to: {OriginalTableName}", originalTableName);

                // Step 4: Rename temp table to original
                var renameSql = _config.RenameTempTableSql
                    .Replace("{TempTableName}", tempTableName)
                    .Replace("{OriginalTableName}", originalTableName);

                using SqlCommand renameCmd = new SqlCommand(renameSql, connection, transaction);
                renameCmd.CommandTimeout = _config.CommandTimeoutSeconds;
                await renameCmd.ExecuteNonQueryAsync(cancellationToken);

                // Step 5: Commit transaction (atomic from here on)
                await transaction.CommitAsync(cancellationToken);

                _logger.LogInformation("Successfully flushed {Count} entities via table swap", insertedCount);
                return insertedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to flush table via swap. Attempting rollback.");
                try
                {
                    await transaction.RollbackAsync(cancellationToken);
                    _logger.LogInformation("Successfully rolled back failed flush transaction");
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "Failed to rollback transaction");
                }

                throw;
            }
        }

        private async Task<int> FlushSqliteAsync(List<TEntity> entities, CancellationToken cancellationToken)
        {
            // SQLite version of table swap (simpler since SQLite has fewer locking complications)
            // For now, just log that it's not implemented - SQLite users would use streaming flush
            _logger.LogWarning("Table swap flush not yet implemented for SQLite");
            throw new NotImplementedException("Table swap flush strategy is not yet implemented for SQLite. Use streaming flush instead.");
        }

        private IsolationLevel ParseIsolationLevel(string isolationLevel)
        {
            return isolationLevel switch
            {
                "ReadUncommitted" => IsolationLevel.ReadUncommitted,
                "ReadCommitted" => IsolationLevel.ReadCommitted,
                "RepeatableRead" => IsolationLevel.RepeatableRead,
                "Serializable" => IsolationLevel.Serializable,
                "Snapshot" => IsolationLevel.Snapshot,
                _ => IsolationLevel.ReadCommitted
            };
        }
    }
}
