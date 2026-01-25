using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace HighSpeedDAL.DataManagement.SoftDelete
{
    /// <summary>
    /// Manages soft delete operations with recovery, cascade handling, and auto-purge capabilities.
    /// </summary>
    public class SoftDeleteManager : ISoftDeleteManager
    {
        private readonly ILogger<SoftDeleteManager> _logger;
        private readonly string _connectionString;
        private readonly bool _isSqlServer;
        private readonly SoftDeleteOptions _defaultOptions;

        // Cache for reflection metadata
        private readonly ConcurrentDictionary<Type, SoftDeleteAttribute?> _attributeCache;
        private readonly ConcurrentDictionary<Type, PropertyInfo?> _isDeletedPropertyCache;
        private readonly ConcurrentDictionary<Type, PropertyInfo?> _deletedAtPropertyCache;
        private readonly ConcurrentDictionary<Type, PropertyInfo?> _deletedByPropertyCache;
        private readonly ConcurrentDictionary<Type, PropertyInfo?> _idPropertyCache;
        private readonly ConcurrentDictionary<Type, string> _tableNameCache;

        /// <summary>
        /// Initializes a new instance of the <see cref="SoftDeleteManager"/> class.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        /// <param name="connectionString">The database connection string.</param>
        /// <param name="isSqlServer">True for SQL Server, false for SQLite.</param>
        /// <param name="defaultOptions">Default soft delete options.</param>
        public SoftDeleteManager(
            ILogger<SoftDeleteManager> logger,
            string connectionString,
            bool isSqlServer = true,
            SoftDeleteOptions? defaultOptions = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _isSqlServer = isSqlServer;
            _defaultOptions = defaultOptions ?? new SoftDeleteOptions();

            _attributeCache = new ConcurrentDictionary<Type, SoftDeleteAttribute?>();
            _isDeletedPropertyCache = new ConcurrentDictionary<Type, PropertyInfo?>();
            _deletedAtPropertyCache = new ConcurrentDictionary<Type, PropertyInfo?>();
            _deletedByPropertyCache = new ConcurrentDictionary<Type, PropertyInfo?>();
            _idPropertyCache = new ConcurrentDictionary<Type, PropertyInfo?>();
            _tableNameCache = new ConcurrentDictionary<Type, string>();
        }

        /// <inheritdoc/>
        public async Task<SoftDeleteResult> SoftDeleteAsync<T>(
            object entityId,
            bool cascadeToRelated = false,
            string? deletedBy = null,
            CancellationToken cancellationToken = default) where T : class
        {
            if (entityId == null)
            {
                throw new ArgumentNullException(nameof(entityId));
            }

            SoftDeleteAttribute? attribute = GetSoftDeleteAttribute<T>();
            if (attribute == null)
            {
                throw new InvalidOperationException($"{typeof(T).Name} is not configured for soft delete");
            }

            ValidateEntity<T>();

            DateTime deletedAt = DateTime.UtcNow;
            string deletedByUser = deletedBy ?? _defaultOptions.CurrentUserId ?? "System";

            try
            {
                string tableName = GetTableName<T>();
                PropertyInfo? idProperty = GetIdProperty<T>();
                
                if (idProperty == null)
                {
                    throw new InvalidOperationException($"ID property not found on {typeof(T).Name}");
                }

                string sql = $@"
                    UPDATE {tableName}
                    SET {attribute.IsDeletedPropertyName} = @IsDeleted,
                        {attribute.DeletedAtPropertyName} = @DeletedAt,
                        {attribute.DeletedByPropertyName} = @DeletedBy
                    WHERE {idProperty.Name} = @Id
                      AND {attribute.IsDeletedPropertyName} = 0";

                using (DbConnection connection = CreateConnection())
                {
                    await connection.OpenAsync(cancellationToken);

                    using (DbCommand command = connection.CreateCommand())
                    {
                        command.CommandText = sql;
                        AddParameter(command, "@IsDeleted", true);
                        AddParameter(command, "@DeletedAt", deletedAt);
                        AddParameter(command, "@DeletedBy", deletedByUser);
                        AddParameter(command, "@Id", entityId);

                        int rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);

                        if (rowsAffected == 0)
                        {
                            return SoftDeleteResult.CreateFailure(
                                $"Entity with ID {entityId} not found or already deleted");
                        }

                        SoftDeleteResult result = SoftDeleteResult.CreateSuccess(
                            entitiesDeleted: rowsAffected,
                            relatedDeleted: 0,
                            deletedBy: deletedByUser);

                        result.DeletedEntityIds.Add(entityId);

                        // Handle cascade if requested
                        if (cascadeToRelated && (attribute.CascadeDelete || cascadeToRelated))
                        {
                            int cascadeCount = await CascadeDelete<T>(
                                entityId,
                                deletedAt,
                                deletedByUser,
                                depth: 0,
                                maxDepth: attribute.MaxCascadeDepth,
                                cancellationToken);

                            result.RelatedEntitiesDeleted = cascadeCount;
                        }

                        _logger.LogInformation(
                            "Soft deleted {EntityType} with ID {EntityId} by {User}",
                            typeof(T).Name, entityId, deletedByUser);

                        return result;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to soft delete {EntityType} with ID {EntityId}",
                    typeof(T).Name, entityId);
                return SoftDeleteResult.CreateFailure(ex.Message);
            }
        }

        /// <inheritdoc/>
        public async Task<SoftDeleteResult> SoftDeleteEntityAsync<T>(
            T entity,
            bool cascadeToRelated = false,
            string? deletedBy = null,
            CancellationToken cancellationToken = default) where T : class
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            PropertyInfo? idProperty = GetIdProperty<T>();
            if (idProperty == null)
            {
                throw new InvalidOperationException($"ID property not found on {typeof(T).Name}");
            }

            object? entityId = idProperty.GetValue(entity);
            return entityId == null
                ? throw new InvalidOperationException("Entity ID is null")
                : await SoftDeleteAsync<T>(entityId, cascadeToRelated, deletedBy, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<SoftDeleteResult> SoftDeleteManyAsync<T>(
            IEnumerable<object> entityIds,
            bool cascadeToRelated = false,
            string? deletedBy = null,
            CancellationToken cancellationToken = default) where T : class
        {
            if (entityIds == null)
            {
                throw new ArgumentNullException(nameof(entityIds));
            }

            int totalDeleted = 0;
            int totalCascaded = 0;
            List<object> deletedIds = [];

            foreach (object entityId in entityIds)
            {
                SoftDeleteResult result = await SoftDeleteAsync<T>(
                    entityId,
                    cascadeToRelated,
                    deletedBy,
                    cancellationToken);

                if (result.Success)
                {
                    totalDeleted += result.EntitiesDeleted;
                    totalCascaded += result.RelatedEntitiesDeleted;
                    deletedIds.AddRange(result.DeletedEntityIds);
                }
            }

            SoftDeleteResult finalResult = SoftDeleteResult.CreateSuccess(
                totalDeleted,
                totalCascaded,
                deletedBy ?? _defaultOptions.CurrentUserId ?? "System");
            
            finalResult.DeletedEntityIds = deletedIds;

            return finalResult;
        }

        /// <inheritdoc/>
        public async Task<bool> RecoverAsync<T>(
            object entityId,
            CancellationToken cancellationToken = default) where T : class
        {
            if (entityId == null)
            {
                throw new ArgumentNullException(nameof(entityId));
            }

            SoftDeleteAttribute? attribute = GetSoftDeleteAttribute<T>();
            if (attribute == null)
            {
                throw new InvalidOperationException($"{typeof(T).Name} is not configured for soft delete");
            }

            ValidateEntity<T>();

            try
            {
                string tableName = GetTableName<T>();
                PropertyInfo? idProperty = GetIdProperty<T>();
                
                if (idProperty == null)
                {
                    throw new InvalidOperationException($"ID property not found on {typeof(T).Name}");
                }

                string sql = $@"
                    UPDATE {tableName}
                    SET {attribute.IsDeletedPropertyName} = @IsDeleted,
                        {attribute.DeletedAtPropertyName} = @DeletedAt,
                        {attribute.DeletedByPropertyName} = @DeletedBy
                    WHERE {idProperty.Name} = @Id
                      AND {attribute.IsDeletedPropertyName} = 1";

                using (DbConnection connection = CreateConnection())
                {
                    await connection.OpenAsync(cancellationToken);

                    using (DbCommand command = connection.CreateCommand())
                    {
                        command.CommandText = sql;
                        AddParameter(command, "@IsDeleted", false);
                        AddParameter(command, "@DeletedAt", DBNull.Value);
                        AddParameter(command, "@DeletedBy", DBNull.Value);
                        AddParameter(command, "@Id", entityId);

                        int rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);

                        if (rowsAffected > 0)
                        {
                            _logger.LogInformation(
                                "Recovered {EntityType} with ID {EntityId}",
                                typeof(T).Name, entityId);
                        }

                        return rowsAffected > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to recover {EntityType} with ID {EntityId}",
                    typeof(T).Name, entityId);
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<int> RecoverManyAsync<T>(
            IEnumerable<object> entityIds,
            CancellationToken cancellationToken = default) where T : class
        {
            if (entityIds == null)
            {
                throw new ArgumentNullException(nameof(entityIds));
            }

            int recovered = 0;

            foreach (object entityId in entityIds)
            {
                if (await RecoverAsync<T>(entityId, cancellationToken))
                {
                    recovered++;
                }
            }

            return recovered;
        }

        /// <inheritdoc/>
        public async Task<bool> PurgeAsync<T>(
            object entityId,
            CancellationToken cancellationToken = default) where T : class
        {
            if (entityId == null)
            {
                throw new ArgumentNullException(nameof(entityId));
            }

            SoftDeleteAttribute? attribute = GetSoftDeleteAttribute<T>();
            if (attribute == null)
            {
                throw new InvalidOperationException($"{typeof(T).Name} is not configured for soft delete");
            }

            try
            {
                string tableName = GetTableName<T>();
                PropertyInfo? idProperty = GetIdProperty<T>();
                
                if (idProperty == null)
                {
                    throw new InvalidOperationException($"ID property not found on {typeof(T).Name}");
                }

                // Only purge if already soft deleted
                string sql = $@"
                    DELETE FROM {tableName}
                    WHERE {idProperty.Name} = @Id
                      AND {attribute.IsDeletedPropertyName} = 1";

                using (DbConnection connection = CreateConnection())
                {
                    await connection.OpenAsync(cancellationToken);

                    using (DbCommand command = connection.CreateCommand())
                    {
                        command.CommandText = sql;
                        AddParameter(command, "@Id", entityId);

                        int rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);

                        if (rowsAffected > 0)
                        {
                            _logger.LogWarning(
                                "Permanently purged {EntityType} with ID {EntityId}",
                                typeof(T).Name, entityId);
                        }

                        return rowsAffected > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to purge {EntityType} with ID {EntityId}",
                    typeof(T).Name, entityId);
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<int> PurgeExpiredAsync<T>(
            int? olderThanDays = null,
            CancellationToken cancellationToken = default) where T : class
        {
            SoftDeleteAttribute? attribute = GetSoftDeleteAttribute<T>();
            if (attribute == null)
            {
                throw new InvalidOperationException($"{typeof(T).Name} is not configured for soft delete");
            }

            int daysToRetain = olderThanDays ?? attribute.RetentionDays;
            if (daysToRetain == 0)
            {
                // Unlimited retention
                return 0;
            }

            DateTime cutoffDate = DateTime.UtcNow.AddDays(-daysToRetain);
            string tableName = GetTableName<T>();

            string sql = $@"
                DELETE FROM {tableName}
                WHERE {attribute.IsDeletedPropertyName} = 1
                  AND {attribute.DeletedAtPropertyName} < @CutoffDate";

            try
            {
                using (DbConnection connection = CreateConnection())
                {
                    await connection.OpenAsync(cancellationToken);

                    using (DbCommand command = connection.CreateCommand())
                    {
                        command.CommandText = sql;
                        AddParameter(command, "@CutoffDate", cutoffDate);

                        int rowsDeleted = await command.ExecuteNonQueryAsync(cancellationToken);

                        _logger.LogInformation(
                            "Purged {RowCount} expired soft deleted records from {EntityType} (retention: {Days} days)",
                            rowsDeleted, typeof(T).Name, daysToRetain);

                        return rowsDeleted;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to purge expired records for {EntityType}",
                    typeof(T).Name);
                return 0;
            }
        }

        /// <inheritdoc/>
        public async Task<List<T>> GetSoftDeletedAsync<T>(
            CancellationToken cancellationToken = default) where T : class
        {
            SoftDeleteAttribute? attribute = GetSoftDeleteAttribute<T>();
            if (attribute == null)
            {
                return [];
            }

            string tableName = GetTableName<T>();
            string sql = $@"
                SELECT *
                FROM {tableName}
                WHERE {attribute.IsDeletedPropertyName} = 1";

            return await ExecuteQueryAsync<T>(sql, null, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<List<T>> GetSoftDeletedInRangeAsync<T>(
            DateTime startDate,
            DateTime endDate,
            CancellationToken cancellationToken = default) where T : class
        {
            SoftDeleteAttribute? attribute = GetSoftDeleteAttribute<T>();
            if (attribute == null)
            {
                return [];
            }

            string tableName = GetTableName<T>();
            string sql = $@"
                SELECT *
                FROM {tableName}
                WHERE {attribute.IsDeletedPropertyName} = 1
                  AND {attribute.DeletedAtPropertyName} >= @StartDate
                  AND {attribute.DeletedAtPropertyName} <= @EndDate";

            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { "@StartDate", startDate },
                { "@EndDate", endDate }
            };

            return await ExecuteQueryAsync<T>(sql, parameters, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<bool> IsSoftDeletedAsync<T>(
            object entityId,
            CancellationToken cancellationToken = default) where T : class
        {
            SoftDeleteAttribute? attribute = GetSoftDeleteAttribute<T>();
            if (attribute == null)
            {
                return false;
            }

            PropertyInfo? idProperty = GetIdProperty<T>();
            if (idProperty == null)
            {
                return false;
            }

            string tableName = GetTableName<T>();
            string sql = $@"
                SELECT COUNT(*)
                FROM {tableName}
                WHERE {idProperty.Name} = @Id
                  AND {attribute.IsDeletedPropertyName} = 1";

            using (DbConnection connection = CreateConnection())
            {
                await connection.OpenAsync(cancellationToken);

                using (DbCommand command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    AddParameter(command, "@Id", entityId);

                    object? result = await command.ExecuteScalarAsync(cancellationToken);
                    return Convert.ToInt32(result) > 0;
                }
            }
        }

        /// <inheritdoc/>
        public async Task<int> GetSoftDeletedCountAsync<T>(
            CancellationToken cancellationToken = default) where T : class
        {
            SoftDeleteAttribute? attribute = GetSoftDeleteAttribute<T>();
            if (attribute == null)
            {
                return 0;
            }

            string tableName = GetTableName<T>();
            string sql = $@"
                SELECT COUNT(*)
                FROM {tableName}
                WHERE {attribute.IsDeletedPropertyName} = 1";

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
        public bool IsSoftDeleteEnabled<T>() where T : class
        {
            return GetSoftDeleteAttribute<T>() != null;
        }

        /// <inheritdoc/>
        public SoftDeleteAttribute? GetSoftDeleteAttribute<T>() where T : class
        {
            return _attributeCache.GetOrAdd(typeof(T), type =>
            {
                return type.GetCustomAttribute<SoftDeleteAttribute>(inherit: true);
            });
        }

        /// <inheritdoc/>
        public void ValidateEntity<T>() where T : class
        {
            SoftDeleteAttribute? attribute = GetSoftDeleteAttribute<T>();
            if (attribute == null || !attribute.ValidateProperties)
            {
                return;
            }

            Type entityType = typeof(T);
            List<string> missingProperties = [];

            // Check IsDeleted property
            PropertyInfo? isDeletedProp = entityType.GetProperty(
                attribute.IsDeletedPropertyName,
                BindingFlags.Public | BindingFlags.Instance);
            
            if (isDeletedProp == null)
            {
                missingProperties.Add(attribute.IsDeletedPropertyName);
            }

            // Check DeletedAt property
            PropertyInfo? deletedAtProp = entityType.GetProperty(
                attribute.DeletedAtPropertyName,
                BindingFlags.Public | BindingFlags.Instance);
            
            if (deletedAtProp == null)
            {
                missingProperties.Add(attribute.DeletedAtPropertyName);
            }

            // Check DeletedBy property
            PropertyInfo? deletedByProp = entityType.GetProperty(
                attribute.DeletedByPropertyName,
                BindingFlags.Public | BindingFlags.Instance);
            
            if (deletedByProp == null)
            {
                missingProperties.Add(attribute.DeletedByPropertyName);
            }

            if (missingProperties.Count > 0)
            {
                throw new InvalidOperationException(
                    $"{entityType.Name} is missing required soft delete properties: {string.Join(", ", missingProperties)}");
            }
        }

        #region Private Helper Methods

        private async Task<int> CascadeDelete<T>(
            object parentId,
            DateTime deletedAt,
            string deletedBy,
            int depth,
            int maxDepth,
            CancellationToken cancellationToken) where T : class
        {
            if (depth >= maxDepth)
            {
                _logger.LogWarning(
                    "Max cascade depth {MaxDepth} reached for {EntityType}",
                    maxDepth, typeof(T).Name);
                return 0;
            }

            // This is a simplified implementation
            // In a real scenario, this would discover foreign key relationships
            // and cascade to related entities
            
            // For now, return 0 (no cascade performed)
            // A full implementation would:
            // 1. Query database schema for FK relationships
            // 2. Find related entities
            // 3. Recursively soft delete them
            
            return 0;
        }

        private PropertyInfo? GetIdProperty<T>() where T : class
        {
            return _idPropertyCache.GetOrAdd(typeof(T), type =>
            {
                PropertyInfo? idProp = type.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
                if (idProp != null)
                {
                    return idProp;
                }

                string typeIdName = $"{type.Name}Id";
                return type.GetProperty(typeIdName, BindingFlags.Public | BindingFlags.Instance);
            });
        }

        private string GetTableName<T>() where T : class
        {
            return _tableNameCache.GetOrAdd(typeof(T), type =>
            {
                return $"{type.Name}s";
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
