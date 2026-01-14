using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using HighSpeedDAL.Core.Base;
using HighSpeedDAL.Core.Interfaces;
using HighSpeedDAL.Core.Resilience;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace HighSpeedDAL.Sqlite;

/// <summary>
/// Connection factory for SQLite databases
/// </summary>
public sealed class SqliteConnectionFactory : IDbConnectionFactory
{
    private readonly ILogger<SqliteConnectionFactory> _logger;

    public SqliteConnectionFactory(ILogger<SqliteConnectionFactory> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IDbConnection> CreateConnectionAsync(
        string connectionString,
        DatabaseProvider provider,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string cannot be null or empty", nameof(connectionString));
        }

        if (provider != DatabaseProvider.Sqlite)
        {
            string errorMessage = $"Invalid provider {provider} for SqliteConnectionFactory. Expected: {DatabaseProvider.Sqlite}";
            _logger.LogError(errorMessage);
            throw new ArgumentException(errorMessage, nameof(provider));
        }

        try
        {
            SqliteConnection connection = new SqliteConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            // Enable foreign keys (not enabled by default in SQLite)
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA foreign_keys = ON;";
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            _logger.LogDebug("SQLite connection opened successfully");
            return connection;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open SQLite connection");
            throw;
        }
    }
}

/// <summary>
/// SQLite specific implementation of DAL operations
/// </summary>
public abstract class SqliteDalBase<TEntity, TConnection> : DalOperationsBase<TEntity, TConnection>
    where TEntity : class, new()
    where TConnection : DatabaseConnectionBase
{
    private readonly DatabaseRetryPolicy _retryPolicy;

    protected SqliteDalBase(
        TConnection connection,
        ILogger logger,
        IDbConnectionFactory connectionFactory,
        DatabaseRetryPolicy retryPolicy)
        : base(connection, logger, connectionFactory)
    {
        _retryPolicy = retryPolicy ?? throw new ArgumentNullException(nameof(retryPolicy));
    }

    protected override async Task<TResult> ExecuteWithRetryAsync<TResult>(
        Func<Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        return await _retryPolicy.ExecuteAsync(operation, cancellationToken);
    }

    protected override void AddParameters(IDbCommand command, object parameters)
    {
        if (command is not SqliteCommand sqliteCommand)
        {
            throw new ArgumentException("Command must be SqliteCommand", nameof(command));
        }

        if (parameters is System.Collections.IDictionary dictionary)
        {
            foreach (System.Collections.DictionaryEntry entry in dictionary)
            {
                string key = entry.Key?.ToString() ?? string.Empty;
                object? value = entry.Value;

                SqliteParameter parameter = new SqliteParameter($"@{key}", value ?? DBNull.Value);
                sqliteCommand.Parameters.Add(parameter);
            }
        }
        else
        {
            System.Reflection.PropertyInfo[] properties = parameters.GetType()
                .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            foreach (System.Reflection.PropertyInfo property in properties)
            {
                object? value = property.GetValue(parameters);
                SqliteParameter parameter = new SqliteParameter($"@{property.Name}", value ?? DBNull.Value);
                sqliteCommand.Parameters.Add(parameter);
            }
        }
    }

    /// <summary>
    /// SQLite doesn't have SqlBulkCopy, so we use batched inserts with transactions
    /// </summary>
    protected async Task<int> BulkInsertInternalAsync(
        string tableName,
        System.Collections.Generic.IEnumerable<TEntity> entities,
        Func<TEntity, System.Collections.Generic.Dictionary<string, object>> entityMapper,
        CancellationToken cancellationToken = default)
    {
        System.Collections.Generic.List<TEntity> entityList = entities.ToList();
        if (entityList.Count == 0)
        {
            return 0;
        }

        int insertedCount = 0;

        using (SqliteConnection connection = new SqliteConnection(Connection.ConnectionString))
        {
            await connection.OpenAsync(cancellationToken);

            using (SqliteTransaction transaction = connection.BeginTransaction())
            {
                try
                {
                    // Build INSERT statement from first entity
                    System.Collections.Generic.Dictionary<string, object> firstMapping = entityMapper(entityList[0]);
                    string columns = string.Join(", ", firstMapping.Keys.Select(k => $"[{k}]"));
                    string parameters = string.Join(", ", firstMapping.Keys.Select(k => $"@{k}"));
                    string insertSql = $"INSERT INTO [{tableName}] ({columns}) VALUES ({parameters})";

                    foreach (TEntity entity in entityList)
                    {
                        using (SqliteCommand command = connection.CreateCommand())
                        {
                            command.Transaction = transaction;
                            command.CommandText = insertSql;

                            System.Collections.Generic.Dictionary<string, object> mapping = entityMapper(entity);
                            foreach (System.Collections.Generic.KeyValuePair<string, object> kvp in mapping)
                            {
                                command.Parameters.AddWithValue($"@{kvp.Key}", kvp.Value ?? DBNull.Value);
                            }

                            await command.ExecuteNonQueryAsync(cancellationToken);
                            insertedCount++;
                        }
                    }

                    transaction.Commit();
                    Logger.LogInformation("Bulk inserted {Count} rows into {Table} using batched inserts", insertedCount, tableName);
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    Logger.LogError(ex, "Bulk insert failed for table {Table}", tableName);
                    throw;
                }
            }
        }

        return insertedCount;
    }
}
