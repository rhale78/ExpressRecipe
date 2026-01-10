using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Reflection;

namespace ExpressRecipe.Data.Common.HighSpeedDAL;

/// <summary>
/// Base class for high-speed DAL operations following HighSpeedDAL patterns.
/// Provides bulk insert, retry logic, and performance-optimized database operations.
/// </summary>
/// <typeparam name="TEntity">The entity type</typeparam>
/// <typeparam name="TConnection">The database connection type</typeparam>
public abstract class DalOperationsBase<TEntity, TConnection> 
    where TEntity : class, new()
    where TConnection : DatabaseConnectionBase
{
    protected readonly TConnection Connection;
    protected readonly ILogger Logger;
    private const int MaxRetryAttempts = 3;
    private const int BaseDelayMilliseconds = 100;

    protected DalOperationsBase(TConnection connection, ILogger logger)
    {
        Connection = connection ?? throw new ArgumentNullException(nameof(connection));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #region Execution Methods with Retry

    /// <summary>
    /// Executes a SQL command with retry logic for transient errors.
    /// </summary>
    protected async Task<int> ExecuteNonQueryAsync(
        string sql,
        object? parameters = null,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteWithRetryAsync(async () =>
        {
            IDbConnection? connection = null;
            bool shouldDisposeConnection = transaction == null;

            try
            {
                connection = transaction?.Connection ?? await Connection.CreateConnectionAsync(cancellationToken);
                
                using var command = connection.CreateCommand();
                command.CommandText = sql;
                command.CommandType = CommandType.Text;
                command.Transaction = transaction;

                if (parameters != null)
                {
                    AddParameters(command, parameters);
                }

                Logger.LogDebug("Executing SQL: {Sql}", sql);
                
                if (command is SqlCommand sqlCmd)
                {
                    return await sqlCmd.ExecuteNonQueryAsync(cancellationToken);
                }
                return command.ExecuteNonQuery();
            }
            finally
            {
                if (shouldDisposeConnection && connection != null)
                {
                    connection.Dispose();
                }
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Executes a SQL query and returns results.
    /// </summary>
    protected async Task<List<TResult>> ExecuteQueryAsync<TResult>(
        string sql,
        Func<IDataReader, TResult> mapper,
        object? parameters = null,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteWithRetryAsync(async () =>
        {
            IDbConnection? connection = null;
            bool shouldDisposeConnection = transaction == null;

            try
            {
                connection = transaction?.Connection ?? await Connection.CreateConnectionAsync(cancellationToken);

                using var command = connection.CreateCommand();
                command.CommandText = sql;
                command.CommandType = CommandType.Text;
                command.Transaction = transaction;

                if (parameters != null)
                {
                    AddParameters(command, parameters);
                }

                Logger.LogDebug("Executing query: {Sql}", sql);

                var results = new List<TResult>();
                
                if (command is SqlCommand sqlCmd)
                {
                    using var reader = await sqlCmd.ExecuteReaderAsync(cancellationToken);
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        results.Add(mapper(reader));
                    }
                }
                else
                {
                    using var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        results.Add(mapper(reader));
                    }
                }

                Logger.LogDebug("Query returned {Count} results", results.Count);
                return results;
            }
            finally
            {
                if (shouldDisposeConnection && connection != null)
                {
                    connection.Dispose();
                }
            }
        }, cancellationToken);
    }

    #endregion

    #region Bulk Operations

    /// <summary>
    /// Executes a bulk insert using SqlBulkCopy for maximum performance.
    /// Inspired by HighSpeedDAL framework.
    /// </summary>
    protected async Task<int> BulkInsertAsync(
        string tableName,
        IEnumerable<TEntity> entities,
        Func<TEntity, Dictionary<string, object>> entityMapper,
        CancellationToken cancellationToken = default)
    {
        var entitiesList = entities.ToList();
        if (!entitiesList.Any())
        {
            return 0;
        }

        var dataTable = CreateDataTable(tableName, entitiesList, entityMapper);

        using var connection = new SqlConnection(Connection.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        using var bulkCopy = new SqlBulkCopy(connection)
        {
            DestinationTableName = tableName,
            BatchSize = 1000,
            BulkCopyTimeout = 300 // 5 minutes
        };

        try
        {
            Logger.LogDebug("Starting bulk insert of {Count} rows into {Table}", dataTable.Rows.Count, tableName);
            await bulkCopy.WriteToServerAsync(dataTable, cancellationToken);
            Logger.LogInformation("Successfully bulk inserted {Count} rows into {Table}", dataTable.Rows.Count, tableName);
            return dataTable.Rows.Count;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Bulk insert failed for table {Table}", tableName);
            throw;
        }
    }

    private DataTable CreateDataTable(
        string tableName,
        List<TEntity> entities,
        Func<TEntity, Dictionary<string, object>> entityMapper)
    {
        var dataTable = new DataTable(tableName);

        // Get column schema from first entity
        var firstEntity = entities.FirstOrDefault();
        if (firstEntity == null)
        {
            return dataTable;
        }

        var firstMapping = entityMapper(firstEntity);

        foreach (var columnName in firstMapping.Keys)
        {
            var columnType = firstMapping[columnName]?.GetType() ?? typeof(object);
            
            // Handle nullable types
            if (columnType.IsGenericType && columnType.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                columnType = Nullable.GetUnderlyingType(columnType) ?? typeof(object);
            }

            dataTable.Columns.Add(columnName, columnType);
        }

        // Add rows
        foreach (var entity in entities)
        {
            var mapping = entityMapper(entity);
            var row = dataTable.NewRow();

            foreach (var kvp in mapping)
            {
                row[kvp.Key] = kvp.Value ?? DBNull.Value;
            }

            dataTable.Rows.Add(row);
        }

        return dataTable;
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Adds parameters to a database command.
    /// </summary>
    protected virtual void AddParameters(IDbCommand command, object parameters)
    {
        if (command is not SqlCommand sqlCommand)
        {
            throw new ArgumentException("Command must be SqlCommand", nameof(command));
        }

        if (parameters is IDictionary<string, object> dictionary)
        {
            foreach (var kvp in dictionary)
            {
                var parameter = new SqlParameter($"@{kvp.Key}", kvp.Value ?? DBNull.Value);
                sqlCommand.Parameters.Add(parameter);
            }
        }
        else
        {
            var properties = parameters.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var property in properties)
            {
                var value = property.GetValue(parameters);
                var parameter = new SqlParameter($"@{property.Name}", value ?? DBNull.Value);
                sqlCommand.Parameters.Add(parameter);
            }
        }
    }

    /// <summary>
    /// Executes an operation with automatic retry logic for transient database errors.
    /// Implements exponential backoff.
    /// </summary>
    protected async Task<TResult> ExecuteWithRetryAsync<TResult>(
        Func<Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < MaxRetryAttempts; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (SqlException ex) when (IsTransientError(ex) && attempt < MaxRetryAttempts - 1)
            {
                var delay = BaseDelayMilliseconds * (int)Math.Pow(2, attempt);
                Logger.LogWarning(ex, 
                    "Transient database error on attempt {Attempt}/{MaxAttempts}. Retrying in {Delay}ms", 
                    attempt + 1, MaxRetryAttempts, delay);
                
                await Task.Delay(delay, cancellationToken);
            }
        }

        // Final attempt without catching the exception
        return await operation();
    }

    /// <summary>
    /// Determines if a SQL exception is transient and should be retried.
    /// </summary>
    private static bool IsTransientError(SqlException ex)
    {
        // Common transient error codes:
        // -2: Timeout
        // 1205: Deadlock victim
        // 40197: Service error
        // 40501: Service busy
        // 40613: Database unavailable
        // 49918: Cannot process request
        return ex.Number switch
        {
            -2 or 1205 or 40197 or 40501 or 40613 or 49918 => true,
            _ => false
        };
    }

    #endregion
}
