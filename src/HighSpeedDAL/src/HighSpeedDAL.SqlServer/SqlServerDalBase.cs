using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using HighSpeedDAL.Core;
using HighSpeedDAL.Core.Base;
using HighSpeedDAL.Core.Interfaces;
using HighSpeedDAL.Core.Resilience;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace HighSpeedDAL.SqlServer
{
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
            DatabaseRetryPolicy retryPolicy,
            DalMetricsCollector? metricsCollector = null)
            : base(connection, logger, connectionFactory, metricsCollector)
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
        /// Executes a bulk insert using SqlBulkCopy for maximum performance.
        /// Handles duplicate key violations by extracting duplicates and retrying.
        /// </summary>
        /// <param name="tableName">Target table name</param>
        /// <param name="entities">Entities to insert</param>
        /// <param name="entityMapper">Function to map entity to column dictionary</param>
        /// <param name="duplicateKeyMatcher">Function that takes (indexName, duplicateValue) and returns matching entities from the list</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>BulkInsertResult containing inserted count and any duplicate entities</returns>
        protected async Task<BulkInsertResult<TEntity>> BulkInsertWithDuplicateHandlingAsync(
            string tableName,
            List<TEntity> entities,
            Func<TEntity, Dictionary<string, object>> entityMapper,
            Func<string, string, List<TEntity>, List<TEntity>> duplicateKeyMatcher,
            CancellationToken cancellationToken = default)
        {
            BulkInsertResult<TEntity> result = new BulkInsertResult<TEntity>
            {
                TotalAttempted = entities.Count
            };

            if (entities.Count == 0)
            {
                return result;
            }

            List<TEntity> remainingEntities = new List<TEntity>(entities);
            int maxRetries = 100; // Prevent infinite loops
            int retryCount = 0;

            while (remainingEntities.Count > 0 && retryCount < maxRetries)
            {
                try
                {
                    DataTable dataTable = CreateDataTable(tableName, remainingEntities, entityMapper);

                    using (SqlConnection connection = new SqlConnection(Connection.ConnectionString))
                    {
                        await connection.OpenAsync(cancellationToken);

                        using (SqlBulkCopy bulkCopy = new SqlBulkCopy(connection))
                        {
                            bulkCopy.DestinationTableName = tableName;
                            bulkCopy.BatchSize = 10000;
                            bulkCopy.BulkCopyTimeout = 300;

                            foreach (DataColumn column in dataTable.Columns)
                            {
                                bulkCopy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
                            }

                            Logger.LogDebug("Bulk insert attempt {Attempt}: {Count} rows into {Table}",
                                retryCount + 1, dataTable.Rows.Count, tableName);

                            await bulkCopy.WriteToServerAsync(dataTable, cancellationToken);

                            result.InsertedCount += remainingEntities.Count;
                            Logger.LogInformation("Successfully bulk inserted {Count} rows into {Table}",
                                remainingEntities.Count, tableName);

                            // All remaining entities inserted successfully
                            break;
                        }
                    }
                }
                catch (SqlException ex) when (ex.Number == 2601 || ex.Number == 2627)
                {
                    // 2601 = unique index violation, 2627 = primary key violation
                    (string? indexName, string? duplicateValue) = ParseDuplicateKeyError(ex.Message);

                    if (string.IsNullOrEmpty(duplicateValue))
                    {
                        Logger.LogWarning("Could not parse duplicate key value from error: {Message}", ex.Message);
                        result.Success = false;
                        result.ErrorMessage = ex.Message;
                        throw;
                    }

                    // Find matching entities using the provided matcher
                    List<TEntity> matchingEntities = duplicateKeyMatcher(indexName, duplicateValue, remainingEntities);

                    if (matchingEntities.Count == 0)
                    {
                        Logger.LogWarning("Duplicate key error but no matching entity found for value '{Value}' on index '{Index}'",
                            duplicateValue, indexName);
                        result.Success = false;
                        result.ErrorMessage = ex.Message;
                        throw;
                    }

                    // Move matched entities to duplicates list
                    foreach (TEntity match in matchingEntities)
                    {
                        remainingEntities.Remove(match);
                        result.DuplicateEntities.Add(match);
                    }

                    Logger.LogDebug("Found {Count} duplicate(s) for value '{Value}', {Remaining} entities remaining",
                        matchingEntities.Count, duplicateValue, remainingEntities.Count);

                    retryCount++;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Bulk insert failed for table {Table}", tableName);
                    result.Success = false;
                    result.ErrorMessage = ex.Message;
                    throw;
                }
            }

            if (retryCount >= maxRetries)
            {
                Logger.LogWarning("Bulk insert hit max retry limit ({Max}) for table {Table}", maxRetries, tableName);
            }

            if (result.HasDuplicates)
            {
                Logger.LogInformation("Bulk insert completed: {Inserted} inserted, {Duplicates} duplicates found for table {Table}",
                    result.InsertedCount, result.DuplicateEntities.Count, tableName);
            }

            return result;
        }

        /// <summary>
        /// Parses the duplicate key error message to extract index name and duplicate value
        /// </summary>
        private static (string indexName, string duplicateValue) ParseDuplicateKeyError(string message)
        {
            // Example: "Cannot insert duplicate key row in object 'dbo.Product' with unique index 'IX_Product_Barcode'. The duplicate key value is (00001327)."
            // Example: "Violation of UNIQUE KEY constraint 'IX_Product_Barcode'. Cannot insert duplicate key in object 'dbo.Product'. The duplicate key value is (00001327)."

            string indexName = "";
            string duplicateValue = "";

            // Try to extract index name - handles both error formats
            Match indexMatch = Regex.Match(message, @"(?:index|constraint)\s+'([^']+)'", RegexOptions.IgnoreCase);
            if (indexMatch.Success)
            {
                indexName = indexMatch.Groups[1].Value;
            }

            // Extract duplicate value - handles single and composite keys
            Match valueMatch = Regex.Match(message, @"duplicate key value is \(([^)]+)\)", RegexOptions.IgnoreCase);
            if (valueMatch.Success)
            {
                duplicateValue = valueMatch.Groups[1].Value.Trim();
            }

            return (indexName, duplicateValue);
        }

        /// <summary>
        /// Executes a bulk insert using SqlBulkCopy for maximum performance (legacy method without duplicate handling)
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
                    bulkCopy.BatchSize = 10000;
                    bulkCopy.BulkCopyTimeout = 300; // 5 minutes

                    foreach (DataColumn column in dataTable.Columns)
                    {
                        bulkCopy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
                    }

                    try
                    {
                        Logger.LogDebug("Starting bulk insert of {Count} rows into {Table}", dataTable.Rows.Count, tableName);
                        await bulkCopy.WriteToServerAsync(dataTable, cancellationToken);
                        Logger.LogDebug("Bulk inserted {Count} rows into {Table}", dataTable.Rows.Count, tableName);
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
                    PropertyInfo? property = typeof(TEntity).GetProperty(columnName);
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
}
