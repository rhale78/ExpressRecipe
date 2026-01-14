using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HighSpeedDAL.Core.Attributes;
using Microsoft.Extensions.Logging;

namespace HighSpeedDAL.Core.InMemoryTable;

/// <summary>
/// Manages multiple in-memory tables and handles flushing to staging/main tables.
/// 
/// Responsibilities:
/// - Register and manage in-memory tables
/// - Schedule periodic flushes based on configuration
/// - Handle flush to staging or direct to main table
/// - Provide statistics and monitoring
/// - Coordinate concurrent flush operations
/// 
/// Thread-safe for concurrent operations.
/// </summary>
public sealed class InMemoryTableManager : IDisposable
{
    private readonly ILogger<InMemoryTableManager> _logger;
    private readonly ConcurrentDictionary<string, IInMemoryTableFlushable> _tables;
    private readonly ConcurrentDictionary<string, Timer> _flushTimers;
    private readonly SemaphoreSlim _flushLock;
    private readonly Func<DbConnection>? _connectionFactory;
    private bool _disposed;

    /// <summary>
    /// Event raised when a flush operation completes
    /// </summary>
    public event EventHandler<FlushCompletedEventArgs>? FlushCompleted;

    /// <summary>
    /// Event raised when a flush operation fails
    /// </summary>
    public event EventHandler<FlushFailedEventArgs>? FlushFailed;

    /// <summary>
    /// Creates a new InMemoryTableManager
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="connectionFactory">Factory to create database connections for flushing</param>
    public InMemoryTableManager(ILogger<InMemoryTableManager> logger, Func<DbConnection>? connectionFactory = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _connectionFactory = connectionFactory;
        _tables = new ConcurrentDictionary<string, IInMemoryTableFlushable>(StringComparer.OrdinalIgnoreCase);
        _flushTimers = new ConcurrentDictionary<string, Timer>(StringComparer.OrdinalIgnoreCase);
        _flushLock = new SemaphoreSlim(1, 1);
        _disposed = false;
    }

    /// <summary>
    /// Registers an in-memory table for management
    /// </summary>
    public InMemoryTable<TEntity> RegisterTable<TEntity>(
        InMemoryTableAttribute? config = null,
        string? tableName = null) where TEntity : class, new()
    {
        ThrowIfDisposed();

        config ??= new InMemoryTableAttribute();
        InMemoryTable<TEntity> table = new InMemoryTable<TEntity>(_logger, config, tableName);

        string name = table.TableName;
        IInMemoryTableFlushable flushable = new InMemoryTableFlushableWrapper<TEntity>(table, config, _logger);

        if (!_tables.TryAdd(name, flushable))
        {
            throw new InvalidOperationException($"Table '{name}' is already registered");
        }

        // Subscribe to flush required events
        table.FlushRequired += OnFlushRequired;

        // Start flush timer if configured
        if (config.FlushIntervalSeconds > 0)
        {
            StartFlushTimer(name, config.FlushIntervalSeconds);
        }

        _logger.LogInformation(
            "Registered in-memory table '{TableName}' with flush interval {Interval}s",
            name, config.FlushIntervalSeconds);

        return table;
    }

    /// <summary>
    /// Gets a registered table by name
    /// </summary>
    public InMemoryTable<TEntity>? GetTable<TEntity>(string tableName) where TEntity : class, new()
    {
        ThrowIfDisposed();

        if (_tables.TryGetValue(tableName, out IInMemoryTableFlushable? flushable) &&
            flushable is InMemoryTableFlushableWrapper<TEntity> wrapper)
        {
            return wrapper.Table;
        }

        return null;
    }

    /// <summary>
    /// Gets all registered table names
    /// </summary>
    public IEnumerable<string> GetTableNames()
    {
        return _tables.Keys;
    }

    /// <summary>
    /// Manually triggers a flush for a specific table
    /// </summary>
    public async Task FlushTableAsync(string tableName, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (!_tables.TryGetValue(tableName, out IInMemoryTableFlushable? flushable))
        {
            throw new ArgumentException($"Table '{tableName}' is not registered");
        }

        await FlushInternalAsync(tableName, flushable, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Manually triggers a flush for all tables
    /// </summary>
    public async Task FlushAllAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        // Sort by priority (highest first)
        List<KeyValuePair<string, IInMemoryTableFlushable>> orderedTables = _tables
            .OrderByDescending(t => t.Value.FlushPriority)
            .ToList();

        foreach (KeyValuePair<string, IInMemoryTableFlushable> kvp in orderedTables)
        {
            await FlushInternalAsync(kvp.Key, kvp.Value, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Gets statistics for all tables
    /// </summary>
    public Dictionary<string, TableStatistics> GetStatistics()
    {
        ThrowIfDisposed();

        Dictionary<string, TableStatistics> stats = new Dictionary<string, TableStatistics>();

        foreach (KeyValuePair<string, IInMemoryTableFlushable> kvp in _tables)
        {
            stats[kvp.Key] = kvp.Value.GetStatistics();
        }

        return stats;
    }

    /// <summary>
    /// Unregisters a table
    /// </summary>
    public void UnregisterTable(string tableName)
    {
        ThrowIfDisposed();

        if (_flushTimers.TryRemove(tableName, out Timer? timer))
        {
            timer.Dispose();
        }

        if (_tables.TryRemove(tableName, out IInMemoryTableFlushable? table))
        {
            table.Dispose();
            _logger.LogInformation("Unregistered in-memory table '{TableName}'", tableName);
        }
    }

    private void StartFlushTimer(string tableName, int intervalSeconds)
    {
        Timer timer = new Timer(
            async _ =>
            {
                if (_tables.TryGetValue(tableName, out IInMemoryTableFlushable? flushable))
                {
                    await FlushInternalAsync(tableName, flushable, CancellationToken.None).ConfigureAwait(false);
                }
            },
            null,
            TimeSpan.FromSeconds(intervalSeconds),
            TimeSpan.FromSeconds(intervalSeconds));

        _flushTimers[tableName] = timer;
    }

    private void OnFlushRequired(object? sender, FlushRequiredEventArgs e)
    {
        _ = Task.Run(async () =>
        {
            if (_tables.TryGetValue(e.TableName, out IInMemoryTableFlushable? flushable))
            {
                await FlushInternalAsync(e.TableName, flushable, CancellationToken.None).ConfigureAwait(false);
            }
        });
    }

    private async Task FlushInternalAsync(
        string tableName,
        IInMemoryTableFlushable flushable,
        CancellationToken cancellationToken)
    {
        await _flushLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            DateTime startTime = DateTime.UtcNow;
            int rowsFlushed = 0;

            _logger.LogDebug("Starting flush for table '{TableName}'", tableName);

            try
            {
                if (_connectionFactory != null)
                {
                    using DbConnection connection = _connectionFactory();
                    if (connection.State != ConnectionState.Open)
                    {
                        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                    }

                    rowsFlushed = await flushable.FlushAsync(connection, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    // No connection factory - just accept changes locally
                    rowsFlushed = flushable.AcceptChangesOnly();
                }

                TimeSpan duration = DateTime.UtcNow - startTime;

                _logger.LogInformation(
                    "Flushed {RowCount} rows from '{TableName}' in {Duration:F2}ms",
                    rowsFlushed, tableName, duration.TotalMilliseconds);

                FlushCompleted?.Invoke(this, new FlushCompletedEventArgs(tableName, rowsFlushed, duration));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to flush table '{TableName}'", tableName);
                FlushFailed?.Invoke(this, new FlushFailedEventArgs(tableName, ex));
            }
        }
        finally
        {
            _flushLock.Release();
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(InMemoryTableManager));
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            foreach (Timer timer in _flushTimers.Values)
            {
                timer.Dispose();
            }
            _flushTimers.Clear();

            foreach (IInMemoryTableFlushable table in _tables.Values)
            {
                table.Dispose();
            }
            _tables.Clear();

            _flushLock.Dispose();
            _disposed = true;
        }
    }
}

#region Supporting Types

/// <summary>
/// Interface for flushable in-memory tables
/// </summary>
internal interface IInMemoryTableFlushable : IDisposable
{
    int FlushPriority { get; }
    Task<int> FlushAsync(DbConnection connection, CancellationToken cancellationToken);
    int AcceptChangesOnly();
    TableStatistics GetStatistics();
}

/// <summary>
/// Wrapper to make InMemoryTable flushable
/// </summary>
internal sealed class InMemoryTableFlushableWrapper<TEntity> : IInMemoryTableFlushable where TEntity : class, new()
{
    private readonly InMemoryTable<TEntity> _table;
    private readonly InMemoryTableAttribute _config;
    private readonly ILogger _logger;
    private int _totalFlushed;
    private DateTime? _lastFlushTime;

    // Cached SQL statements for performance (not readonly since assigned in GenerateCachedSqlStatements)
    private string _cachedInsertSql = string.Empty;
    private string _cachedUpdateSql = string.Empty;
    private string _cachedDeleteSql = string.Empty;
    private string[] _cachedInsertColumns = Array.Empty<string>();
    private string[] _cachedInsertParamNames = Array.Empty<string>();
    private string[] _cachedUpdateSetClauses = Array.Empty<string>();
    private string _cachedUpdateWhereClause = string.Empty;
    private string _cachedDeleteWhereClause = string.Empty;
    private readonly string _tableName;
    private readonly string _pkColumnName;

    public InMemoryTable<TEntity> Table => _table;
    public int FlushPriority => _config.FlushPriority;

    public InMemoryTableFlushableWrapper(
        InMemoryTable<TEntity> table,
        InMemoryTableAttribute config,
        ILogger logger)
    {
        _table = table;
        _config = config;
        _logger = logger;

        // Cache table name and PK column name
        _tableName = config.FlushToStaging ? $"{table.TableName}_Staging" : table.TableName;
        _pkColumnName = table.Schema.PrimaryKeyColumn?.Name ?? "Id";

        // Pre-generate SQL statements for performance
        GenerateCachedSqlStatements();
    }

    private void GenerateCachedSqlStatements()
    {
        List<string> columns = new List<string>();
        List<string> paramNames = new List<string>();
        List<string> setClauses = new List<string>();

        foreach (ColumnDefinition column in _table.Schema.Columns)
        {
            if (column.IsAutoIncrement && column.IsPrimaryKey)
            {
                continue; // Skip auto-increment PK for inserts
            }

            columns.Add($"[{column.Name}]");
            paramNames.Add($"@{column.Name}");

            if (!column.IsPrimaryKey)
            {
                setClauses.Add($"[{column.Name}] = @{column.Name}");
            }
        }

        _cachedInsertColumns = columns.ToArray();
        _cachedInsertParamNames = paramNames.ToArray();
        _cachedUpdateSetClauses = setClauses.ToArray();

        // Generate complete SQL statements
        _cachedInsertSql = $"INSERT INTO [{_tableName}] ({string.Join(", ", _cachedInsertColumns)}) VALUES ({string.Join(", ", _cachedInsertParamNames)})";
        _cachedUpdateSql = $"UPDATE [{_tableName}] SET {string.Join(", ", _cachedUpdateSetClauses)} WHERE [{_pkColumnName}] = @PK";
        _cachedDeleteSql = $"DELETE FROM [{_tableName}] WHERE [{_pkColumnName}] = @PK";

        _cachedUpdateWhereClause = $"[{_pkColumnName}] = @PK";
        _cachedDeleteWhereClause = $"[{_pkColumnName}] = @PK";
    }

    public async Task<int> FlushAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        IReadOnlyList<InMemoryRow> pendingChanges = _table.GetPendingChanges();
        if (pendingChanges.Count == 0)
        {
            return 0;
        }

        int flushed = 0;
        // Use cached table name (already resolved in constructor)

        // Process in batches
        IReadOnlyList<OperationRecord> operations = _table.GetOperationLog();

        foreach (IEnumerable<OperationRecord> batch in Batch(operations, _config.FlushBatchSize))
        {
            using DbTransaction transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                foreach (OperationRecord op in batch)
                {
                    DbCommand command = connection.CreateCommand();
                    command.Transaction = transaction;

                    switch (op.Operation)
                    {
                        case OperationType.Insert:
                            await ExecuteInsertAsync(command, _tableName, op.RowSnapshot!, cancellationToken).ConfigureAwait(false);
                            break;

                        case OperationType.Update:
                            await ExecuteUpdateAsync(command, _tableName, op.RowSnapshot!, op.PrimaryKey, cancellationToken).ConfigureAwait(false);
                            break;

                        case OperationType.Delete:
                            await ExecuteDeleteAsync(command, _tableName, op.PrimaryKey, cancellationToken).ConfigureAwait(false);
                            break;
                    }

                    flushed++;
                }

                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                throw;
            }
        }

        // Accept changes and optionally clear
        _table.AcceptChanges();
        _totalFlushed += flushed;
        _lastFlushTime = DateTime.UtcNow;

        return flushed;
    }

    public int AcceptChangesOnly()
    {
        IReadOnlyList<InMemoryRow> pending = _table.GetPendingChanges();
        int count = pending.Count;
        _table.AcceptChanges();
        _totalFlushed += count;
        _lastFlushTime = DateTime.UtcNow;
        return count;
    }

    public TableStatistics GetStatistics()
    {
        return new TableStatistics
        {
            TableName = _table.TableName,
            CurrentRowCount = _table.RowCount,
            TotalRowCount = _table.TotalRowCount,
            PendingChanges = _table.GetPendingChanges().Count,
            TotalFlushed = _totalFlushed,
            LastFlushTime = _lastFlushTime
        };
    }

    private async Task ExecuteInsertAsync(
        DbCommand command,
        string tableName,
        InMemoryRow row,
        CancellationToken cancellationToken)
    {
        // Use cached SQL statement (already generated in constructor)
        command.CommandText = _cachedInsertSql;

        // Add parameters
        foreach (ColumnDefinition column in _table.Schema.Columns)
        {
            if (column.IsAutoIncrement && column.IsPrimaryKey)
            {
                continue; // Skip auto-increment columns
            }

            DbParameter param = command.CreateParameter();
            param.ParameterName = $"@{column.Name}";
            param.Value = row[column.Name] ?? DBNull.Value;
            command.Parameters.Add(param);
        }

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task ExecuteUpdateAsync(
        DbCommand command,
        string tableName,
        InMemoryRow row,
        object primaryKey,
        CancellationToken cancellationToken)
    {
        // Use cached SQL statement (already generated in constructor)
        command.CommandText = _cachedUpdateSql;

        // Add column parameters
        foreach (ColumnDefinition column in _table.Schema.Columns)
        {
            if (column.IsPrimaryKey)
            {
                continue;
            }

            DbParameter param = command.CreateParameter();
            param.ParameterName = $"@{column.Name}";
            param.Value = row[column.Name] ?? DBNull.Value;
            command.Parameters.Add(param);
        }

        // Add primary key parameter
        DbParameter pkParam = command.CreateParameter();
        pkParam.ParameterName = "@PK";
        pkParam.Value = primaryKey;
        command.Parameters.Add(pkParam);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task ExecuteDeleteAsync(
        DbCommand command,
        string tableName,
        object primaryKey,
        CancellationToken cancellationToken)
    {
        // Use cached SQL statement (already generated in constructor)
        command.CommandText = _cachedDeleteSql;

        DbParameter pkParam = command.CreateParameter();
        pkParam.ParameterName = "@PK";
        pkParam.Value = primaryKey;
        command.Parameters.Add(pkParam);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static IEnumerable<IEnumerable<T>> Batch<T>(IEnumerable<T> source, int batchSize)
    {
        List<T> batch = new List<T>(batchSize);
        foreach (T item in source)
        {
            batch.Add(item);
            if (batch.Count >= batchSize)
            {
                yield return batch;
                batch = new List<T>(batchSize);
            }
        }
        if (batch.Count > 0)
        {
            yield return batch;
        }
    }

    public void Dispose()
    {
        _table.Dispose();
    }
}

/// <summary>
/// Statistics for an in-memory table
/// </summary>
public sealed class TableStatistics
{
    public string TableName { get; set; } = string.Empty;
    public int CurrentRowCount { get; set; }
    public int TotalRowCount { get; set; }
    public int PendingChanges { get; set; }
    public int TotalFlushed { get; set; }
    public DateTime? LastFlushTime { get; set; }
}

/// <summary>
/// Event args for flush completed event
/// </summary>
public sealed class FlushCompletedEventArgs : EventArgs
{
    public string TableName { get; }
    public int RowsFlushed { get; }
    public TimeSpan Duration { get; }

    public FlushCompletedEventArgs(string tableName, int rowsFlushed, TimeSpan duration)
    {
        TableName = tableName;
        RowsFlushed = rowsFlushed;
        Duration = duration;
    }
}

/// <summary>
/// Event args for flush failed event
/// </summary>
public sealed class FlushFailedEventArgs : EventArgs
{
    public string TableName { get; }
    public Exception Exception { get; }

    public FlushFailedEventArgs(string tableName, Exception exception)
    {
        TableName = tableName;
        Exception = exception;
    }
}

#endregion
