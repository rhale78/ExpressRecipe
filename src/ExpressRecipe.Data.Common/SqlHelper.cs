using Microsoft.Data.SqlClient;
using System.Data;

namespace ExpressRecipe.Data.Common;

/// <summary>
/// Base helper class for ADO.NET data access.
/// Provides reusable methods for common database operations.
/// </summary>
public abstract class SqlHelper
{
    protected string ConnectionString { get; }

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
        params SqlParameter[] parameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        if (parameters.Length > 0)
        {
            command.Parameters.AddRange(parameters);
        }

        var result = await command.ExecuteScalarAsync();
        return result == DBNull.Value || result == null ? default : (T)result;
    }

    /// <summary>
    /// Executes a command that doesn't return data (INSERT, UPDATE, DELETE).
    /// Returns the number of rows affected.
    /// </summary>
    protected async Task<int> ExecuteNonQueryAsync(
        string sql,
        params SqlParameter[] parameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        if (parameters.Length > 0)
        {
            command.Parameters.AddRange(parameters);
        }

        return await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Executes a query and maps results using the provided mapper function.
    /// </summary>
    protected async Task<List<T>> ExecuteReaderAsync<T>(
        string sql,
        Func<SqlDataReader, T> mapper,
        params SqlParameter[] parameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        ArgumentNullException.ThrowIfNull(mapper);

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        if (parameters.Length > 0)
        {
            command.Parameters.AddRange(parameters);
        }

        var results = new List<T>();
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            results.Add(mapper(reader));
        }

        return results;
    }

    /// <summary>
    /// Executes a query and returns the first result or default.
    /// </summary>
    protected async Task<T?> ExecuteReaderSingleAsync<T>(
        string sql,
        Func<SqlDataReader, T> mapper,
        params SqlParameter[] parameters)
    {
        var results = await ExecuteReaderAsync(sql, mapper, parameters);
        return results.FirstOrDefault();
    }

    /// <summary>
    /// Executes an operation within a transaction.
    /// Automatically commits on success, rolls back on exception.
    /// </summary>
    protected async Task<T> ExecuteTransactionAsync<T>(
        Func<SqlConnection, SqlTransaction, Task<T>> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

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
    }

    /// <summary>
    /// Executes multiple operations within a transaction.
    /// Automatically commits on success, rolls back on exception.
    /// </summary>
    protected async Task ExecuteTransactionAsync(
        Func<SqlConnection, SqlTransaction, Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

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
    }

    /// <summary>
    /// Helper method to safely get a Guid from a SqlDataReader.
    /// </summary>
    protected static Guid GetGuid(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.GetGuid(ordinal);
    }

    /// <summary>
    /// Helper method to safely get a nullable Guid from a SqlDataReader.
    /// </summary>
    protected static Guid? GetNullableGuid(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetGuid(ordinal);
    }

    /// <summary>
    /// Helper method to safely get a string from a SqlDataReader.
    /// </summary>
    protected static string GetString(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.GetString(ordinal);
    }

    /// <summary>
    /// Helper method to safely get a nullable string from a SqlDataReader.
    /// </summary>
    protected static string? GetNullableString(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    /// <summary>
    /// Helper method to safely get an int from a SqlDataReader.
    /// </summary>
    protected static int GetInt32(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.GetInt32(ordinal);
    }

    /// <summary>
    /// Helper method to safely get a nullable int from a SqlDataReader.
    /// </summary>
    protected static int? GetNullableInt32(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }

    /// <summary>
    /// Helper method to safely get a bool from a SqlDataReader.
    /// </summary>
    protected static bool GetBoolean(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.GetBoolean(ordinal);
    }

    /// <summary>
    /// Helper method to safely get a DateTime from a SqlDataReader.
    /// </summary>
    protected static DateTime GetDateTime(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.GetDateTime(ordinal);
    }

    /// <summary>
    /// Helper method to safely get a nullable DateTime from a SqlDataReader.
    /// </summary>
    protected static DateTime? GetNullableDateTime(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
    }

    /// <summary>
    /// Helper method to safely get a decimal from a SqlDataReader.
    /// </summary>
    protected static decimal GetDecimal(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.GetDecimal(ordinal);
    }

    /// <summary>
    /// Helper method to safely get a nullable decimal from a SqlDataReader.
    /// </summary>
    protected static decimal? GetNullableDecimal(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetDecimal(ordinal);
    }

    /// <summary>
    /// Creates a SQL parameter.
    /// </summary>
    protected static SqlParameter CreateParameter(string name, object? value)
    {
        return new SqlParameter(name, value ?? DBNull.Value);
    }

    /// <summary>
    /// Creates a SQL parameter with a specific type.
    /// </summary>
    protected static SqlParameter CreateParameter(string name, object? value, SqlDbType dbType)
    {
        return new SqlParameter
        {
            ParameterName = name,
            Value = value ?? DBNull.Value,
            SqlDbType = dbType
        };
    }
}
