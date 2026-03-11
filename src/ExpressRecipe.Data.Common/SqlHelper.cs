using Microsoft.Data.SqlClient;
using System.Data;
using System.Data.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

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

    // Matches SQL Server temp-table names: #name or ##name, letters/digits/underscore only.
    private static readonly Regex TempTableNamePattern =
        new(@"^##?[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

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
    /// Executes a query and returns a single scalar value with cancellation support.
    /// </summary>
    protected async Task<T?> ExecuteScalarAsync<T>(
        string sql,
        CancellationToken cancellationToken,
        params DbParameter[] parameters)
    {
        return await ExecuteScalarAsync<T>(sql, timeoutSeconds: 30, cancellationToken, parameters);
    }

    /// <summary>
    /// Executes a query and returns a single scalar value with custom timeout.
    /// </summary>
    protected async Task<T?> ExecuteScalarAsync<T>(
        string sql,
        int timeoutSeconds,
        params DbParameter[] parameters)
    {
        return await ExecuteScalarAsync<T>(sql, timeoutSeconds, CancellationToken.None, parameters);
    }

    /// <summary>
    /// Executes a query and returns a single scalar value with custom timeout and cancellation support.
    /// </summary>
    protected async Task<T?> ExecuteScalarAsync<T>(
        string sql,
        int timeoutSeconds,
        CancellationToken cancellationToken,
        params DbParameter[] parameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        return await ExecuteWithDeadlockRetryAsync(async () =>
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand(sql, connection);
            command.CommandTimeout = timeoutSeconds;

            if (parameters.Length > 0)
            {
                foreach (var p in parameters)
                {
                    command.Parameters.Add(CloneParameter(p));
                }
            }

            var result = await command.ExecuteScalarAsync(cancellationToken);
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
        return await ExecuteNonQueryAsync(sql, timeoutSeconds: 30, CancellationToken.None, parameters);
    }

    /// <summary>
    /// Executes a command that doesn't return data with cancellation support.
    /// Returns the number of rows affected.
    /// </summary>
    protected async Task<int> ExecuteNonQueryAsync(
        string sql,
        CancellationToken cancellationToken,
        params DbParameter[] parameters)
    {
        return await ExecuteNonQueryAsync(sql, timeoutSeconds: 30, cancellationToken, parameters);
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
        return await ExecuteNonQueryAsync(sql, timeoutSeconds, CancellationToken.None, parameters);
    }

    /// <summary>
    /// Executes a command that doesn't return data with custom timeout and cancellation support.
    /// Returns the number of rows affected.
    /// </summary>
    protected async Task<int> ExecuteNonQueryAsync(
        string sql,
        int timeoutSeconds,
        CancellationToken cancellationToken,
        params DbParameter[] parameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        return await ExecuteWithDeadlockRetryAsync(async () =>
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand(sql, connection);
            command.CommandTimeout = timeoutSeconds;

            if (parameters.Length > 0)
            {
                foreach (var p in parameters)
                {
                    command.Parameters.Add(CloneParameter(p));
                }
            }

            return await command.ExecuteNonQueryAsync(cancellationToken);
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
        return await ExecuteReaderAsync(sql, mapper, timeoutSeconds: 30, CancellationToken.None, parameters);
    }

    /// <summary>
    /// Executes a query and maps results with cancellation support.
    /// </summary>
    protected async Task<List<T>> ExecuteReaderAsync<T>(
        string sql,
        Func<IDataRecord, T> mapper,
        CancellationToken cancellationToken,
        params DbParameter[] parameters)
    {
        return await ExecuteReaderAsync(sql, mapper, timeoutSeconds: 30, cancellationToken, parameters);
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
        return await ExecuteReaderAsync(sql, mapper, timeoutSeconds, CancellationToken.None, parameters);
    }

    /// <summary>
    /// Executes a query and maps results using the provided mapper function with custom timeout and cancellation support.
    /// </summary>
    protected async Task<List<T>> ExecuteReaderAsync<T>(
        string sql,
        Func<IDataRecord, T> mapper,
        int timeoutSeconds,
        CancellationToken cancellationToken,
        params DbParameter[] parameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        ArgumentNullException.ThrowIfNull(mapper);

        return await ExecuteWithDeadlockRetryAsync(async () =>
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync(cancellationToken);

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
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
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

    // Overload for SqlDataReader mapper with cancellation support
    protected async Task<List<T>> ExecuteReaderAsync<T>(
        string sql,
        Func<SqlDataReader, T> mapper,
        CancellationToken cancellationToken,
        params DbParameter[] parameters)
    {
        if (mapper == null) throw new ArgumentNullException(nameof(mapper));

        return await ExecuteReaderAsync(sql, record => mapper((SqlDataReader)record), timeoutSeconds: 30, cancellationToken, parameters);
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
    protected async Task<T> ExecuteWithDeadlockRetryAsync<T>(Func<Task<T>> operation)
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
    protected async Task ExecuteWithDeadlockRetryAsync(Func<Task> operation)
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
    /// Bulk-inserts a list of items using a temporary staging table and SqlBulkCopy.
    /// Callers supply: a column-definition snippet for CREATE TABLE, a delegate that
    /// populates a <see cref="DataTable"/>, and the final SQL to move rows from the
    /// temp table into the target (INSERT … SELECT or MERGE).
    /// </summary>
    /// <typeparam name="T">Domain entity type.</typeparam>
    /// <param name="items">Rows to insert.</param>
    /// <param name="tempTableColumnsSql">Column definitions for the temp table, e.g.
    /// <c>Id UNIQUEIDENTIFIER NOT NULL, Name NVARCHAR(200) NOT NULL</c>.</param>
    /// <param name="dataTableBuilder">Factory that maps <paramref name="items"/> to a
    /// <see cref="DataTable"/> whose schema matches <paramref name="tempTableColumnsSql"/>.</param>
    /// <param name="tempTableName">Name of the temp table (e.g. <c>#StagingProduct</c>).</param>
    /// <param name="finalSql">SQL executed after the bulk-copy (INSERT … SELECT or MERGE).</param>
    /// <returns>Number of rows affected by <paramref name="finalSql"/>.</returns>
    protected async Task<int> BulkInsertViaTvpAsync<T>(
        IReadOnlyList<T> items,
        string tempTableColumnsSql,
        Func<IReadOnlyList<T>, DataTable> dataTableBuilder,
        string tempTableName,
        string finalSql)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentException.ThrowIfNullOrWhiteSpace(tempTableColumnsSql);
        ArgumentNullException.ThrowIfNull(dataTableBuilder);
        ArgumentException.ThrowIfNullOrWhiteSpace(tempTableName);
        ArgumentException.ThrowIfNullOrWhiteSpace(finalSql);

        if (!TempTableNamePattern.IsMatch(tempTableName))
        {
            throw new ArgumentException(
                "tempTableName must be a valid SQL Server temp-table identifier starting with '#' or '##'.",
                nameof(tempTableName));
        }

        if (items.Count == 0)
        {
            return 0;
        }

        return await ExecuteWithDeadlockRetryAsync(async () =>
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();

            // tempTableName is safe to interpolate: TempTableNamePattern validation above
            // restricts it to ^##?[A-Za-z_][A-Za-z0-9_]*$ — no injection chars possible.
            await using var createCmd = new SqlCommand(
                $"CREATE TABLE {tempTableName} ({tempTableColumnsSql});",
                connection);
            await createCmd.ExecuteNonQueryAsync();

            var dataTable = dataTableBuilder(items);
            dataTable.TableName = tempTableName;

            using var bulkCopy = new SqlBulkCopy(connection)
            {
                DestinationTableName = tempTableName,
                BulkCopyTimeout = 120
            };
            await bulkCopy.WriteToServerAsync(dataTable);

            await using var finalCmd = new SqlCommand(finalSql, connection);
            finalCmd.CommandTimeout = 120;
            return await finalCmd.ExecuteNonQueryAsync();
        });
    }

    /// <summary>
    /// Bulk-upserts rows using a temp table + SQL MERGE statement.
    /// Creates the temp table with <paramref name="createTempTableSql"/>, loads
    /// <paramref name="data"/> via SqlBulkCopy, then executes <paramref name="mergeSql"/>.
    /// The full operation is wrapped in deadlock-retry logic.
    /// </summary>
    /// <param name="createTempTableSql">DDL to create the temp table, e.g.
    /// <c>CREATE TABLE #Tmp (Id UNIQUEIDENTIFIER NOT NULL, …)</c>.</param>
    /// <param name="data">Populated <see cref="DataTable"/> whose
    /// <see cref="DataTable.TableName"/> must match the temp table name used in
    /// <paramref name="createTempTableSql"/>.</param>
    /// <param name="mergeSql">MERGE statement that reads from the temp table.</param>
    /// <returns>Number of rows affected by the MERGE.</returns>
    protected async Task<int> BulkMergeAsync(
        string createTempTableSql,
        DataTable data,
        string mergeSql)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(createTempTableSql);
        ArgumentNullException.ThrowIfNull(data);
        ArgumentException.ThrowIfNullOrWhiteSpace(mergeSql);

        if (!createTempTableSql.TrimStart().StartsWith("CREATE TABLE", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "createTempTableSql must begin with 'CREATE TABLE'.",
                nameof(createTempTableSql));
        }

        if (!TempTableNamePattern.IsMatch(data.TableName))
        {
            throw new ArgumentException(
                "data.TableName must be a valid SQL Server temp-table identifier starting with '#' or '##'.",
                nameof(data));
        }

        if (data.Rows.Count == 0)
        {
            return 0;
        }

        return await ExecuteWithDeadlockRetryAsync(async () =>
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();

            await using var createCmd = new SqlCommand(createTempTableSql, connection);
            await createCmd.ExecuteNonQueryAsync();

            using var bulkCopy = new SqlBulkCopy(connection)
            {
                DestinationTableName = data.TableName,
                BulkCopyTimeout = 120
            };
            await bulkCopy.WriteToServerAsync(data);

            await using var mergeCmd = new SqlCommand(mergeSql, connection);
            mergeCmd.CommandTimeout = 120;
            return await mergeCmd.ExecuteNonQueryAsync();
        });
    }

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
}
