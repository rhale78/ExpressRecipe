using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace HighSpeedDAL.DataManagement.Archival
{
    /// <summary>
    /// Manages automatic archival of old data to archive tables with restore capabilities.
    /// </summary>
    public class ArchivalManager : IArchivalManager
    {
        private readonly ILogger<ArchivalManager> _logger;
        private readonly string _connectionString;
        private readonly bool _isSqlServer;
        private readonly ArchivalOptions _defaultOptions;

        // Cache for reflection metadata
        private readonly ConcurrentDictionary<Type, ArchivalAttribute?> _attributeCache;
        private readonly ConcurrentDictionary<Type, PropertyInfo?> _ageDatePropertyCache;
        private readonly ConcurrentDictionary<Type, PropertyInfo?> _idPropertyCache;
        private readonly ConcurrentDictionary<Type, string> _tableNameCache;
        private readonly ConcurrentDictionary<Type, string> _archiveTableNameCache;

        /// <summary>
        /// Initializes a new instance of the <see cref="ArchivalManager"/> class.
        /// </summary>
        public ArchivalManager(
            ILogger<ArchivalManager> logger,
            string connectionString,
            bool isSqlServer = true,
            ArchivalOptions? defaultOptions = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _isSqlServer = isSqlServer;
            _defaultOptions = defaultOptions ?? new ArchivalOptions();

            _attributeCache = new ConcurrentDictionary<Type, ArchivalAttribute?>();
            _ageDatePropertyCache = new ConcurrentDictionary<Type, PropertyInfo?>();
            _idPropertyCache = new ConcurrentDictionary<Type, PropertyInfo?>();
            _tableNameCache = new ConcurrentDictionary<Type, string>();
            _archiveTableNameCache = new ConcurrentDictionary<Type, string>();
        }

        /// <inheritdoc/>
        public async Task<ArchivalResult> ArchiveAsync<T>(
            CancellationToken cancellationToken = default) where T : class
        {
            ArchivalAttribute? attribute = GetArchivalAttribute<T>();
            return attribute == null
                ? throw new InvalidOperationException($"{typeof(T).Name} is not configured for archival")
                : attribute.Strategy switch
            {
                ArchivalStrategy.ByAge => await ArchiveByAgeAsync<T>(attribute.AgeDays, cancellationToken),
                ArchivalStrategy.ByCount => await ArchiveByCountAsync<T>(attribute.MaxRecordsToKeep, cancellationToken),
                _ => throw new NotSupportedException($"Archival strategy {attribute.Strategy} not yet implemented")
            };
        }

        /// <inheritdoc/>
        public async Task<ArchivalResult> ArchiveByAgeAsync<T>(
            int olderThanDays,
            CancellationToken cancellationToken = default) where T : class
        {
            ArchivalAttribute? attribute = GetArchivalAttribute<T>();
            if (attribute == null)
            {
                throw new InvalidOperationException($"{typeof(T).Name} is not configured for archival");
            }

            try
            {
                // Ensure archive table exists
                if (attribute.AutoCreateArchiveTable)
                {
                    await EnsureArchiveTableExistsAsync<T>(cancellationToken);
                }

                string tableName = GetTableName<T>();
                string archiveTableName = GetArchiveTableName<T>();
                PropertyInfo? ageDateProperty = GetAgeDateProperty<T>(attribute);
                
                if (ageDateProperty == null)
                {
                    throw new InvalidOperationException(
                        $"Age date property '{attribute.AgeDatePropertyName}' not found on {typeof(T).Name}");
                }

                DateTime cutoffDate = DateTime.UtcNow.AddDays(-olderThanDays);
                
                // Get records to archive
                List<object> recordIds = await GetRecordIdsToArchiveByAgeAsync<T>(
                    ageDateProperty.Name,
                    cutoffDate,
                    attribute.BatchSize,
                    cancellationToken);

                if (recordIds.Count == 0)
                {
                    _logger.LogInformation("No records to archive for {EntityType}", typeof(T).Name);
                    return ArchivalResult.CreateSuccess(0, 0, ArchivalStrategy.ByAge, archiveTableName);
                }

                // Copy records to archive table
                int archived = await CopyRecordsToArchiveAsync<T>(
                    recordIds,
                    tableName,
                    archiveTableName,
                    cancellationToken);

                int deleted = 0;
                if (attribute.DeleteAfterArchive && archived > 0)
                {
                    deleted = await DeleteRecordsFromMainTableAsync<T>(recordIds, cancellationToken);
                }

                _logger.LogInformation(
                    "Archived {Archived} records from {EntityType} (deleted: {Deleted})",
                    archived, typeof(T).Name, deleted);

                ArchivalResult result = ArchivalResult.CreateSuccess(
                    archived, deleted, ArchivalStrategy.ByAge, archiveTableName);
                
                if (attribute.LogArchivedIds)
                {
                    result.ArchivedRecordIds = recordIds;
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to archive records for {EntityType}", typeof(T).Name);
                return ArchivalResult.CreateFailure(ex.Message, ArchivalStrategy.ByAge);
            }
        }

        /// <inheritdoc/>
        public async Task<ArchivalResult> ArchiveByCountAsync<T>(
            int maxRecordsToKeep,
            CancellationToken cancellationToken = default) where T : class
        {
            ArchivalAttribute? attribute = GetArchivalAttribute<T>();
            if (attribute == null)
            {
                throw new InvalidOperationException($"{typeof(T).Name} is not configured for archival");
            }

            try
            {
                // Ensure archive table exists
                if (attribute.AutoCreateArchiveTable)
                {
                    await EnsureArchiveTableExistsAsync<T>(cancellationToken);
                }

                string tableName = GetTableName<T>();
                string archiveTableName = GetArchiveTableName<T>();
                PropertyInfo? idProperty = GetIdProperty<T>();
                
                if (idProperty == null)
                {
                    throw new InvalidOperationException($"ID property not found on {typeof(T).Name}");
                }

                // Get total count
                int totalCount = await GetTotalRecordCountAsync<T>(cancellationToken);
                
                if (totalCount <= maxRecordsToKeep)
                {
                    _logger.LogInformation(
                        "Record count ({Count}) is within limit ({Max}) for {EntityType}",
                        totalCount, maxRecordsToKeep, typeof(T).Name);
                    return ArchivalResult.CreateSuccess(0, 0, ArchivalStrategy.ByCount, archiveTableName);
                }

                int toArchive = totalCount - maxRecordsToKeep;

                // Get oldest records to archive
                List<object> recordIds = await GetOldestRecordIdsAsync<T>(
                    toArchive,
                    attribute.AgeDatePropertyName,
                    cancellationToken);

                // Copy records to archive table
                int archived = await CopyRecordsToArchiveAsync<T>(
                    recordIds,
                    tableName,
                    archiveTableName,
                    cancellationToken);

                int deleted = 0;
                if (attribute.DeleteAfterArchive && archived > 0)
                {
                    deleted = await DeleteRecordsFromMainTableAsync<T>(recordIds, cancellationToken);
                }

                _logger.LogInformation(
                    "Archived {Archived} records from {EntityType} to maintain max count of {Max}",
                    archived, typeof(T).Name, maxRecordsToKeep);

                ArchivalResult result = ArchivalResult.CreateSuccess(
                    archived, deleted, ArchivalStrategy.ByCount, archiveTableName);
                
                if (attribute.LogArchivedIds)
                {
                    result.ArchivedRecordIds = recordIds;
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to archive records by count for {EntityType}", typeof(T).Name);
                return ArchivalResult.CreateFailure(ex.Message, ArchivalStrategy.ByCount);
            }
        }

        /// <inheritdoc/>
        public async Task<int> RestoreFromArchiveAsync<T>(
            IEnumerable<object> recordIds,
            bool deleteFromArchive = true,
            CancellationToken cancellationToken = default) where T : class
        {
            if (recordIds == null)
            {
                throw new ArgumentNullException(nameof(recordIds));
            }

            ArchivalAttribute? attribute = GetArchivalAttribute<T>();
            if (attribute == null)
            {
                throw new InvalidOperationException($"{typeof(T).Name} is not configured for archival");
            }

            string tableName = GetTableName<T>();
            string archiveTableName = GetArchiveTableName<T>();
            PropertyInfo? idProperty = GetIdProperty<T>();
            
            if (idProperty == null)
            {
                throw new InvalidOperationException($"ID property not found on {typeof(T).Name}");
            }

            int restored = 0;

            try
            {
                List<object> idList = recordIds.ToList();
                
                // Copy from archive back to main table
                restored = await CopyRecordsToArchiveAsync<T>(
                    idList,
                    archiveTableName,
                    tableName,
                    cancellationToken);

                // Delete from archive if requested
                if (deleteFromArchive && restored > 0)
                {
                    await PurgeFromArchiveAsync<T>(idList, cancellationToken);
                }

                _logger.LogInformation(
                    "Restored {Count} records from archive for {EntityType}",
                    restored, typeof(T).Name);

                return restored;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to restore records for {EntityType}", typeof(T).Name);
                return restored;
            }
        }

        /// <inheritdoc/>
        public async Task<List<T>> GetArchivedRecordsAsync<T>(
            CancellationToken cancellationToken = default) where T : class
        {
            ArchivalAttribute? attribute = GetArchivalAttribute<T>();
            if (attribute == null)
            {
                return [];
            }

            string archiveTableName = GetArchiveTableName<T>();
            string sql = $"SELECT * FROM {archiveTableName}";

            return await ExecuteQueryAsync<T>(sql, null, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<List<T>> GetArchivedRecordsInRangeAsync<T>(
            DateTime startDate,
            DateTime endDate,
            CancellationToken cancellationToken = default) where T : class
        {
            ArchivalAttribute? attribute = GetArchivalAttribute<T>();
            if (attribute == null)
            {
                return [];
            }

            string archiveTableName = GetArchiveTableName<T>();
            string sql = $@"
                SELECT *
                FROM {archiveTableName}
                WHERE {attribute.AgeDatePropertyName} >= @StartDate
                  AND {attribute.AgeDatePropertyName} <= @EndDate";

            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { "@StartDate", startDate },
                { "@EndDate", endDate }
            };

            return await ExecuteQueryAsync<T>(sql, parameters, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<int> GetArchivedCountAsync<T>(
            CancellationToken cancellationToken = default) where T : class
        {
            ArchivalAttribute? attribute = GetArchivalAttribute<T>();
            if (attribute == null)
            {
                return 0;
            }

            string archiveTableName = GetArchiveTableName<T>();
            string sql = $"SELECT COUNT(*) FROM {archiveTableName}";

            using (DbConnection connection = CreateConnection())
            {
                await connection.OpenAsync(cancellationToken);

                using (DbCommand command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    object? result = await command.ExecuteScalarAsync(cancellationToken);
                    return Convert.ToInt32(result);
                }
            }
        }

        /// <inheritdoc/>
        public async Task CreateArchiveTableAsync<T>(
            CancellationToken cancellationToken = default) where T : class
        {
            ArchivalAttribute? attribute = GetArchivalAttribute<T>();
            if (attribute == null)
            {
                return;
            }

            string tableName = GetTableName<T>();
            string archiveTableName = GetArchiveTableName<T>();

            string createTableSql;

            if (_isSqlServer)
            {
                createTableSql = $@"
                    IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'{archiveTableName}') AND type in (N'U'))
                    BEGIN
                        SELECT * 
                        INTO {archiveTableName}
                        FROM {tableName}
                        WHERE 1=0
                    END";
            }
            else
            {
                // For SQLite, create a copy of the table structure
                createTableSql = $@"
                    CREATE TABLE IF NOT EXISTS {archiveTableName} AS 
                    SELECT * FROM {tableName} WHERE 1=0";
            }

            using (DbConnection connection = CreateConnection())
            {
                await connection.OpenAsync(cancellationToken);

                using (DbCommand command = connection.CreateCommand())
                {
                    command.CommandText = createTableSql;
                    await command.ExecuteNonQueryAsync(cancellationToken);
                }
            }

            _logger.LogInformation(
                "Created archive table {ArchiveTable} for {EntityType}",
                archiveTableName, typeof(T).Name);
        }

        /// <inheritdoc/>
        public async Task<int> PurgeFromArchiveAsync<T>(
            IEnumerable<object> recordIds,
            CancellationToken cancellationToken = default) where T : class
        {
            if (recordIds == null)
            {
                throw new ArgumentNullException(nameof(recordIds));
            }

            ArchivalAttribute? attribute = GetArchivalAttribute<T>();
            if (attribute == null)
            {
                throw new InvalidOperationException($"{typeof(T).Name} is not configured for archival");
            }

            PropertyInfo? idProperty = GetIdProperty<T>();
            if (idProperty == null)
            {
                throw new InvalidOperationException($"ID property not found on {typeof(T).Name}");
            }

            string archiveTableName = GetArchiveTableName<T>();
            int deleted = 0;

            using (DbConnection connection = CreateConnection())
            {
                await connection.OpenAsync(cancellationToken);

                foreach (object id in recordIds)
                {
                    using (DbCommand command = connection.CreateCommand())
                    {
                        command.CommandText = $"DELETE FROM {archiveTableName} WHERE {idProperty.Name} = @Id";
                        AddParameter(command, "@Id", id);
                        deleted += await command.ExecuteNonQueryAsync(cancellationToken);
                    }
                }
            }

            _logger.LogInformation(
                "Purged {Count} records from archive for {EntityType}",
                deleted, typeof(T).Name);

            return deleted;
        }

        /// <inheritdoc/>
        public bool IsArchivalEnabled<T>() where T : class
        {
            return GetArchivalAttribute<T>() != null;
        }

        /// <inheritdoc/>
        public ArchivalAttribute? GetArchivalAttribute<T>() where T : class
        {
            return _attributeCache.GetOrAdd(typeof(T), type =>
            {
                return type.GetCustomAttribute<ArchivalAttribute>(inherit: true);
            });
        }

        #region Private Helper Methods

        private async Task EnsureArchiveTableExistsAsync<T>(CancellationToken cancellationToken) where T : class
        {
            string archiveTableName = GetArchiveTableName<T>();

            string checkTableSql;
            if (_isSqlServer)
            {
                checkTableSql = $@"
                    SELECT COUNT(*) 
                    FROM sys.objects 
                    WHERE object_id = OBJECT_ID(N'{archiveTableName}') AND type in (N'U')";
            }
            else
            {
                checkTableSql = $@"
                    SELECT COUNT(*) 
                    FROM sqlite_master 
                    WHERE type='table' AND name='{archiveTableName}'";
            }

            using (DbConnection connection = CreateConnection())
            {
                await connection.OpenAsync(cancellationToken);

                using (DbCommand command = connection.CreateCommand())
                {
                    command.CommandText = checkTableSql;
                    object? result = await command.ExecuteScalarAsync(cancellationToken);
                    int tableCount = Convert.ToInt32(result);

                    if (tableCount == 0)
                    {
                        await CreateArchiveTableAsync<T>(cancellationToken);
                    }
                }
            }
        }

        private async Task<List<object>> GetRecordIdsToArchiveByAgeAsync<T>(
            string ageDateColumn,
            DateTime cutoffDate,
            int batchSize,
            CancellationToken cancellationToken) where T : class
        {
            string tableName = GetTableName<T>();
            PropertyInfo? idProperty = GetIdProperty<T>();
            
            if (idProperty == null)
            {
                throw new InvalidOperationException($"ID property not found on {typeof(T).Name}");
            }

            string sql;
            if (_isSqlServer)
            {
                sql = $@"
                    SELECT TOP {batchSize} {idProperty.Name}
                    FROM {tableName}
                    WHERE {ageDateColumn} < @CutoffDate
                    ORDER BY {ageDateColumn} ASC";
            }
            else
            {
                sql = $@"
                    SELECT {idProperty.Name}
                    FROM {tableName}
                    WHERE {ageDateColumn} < @CutoffDate
                    ORDER BY {ageDateColumn} ASC
                    LIMIT {batchSize}";
            }

            List<object> ids = [];

            using (DbConnection connection = CreateConnection())
            {
                await connection.OpenAsync(cancellationToken);

                using (DbCommand command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    AddParameter(command, "@CutoffDate", cutoffDate);

                    using (DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
                    {
                        while (await reader.ReadAsync(cancellationToken))
                        {
                            ids.Add(reader.GetValue(0));
                        }
                    }
                }
            }

            return ids;
        }

        private async Task<List<object>> GetOldestRecordIdsAsync<T>(
            int count,
            string ageDateColumn,
            CancellationToken cancellationToken) where T : class
        {
            string tableName = GetTableName<T>();
            PropertyInfo? idProperty = GetIdProperty<T>();
            
            if (idProperty == null)
            {
                throw new InvalidOperationException($"ID property not found on {typeof(T).Name}");
            }

            string sql;
            if (_isSqlServer)
            {
                sql = $@"
                    SELECT TOP {count} {idProperty.Name}
                    FROM {tableName}
                    ORDER BY {ageDateColumn} ASC";
            }
            else
            {
                sql = $@"
                    SELECT {idProperty.Name}
                    FROM {tableName}
                    ORDER BY {ageDateColumn} ASC
                    LIMIT {count}";
            }

            List<object> ids = [];

            using (DbConnection connection = CreateConnection())
            {
                await connection.OpenAsync(cancellationToken);

                using (DbCommand command = connection.CreateCommand())
                {
                    command.CommandText = sql;

                    using (DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
                    {
                        while (await reader.ReadAsync(cancellationToken))
                        {
                            ids.Add(reader.GetValue(0));
                        }
                    }
                }
            }

            return ids;
        }

        private async Task<int> GetTotalRecordCountAsync<T>(CancellationToken cancellationToken) where T : class
        {
            string tableName = GetTableName<T>();
            string sql = $"SELECT COUNT(*) FROM {tableName}";

            using (DbConnection connection = CreateConnection())
            {
                await connection.OpenAsync(cancellationToken);

                using (DbCommand command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    object? result = await command.ExecuteScalarAsync(cancellationToken);
                    return Convert.ToInt32(result);
                }
            }
        }

        private async Task<int> CopyRecordsToArchiveAsync<T>(
            List<object> recordIds,
            string sourceTable,
            string destinationTable,
            CancellationToken cancellationToken) where T : class
        {
            if (recordIds.Count == 0)
            {
                return 0;
            }

            PropertyInfo? idProperty = GetIdProperty<T>();
            if (idProperty == null)
            {
                throw new InvalidOperationException($"ID property not found on {typeof(T).Name}");
            }

            // Build parameterized IN clause
            StringBuilder inClause = new StringBuilder();
            for (int i = 0; i < recordIds.Count; i++)
            {
                if (i > 0)
                {
                    inClause.Append(", ");
                }
                inClause.Append($"@Id{i}");
            }

            string sql = $@"
                INSERT INTO {destinationTable}
                SELECT * FROM {sourceTable}
                WHERE {idProperty.Name} IN ({inClause})";

            using (DbConnection connection = CreateConnection())
            {
                await connection.OpenAsync(cancellationToken);

                using (DbCommand command = connection.CreateCommand())
                {
                    command.CommandText = sql;

                    for (int i = 0; i < recordIds.Count; i++)
                    {
                        AddParameter(command, $"@Id{i}", recordIds[i]);
                    }

                    return await command.ExecuteNonQueryAsync(cancellationToken);
                }
            }
        }

        private async Task<int> DeleteRecordsFromMainTableAsync<T>(
            List<object> recordIds,
            CancellationToken cancellationToken) where T : class
        {
            if (recordIds.Count == 0)
            {
                return 0;
            }

            string tableName = GetTableName<T>();
            PropertyInfo? idProperty = GetIdProperty<T>();
            
            if (idProperty == null)
            {
                throw new InvalidOperationException($"ID property not found on {typeof(T).Name}");
            }

            // Build parameterized IN clause
            StringBuilder inClause = new StringBuilder();
            for (int i = 0; i < recordIds.Count; i++)
            {
                if (i > 0)
                {
                    inClause.Append(", ");
                }
                inClause.Append($"@Id{i}");
            }

            string sql = $"DELETE FROM {tableName} WHERE {idProperty.Name} IN ({inClause})";

            using (DbConnection connection = CreateConnection())
            {
                await connection.OpenAsync(cancellationToken);

                using (DbCommand command = connection.CreateCommand())
                {
                    command.CommandText = sql;

                    for (int i = 0; i < recordIds.Count; i++)
                    {
                        AddParameter(command, $"@Id{i}", recordIds[i]);
                    }

                    return await command.ExecuteNonQueryAsync(cancellationToken);
                }
            }
        }

        private PropertyInfo? GetAgeDateProperty<T>(ArchivalAttribute attribute) where T : class
        {
            return _ageDatePropertyCache.GetOrAdd(typeof(T), type =>
            {
                return type.GetProperty(attribute.AgeDatePropertyName, BindingFlags.Public | BindingFlags.Instance);
            });
        }

        private PropertyInfo? GetIdProperty<T>() where T : class
        {
            return _idPropertyCache.GetOrAdd(typeof(T), type =>
            {
                PropertyInfo? idProp = type.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
                return idProp != null ? idProp : type.GetProperty($"{type.Name}Id", BindingFlags.Public | BindingFlags.Instance);
            });
        }

        private string GetTableName<T>() where T : class
        {
            return _tableNameCache.GetOrAdd(typeof(T), type => $"{type.Name}s");
        }

        private string GetArchiveTableName<T>() where T : class
        {
            return _archiveTableNameCache.GetOrAdd(typeof(T), type =>
            {
                ArchivalAttribute? attribute = GetArchivalAttribute<T>();
                return attribute != null && !string.IsNullOrEmpty(attribute.ArchiveTableName)
                    ? attribute.ArchiveTableName
                    : $"{GetTableName<T>()}Archive";
            });
        }

        private DbConnection CreateConnection()
        {
            return _isSqlServer ? new SqlConnection(_connectionString) : new SqliteConnection(_connectionString);
        }

        private void AddParameter(DbCommand command, string parameterName, object value)
        {
            DbParameter parameter = command.CreateParameter();
            parameter.ParameterName = parameterName;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }

        private async Task<List<T>> ExecuteQueryAsync<T>(
            string sql,
            Dictionary<string, object>? parameters,
            CancellationToken cancellationToken) where T : class
        {
            List<T> results = [];

            using (DbConnection connection = CreateConnection())
            {
                await connection.OpenAsync(cancellationToken);

                using (DbCommand command = connection.CreateCommand())
                {
                    command.CommandText = sql;

                    if (parameters != null)
                    {
                        foreach (KeyValuePair<string, object> param in parameters)
                        {
                            AddParameter(command, param.Key, param.Value);
                        }
                    }

                    using (DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
                    {
                        while (await reader.ReadAsync(cancellationToken))
                        {
                            T? entity = MapReaderToEntity<T>(reader);
                            if (entity != null)
                            {
                                results.Add(entity);
                            }
                        }
                    }
                }
            }

            return results;
        }

        private T? MapReaderToEntity<T>(DbDataReader reader) where T : class
        {
            T entity = Activator.CreateInstance<T>();
            PropertyInfo[] properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite)
                .ToArray();

            for (int i = 0; i < reader.FieldCount; i++)
            {
                string columnName = reader.GetName(i);
                PropertyInfo? property = properties.FirstOrDefault(p =>
                    p.Name.Equals(columnName, StringComparison.OrdinalIgnoreCase));

                if (property != null && !reader.IsDBNull(i))
                {
                    object value = reader.GetValue(i);

                    if (property.PropertyType != value.GetType())
                    {
                        try
                        {
                            value = Convert.ChangeType(value, property.PropertyType);
                        }
                        catch
                        {
                            continue;
                        }
                    }

                    property.SetValue(entity, value);
                }
            }

            return entity;
        }

        #endregion
    }
}
