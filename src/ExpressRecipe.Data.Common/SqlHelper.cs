using Microsoft.Data.SqlClient;
using System.Data;
using System.Data.Common;
using System;
using System.Collections.Generic;
using System.Linq;
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
