using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using HighSpeedDAL.Core.Base;
using HighSpeedDAL.Core.Interfaces;
using HighSpeedDAL.Core.Resilience;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace HighSpeedDAL.SqlServer;

/// <summary>
/// SQL Server specific implementation of DAL operations
/// </summary>
public abstract class SqlServerDalBase<TEntity, TConnection> : DalOperationsBase<TEntity, TConnection>
    where TEntity : class, new()
    where TConnection : DatabaseConnectionBase
{
    private readonly DatabaseRetryPolicy _retryPolicy;

    protected SqlServerDalBase(
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
        if (command is not SqlCommand sqlCommand)
        {
            throw new ArgumentException("Command must be SqlCommand", nameof(command));
        }

        if (parameters is IDictionary<string, object> dictionary)
        {
            foreach (KeyValuePair<string, object> kvp in dictionary)
            {
                SqlParameter parameter = new SqlParameter($"@{kvp.Key}", kvp.Value ?? DBNull.Value);
                sqlCommand.Parameters.Add(parameter);
            }
        }
        else
        {
            PropertyInfo[] properties = parameters.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (PropertyInfo property in properties)
            {
                object? value = property.GetValue(parameters);
                SqlParameter parameter = new SqlParameter($"@{property.Name}", value ?? DBNull.Value);
                sqlCommand.Parameters.Add(parameter);
            }
        }
    }

    /// <summary>
    /// Executes a bulk insert using SqlBulkCopy for maximum performance
    /// </summary>
    protected async Task<int> BulkInsertInternalAsync(
        string tableName,
        IEnumerable<TEntity> entities,
        Func<TEntity, Dictionary<string, object>> entityMapper,
        CancellationToken cancellationToken = default)
    {
        if (entities == null || !entities.Any())
        {
            return 0;
        }

        DataTable dataTable = CreateDataTable(tableName, entities, entityMapper);

        using (SqlConnection connection = new SqlConnection(Connection.ConnectionString))
        {
            await connection.OpenAsync(cancellationToken);

            using (SqlBulkCopy bulkCopy = new SqlBulkCopy(connection))
            {
                bulkCopy.DestinationTableName = tableName;
                bulkCopy.BatchSize = 1000;
                bulkCopy.BulkCopyTimeout = 300; // 5 minutes

                foreach (DataColumn column in dataTable.Columns)
                {
                    bulkCopy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
                }

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
        }
    }

    private DataTable CreateDataTable(
        string tableName,
        IEnumerable<TEntity> entities,
        Func<TEntity, Dictionary<string, object>> entityMapper)
    {
        DataTable dataTable = new DataTable(tableName);

        // Get column schema from first entity
        TEntity? firstEntity = entities.FirstOrDefault();
        if (firstEntity == null)
        {
            return dataTable;
        }

        Dictionary<string, object> firstMapping = entityMapper(firstEntity);

        foreach (string columnName in firstMapping.Keys)
        {
            object? value = firstMapping[columnName];
            Type columnType;

            // If value is null or DBNull, infer type from entity property via reflection
            if (value == null || value is DBNull)
            {
                var property = typeof(TEntity).GetProperty(columnName);
                if (property != null)
                {
                    columnType = property.PropertyType;
                    // Handle nullable types
                    if (columnType.IsGenericType && columnType.GetGenericTypeDefinition() == typeof(Nullable<>))
                    {
                        columnType = Nullable.GetUnderlyingType(columnType) ?? typeof(object);
                    }
                }
                else
                {
                    columnType = typeof(string); // Safe default for unknown columns
                }
            }
            else
            {
                columnType = value.GetType();
                // Handle nullable types
                if (columnType.IsGenericType && columnType.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    columnType = Nullable.GetUnderlyingType(columnType) ?? typeof(object);
                }
            }

            dataTable.Columns.Add(columnName, columnType);
        }

        // Add rows
        foreach (TEntity entity in entities)
        {
            Dictionary<string, object> mapping = entityMapper(entity);
            DataRow row = dataTable.NewRow();

            foreach (KeyValuePair<string, object> kvp in mapping)
            {
                row[kvp.Key] = kvp.Value ?? DBNull.Value;
            }

            dataTable.Rows.Add(row);
        }

        return dataTable;
    }
}
