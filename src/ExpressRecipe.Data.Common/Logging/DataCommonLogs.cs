namespace ExpressRecipe.Data.Common.Logging;

using Microsoft.Extensions.Logging;

/// <summary>
/// High-performance source-generated logging for SqlHelper.
/// Called by every database operation across all services.
/// </summary>
public static partial class SqlHelperLogs
{
    [LoggerMessage(
        EventId = 100,
        Level = LogLevel.Debug,
        Message = "[SqlHelper] Query executed in {ElapsedMs}ms")]
    public static partial void LogQueryExecuted(this ILogger logger, long elapsedMs);

    [LoggerMessage(
        EventId = 101,
        Level = LogLevel.Debug,
        Message = "[SqlHelper] Bulk operation: {RowCount} rows affected in {ElapsedMs}ms")]
    public static partial void LogBulkOperation(this ILogger logger, int rowCount, long elapsedMs);

    [LoggerMessage(
        EventId = 102,
        Level = LogLevel.Warning,
        Message = "[SqlHelper] Slow query detected: {ElapsedMs}ms (threshold: {ThresholdMs}ms)")]
    public static partial void LogSlowQuery(this ILogger logger, long elapsedMs, int thresholdMs);

    [LoggerMessage(
        EventId = 103,
        Level = LogLevel.Error,
        Message = "[SqlHelper] SQL execution failed")]
    public static partial void LogSqlError(this ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 104,
        Level = LogLevel.Debug,
        Message = "[SqlHelper] Transaction started")]
    public static partial void LogTransactionStarted(this ILogger logger);

    [LoggerMessage(
        EventId = 105,
        Level = LogLevel.Debug,
        Message = "[SqlHelper] Transaction committed in {ElapsedMs}ms")]
    public static partial void LogTransactionCommitted(this ILogger logger, long elapsedMs);

    [LoggerMessage(
        EventId = 106,
        Level = LogLevel.Warning,
        Message = "[SqlHelper] Transaction rolled back")]
    public static partial void LogTransactionRolledBack(this ILogger logger);
}

/// <summary>
/// High-performance source-generated logging for BulkOperationsHelper.
/// Critical path for bulk imports (500k+ operations).
/// </summary>
public static partial class BulkOperationsLogs
{
    [LoggerMessage(
        EventId = 110,
        Level = LogLevel.Information,
        Message = "[BulkOps] Bulk copy completed: {RowCount} rows to {TableName} in {ElapsedMs}ms ({RecordsPerSec:F1} rec/sec)")]
    public static partial void LogBulkCopyCompleted(this ILogger logger, int rowCount, string tableName, long elapsedMs, double recordsPerSec);

    [LoggerMessage(
        EventId = 111,
        Level = LogLevel.Debug,
        Message = "[BulkOps] Creating temp table: {TableName}")]
    public static partial void LogTempTableCreated(this ILogger logger, string tableName);

    [LoggerMessage(
        EventId = 112,
        Level = LogLevel.Debug,
        Message = "[BulkOps] Temp table populated: {RowCount} rows")]
    public static partial void LogTempTablePopulated(this ILogger logger, int rowCount);

    [LoggerMessage(
        EventId = 113,
        Level = LogLevel.Information,
        Message = "[BulkOps] MERGE operation: {InsertCount} inserted, {UpdateCount} updated in {ElapsedMs}ms")]
    public static partial void LogMergeCompleted(this ILogger logger, int insertCount, int updateCount, long elapsedMs);

    [LoggerMessage(
        EventId = 114,
        Level = LogLevel.Error,
        Message = "[BulkOps] Bulk operation failed for table: {TableName}")]
    public static partial void LogBulkOperationFailed(this ILogger logger, string tableName, Exception exception);

    [LoggerMessage(
        EventId = 115,
        Level = LogLevel.Debug,
        Message = "[BulkOps] Batch size: {BatchSize}, Total rows: {TotalRows}")]
    public static partial void LogBatchInfo(this ILogger logger, int batchSize, int totalRows);
}

/// <summary>
/// High-performance source-generated logging for MigrationRunner.
/// Database schema migrations.
/// </summary>
public static partial class MigrationLogs
{
    [LoggerMessage(
        EventId = 120,
        Level = LogLevel.Information,
        Message = "[Migration] Applying migration: {MigrationId}")]
    public static partial void LogApplyingMigration(this ILogger logger, string migrationId);

    [LoggerMessage(
        EventId = 121,
        Level = LogLevel.Information,
        Message = "[Migration] Migration completed: {MigrationId} in {ElapsedMs}ms")]
    public static partial void LogMigrationCompleted(this ILogger logger, string migrationId, long elapsedMs);

    [LoggerMessage(
        EventId = 122,
        Level = LogLevel.Information,
        Message = "[Migration] Skipping already applied: {MigrationId}")]
    public static partial void LogMigrationSkipped(this ILogger logger, string migrationId);

    [LoggerMessage(
        EventId = 123,
        Level = LogLevel.Information,
        Message = "[Migration] Migration tracking table ensured")]
    public static partial void LogMigrationTableEnsured(this ILogger logger);

    [LoggerMessage(
        EventId = 124,
        Level = LogLevel.Information,
        Message = "[Migration] All migrations applied successfully")]
    public static partial void LogAllMigrationsCompleted(this ILogger logger);

    [LoggerMessage(
        EventId = 125,
        Level = LogLevel.Error,
        Message = "[Migration] Migration failed: {MigrationId}")]
    public static partial void LogMigrationFailed(this ILogger logger, string migrationId, Exception exception);

    [LoggerMessage(
        EventId = 126,
        Level = LogLevel.Debug,
        Message = "[Migration] Executing batch {BatchNumber} of {TotalBatches}")]
    public static partial void LogMigrationBatch(this ILogger logger, int batchNumber, int totalBatches);
}
