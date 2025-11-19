using Microsoft.Data.Sqlite;
using System.Data;

namespace ExpressRecipe.Data.Common;

/// <summary>
/// Base helper class for SQLite data access (local/offline storage).
/// Provides reusable methods for common database operations.
/// </summary>
public abstract class SqliteHelper
{
    protected string ConnectionString { get; }

    protected SqliteHelper(string dbPath)
    {
        if (string.IsNullOrWhiteSpace(dbPath))
        {
            throw new ArgumentException("Database path cannot be null or empty", nameof(dbPath));
        }

        ConnectionString = $"Data Source={dbPath}";
    }

    /// <summary>
    /// Executes a query and returns a single scalar value.
    /// </summary>
    protected async Task<T?> ExecuteScalarAsync<T>(
        string sql,
        params SqliteParameter[] parameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();

        await using var command = new SqliteCommand(sql, connection);
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
        params SqliteParameter[] parameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();

        await using var command = new SqliteCommand(sql, connection);
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
        Func<SqliteDataReader, T> mapper,
        params SqliteParameter[] parameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        ArgumentNullException.ThrowIfNull(mapper);

        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();

        await using var command = new SqliteCommand(sql, connection);
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
        Func<SqliteDataReader, T> mapper,
        params SqliteParameter[] parameters)
    {
        var results = await ExecuteReaderAsync(sql, mapper, parameters);
        return results.FirstOrDefault();
    }

    /// <summary>
    /// Executes an operation within a transaction.
    /// Automatically commits on success, rolls back on exception.
    /// </summary>
    protected async Task<T> ExecuteTransactionAsync<T>(
        Func<SqliteConnection, SqliteTransaction, Task<T>> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await using var connection = new SqliteConnection(ConnectionString);
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
        Func<SqliteConnection, SqliteTransaction, Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await using var connection = new SqliteConnection(ConnectionString);
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
    /// Initialize the database schema.
    /// Override this to create tables on first run.
    /// </summary>
    protected virtual async Task InitializeDatabaseAsync()
    {
        // Default implementation does nothing
        // Override in derived classes to create schema
        await Task.CompletedTask;
    }

    /// <summary>
    /// Helper method to safely get a string from a SqliteDataReader.
    /// </summary>
    protected static string GetString(SqliteDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.GetString(ordinal);
    }

    /// <summary>
    /// Helper method to safely get a nullable string from a SqliteDataReader.
    /// </summary>
    protected static string? GetNullableString(SqliteDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    /// <summary>
    /// Helper method to safely get an int from a SqliteDataReader.
    /// </summary>
    protected static int GetInt32(SqliteDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.GetInt32(ordinal);
    }

    /// <summary>
    /// Helper method to safely get a nullable int from a SqliteDataReader.
    /// </summary>
    protected static int? GetNullableInt32(SqliteDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }

    /// <summary>
    /// Helper method to safely get a bool from a SqliteDataReader.
    /// </summary>
    protected static bool GetBoolean(SqliteDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.GetBoolean(ordinal);
    }

    /// <summary>
    /// Helper method to safely get a long from a SqliteDataReader (SQLite stores dates as Unix timestamps).
    /// </summary>
    protected static long GetInt64(SqliteDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.GetInt64(ordinal);
    }

    /// <summary>
    /// Helper method to safely get a DateTime from a SqliteDataReader.
    /// SQLite stores dates as Unix timestamps (seconds since epoch).
    /// </summary>
    protected static DateTime GetDateTime(SqliteDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        var unixTimestamp = reader.GetInt64(ordinal);
        return DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).DateTime;
    }

    /// <summary>
    /// Helper method to safely get a nullable DateTime from a SqliteDataReader.
    /// </summary>
    protected static DateTime? GetNullableDateTime(SqliteDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        if (reader.IsDBNull(ordinal)) return null;

        var unixTimestamp = reader.GetInt64(ordinal);
        return DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).DateTime;
    }

    /// <summary>
    /// Helper method to safely get a double from a SqliteDataReader.
    /// </summary>
    protected static double GetDouble(SqliteDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.GetDouble(ordinal);
    }

    /// <summary>
    /// Helper method to safely get a nullable double from a SqliteDataReader.
    /// </summary>
    protected static double? GetNullableDouble(SqliteDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetDouble(ordinal);
    }

    /// <summary>
    /// Creates a SQLite parameter.
    /// </summary>
    protected static SqliteParameter CreateParameter(string name, object? value)
    {
        return new SqliteParameter(name, value ?? DBNull.Value);
    }

    /// <summary>
    /// Creates a SQLite parameter with a specific type.
    /// </summary>
    protected static SqliteParameter CreateParameter(string name, object? value, SqliteType dbType)
    {
        return new SqliteParameter
        {
            ParameterName = name,
            Value = value ?? DBNull.Value,
            SqliteType = dbType
        };
    }

    /// <summary>
    /// Converts a DateTime to Unix timestamp for SQLite storage.
    /// </summary>
    protected static long DateTimeToUnixTimestamp(DateTime dateTime)
    {
        return new DateTimeOffset(dateTime).ToUnixTimeSeconds();
    }
}
