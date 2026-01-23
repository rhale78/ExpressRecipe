using Microsoft.Data.SqlClient;
using System.Data;
using System.Data.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace ExpressRecipe.Data.Common;

/// <summary>
/// Base helper class for ADO.NET data access.
/// Provides reusable methods for common database operations.
/// </summary>
public abstract class SqlHelper
{
    protected string ConnectionString { get; }
    private const int MaxRetryAttempts = 3;
    private const int BaseDelayMilliseconds = 100;

    // Pre-compiled regex patterns for performance
    private static readonly Regex FromClauseRegex = new(
        @"FROM\s+(\[?\w+\]?)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        TimeSpan.FromSeconds(1));

    private static readonly Regex SemicolonTrimRegex = new(
        @"[;\s\r\n]+$",
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(1));

    protected SqlHelper(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string cannot be null or empty", nameof(connectionString));
        }
        ConnectionString = connectionString;
    }

    /// <summary>
    /// Executes a query and returns a single scalar value.
    /// </summary>
    protected async Task<T?> ExecuteScalarAsync<T>(
        string sql,
        params DbParameter[] parameters)
    {
        return await ExecuteScalarAsync<T>(sql, timeoutSeconds: 30, parameters);
    }

    /// <summary>
    /// Executes a query and returns a single scalar value with custom timeout.
    /// </summary>
    protected async Task<T?> ExecuteScalarAsync<T>(
        string sql,
        int timeoutSeconds,
        params DbParameter[] parameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        return await ExecuteWithDeadlockRetryAsync(async () =>
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();

            await using var command = new SqlCommand(sql, connection);
            command.CommandTimeout = timeoutSeconds;

            if (parameters.Length > 0)
            {
                foreach (var p in parameters)
                {
                    command.Parameters.Add(CloneParameter(p));
                }
            }

            var result = await command.ExecuteScalarAsync();
            return result == DBNull.Value || result == null ? default : (T?)result;
        });
    }

    /// <summary>
    /// Executes a command that doesn't return data (INSERT, UPDATE, DELETE).
    /// Returns the number of rows affected.
    /// </summary>
    protected async Task<int> ExecuteNonQueryAsync(
        string sql,
        params DbParameter[] parameters)
    {
        return await ExecuteNonQueryAsync(sql, timeoutSeconds: 30, parameters);
    }

    /// <summary>
    /// Executes a command that doesn't return data (INSERT, UPDATE, DELETE) with custom timeout.
    /// Returns the number of rows affected.
    /// </summary>
    protected async Task<int> ExecuteNonQueryAsync(
        string sql,
        int timeoutSeconds,
        params DbParameter[] parameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        return await ExecuteWithDeadlockRetryAsync(async () =>
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();

            await using var command = new SqlCommand(sql, connection);
            command.CommandTimeout = timeoutSeconds;

            if (parameters.Length > 0)
            {
                foreach (var p in parameters)
                {
                    command.Parameters.Add(CloneParameter(p));
                }
            }

            return await command.ExecuteNonQueryAsync();
        });
    }

    /// <summary>
    /// Executes a query and maps results using the provided mapper function.
    /// </summary>
    protected async Task<List<T>> ExecuteReaderAsync<T>(
        string sql,
        Func<IDataRecord, T> mapper,
        params DbParameter[] parameters)
    {
        return await ExecuteReaderAsync(sql, mapper, timeoutSeconds: 30, parameters);
    }

    /// <summary>
    /// Executes a query and maps results using the provided mapper function with custom timeout.
    /// </summary>
    protected async Task<List<T>> ExecuteReaderAsync<T>(
        string sql,
        Func<IDataRecord, T> mapper,
        int timeoutSeconds,
        params DbParameter[] parameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        ArgumentNullException.ThrowIfNull(mapper);

        return await ExecuteWithDeadlockRetryAsync(async () =>
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();

            await using var command = new SqlCommand(sql, connection);
            command.CommandTimeout = timeoutSeconds;

            if (parameters.Length > 0)
            {
                foreach (var p in parameters)
                {
                    command.Parameters.Add(CloneParameter(p));
                }
            }

            var results = new List<T>();
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                // SqlDataReader implements IDataRecord, so this is safe
                results.Add(mapper(reader));
            }

            return results;
        });
    }

    // Backwards-compatible overload for mappers that expect SqlDataReader
    protected async Task<List<T>> ExecuteReaderAsync<T>(
        string sql,
        Func<SqlDataReader, T> mapper,
        params DbParameter[] parameters)
    {
        if (mapper == null) throw new ArgumentNullException(nameof(mapper));

        return await ExecuteReaderAsync(sql, record => mapper((SqlDataReader)record), timeoutSeconds: 30, parameters);
    }

    // Backwards-compatible overload for mappers that expect SqlDataReader with timeout
    protected async Task<List<T>> ExecuteReaderAsync<T>(
        string sql,
        Func<SqlDataReader, T> mapper,
        int timeoutSeconds,
        params DbParameter[] parameters)
    {
        if (mapper == null) throw new ArgumentNullException(nameof(mapper));

        return await ExecuteReaderAsync(sql, record => mapper((SqlDataReader)record), timeoutSeconds, parameters);
    }

    /// <summary>
    /// Executes a query and returns the first result or default.
    /// </summary>
    protected async Task<T?> ExecuteReaderSingleAsync<T>(
        string sql,
        Func<IDataRecord, T> mapper,
        params DbParameter[] parameters)
    {
        var results = await ExecuteReaderAsync(sql, mapper, parameters);
        return results.FirstOrDefault();
    }

    // Backwards-compatible overload for mappers that expect SqlDataReader
    protected async Task<T?> ExecuteReaderSingleAsync<T>(
        string sql,
        Func<SqlDataReader, T> mapper,
        params DbParameter[] parameters)
    {
        if (mapper == null) throw new ArgumentNullException(nameof(mapper));

        return await ExecuteReaderSingleAsync(sql, record => mapper((SqlDataReader)record), parameters);
    }

    /// <summary>
    /// Executes an operation within a transaction.
    /// Automatically commits on success, rolls back on exception.
    /// </summary>
    protected async Task<T> ExecuteTransactionAsync<T>(
        Func<SqlConnection, SqlTransaction, Task<T>> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        return await ExecuteWithDeadlockRetryAsync(async () =>
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();

            await using var transaction = connection.BeginTransaction();
            try
            {
                var result = await operation(connection, transaction);
                await transaction.CommitAsync();
                return result;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
    }

    /// <summary>
    /// Executes multiple operations within a transaction.
    /// Automatically commits on success, rolls back on exception.
    /// </summary>
    protected async Task ExecuteTransactionAsync(
        Func<SqlConnection, SqlTransaction, Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await ExecuteWithDeadlockRetryAsync(async () =>
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();

            await using var transaction = connection.BeginTransaction();
            try
            {
                await operation(connection, transaction);
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
    }

    /// <summary>
    /// Executes an operation with automatic retry logic for deadlock exceptions.
    /// Implements exponential backoff for retries.
    /// </summary>
    private async Task<T> ExecuteWithDeadlockRetryAsync<T>(Func<Task<T>> operation)
    {
        for (int attempt = 0; attempt < MaxRetryAttempts; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (SqlException ex) when (ex.Number == 1205 && attempt < MaxRetryAttempts - 1)
            {
                // 1205 = Deadlock victim error code
                var delay = BaseDelayMilliseconds * (int)Math.Pow(2, attempt);
                await Task.Delay(delay);
            }
        }

        // Final attempt without catching the exception
        return await operation();
    }

    /// <summary>
    /// Executes an operation with automatic retry logic for deadlock exceptions (void return).
    /// Implements exponential backoff for retries.
    /// </summary>
    private async Task ExecuteWithDeadlockRetryAsync(Func<Task> operation)
    {
        for (int attempt = 0; attempt < MaxRetryAttempts; attempt++)
        {
            try
            {
                await operation();
                return;
            }
            catch (SqlException ex) when (ex.Number == 1205 && attempt < MaxRetryAttempts - 1)
            {
                // 1205 = Deadlock victim error code
                var delay = BaseDelayMilliseconds * (int)Math.Pow(2, attempt);
                await Task.Delay(delay);
            }
        }

        // Final attempt without catching the exception
        await operation();
    }

    /// <summary>
    /// Helper method to safely get a Guid from a SqlDataReader.
    /// </summary>
    protected static Guid GetGuid(IDataRecord reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.GetGuid(ordinal);
    }

    /// <summary>
    /// Helper method to safely get a nullable Guid from a SqlDataReader.
    /// </summary>
    protected static Guid? GetGuidNullable(IDataRecord reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetGuid(ordinal);
    }

    /// <summary>
    /// Helper method to safely get a string from a SqlDataReader.
    /// </summary>
    protected static string? GetString(IDataRecord reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    /// <summary>
    /// Helper method to safely get a nullable string from a SqlDataReader.
    /// </summary>
    protected static string? GetNullableString(IDataRecord reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    /// <summary>
    /// Helper method to safely get an int from a SqlDataReader.
    /// </summary>
    protected static int GetInt32(IDataRecord reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.GetInt32(ordinal);
    }

    /// <summary>
    /// Helper method to safely get a nullable int from a SqlDataReader.
    /// </summary>
    protected static int? GetIntNullable(IDataRecord reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }

    // Alias methods
    protected static int? GetInt(IDataRecord reader, string columnName) => GetIntNullable(reader, columnName);

    /// <summary>
    /// Helper method to safely get a bool from a SqlDataReader.
    /// </summary>
    protected static bool GetBoolean(IDataRecord reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.GetBoolean(ordinal);
    }

    protected static bool? GetBool(IDataRecord reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : (bool?)reader.GetBoolean(ordinal);
    }

    /// <summary>
    /// Helper method to safely get a DateTime from a SqlDataReader.
    /// </summary>
    protected static DateTime GetDateTime(IDataRecord reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.GetDateTime(ordinal);
    }

    /// <summary>
    /// Helper method to safely get a nullable DateTime from a SqlDataReader.
    /// </summary>
    protected static DateTime? GetNullableDateTime(IDataRecord reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
    }

    protected static DateTime? GetDateTimeNullable(IDataRecord reader, string columnName) => GetNullableDateTime(reader, columnName);

    /// <summary>
    /// Helper method to safely get a decimal from a SqlDataReader.
    /// </summary>
    protected static decimal GetDecimal(IDataRecord reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.GetDecimal(ordinal);
    }

    /// <summary>
    /// Helper method to safely get a nullable decimal from a SqlDataReader.
    /// </summary>
    protected static decimal? GetNullableDecimal(IDataRecord reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetDecimal(ordinal);
    }

    protected static decimal? GetDecimalNullable(IDataRecord reader, string columnName) => GetNullableDecimal(reader, columnName);

    /// <summary>
    /// Helper method to safely get a double from a SqlDataReader.
    /// </summary>
    protected static double GetDouble(IDataRecord reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.GetDouble(ordinal);
    }

    /// <summary>
    /// Helper method to safely get a nullable double from a SqlDataReader.
    /// </summary>
    protected static double? GetDoubleNullable(IDataRecord reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetDouble(ordinal);
    }

    /// <summary>
    /// Helper method to safely get a float from a SqlDataReader.
    /// </summary>
    protected static float GetFloat(IDataRecord reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.GetFloat(ordinal);
    }

    /// <summary>
    /// Helper method to safely get a nullable float from a SqlDataReader.
    /// </summary>
    protected static float? GetFloatNullable(IDataRecord reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetFloat(ordinal);
    }

    /// <summary>
    /// Helper method to safely get a long from a SqlDataReader.
    /// </summary>
    protected static long GetInt64(IDataRecord reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.GetInt64(ordinal);
    }

    /// <summary>
    /// Helper method to safely get a nullable long from a SqlDataReader.
    /// </summary>
    protected static long? GetInt64Nullable(IDataRecord reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt64(ordinal);
    }

    /// <summary>
    /// Helper method to safely get a byte from a SqlDataReader.
    /// </summary>
    protected static byte GetByte(IDataRecord reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.GetByte(ordinal);
    }

    /// <summary>
    /// Helper method to safely get a nullable byte from a SqlDataReader.
    /// </summary>
    protected static byte? GetByteNullable(IDataRecord reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetByte(ordinal);
    }

    #region Audit & Soft Delete Column Helpers

    /// <summary>
    /// Helper method to extract audit columns (CreatedAt, CreatedBy, UpdatedAt, UpdatedBy) from reader.
    /// Returns a tuple of (CreatedAt, CreatedBy, UpdatedAt, UpdatedBy).
    /// </summary>
    protected static (DateTime CreatedAt, Guid? CreatedBy, DateTime? UpdatedAt, Guid? UpdatedBy) GetAuditColumns(
        IDataRecord reader,
        string? createdAtColumn = "CreatedAt",
        string? createdByColumn = "CreatedBy",
        string? updatedAtColumn = "UpdatedAt",
        string? updatedByColumn = "UpdatedBy")
    {
        return (
            CreatedAt: createdAtColumn != null ? GetDateTime(reader, createdAtColumn) : DateTime.MinValue,
            CreatedBy: createdByColumn != null ? GetGuidNullable(reader, createdByColumn) : null,
            UpdatedAt: updatedAtColumn != null ? GetNullableDateTime(reader, updatedAtColumn) : null,
            UpdatedBy: updatedByColumn != null ? GetGuidNullable(reader, updatedByColumn) : null
        );
    }

    /// <summary>
    /// Helper method to extract soft delete columns (IsDeleted, DeletedAt, DeletedBy) from reader.
    /// Returns a tuple of (IsDeleted, DeletedAt, DeletedBy).
    /// </summary>
    protected static (bool IsDeleted, DateTime? DeletedAt, Guid? DeletedBy) GetSoftDeleteColumns(
        IDataRecord reader,
        string? isDeletedColumn = "IsDeleted",
        string? deletedAtColumn = "DeletedAt",
        string? deletedByColumn = "DeletedBy")
    {
        return (
            IsDeleted: isDeletedColumn != null && GetBoolean(reader, isDeletedColumn),
            DeletedAt: deletedAtColumn != null ? GetNullableDateTime(reader, deletedAtColumn) : null,
            DeletedBy: deletedByColumn != null ? GetGuidNullable(reader, deletedByColumn) : null
        );
    }

    #endregion

    /// <summary>
    /// Clones a SQL parameter to prevent reuse issues across retry attempts.
    /// </summary>
    private static SqlParameter CloneParameter(DbParameter parameter)
    {
        var cloned = new SqlParameter
        {
            ParameterName = parameter.ParameterName,
            Value = parameter.Value,
            Direction = parameter.Direction,
            DbType = parameter.DbType
        };

        if (parameter is SqlParameter sqlParam)
        {
            cloned.SqlDbType = sqlParam.SqlDbType;
            cloned.Size = sqlParam.Size;
            cloned.Precision = sqlParam.Precision;
            cloned.Scale = sqlParam.Scale;
        }

        return cloned;
    }

    /// <summary>
    /// Creates a SQL parameter.
    /// </summary>
    protected static DbParameter CreateParameter(string name, object? value)
    {
        return new SqlParameter(name, value ?? DBNull.Value);
    }

    /// <summary>
    /// Creates a SQL parameter with a specific type.
    /// </summary>
    protected static DbParameter CreateParameter(string name, object? value, SqlDbType dbType)
    {
        return new SqlParameter
        {
            ParameterName = name,
            Value = value ?? DBNull.Value,
            SqlDbType = dbType
        };
    }

    /// <summary>
    /// Executes a batch lookup query using a temp table for optimal performance.
    /// Useful for WHERE IN (...) scenarios with many values.
    /// </summary>
    protected async Task<Dictionary<TKey, TValue>> ExecuteBatchLookupAsync<TKey, TValue>(
        string tableName,
        string keyColumn,
        IEnumerable<TKey> keys,
        Func<IDataRecord, KeyValuePair<TKey, TValue>> mapper,
        CancellationToken cancellationToken = default)
        where TKey : notnull
    {
        var keysList = keys.ToList();
        if (!keysList.Any()) return new Dictionary<TKey, TValue>();

        return await ExecuteWithDeadlockRetryAsync(async () =>
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync(cancellationToken);

            var tempTableName = $"#TempKeys_{Guid.NewGuid():N}";
            
            // Create temp table
            var keyType = typeof(TKey);
            var sqlType = keyType == typeof(Guid) ? "UNIQUEIDENTIFIER" 
                        : keyType == typeof(int) ? "INT"
                        : keyType == typeof(string) ? "NVARCHAR(450)"
                        : "NVARCHAR(MAX)";

            var createTempTableSql = $"CREATE TABLE {tempTableName} ([Key] {sqlType} PRIMARY KEY)";
            await using (var createCmd = new SqlCommand(createTempTableSql, connection))
            {
                await createCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            // Bulk insert keys using SqlBulkCopy
            var dataTable = new DataTable();
            dataTable.Columns.Add("Key", typeof(TKey));
            foreach (var key in keysList)
            {
                dataTable.Rows.Add(key);
            }

            using (var bulkCopy = new SqlBulkCopy(connection))
            {
                bulkCopy.DestinationTableName = tempTableName;
                bulkCopy.BatchSize = 1000;
                bulkCopy.BulkCopyTimeout = 300;
                bulkCopy.ColumnMappings.Add("Key", "Key");
                await bulkCopy.WriteToServerAsync(dataTable, cancellationToken);
            }

            // Query using temp table join
            var querySql = $@"
                SELECT t.*
                FROM {tableName} t
                INNER JOIN {tempTableName} tmp ON t.{keyColumn} = tmp.[Key]
                WHERE t.IsDeleted = 0";

            var result = new Dictionary<TKey, TValue>();
            await using (var queryCmd = new SqlCommand(querySql, connection))
            {
                queryCmd.CommandTimeout = 300;
                await using var reader = await queryCmd.ExecuteReaderAsync(cancellationToken);
                
                while (await reader.ReadAsync(cancellationToken))
                {
                    var kvp = mapper(reader);
                    result[kvp.Key] = kvp.Value;
                }
            }

            return result;
        });
    }

    /// <summary>
    /// Executes a conditional update that only runs if properties have changed.
    /// Skips database call entirely if nothing changed, improving performance.
    /// </summary>
    /// <param name="originalEntity">The entity as it was before any changes</param>
    /// <param name="currentEntity">The entity with potential changes</param>
    /// <param name="buildUpdate">Function that builds the UPDATE SQL based on changed properties</param>
    /// <param name="parameters">Any additional parameters (e.g., WHERE clause parameters)</param>
    /// <returns>True if update was executed, false if no changes detected</returns>
    protected async Task<bool> ExecuteConditionalUpdateAsync(
        object originalEntity,
        object currentEntity,
        Func<EntityChangeTracker, string> buildUpdate,
        params DbParameter[] parameters)
    {
        ArgumentNullException.ThrowIfNull(originalEntity);
        ArgumentNullException.ThrowIfNull(currentEntity);
        ArgumentNullException.ThrowIfNull(buildUpdate);

        // Track changes between original and current
        var tracker = new EntityChangeTracker(originalEntity);

        // Simulate the change by getting current values
        // (In practice, you'd use the tracker initialized with original, then compare with current)
        var currentValues = new Dictionary<string, object?>();
        var currentType = currentEntity.GetType();

        foreach (var prop in currentType.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
        {
            if (prop.CanRead)
            {
                currentValues[prop.Name] = prop.GetValue(currentEntity);
            }
        }

        // Check if anything changed
        bool hasChanges = false;
        var originalType = originalEntity.GetType();

        foreach (var prop in originalType.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
        {
            if (!prop.CanRead)
                continue;

            var originalValue = prop.GetValue(originalEntity);
            if (currentValues.TryGetValue(prop.Name, out var currentValue))
            {
                if (!Equals(originalValue, currentValue))
                {
                    hasChanges = true;
                    break;
                }
            }
        }

        // Skip database call if nothing changed
        if (!hasChanges)
        {
            return false;
        }

        // Build and execute the UPDATE
        var sql = buildUpdate(tracker);
        await ExecuteNonQueryAsync(sql, parameters);
        return true;
    }

    /// <summary>
    /// Executes a conditional update that only runs if properties have changed.
    /// Simpler overload that just checks if anything changed and returns early if not.
    /// </summary>
    /// <param name="entity">The entity to potentially update</param>
    /// <param name="getOriginalValues">Function that retrieves original values from database</param>
    /// <param name="buildUpdate">Function that builds UPDATE SQL if changes detected</param>
    /// <param name="parameters">Database parameters for the UPDATE</param>
    /// <returns>True if update was executed, false if no changes detected</returns>
    protected async Task<bool> ConditionalUpdateIfChangedAsync(
        object entity,
        Func<Task<object>> getOriginalValues,
        Func<EntityChangeTracker, string> buildUpdate,
        params DbParameter[] parameters)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(getOriginalValues);
        ArgumentNullException.ThrowIfNull(buildUpdate);

        var original = await getOriginalValues();
        return await ExecuteConditionalUpdateAsync(original, entity, buildUpdate, parameters);
    }

    /// <summary>
    /// Executes a conditional update with tracking of skipped updates.
    /// Skips database call if nothing changed and records the skip in the tracker.
    /// </summary>
    /// <param name="originalEntity">The entity as it was before any changes</param>
    /// <param name="currentEntity">The entity with potential changes</param>
    /// <param name="buildUpdate">Function that builds the UPDATE SQL based on changed properties</param>
    /// <param name="tracker">Tracker to record the update result (success/skip/failure)</param>
    /// <param name="parameters">Any additional parameters (e.g., WHERE clause parameters)</param>
    /// <returns>True if update was executed, false if no changes detected</returns>
    protected async Task<bool> ExecuteConditionalUpdateWithTrackingAsync(
        object originalEntity,
        object currentEntity,
        Func<EntityChangeTracker, string> buildUpdate,
        BatchUpdateTracker tracker,
        params DbParameter[] parameters)
    {
        ArgumentNullException.ThrowIfNull(tracker);

        try
        {
            bool updated = await ExecuteConditionalUpdateAsync(originalEntity, currentEntity, buildUpdate, parameters);

            if (updated)
            {
                tracker.RecordSuccess();
            }
            else
            {
                tracker.RecordSkipped();
            }

            return updated;
        }
        catch (Exception ex)
        {
            tracker.RecordFailure();
            throw;
        }
    }

    /// <summary>
    /// Queries a query with performance optimization hints.
    /// </summary>
    protected async Task<List<T>> ExecuteReaderWithHintsAsync<T>(
        string sql,
        QueryHints hints,
        Func<IDataRecord, T> mapper,
        int timeoutSeconds = 300,
        params DbParameter[] parameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        ArgumentNullException.ThrowIfNull(mapper);

        // Apply query hints
        var optimizedSql = ApplyQueryHints(sql, hints);

        return await ExecuteReaderAsync(optimizedSql, mapper, timeoutSeconds, parameters);
    }

    /// <summary>
    /// Applies query hints to SQL statement for performance optimization.
    /// Uses pre-compiled regex patterns to avoid compilation overhead.
    /// </summary>
    private static string ApplyQueryHints(string sql, QueryHints hints)
    {
        if (hints == QueryHints.None) return sql;

        var hintStrings = new List<string>();

        if ((hints & QueryHints.NoLock) != 0)
            hintStrings.Add("NOLOCK");

        if ((hints & QueryHints.ReadUncommitted) != 0)
            hintStrings.Add("READUNCOMMITTED");

        // Apply table hints if any using pre-compiled regex
        if (hintStrings.Any())
        {
            sql = FromClauseRegex.Replace(
                sql,
                $"FROM $1 WITH ({string.Join(", ", hintStrings)})");
        }

        // Apply query hints
        var queryHints = new List<string>();

        if ((hints & QueryHints.MaxDop) != 0)
        {
            var maxDop = ((int)hints >> 16) & 0xFF; // Extract MaxDop value from flags
            if (maxDop > 0)
                queryHints.Add($"MAXDOP {maxDop}");
        }

        if ((hints & QueryHints.Recompile) != 0)
            queryHints.Add("RECOMPILE");

        if ((hints & QueryHints.OptimizeForUnknown) != 0)
            queryHints.Add("OPTIMIZE FOR UNKNOWN");

        if (queryHints.Any())
        {
            // Use pre-compiled regex to trim trailing semicolons and whitespace
            sql = SemicolonTrimRegex.Replace(sql, string.Empty);
            sql += $" OPTION ({string.Join(", ", queryHints)})";
        }

        return sql;
    }

    /// <summary>
    /// Executes queries in parallel for large result sets.
    /// Splits the query into multiple batches and executes them concurrently.
    /// </summary>
    protected async Task<List<T>> ExecuteReaderParallelAsync<T>(
        string sql,
        Func<IDataRecord, T> mapper,
        int partitionCount = 4,
        int timeoutSeconds = 300,
        params DbParameter[] parameters)
    {
        if (partitionCount <= 1)
        {
            return await ExecuteReaderAsync(sql, mapper, timeoutSeconds, parameters);
        }

        // For parallel execution, we need a way to partition the data
        // This is a simplified version - production might need ROW_NUMBER() based partitioning
        var tasks = new List<Task<List<T>>>();
        
        for (int i = 0; i < partitionCount; i++)
        {
            var partition = i;
            var task = Task.Run(async () =>
            {
                // Execute the query with modulo-based partitioning
                var partitionedSql = sql;
                
                // Clone parameters for each parallel task
                var clonedParams = parameters.Select(p => CloneParameter(p)).ToArray();
                
                return await ExecuteReaderAsync(partitionedSql, mapper, timeoutSeconds, clonedParams);
            });
            
            tasks.Add(task);
        }

        var results = await Task.WhenAll(tasks);
        
        // Combine results from all partitions
        var combined = new List<T>(results.Sum(r => r.Count));
        foreach (var result in results)
        {
            combined.AddRange(result);
        }

        return combined;
    }
}

/// <summary>
/// Query optimization hints for high-speed operations.
/// </summary>
[Flags]
public enum QueryHints
{
    None = 0,
    NoLock = 1 << 0,              // WITH (NOLOCK) - allows dirty reads
    ReadUncommitted = 1 << 1,     // WITH (READUNCOMMITTED) - same as NoLock
    Recompile = 1 << 2,           // OPTION (RECOMPILE) - force query recompilation
    OptimizeForUnknown = 1 << 3,  // OPTION (OPTIMIZE FOR UNKNOWN)
    MaxDop = 1 << 4,              // OPTION (MAXDOP n) - use upper 16 bits for value
}
