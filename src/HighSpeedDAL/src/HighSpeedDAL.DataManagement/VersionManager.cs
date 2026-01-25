using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace HighSpeedDAL.DataManagement.Versioning
{
    /// <summary>
    /// Manages entity versioning for optimistic concurrency control and temporal queries.
    /// Supports multiple versioning strategies and can track complete version history.
    /// </summary>
    public class VersionManager : IVersionManager
    {
        private readonly ILogger<VersionManager> _logger;
        private readonly string _connectionString;
        private readonly bool _isSqlServer;
        
        // Cache for reflection metadata to avoid repeated lookups
        private readonly ConcurrentDictionary<Type, VersionedAttribute?> _attributeCache;
        private readonly ConcurrentDictionary<Type, PropertyInfo?> _versionPropertyCache;
        private readonly ConcurrentDictionary<Type, PropertyInfo?> _idPropertyCache;
        private readonly ConcurrentDictionary<Type, string> _tableNameCache;

        /// <summary>
        /// Initializes a new instance of the <see cref="VersionManager"/> class.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        /// <param name="connectionString">The database connection string.</param>
        /// <param name="isSqlServer">True for SQL Server, false for SQLite.</param>
        public VersionManager(
            ILogger<VersionManager> logger,
            string connectionString,
            bool isSqlServer = true)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _isSqlServer = isSqlServer;
            
            _attributeCache = new ConcurrentDictionary<Type, VersionedAttribute?>();
            _versionPropertyCache = new ConcurrentDictionary<Type, PropertyInfo?>();
            _idPropertyCache = new ConcurrentDictionary<Type, PropertyInfo?>();
            _tableNameCache = new ConcurrentDictionary<Type, string>();
        }

        /// <inheritdoc/>
        public VersionInfo? GetVersionInfo<T>(T entity) where T : class
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            VersionedAttribute? attribute = GetVersionedAttribute<T>();
            if (attribute == null)
            {
                return null;
            }

            PropertyInfo? versionProperty = GetVersionProperty<T>(attribute);
            if (versionProperty == null)
            {
                _logger.LogWarning(
                    "Version property not found on {EntityType}. Expected property: {PropertyName}",
                    typeof(T).Name,
                    attribute.PropertyName ?? VersionedAttribute.GetDefaultPropertyName(attribute.Strategy));
                return null;
            }

            object? versionValue = versionProperty.GetValue(entity);
            
            VersionInfo versionInfo = new VersionInfo
            {
                Strategy = attribute.Strategy,
                PropertyName = versionProperty.Name,
                ColumnName = attribute.ColumnName ?? versionProperty.Name,
                CreatedAt = DateTime.UtcNow
            };

            // Set the appropriate version value based on strategy
            switch (attribute.Strategy)
            {
                case VersionStrategy.RowVersion:
                    versionInfo.RowVersionValue = versionValue as byte[];
                    break;
                case VersionStrategy.Timestamp:
                    versionInfo.TimestampValue = versionValue as DateTime?;
                    break;
                case VersionStrategy.Integer:
                    versionInfo.IntegerValue = Convert.ToInt32(versionValue);
                    break;
                case VersionStrategy.Guid:
                    versionInfo.GuidValue = versionValue as Guid?;
                    break;
            }

            return versionInfo;
        }

        /// <inheritdoc/>
        public async Task<VersionInfo?> GetVersionInfoByIdAsync<T>(
            object entityId,
            CancellationToken cancellationToken = default) where T : class
        {
            if (entityId == null)
            {
                throw new ArgumentNullException(nameof(entityId));
            }

            VersionedAttribute? attribute = GetVersionedAttribute<T>();
            if (attribute == null)
            {
                return null;
            }

            PropertyInfo? idProperty = GetIdProperty<T>();
            if (idProperty == null)
            {
                throw new InvalidOperationException($"ID property not found on {typeof(T).Name}");
            }

            string tableName = GetTableName<T>();
            string versionColumnName = attribute.ColumnName ?? 
                VersionedAttribute.GetDefaultColumnName(attribute.Strategy);
            string idColumnName = idProperty.Name;

            string sql = $"SELECT {versionColumnName} FROM {tableName} WHERE {idColumnName} = @Id";

            using (DbConnection connection = CreateConnection())
            {
                await connection.OpenAsync(cancellationToken);

                using (DbCommand command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    AddParameter(command, "@Id", entityId);

                    object? versionValue = await command.ExecuteScalarAsync(cancellationToken);
                    
                    if (versionValue == null || versionValue == DBNull.Value)
                    {
                        return null;
                    }

                    VersionInfo versionInfo = new VersionInfo
                    {
                        Strategy = attribute.Strategy,
                        PropertyName = attribute.PropertyName ?? 
                            VersionedAttribute.GetDefaultPropertyName(attribute.Strategy),
                        ColumnName = versionColumnName,
                        CreatedAt = DateTime.UtcNow
                    };

                    // Set the appropriate version value based on strategy
                    switch (attribute.Strategy)
                    {
                        case VersionStrategy.RowVersion:
                            versionInfo.RowVersionValue = versionValue as byte[];
                            break;
                        case VersionStrategy.Timestamp:
                            versionInfo.TimestampValue = Convert.ToDateTime(versionValue);
                            break;
                        case VersionStrategy.Integer:
                            versionInfo.IntegerValue = Convert.ToInt32(versionValue);
                            break;
                        case VersionStrategy.Guid:
                            versionInfo.GuidValue = Guid.Parse(versionValue.ToString()!);
                            break;
                    }

                    return versionInfo;
                }
            }
        }

        /// <inheritdoc/>
        public async Task<bool> ValidateVersionAsync<T>(
            T entity,
            CancellationToken cancellationToken = default) where T : class
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            VersionedAttribute? attribute = GetVersionedAttribute<T>();
            if (attribute == null)
            {
                // Entity is not versioned, validation passes
                return true;
            }

            VersionInfo? currentVersion = GetVersionInfo(entity);
            if (currentVersion == null || !currentVersion.HasValue)
            {
                _logger.LogWarning("Entity {EntityType} is versioned but has no version value", typeof(T).Name);
                return true; // New entity
            }

            // Get the entity ID
            PropertyInfo? idProperty = GetIdProperty<T>();
            if (idProperty == null)
            {
                throw new InvalidOperationException($"ID property not found on {typeof(T).Name}");
            }

            object? entityId = idProperty.GetValue(entity);
            if (entityId == null)
            {
                return true; // New entity
            }

            // Get the current version from the database
            VersionInfo? dbVersion = await GetVersionInfoByIdAsync<T>(entityId, cancellationToken);
            
            if (dbVersion == null)
            {
                return true; // Entity doesn't exist in database
            }

            // Compare versions
            bool versionsMatch = currentVersion.EqualsVersion(dbVersion);
            
            if (!versionsMatch)
            {
                _logger.LogWarning(
                    "Version conflict detected for {EntityType} with ID {EntityId}",
                    typeof(T).Name,
                    entityId);

                if (attribute.ThrowOnConflict)
                {
                    throw new VersionConflictException(
                        typeof(T),
                        entityId,
                        currentVersion.GetVersionValue(),
                        dbVersion.GetVersionValue());
                }
            }

            return versionsMatch;
        }

        /// <inheritdoc/>
        public async Task<bool> UpdateWithVersionCheckAsync<T>(
            T entity,
            CancellationToken cancellationToken = default) where T : class
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            VersionedAttribute? attribute = GetVersionedAttribute<T>();
            if (attribute == null)
            {
                throw new InvalidOperationException($"{typeof(T).Name} is not configured for versioning");
            }

            // Validate version before update
            bool isValid = await ValidateVersionAsync(entity, cancellationToken);
            if (!isValid)
            {
                return false;
            }

            // Increment version for the update
            IncrementVersion(entity);

            // Save to history if enabled
            if (attribute.TrackHistory)
            {
                try
                {
                    await SaveToHistoryAsync(entity, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to save version history for {EntityType}", typeof(T).Name);
                    // Continue with update even if history save fails
                }
            }

            // Perform the actual update
            int rowsAffected = await PerformUpdateAsync(entity, cancellationToken);

            return rowsAffected > 0;
        }

        /// <inheritdoc/>
        public async Task<Dictionary<T, bool>> UpdateManyWithVersionCheckAsync<T>(
            IEnumerable<T> entities,
            CancellationToken cancellationToken = default) where T : class
        {
            if (entities == null)
            {
                throw new ArgumentNullException(nameof(entities));
            }

            Dictionary<T, bool> results = [];

            foreach (T entity in entities)
            {
                try
                {
                    bool success = await UpdateWithVersionCheckAsync(entity, cancellationToken);
                    results[entity] = success;
                }
                catch (VersionConflictException ex)
                {
                    _logger.LogWarning(ex, "Version conflict for entity {EntityType}", typeof(T).Name);
                    results[entity] = false;
                }
            }

            return results;
        }

        /// <inheritdoc/>
        public void IncrementVersion<T>(T entity) where T : class
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            VersionedAttribute? attribute = GetVersionedAttribute<T>();
            if (attribute == null)
            {
                return;
            }

            PropertyInfo? versionProperty = GetVersionProperty<T>(attribute);
            if (versionProperty == null)
            {
                return;
            }

            switch (attribute.Strategy)
            {
                case VersionStrategy.RowVersion:
                    // RowVersion is auto-incremented by SQL Server
                    break;

                case VersionStrategy.Timestamp:
                    versionProperty.SetValue(entity, DateTime.UtcNow);
                    break;

                case VersionStrategy.Integer:
                    object? currentValue = versionProperty.GetValue(entity);
                    int nextVersion = currentValue == null ? 1 : Convert.ToInt32(currentValue) + 1;
                    versionProperty.SetValue(entity, nextVersion);
                    break;

                case VersionStrategy.Guid:
                    versionProperty.SetValue(entity, Guid.NewGuid());
                    break;
            }

            // Update ModifiedBy if tracking
            if (attribute.TrackModifiedBy)
            {
                PropertyInfo? modifiedByProperty = typeof(T).GetProperty(attribute.ModifiedByPropertyName);
                if (modifiedByProperty != null && modifiedByProperty.CanWrite)
                {
                    // For now, set a default value. In a real application, this would come from the current user context
                    modifiedByProperty.SetValue(entity, "System");
                }
            }
        }

        /// <inheritdoc/>
        public async Task<T?> GetAsOfAsync<T>(
            object entityId,
            DateTime asOfDate,
            CancellationToken cancellationToken = default) where T : class
        {
            if (entityId == null)
            {
                throw new ArgumentNullException(nameof(entityId));
            }

            VersionedAttribute? attribute = GetVersionedAttribute<T>();
            if (attribute == null || !attribute.TrackHistory)
            {
                throw new InvalidOperationException(
                    $"{typeof(T).Name} is not configured for version history tracking");
            }

            string historyTableName = attribute.HistoryTableName ?? $"{GetTableName<T>()}History";
            PropertyInfo? idProperty = GetIdProperty<T>();
            
            if (idProperty == null)
            {
                throw new InvalidOperationException($"ID property not found on {typeof(T).Name}");
            }

            string idColumnName = idProperty.Name;

            // Query history table for the version that was active at the specified time
            string sql = _isSqlServer
                ? $@"
                    SELECT TOP 1 * 
                    FROM {historyTableName} 
                    WHERE {idColumnName} = @Id 
                      AND ValidFrom <= @AsOfDate 
                      AND (ValidTo IS NULL OR ValidTo > @AsOfDate)
                    ORDER BY ValidFrom DESC"
                : $@"
                    SELECT * 
                    FROM {historyTableName} 
                    WHERE {idColumnName} = @Id 
                      AND ValidFrom <= @AsOfDate 
                      AND (ValidTo IS NULL OR ValidTo > @AsOfDate)
                    ORDER BY ValidFrom DESC
                    LIMIT 1";

            using (DbConnection connection = CreateConnection())
            {
                await connection.OpenAsync(cancellationToken);

                using (DbCommand command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    AddParameter(command, "@Id", entityId);
                    AddParameter(command, "@AsOfDate", asOfDate);

                    using (DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
                    {
                        if (await reader.ReadAsync(cancellationToken))
                        {
                            return MapReaderToEntity<T>(reader);
                        }
                    }
                }
            }

            return null;
        }

        /// <inheritdoc/>
        public async Task<List<T>> GetVersionHistoryAsync<T>(
            object entityId,
            DateTime? startDate = null,
            DateTime? endDate = null,
            CancellationToken cancellationToken = default) where T : class
        {
            if (entityId == null)
            {
                throw new ArgumentNullException(nameof(entityId));
            }

            VersionedAttribute? attribute = GetVersionedAttribute<T>();
            if (attribute == null || !attribute.TrackHistory)
            {
                throw new InvalidOperationException(
                    $"{typeof(T).Name} is not configured for version history tracking");
            }

            string historyTableName = attribute.HistoryTableName ?? $"{GetTableName<T>()}History";
            PropertyInfo? idProperty = GetIdProperty<T>();
            
            if (idProperty == null)
            {
                throw new InvalidOperationException($"ID property not found on {typeof(T).Name}");
            }

            string idColumnName = idProperty.Name;
            List<string> whereClauses = [$"{idColumnName} = @Id"];

            if (startDate.HasValue)
            {
                whereClauses.Add("ValidFrom >= @StartDate");
            }

            if (endDate.HasValue)
            {
                whereClauses.Add("ValidFrom <= @EndDate");
            }

            string whereClause = string.Join(" AND ", whereClauses);
            string sql = $@"
                SELECT * 
                FROM {historyTableName} 
                WHERE {whereClause}
                ORDER BY ValidFrom DESC";

            List<T> history = [];

            using (DbConnection connection = CreateConnection())
            {
                await connection.OpenAsync(cancellationToken);

                using (DbCommand command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    AddParameter(command, "@Id", entityId);
                    
                    if (startDate.HasValue)
                    {
                        AddParameter(command, "@StartDate", startDate.Value);
                    }
                    
                    if (endDate.HasValue)
                    {
                        AddParameter(command, "@EndDate", endDate.Value);
                    }

                    using (DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
                    {
                        while (await reader.ReadAsync(cancellationToken))
                        {
                            T? entity = MapReaderToEntity<T>(reader);
                            if (entity != null)
                            {
                                history.Add(entity);
                            }
                        }
                    }
                }
            }

            return history;
        }

        /// <inheritdoc/>
        public async Task SaveToHistoryAsync<T>(
            T entity,
            CancellationToken cancellationToken = default) where T : class
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            VersionedAttribute? attribute = GetVersionedAttribute<T>();
            if (attribute == null || !attribute.TrackHistory)
            {
                return;
            }

            // Auto-create history table if configured
            if (attribute.AutoCreateHistoryTable)
            {
                await EnsureHistoryTableExistsAsync<T>(cancellationToken);
            }

            string historyTableName = attribute.HistoryTableName ?? $"{GetTableName<T>()}History";
            
            // Build insert statement
            PropertyInfo[] properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead)
                .ToArray();

            List<string> columnNames = [];
            List<string> parameterNames = [];

            foreach (PropertyInfo property in properties)
            {
                columnNames.Add(property.Name);
                parameterNames.Add($"@{property.Name}");
            }

            // Add temporal tracking columns
            columnNames.Add("ValidFrom");
            columnNames.Add("ValidTo");
            parameterNames.Add("@ValidFrom");
            parameterNames.Add("@ValidTo");

            string sql = $@"
                INSERT INTO {historyTableName} 
                ({string.Join(", ", columnNames)})
                VALUES 
                ({string.Join(", ", parameterNames)})";

            using (DbConnection connection = CreateConnection())
            {
                await connection.OpenAsync(cancellationToken);

                using (DbCommand command = connection.CreateCommand())
                {
                    command.CommandText = sql;

                    foreach (PropertyInfo property in properties)
                    {
                        object? value = property.GetValue(entity);
                        AddParameter(command, $"@{property.Name}", value ?? DBNull.Value);
                    }

                    AddParameter(command, "@ValidFrom", DateTime.UtcNow);
                    AddParameter(command, "@ValidTo", DBNull.Value);

                    await command.ExecuteNonQueryAsync(cancellationToken);
                }
            }

            _logger.LogDebug("Saved version history for {EntityType}", typeof(T).Name);
        }

        /// <inheritdoc/>
        public async Task CreateHistoryTableAsync<T>(
            CancellationToken cancellationToken = default) where T : class
        {
            VersionedAttribute? attribute = GetVersionedAttribute<T>();
            if (attribute == null || !attribute.TrackHistory)
            {
                return;
            }

            string tableName = GetTableName<T>();
            string historyTableName = attribute.HistoryTableName ?? $"{tableName}History";

            string createTableSql;

            if (_isSqlServer)
            {
                createTableSql = $@"
                    IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'{historyTableName}') AND type in (N'U'))
                    BEGIN
                        SELECT * 
                        INTO {historyTableName}
                        FROM {tableName}
                        WHERE 1=0
                        
                        ALTER TABLE {historyTableName}
                        ADD ValidFrom DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                            ValidTo DATETIME2 NULL,
                            HistoryId INT IDENTITY(1,1) PRIMARY KEY
                    END";
            }
            else
            {
                // For SQLite, we need to build the CREATE TABLE statement manually
                createTableSql = $@"
                    CREATE TABLE IF NOT EXISTS {historyTableName} (
                        HistoryId INTEGER PRIMARY KEY AUTOINCREMENT,
                        ValidFrom TEXT NOT NULL DEFAULT (datetime('now')),
                        ValidTo TEXT NULL
                    )";
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

            _logger.LogInformation("Created history table {HistoryTableName} for {EntityType}",
                historyTableName, typeof(T).Name);
        }

        /// <inheritdoc/>
        public async Task<int> PurgeOldHistoryAsync<T>(
            int? retentionDays = null,
            CancellationToken cancellationToken = default) where T : class
        {
            VersionedAttribute? attribute = GetVersionedAttribute<T>();
            if (attribute == null || !attribute.TrackHistory)
            {
                return 0;
            }

            int daysToRetain = retentionDays ?? attribute.HistoryRetentionDays;
            if (daysToRetain == 0)
            {
                // Unlimited retention
                return 0;
            }

            DateTime cutoffDate = DateTime.UtcNow.AddDays(-daysToRetain);
            string historyTableName = attribute.HistoryTableName ?? $"{GetTableName<T>()}History";

            string sql = $@"
                DELETE FROM {historyTableName}
                WHERE ValidFrom < @CutoffDate";

            using (DbConnection connection = CreateConnection())
            {
                await connection.OpenAsync(cancellationToken);

                using (DbCommand command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    AddParameter(command, "@CutoffDate", cutoffDate);

                    int rowsDeleted = await command.ExecuteNonQueryAsync(cancellationToken);
                    
                    _logger.LogInformation(
                        "Purged {RowCount} old history records from {HistoryTableName} (retention: {Days} days)",
                        rowsDeleted, historyTableName, daysToRetain);

                    return rowsDeleted;
                }
            }
        }

        /// <inheritdoc/>
        public bool IsVersioned<T>() where T : class
        {
            return GetVersionedAttribute<T>() != null;
        }

        /// <inheritdoc/>
        public VersionedAttribute? GetVersionedAttribute<T>() where T : class
        {
            return _attributeCache.GetOrAdd(typeof(T), type =>
            {
                return type.GetCustomAttribute<VersionedAttribute>(inherit: true);
            });
        }

        /// <inheritdoc/>
        public bool VersionsEqual(object? version1, object? version2, VersionStrategy strategy)
        {
            return version1 == null && version2 == null
                ? true
                : version1 == null || version2 == null
                ? false
                : strategy switch
            {
                VersionStrategy.RowVersion => ByteArrayEquals(version1 as byte[], version2 as byte[]),
                VersionStrategy.Timestamp => Equals(version1, version2),
                VersionStrategy.Integer => Equals(version1, version2),
                VersionStrategy.Guid => Equals(version1, version2),
                _ => false
            };
        }

        #region Private Helper Methods

        /// <summary>
        /// Gets the version property for an entity type.
        /// </summary>
        private PropertyInfo? GetVersionProperty<T>(VersionedAttribute attribute) where T : class
        {
            return _versionPropertyCache.GetOrAdd(typeof(T), type =>
            {
                string propertyName = attribute.PropertyName ?? 
                    VersionedAttribute.GetDefaultPropertyName(attribute.Strategy);

                return type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            });
        }

        /// <summary>
        /// Gets the ID property for an entity type.
        /// </summary>
        private PropertyInfo? GetIdProperty<T>() where T : class
        {
            return _idPropertyCache.GetOrAdd(typeof(T), type =>
            {
                // Look for property named "Id" or "{TypeName}Id"
                PropertyInfo? idProp = type.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
                if (idProp != null)
                {
                    return idProp;
                }

                string typeIdName = $"{type.Name}Id";
                idProp = type.GetProperty(typeIdName, BindingFlags.Public | BindingFlags.Instance);
                
                return idProp;
            });
        }

        /// <summary>
        /// Gets the table name for an entity type.
        /// </summary>
        private string GetTableName<T>() where T : class
        {
            return _tableNameCache.GetOrAdd(typeof(T), type =>
            {
                // Default to type name + "s" for plural
                return $"{type.Name}s";
            });
        }

        /// <summary>
        /// Creates a database connection based on the configured provider.
        /// </summary>
        private DbConnection CreateConnection()
        {
            return _isSqlServer ? new SqlConnection(_connectionString) : new SqliteConnection(_connectionString);
        }

        /// <summary>
        /// Adds a parameter to a database command.
        /// </summary>
        private void AddParameter(DbCommand command, string parameterName, object value)
        {
            DbParameter parameter = command.CreateParameter();
            parameter.ParameterName = parameterName;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }

        /// <summary>
        /// Performs the actual update operation with version checking.
        /// </summary>
        private async Task<int> PerformUpdateAsync<T>(T entity, CancellationToken cancellationToken) where T : class
        {
            // This is a simplified implementation
            // In a real scenario, this would integrate with the main DAL layer
            
            PropertyInfo? idProperty = GetIdProperty<T>();
            if (idProperty == null)
            {
                throw new InvalidOperationException($"ID property not found on {typeof(T).Name}");
            }

            object? entityId = idProperty.GetValue(entity);
            string tableName = GetTableName<T>();

            // Build UPDATE statement
            PropertyInfo[] properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite && p.Name != idProperty.Name)
                .ToArray();

            List<string> setClauses = [];
            foreach (PropertyInfo property in properties)
            {
                setClauses.Add($"{property.Name} = @{property.Name}");
            }

            string sql = $@"
                UPDATE {tableName}
                SET {string.Join(", ", setClauses)}
                WHERE {idProperty.Name} = @Id";

            using (DbConnection connection = CreateConnection())
            {
                await connection.OpenAsync(cancellationToken);

                using (DbCommand command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    
                    foreach (PropertyInfo property in properties)
                    {
                        object? value = property.GetValue(entity);
                        AddParameter(command, $"@{property.Name}", value ?? DBNull.Value);
                    }
                    
                    AddParameter(command, "@Id", entityId!);

                    return await command.ExecuteNonQueryAsync(cancellationToken);
                }
            }
        }

        /// <summary>
        /// Ensures the history table exists, creating it if necessary.
        /// </summary>
        private async Task EnsureHistoryTableExistsAsync<T>(CancellationToken cancellationToken) where T : class
        {
            // Check if table exists
            string historyTableName = GetVersionedAttribute<T>()?.HistoryTableName ?? 
                $"{GetTableName<T>()}History";

            string checkTableSql;
            if (_isSqlServer)
            {
                checkTableSql = $@"
                    SELECT COUNT(*) 
                    FROM sys.objects 
                    WHERE object_id = OBJECT_ID(N'{historyTableName}') AND type in (N'U')";
            }
            else
            {
                checkTableSql = $@"
                    SELECT COUNT(*) 
                    FROM sqlite_master 
                    WHERE type='table' AND name='{historyTableName}'";
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
                        await CreateHistoryTableAsync<T>(cancellationToken);
                    }
                }
            }
        }

        /// <summary>
        /// Maps a data reader to an entity instance.
        /// </summary>
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
                    
                    // Handle type conversions
                    if (property.PropertyType != value.GetType())
                    {
                        try
                        {
                            value = Convert.ChangeType(value, property.PropertyType);
                        }
                        catch
                        {
                            // Skip if conversion fails
                            continue;
                        }
                    }

                    property.SetValue(entity, value);
                }
            }

            return entity;
        }

        /// <summary>
        /// Compares two byte arrays for equality.
        /// </summary>
        private bool ByteArrayEquals(byte[]? a, byte[]? b)
        {
            if (a == null && b == null)
            {
                return true;
            }

            if (a == null || b == null || a.Length != b.Length)
            {
                return false;
            }

            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }

            return true;
        }

        #endregion
    }
}

