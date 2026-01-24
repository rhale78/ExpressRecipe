using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HighSpeedDAL.Core.InMemoryTable;

/// <summary>
/// Defines how an InMemoryTable flushes its data to the backing database.
/// Different strategies allow for different flush patterns (immediate, batched, table swap, etc.)
/// </summary>
public interface IFlushStrategy<TEntity> where TEntity : class, new()
{
    /// <summary>
    /// Flushes the provided entities to the backing database using this strategy.
    /// Should be atomic (all-or-nothing) when possible.
    /// </summary>
    /// <param name="entities">All entities to flush from in-memory table</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of entities flushed</returns>
    Task<int> FlushAsync(List<TEntity> entities, CancellationToken cancellationToken = default);

    /// <summary>
    /// Name of the flush strategy for logging purposes
    /// </summary>
    string StrategyName { get; }
}

/// <summary>
/// Configuration for table swap flush strategy
/// </summary>
public class TableSwapFlushStrategyConfig
{
    /// <summary>
    /// SQL to create temp table (should be same schema as main table)
    /// Placeholder: {TempTableName}, {OriginalTableName}
    /// </summary>
    public required string CreateTempTableSql { get; set; }

    /// <summary>
    /// SQL to bulk insert entities into temp table
    /// Placeholder: {TempTableName}
    /// Should accept parameter @Entities (JSON or serialized format)
    /// </summary>
    public required string BulkInsertToTempTableSql { get; set; }

    /// <summary>
    /// SQL to drop the original table
    /// Placeholder: {OriginalTableName}
    /// </summary>
    public required string DropOriginalTableSql { get; set; }

    /// <summary>
    /// SQL to rename temp table to original table name
    /// Placeholder: {TempTableName}, {OriginalTableName}
    /// </summary>
    public required string RenameTempTableSql { get; set; }

    /// <summary>
    /// Connection string to use for flush operations
    /// </summary>
    public required string ConnectionString { get; set; }

    /// <summary>
    /// Database provider (SqlServer, Sqlite, etc.)
    /// </summary>
    public required string DatabaseProvider { get; set; }

    /// <summary>
    /// Transaction isolation level for flush operations
    /// </summary>
    public required string TransactionIsolationLevel { get; set; }

    /// <summary>
    /// Timeout in seconds for flush operations
    /// </summary>
    public int CommandTimeoutSeconds { get; set; } = 300;
}
