using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HighSpeedDAL.Core.Attributes;
using Microsoft.Extensions.Logging;

namespace HighSpeedDAL.Core.InMemoryTable;

/// <summary>
/// High-performance in-memory table with SQL-like query support.
/// 
/// Features:
/// - Thread-safe CRUD operations using ConcurrentDictionary
/// - Automatic primary key generation
/// - Index support (unique and non-unique)
/// - Constraint validation (data types, lengths, nullability)
/// - SQL-like WHERE clause support
/// - Configurable flush to staging/main table
/// 
/// Usage:
/// var table = new InMemoryTable&lt;User&gt;(logger, config);
/// await table.InsertAsync(user);
/// var results = table.Select("Age > 21 AND Status = 'Active'");
/// </summary>
/// <typeparam name="TEntity">The entity type</typeparam>
public sealed class InMemoryTable<TEntity> : IDisposable where TEntity : class, new()
{
    private readonly ILogger _logger;
    private readonly InMemoryTableAttribute _config;
    private readonly InMemoryTableSchema _schema;
    private readonly ConcurrentDictionary<object, InMemoryRow> _rows;
    private readonly WhereClauseParser _whereParser;
    private readonly ReaderWriterLockSlim _indexLock;
    private long _nextId;
    private readonly List<OperationRecord> _operationLog;
    private readonly object _operationLogLock;
    private readonly MemoryMappedFileStore<TEntity>? _memoryMappedStore;
    private readonly Timer? _syncTimer;
    private readonly ILoggerFactory? _loggerFactory;
    private bool _disposed;

    // PERFORMANCE: Property value caches for O(1) lookups
    // Stores: Dictionary<propertyName, Dictionary<propertyValue, List<TEntity>>>
    // e.g., Dictionary<"ProcessingStatus", Dictionary<"Pending", [Product1, Product2, ...]>>
    // Single or multiple entities per property value - lookup is always O(1)
    private readonly Dictionary<string, Dictionary<string, List<TEntity>>> _propertyValueCache =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly object _cacheLock = new();

    /// <summary>
    /// Name of the table
    /// </summary>
    public string TableName => _schema.TableName;

    /// <summary>
    /// Number of rows in the table (excluding deleted rows)
    /// </summary>
    public int RowCount => _rows.Values.Count(r => r.State != RowState.Deleted);

    /// <summary>
    /// Total number of rows including deleted (pending flush)
    /// </summary>
    public int TotalRowCount => _rows.Count;

    /// <summary>
    /// The table schema
    /// </summary>
    public InMemoryTableSchema Schema => _schema;

    /// <summary>
    /// Event raised when a flush is required (max rows reached, etc.)
    /// </summary>
    public event EventHandler<FlushRequiredEventArgs>? FlushRequired;

    /// <summary>
    /// Creates a new in-memory table
    /// </summary>
    public InMemoryTable(ILogger logger, InMemoryTableAttribute? config = null, string? tableName = null)
        : this((ILoggerFactory?)null, logger, config, tableName)
    {
    }

    /// <summary>
    /// Creates a new in-memory table with logger factory support for memory-mapped files
    /// </summary>
    public InMemoryTable(ILoggerFactory loggerFactory, InMemoryTableAttribute? config = null, string? tableName = null)
        : this(loggerFactory, loggerFactory?.CreateLogger<InMemoryTable<TEntity>>() ?? throw new ArgumentNullException(nameof(loggerFactory)), config, tableName)
    {
    }

    /// <summary>
    /// Internal constructor with full parameter set
    /// </summary>
    private InMemoryTable(ILoggerFactory? loggerFactory, ILogger logger, InMemoryTableAttribute? config = null, string? tableName = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _loggerFactory = loggerFactory;
        _config = config ?? new InMemoryTableAttribute();
        _config.Validate();

        _schema = InMemoryTableSchema.FromEntityType<TEntity>(tableName);

        // Pre-allocate capacity based on configured MaxRowCount to avoid resize overhead during load
        int capacity = _config.MaxRowCount > 0 ? _config.MaxRowCount : 1000;
        _rows = new ConcurrentDictionary<object, InMemoryRow>(
            concurrencyLevel: Environment.ProcessorCount * 2,  // Higher concurrency for many cores
            capacity: capacity);

        _whereParser = new WhereClauseParser(_schema);
        _indexLock = new ReaderWriterLockSlim();
        _nextId = 1;
        _operationLog = new List<OperationRecord>();
        _operationLogLock = new object();
        _disposed = false;

        // Initialize memory-mapped file store if configured
        if (!string.IsNullOrWhiteSpace(_config.MemoryMappedFileName))
        {
            if (_loggerFactory == null)
            {
                throw new InvalidOperationException(
                    "ILoggerFactory must be provided to InMemoryTable constructor when using memory-mapped files. " +
                    "Use the constructor overload: InMemoryTable(ILoggerFactory loggerFactory, ...)");
            }

            var storeLogger = _loggerFactory.CreateLogger<MemoryMappedFileStore<TEntity>>();
            var syncLogger = _loggerFactory.CreateLogger<MemoryMappedSynchronizer>();

            _memoryMappedStore = new MemoryMappedFileStore<TEntity>(
                _config.MemoryMappedFileName, 
                _config, 
                storeLogger, 
                syncLogger);

            // Auto-load data from file
            if (_config.AutoLoadOnStartup)
            {
                var loadedRows = _memoryMappedStore.LoadAsync().GetAwaiter().GetResult();
                foreach (var entity in loadedRows)
                {
                    // Load directly without validation/constraints (already validated on save)
                    var row = new InMemoryRow(_schema);
                    row.FromEntity(entity);
                    object pk = row.PrimaryKeyValue!;
                    _rows.TryAdd(pk, row);

                    // Update next ID
                    if (pk is long longPk && longPk >= _nextId)
                        _nextId = longPk + 1;
                    else if (pk is int intPk && intPk >= _nextId)
                        _nextId = intPk + 1;
                }
                _logger.LogInformation("Loaded {RowCount} rows from memory-mapped file '{FileName}'",
                    loadedRows.Count, _config.MemoryMappedFileName);
            }

            // Setup sync timer for batched mode
            if (_config.SyncMode == MemoryMappedSyncMode.Batched && _config.FlushIntervalSeconds > 0)
            {
                _syncTimer = new Timer(
                    _ => FlushToMemoryMappedFileAsync().GetAwaiter().GetResult(),
                    null,
                    TimeSpan.FromSeconds(_config.FlushIntervalSeconds),
                    TimeSpan.FromSeconds(_config.FlushIntervalSeconds));
            }
        }

        _logger.LogInformation("Created in-memory table '{TableName}' with {ColumnCount} columns",
            _schema.TableName, _schema.Columns.Count);
    }

    /// <summary>
    /// Creates a new in-memory table with custom schema
    /// </summary>
    public InMemoryTable(ILogger logger, InMemoryTableSchema schema, InMemoryTableAttribute? config = null)
        : this((ILoggerFactory?)null, logger, schema, config)
    {
    }

    /// <summary>
    /// Creates a new in-memory table with custom schema and logger factory support
    /// </summary>
    public InMemoryTable(ILoggerFactory loggerFactory, InMemoryTableSchema schema, InMemoryTableAttribute? config = null)
        : this(loggerFactory, loggerFactory?.CreateLogger<InMemoryTable<TEntity>>() ?? throw new ArgumentNullException(nameof(loggerFactory)), schema, config)
    {
    }

    /// <summary>
    /// Internal constructor with full parameter set for custom schema
    /// </summary>
    private InMemoryTable(ILoggerFactory? loggerFactory, ILogger logger, InMemoryTableSchema schema, InMemoryTableAttribute? config = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _loggerFactory = loggerFactory;
        _schema = schema ?? throw new ArgumentNullException(nameof(schema));
        _config = config ?? new InMemoryTableAttribute();
        _config.Validate();

        _rows = new ConcurrentDictionary<object, InMemoryRow>();
        _whereParser = new WhereClauseParser(_schema);
        _indexLock = new ReaderWriterLockSlim();
        _nextId = 1;
        _operationLog = new List<OperationRecord>();
        _operationLogLock = new object();
        _disposed = false;

        // Initialize memory-mapped file store if configured
        if (!string.IsNullOrWhiteSpace(_config.MemoryMappedFileName))
        {
            if (_loggerFactory == null)
            {
                throw new InvalidOperationException(
                    "ILoggerFactory must be provided to InMemoryTable constructor when using memory-mapped files. " +
                    "Use the constructor overload: InMemoryTable(ILoggerFactory loggerFactory, ...)");
            }

            var storeLogger = _loggerFactory.CreateLogger<MemoryMappedFileStore<TEntity>>();
            var syncLogger = _loggerFactory.CreateLogger<MemoryMappedSynchronizer>();

            _memoryMappedStore = new MemoryMappedFileStore<TEntity>(
                _config.MemoryMappedFileName, 
                _config, 
                storeLogger, 
                syncLogger);

            // Auto-load data from file
            if (_config.AutoLoadOnStartup)
            {
                var loadedRows = _memoryMappedStore.LoadAsync().GetAwaiter().GetResult();
                foreach (var entity in loadedRows)
                {
                    // Load directly without validation/constraints (already validated on save)
                    var row = new InMemoryRow(_schema);
                    row.FromEntity(entity);
                    object pk = row.PrimaryKeyValue!;
                    _rows.TryAdd(pk, row);

                    // Update next ID
                    if (pk is long longPk && longPk >= _nextId)
                        _nextId = longPk + 1;
                    else if (pk is int intPk && intPk >= _nextId)
                        _nextId = intPk + 1;
                }
                _logger.LogInformation("Loaded {RowCount} rows from memory-mapped file '{FileName}'",
                    loadedRows.Count, _config.MemoryMappedFileName);
            }

            // Setup sync timer for batched mode
            if (_config.SyncMode == MemoryMappedSyncMode.Batched && _config.FlushIntervalSeconds > 0)
            {
                _syncTimer = new Timer(
                    _ => FlushToMemoryMappedFileAsync().GetAwaiter().GetResult(),
                    null,
                    TimeSpan.FromSeconds(_config.FlushIntervalSeconds),
                    TimeSpan.FromSeconds(_config.FlushIntervalSeconds));
            }
        }

        _logger.LogInformation("Created in-memory table '{TableName}' with {ColumnCount} columns",
            _schema.TableName, _schema.Columns.Count);
    }

    #region Insert Operations

    /// <summary>
    /// Inserts a new entity into the table
    /// </summary>
    /// <returns>The generated or provided primary key value</returns>
    public Task<object> InsertAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (entity == null)
        {
            throw new ArgumentNullException(nameof(entity));
        }

        InMemoryRow row = new InMemoryRow(_schema);
        row.FromEntity(entity);

        // Generate ID if needed
        if (_config.AutoGenerateId && _schema.PrimaryKeyColumn != null)
        {
            object? currentPk = row.PrimaryKeyValue;
            bool needsId = currentPk == null ||
                          (currentPk is int intPk && intPk == 0) ||
                          (currentPk is long longPk && longPk == 0);

            if (needsId)
            {
                long newId = Interlocked.Increment(ref _nextId);

                // Convert to the correct type for the property
                Type pkType = _schema.PrimaryKeyColumn.DataType;
                object idValue;

                if (pkType == typeof(long))
                {
                    idValue = newId;
                }
                else if (pkType == typeof(int))
                {
                    idValue = (int)newId;
                }
                else
                {
                    idValue = Convert.ChangeType(newId, pkType);
                }

                row.SetValue(_schema.PrimaryKeyColumn.Name, idValue);

                // Also set on the entity using cached property setter (avoids reflection)
                _schema.PrimaryKeyColumn.SetPropertyValue(entity, idValue);
            }
        }

        // Validate if configured
        if (_config.ValidateOnWrite)
        {
            ValidationResult validation = row.Validate();
            if (!validation.IsValid)
            {
                throw new InvalidOperationException(
                    $"Insert validation failed: {string.Join("; ", validation.Errors)}");
            }
        }

        // Enforce constraints
        if (_config.EnforceConstraints)
        {
            _indexLock.EnterWriteLock();
            try
            {
                // Check unique constraints
                foreach (InMemoryIndex index in _schema.Indexes)
                {
                    if (index.IsUnique)
                    {
                        IndexKey key = index.CreateKey(row);
                        if (index.ContainsKey(key.Values))
                        {
                            throw new InvalidOperationException(
                                $"Unique constraint violation on index '{index.Name}' for key {key}");
                        }
                    }
                }

                // Add to indexes
                foreach (InMemoryIndex index in _schema.Indexes)
                {
                    index.Add(row);
                }
            }
            finally
            {
                _indexLock.ExitWriteLock();
            }
        }

        // Add to main storage
        object pk = row.PrimaryKeyValue!;
        if (!_rows.TryAdd(pk, row))
        {
            throw new InvalidOperationException($"Row with primary key '{pk}' already exists");
        }

        // Track operation
        if (_config.TrackOperations)
        {
            lock (_operationLogLock)
            {
                _operationLog.Add(new OperationRecord(OperationType.Insert, pk, row.Clone()));
            }
        }

        _logger.LogDebug("Inserted row with PK={PrimaryKey} into '{TableName}'", pk, _schema.TableName);

        // PERFORMANCE: Invalidate property caches since data changed
        InvalidatePropertyCaches();

        // Check if flush is needed
        CheckFlushRequired();

        // Flush to memory-mapped file if configured for immediate mode
        if (_memoryMappedStore != null && _config.SyncMode == MemoryMappedSyncMode.Immediate)
        {
            FlushToMemoryMappedFileAsync(cancellationToken).GetAwaiter().GetResult();
        }

        return Task.FromResult(pk);
    }

    /// <summary>
    /// Bulk inserts multiple entities (OPTIMIZED: batches operations to avoid per-entity overhead)
    /// Defers property cache invalidation and flush checks until after all inserts
    /// </summary>
    public Task<int> BulkInsertAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        int count = 0;
        var entityList = entities.ToList();  // Materialize to get count and allow batch operations

        if (entityList.Count == 0)
            return Task.FromResult(0);

        // Batch insert without per-entity overhead
        foreach (TEntity entity in entityList)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            InMemoryRow row = new InMemoryRow(_schema);
            row.FromEntity(entity);

            // Generate ID if needed
            if (_config.AutoGenerateId && _schema.PrimaryKeyColumn != null)
            {
                object? currentPk = row.PrimaryKeyValue;
                bool needsId = currentPk == null ||
                              (currentPk is int intPk && intPk == 0) ||
                              (currentPk is long longPk && longPk == 0);

                if (needsId)
                {
                    long newId = Interlocked.Increment(ref _nextId);
                    Type pkType = _schema.PrimaryKeyColumn.DataType;
                    object idValue = pkType == typeof(long) ? newId :
                                    pkType == typeof(int) ? (int)newId :
                                    Convert.ChangeType(newId, pkType);

                    row.SetValue(_schema.PrimaryKeyColumn.Name, idValue);
                    _schema.PrimaryKeyColumn.SetPropertyValue(entity, idValue);
                }
            }

            // Validate if configured
            if (_config.ValidateOnWrite)
            {
                ValidationResult validation = row.Validate();
                if (!validation.IsValid)
                    throw new InvalidOperationException($"Insert validation failed: {string.Join("; ", validation.Errors)}");
            }

            // Enforce constraints
            if (_config.EnforceConstraints)
            {
                _indexLock.EnterWriteLock();
                try
                {
                    foreach (InMemoryIndex index in _schema.Indexes)
                    {
                        if (index.IsUnique)
                        {
                            IndexKey key = index.CreateKey(row);
                            if (index.ContainsKey(key.Values))
                                throw new InvalidOperationException($"Unique constraint violation on index '{index.Name}' for key {key}");
                        }
                    }

                    foreach (InMemoryIndex index in _schema.Indexes)
                    {
                        index.Add(row);
                    }
                }
                finally
                {
                    _indexLock.ExitWriteLock();
                }
            }

            // Add to main storage
            object pk = row.PrimaryKeyValue!;
            if (!_rows.TryAdd(pk, row))
                throw new InvalidOperationException($"Row with primary key '{pk}' already exists");

            // Track operation (batch logging)
            if (_config.TrackOperations)
            {
                lock (_operationLogLock)
                {
                    _operationLog.Add(new OperationRecord(OperationType.Insert, pk, row.Clone()));
                }
            }

            count++;
        }

        _logger.LogInformation("Bulk inserted {Count} rows into '{TableName}'", count, _schema.TableName);

        // OPTIMIZATION: Invalidate caches ONCE after all inserts, not per-insert
        InvalidatePropertyCaches();

        // Check flush requirement ONCE after all inserts
        CheckFlushRequired();

        // Flush to memory-mapped file ONCE at the end, not per-insert
        if (_memoryMappedStore != null && _config.SyncMode == MemoryMappedSyncMode.Immediate)
        {
            FlushToMemoryMappedFileAsync(cancellationToken).GetAwaiter().GetResult();
        }

        return Task.FromResult(count);
    }

    #endregion

    #region Select Operations

    /// <summary>
    /// PERFORMANCE: Gets a single entity by property value with O(1) cached dictionary lookup
    /// Lazily builds property value cache on first access, subsequent lookups are instant
    /// Returns first match or null
    /// </summary>
    public async Task<TEntity?> GetByPropertyAsync(string propertyName, object? value, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (string.IsNullOrEmpty(propertyName) || value == null)
            return null;

        // Get or build cache for this property
        Dictionary<string, List<TEntity>>? cache = EnsurePropertyCacheBuilt(propertyName);
        if (cache == null)
            return null;

        // O(1) dictionary lookup
        string cacheKey = GetCacheKey(value);
        if (cache.TryGetValue(cacheKey, out var entities) && entities.Count > 0)
        {
            return await Task.FromResult(entities[0]);  // Return first match
        }

        return null;
    }

    /// <summary>
    /// PERFORMANCE: Gets multiple entities by property value with O(1) cache lookup
    /// Returns all entities matching the property value
    /// </summary>
    public async Task<List<TEntity>> GetByPropertyAsync(string propertyName, object? value, bool returnMultiple, CancellationToken cancellationToken = default)
    {
        var results = new List<TEntity>();

        if (string.IsNullOrEmpty(propertyName) || value == null || !returnMultiple)
            return results;

        Dictionary<string, List<TEntity>>? cache = EnsurePropertyCacheBuilt(propertyName);
        if (cache == null)
            return results;

        string cacheKey = GetCacheKey(value);
        if (cache.TryGetValue(cacheKey, out var entities))
        {
            results.AddRange(entities);  // Return ALL matching entities
        }

        return await Task.FromResult(results);
    }

    /// <summary>
    /// PERFORMANCE: Builds or retrieves cached property value dictionary
    /// Lazy initialization: first access builds cache, subsequent accesses are O(1)
    /// Stores all entities for each property value (handles both unique and non-unique properties)
    /// </summary>
    private Dictionary<string, List<TEntity>>? EnsurePropertyCacheBuilt(string propertyName)
    {
        // Fast path: cache already built
        if (_propertyValueCache.TryGetValue(propertyName, out var existingCache))
        {
            return existingCache;
        }

        // Slow path: build cache once, then cache is locked in
        lock (_cacheLock)
        {
            // Double-check: another thread may have built it
            if (_propertyValueCache.TryGetValue(propertyName, out existingCache))
            {
                return existingCache;
            }

            // Validate property exists
            var column = _schema.Columns.FirstOrDefault(c =>
                c.PropertyName.Equals(propertyName, StringComparison.OrdinalIgnoreCase));
            if (column == null)
                return null;

            // Build cache: iterate through all rows ONCE
            // Structure: Dictionary<propertyValue, List<allEntitiesWithThatValue>>
            var cache = new Dictionary<string, List<TEntity>>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in _rows.Values)
            {
                if (row.State == RowState.Deleted)
                    continue;

                try
                {
                    var propValue = row[propertyName];
                    if (propValue == null)
                        continue;

                    string cacheKey = GetCacheKey(propValue);
                    if (!string.IsNullOrEmpty(cacheKey))
                    {
                        // Create list if doesn't exist, then add this entity
                        if (!cache.ContainsKey(cacheKey))
                        {
                            cache[cacheKey] = new List<TEntity>();
                        }

                        cache[cacheKey].Add(row.ToEntity<TEntity>());
                    }
                }
                catch
                {
                    continue;
                }
            }

            _propertyValueCache[propertyName] = cache;
            return cache;
        }
    }

    /// <summary>
    /// Gets cache key for property value (handles string case-insensitivity)
    /// </summary>
    private static string GetCacheKey(object? value)
    {
        if (value == null)
            return "";

        if (value is string str)
            return str;  // Already normalized by StringComparer.OrdinalIgnoreCase in dictionary

        return value.ToString() ?? "";
    }

    /// <summary>
    /// Invalidates all property caches (called after INSERT/UPDATE/DELETE)
    /// </summary>
    private void InvalidatePropertyCaches()
    {
        lock (_cacheLock)
        {
            _propertyValueCache.Clear();
        }
    }

    /// <summary>
    /// Selects all rows from the table
    /// </summary>
    public IEnumerable<TEntity> Select()
    {
        ThrowIfDisposed();

        return _rows.Values
            .Where(r => r.State != RowState.Deleted)
            .Select(r => r.ToEntity<TEntity>());
    }

    /// <summary>
    /// Selects rows matching the WHERE clause
    /// </summary>
    public IEnumerable<TEntity> Select(string whereClause)
    {
        ThrowIfDisposed();

        Func<InMemoryRow, bool> predicate = _whereParser.Parse(whereClause);

        return _rows.Values
            .Where(r => r.State != RowState.Deleted && predicate(r))
            .Select(r => r.ToEntity<TEntity>());
    }

    /// <summary>
    /// Selects rows matching the predicate
    /// </summary>
    public IEnumerable<TEntity> Select(Func<TEntity, bool> predicate)
    {
        ThrowIfDisposed();

        return _rows.Values
            .Where(r => r.State != RowState.Deleted)
            .Select(r => r.ToEntity<TEntity>())
            .Where(predicate);
    }

    /// <summary>
    /// Gets a single row by primary key
    /// </summary>
    public TEntity? GetById(object id)
    {
        ThrowIfDisposed();

        if (_rows.TryGetValue(id, out InMemoryRow? row) && row.State != RowState.Deleted)
        {
            return row.ToEntity<TEntity>();
        }

        return null;
    }

    /// <summary>
    /// Gets a single row by primary key (async)
    /// </summary>
    public Task<TEntity?> GetByIdAsync(object id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(GetById(id));
    }

    /// <summary>
    /// Counts rows matching the WHERE clause
    /// </summary>
    public int CountWhere(string? whereClause = null)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(whereClause))
        {
            return RowCount;
        }

        Func<InMemoryRow, bool> predicate = _whereParser.Parse(whereClause);
        return _rows.Values.Count(r => r.State != RowState.Deleted && predicate(r));
    }

    /// <summary>
    /// Checks if any rows exist matching the WHERE clause
    /// </summary>
    public bool Exists(string? whereClause = null)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(whereClause))
        {
            return RowCount > 0;
        }

        Func<InMemoryRow, bool> predicate = _whereParser.Parse(whereClause);
        return _rows.Values.Any(r => r.State != RowState.Deleted && predicate(r));
    }

    /// <summary>
    /// Checks if a row exists by primary key
    /// </summary>
    public bool ExistsById(object id)
    {
        ThrowIfDisposed();
        return _rows.TryGetValue(id, out InMemoryRow? row) && row.State != RowState.Deleted;
    }

    #endregion

    #region Update Operations

    /// <summary>
    /// Updates an existing entity by primary key
    /// </summary>
    public Task<int> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (entity == null)
        {
            throw new ArgumentNullException(nameof(entity));
        }

        // Get primary key from entity using cached accessor (avoids reflection)
        if (_schema.PrimaryKeyColumn == null)
        {
            throw new InvalidOperationException("Cannot update without a primary key column");
        }

        object? pk = _schema.PrimaryKeyColumn.GetPropertyValue(entity);

        if (pk == null)
        {
            throw new InvalidOperationException("Entity primary key cannot be null");
        }

        if (!_rows.TryGetValue(pk, out InMemoryRow? existingRow))
        {
            return Task.FromResult(0); // Row not found
        }

        if (existingRow.State == RowState.Deleted)
        {
            return Task.FromResult(0); // Row is deleted
        }

        // Capture old index keys before update
        Dictionary<string, IndexKey> oldKeys = new Dictionary<string, IndexKey>();
        foreach (InMemoryIndex index in _schema.Indexes)
        {
            oldKeys[index.Name] = index.GetCurrentKey(existingRow);
        }

        // Update the row
        existingRow.FromEntity(entity);
        existingRow.State = RowState.Modified;

        // Validate if configured
        if (_config.ValidateOnWrite)
        {
            ValidationResult validation = existingRow.Validate();
            if (!validation.IsValid)
            {
                throw new InvalidOperationException(
                    $"Update validation failed: {string.Join("; ", validation.Errors)}");
            }
        }

        // Update indexes
        if (_config.EnforceConstraints)
        {
            _indexLock.EnterWriteLock();
            try
            {
                foreach (InMemoryIndex index in _schema.Indexes)
                {
                    if (!index.Update(existingRow, oldKeys[index.Name]))
                    {
                        throw new InvalidOperationException(
                            $"Unique constraint violation on index '{index.Name}' during update");
                    }
                }
            }
            finally
            {
                _indexLock.ExitWriteLock();
            }
        }

        // Track operation
        if (_config.TrackOperations)
        {
            lock (_operationLogLock)
            {
                _operationLog.Add(new OperationRecord(OperationType.Update, pk, existingRow.Clone()));
            }
        }

        _logger.LogDebug("Updated row with PK={PrimaryKey} in '{TableName}'", pk, _schema.TableName);

        // PERFORMANCE: Invalidate property caches since data changed
        InvalidatePropertyCaches();

        return Task.FromResult(1);
    }

    /// <summary>
    /// Updates rows matching the WHERE clause with the specified values
    /// </summary>
    public Task<int> UpdateAsync(string whereClause, Dictionary<string, object?> values, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (values == null || values.Count == 0)
        {
            return Task.FromResult(0);
        }

        Func<InMemoryRow, bool> predicate = _whereParser.Parse(whereClause);
        int updated = 0;

        foreach (InMemoryRow row in _rows.Values)
        {
            if (row.State == RowState.Deleted || !predicate(row))
            {
                continue;
            }

            // Capture old keys
            Dictionary<string, IndexKey> oldKeys = new Dictionary<string, IndexKey>();
            foreach (InMemoryIndex index in _schema.Indexes)
            {
                oldKeys[index.Name] = index.GetCurrentKey(row);
            }

            // Apply updates
            foreach (KeyValuePair<string, object?> kvp in values)
            {
                row.SetValue(kvp.Key, kvp.Value);
            }
            row.State = RowState.Modified;

            // Update indexes
            if (_config.EnforceConstraints)
            {
                _indexLock.EnterWriteLock();
                try
                {
                    foreach (InMemoryIndex index in _schema.Indexes)
                    {
                        index.Update(row, oldKeys[index.Name]);
                    }
                }
                finally
                {
                    _indexLock.ExitWriteLock();
                }
            }

            // Track operation
            if (_config.TrackOperations)
            {
                lock (_operationLogLock)
                {
                    _operationLog.Add(new OperationRecord(OperationType.Update, row.PrimaryKeyValue!, row.Clone()));
                }
            }

            updated++;
        }

        _logger.LogDebug("Updated {Count} rows in '{TableName}' matching WHERE clause", updated, _schema.TableName);

        // PERFORMANCE: Invalidate property caches since data changed
        if (updated > 0)
        {
            InvalidatePropertyCaches();
        }

        return Task.FromResult(updated);
    }

    /// <summary>
    /// Bulk updates multiple entities
    /// </summary>
    public Task<int> BulkUpdateAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        int count = 0;
        foreach (TEntity entity in entities)
        {
            count += UpdateAsync(entity, cancellationToken).GetAwaiter().GetResult();
        }

        return Task.FromResult(count);
    }

    #endregion

    #region Delete Operations

    /// <summary>
    /// Deletes a row by primary key
    /// </summary>
    public Task<int> DeleteAsync(object id, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (!_rows.TryGetValue(id, out InMemoryRow? row))
        {
            return Task.FromResult(0);
        }

        if (row.State == RowState.Deleted)
        {
            return Task.FromResult(0);
        }

        row.Delete();

        // Remove from indexes
        if (_config.EnforceConstraints)
        {
            _indexLock.EnterWriteLock();
            try
            {
                foreach (InMemoryIndex index in _schema.Indexes)
                {
                    index.Remove(row);
                }
            }
            finally
            {
                _indexLock.ExitWriteLock();
            }
        }

        // Track operation
        if (_config.TrackOperations)
        {
            lock (_operationLogLock)
            {
                _operationLog.Add(new OperationRecord(OperationType.Delete, id, null));
            }
        }

        _logger.LogDebug("Deleted row with PK={PrimaryKey} from '{TableName}'", id, _schema.TableName);

        // PERFORMANCE: Invalidate property caches since data changed
        InvalidatePropertyCaches();

        return Task.FromResult(1);
    }

    /// <summary>
    /// Deletes rows matching the WHERE clause
    /// </summary>
    public Task<int> DeleteAsync(string whereClause, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        Func<InMemoryRow, bool> predicate = _whereParser.Parse(whereClause);
        int deleted = 0;

        foreach (InMemoryRow row in _rows.Values)
        {
            if (row.State == RowState.Deleted || !predicate(row))
            {
                continue;
            }

            row.Delete();

            // Remove from indexes
            if (_config.EnforceConstraints)
            {
                _indexLock.EnterWriteLock();
                try
                {
                    foreach (InMemoryIndex index in _schema.Indexes)
                    {
                        index.Remove(row);
                    }
                }
                finally
                {
                    _indexLock.ExitWriteLock();
                }
            }

            // Track operation
            if (_config.TrackOperations)
            {
                lock (_operationLogLock)
                {
                    _operationLog.Add(new OperationRecord(OperationType.Delete, row.PrimaryKeyValue!, null));
                }
            }

            deleted++;
        }

        _logger.LogDebug("Deleted {Count} rows from '{TableName}' matching WHERE clause", deleted, _schema.TableName);

        // PERFORMANCE: Invalidate property caches since data changed
        if (deleted > 0)
        {
            InvalidatePropertyCaches();
        }

        return Task.FromResult(deleted);
    }

    /// <summary>
    /// Bulk deletes by primary keys
    /// </summary>
    public Task<int> BulkDeleteAsync(IEnumerable<object> ids, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        int count = 0;
        foreach (object id in ids)
        {
            count += DeleteAsync(id, cancellationToken).GetAwaiter().GetResult();
        }

        return Task.FromResult(count);
    }

    #endregion

    #region Flush and Clear Operations

    /// <summary>
    /// Gets all pending changes (rows that need to be flushed)
    /// </summary>
    public IReadOnlyList<InMemoryRow> GetPendingChanges()
    {
        ThrowIfDisposed();

        return _rows.Values
            .Where(r => r.State != RowState.Unchanged)
            .ToList();
    }

    /// <summary>
    /// Gets the operation log (if TrackOperations is enabled)
    /// </summary>
    public IReadOnlyList<OperationRecord> GetOperationLog()
    {
        ThrowIfDisposed();

        lock (_operationLogLock)
        {
            return _operationLog.ToList();
        }
    }

    /// <summary>
    /// Clears the operation log
    /// </summary>
    public void ClearOperationLog()
    {
        lock (_operationLogLock)
        {
            _operationLog.Clear();
        }
    }

    /// <summary>
    /// Accepts all changes, marking rows as unchanged and physically removing deleted rows
    /// </summary>
    public void AcceptChanges()
    {
        ThrowIfDisposed();

        List<object> toRemove = new List<object>();

        foreach (KeyValuePair<object, InMemoryRow> kvp in _rows)
        {
            if (kvp.Value.State == RowState.Deleted)
            {
                toRemove.Add(kvp.Key);
            }
            else
            {
                kvp.Value.AcceptChanges();
            }
        }

        foreach (object key in toRemove)
        {
            _rows.TryRemove(key, out _);
        }

        if (!_config.RetainAfterFlush)
        {
            ClearOperationLog();
        }

        _logger.LogDebug("Accepted changes for '{TableName}', removed {Count} deleted rows",
            _schema.TableName, toRemove.Count);
    }

    /// <summary>
    /// Clears all data from the table
    /// </summary>
    public void Clear()
    {
        ThrowIfDisposed();

        _rows.Clear();

        _indexLock.EnterWriteLock();
        try
        {
            foreach (InMemoryIndex index in _schema.Indexes)
            {
                index.Clear();
            }
        }
        finally
        {
            _indexLock.ExitWriteLock();
        }

        ClearOperationLog();
        _nextId = 1;

        _logger.LogInformation("Cleared all data from '{TableName}'", _schema.TableName);
    }

    /// <summary>
    /// Sets a parameter for WHERE clause parsing
    /// </summary>
    public void SetParameter(string name, object? value)
    {
        _whereParser.SetParameter(name, value);
    }

    #region Load from Database

    /// <summary>
    /// Loads data from the main database table into this in-memory table.
    /// </summary>
    /// <param name="connection">Open database connection</param>
    /// <param name="whereClause">Optional WHERE clause to filter loaded data (without the WHERE keyword)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of rows loaded</returns>
    public async Task<int> LoadFromDatabaseAsync(
        System.Data.Common.DbConnection connection,
        string? whereClause = null,
        CancellationToken cancellationToken = default)
    {
        return await LoadFromTableAsync(connection, _schema.TableName, whereClause, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Loads data from the staging table into this in-memory table.
    /// </summary>
    /// <param name="connection">Open database connection</param>
    /// <param name="whereClause">Optional WHERE clause to filter loaded data (without the WHERE keyword)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of rows loaded</returns>
    public async Task<int> LoadFromStagingAsync(
        System.Data.Common.DbConnection connection,
        string? whereClause = null,
        CancellationToken cancellationToken = default)
    {
        string stagingTableName = $"{_schema.TableName}_Staging";
        return await LoadFromTableAsync(connection, stagingTableName, whereClause, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Internal method to load data from a specified table.
    /// </summary>
    private async Task<int> LoadFromTableAsync(
        System.Data.Common.DbConnection connection,
        string tableName,
        string? whereClause,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        if (connection == null)
        {
            throw new ArgumentNullException(nameof(connection));
        }

        if (connection.State != System.Data.ConnectionState.Open)
        {
            throw new InvalidOperationException("Connection must be open");
        }

        _logger.LogInformation("Loading data from database table '{TableName}' into '{InMemoryTableName}'",
            tableName, _schema.TableName);

        // Build SELECT query
        List<string> columnNames = _schema.Columns.Select(c => $"[{c.Name}]").ToList();
        string selectSql = $"SELECT {string.Join(", ", columnNames)} FROM [{tableName}]";

        if (!string.IsNullOrWhiteSpace(whereClause))
        {
            selectSql += $" WHERE {whereClause}";
        }

        int rowsLoaded = 0;

        using (System.Data.Common.DbCommand command = connection.CreateCommand())
        {
            command.CommandText = selectSql;
            command.CommandTimeout = 300; // 5 minute timeout for large loads

            using (System.Data.Common.DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
            {
                // Cache column ordinals for performance
                Dictionary<string, int> ordinals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (ColumnDefinition column in _schema.Columns)
                {
                    try
                    {
                        ordinals[column.Name] = reader.GetOrdinal(column.Name);
                    }
                    catch
                    {
                        _logger.LogWarning("Column '{ColumnName}' not found in table '{TableName}'",
                            column.Name, tableName);
                    }
                }

                // Read all rows
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    TEntity entity = new TEntity();

                    // Map columns to entity using cached accessors
                    foreach (ColumnDefinition column in _schema.Columns)
                    {
                        if (!ordinals.TryGetValue(column.Name, out int ordinal))
                        {
                            continue;
                        }

                        try
                        {
                            object? value = reader.IsDBNull(ordinal) ? null : reader.GetValue(ordinal);

                            if (value != null)
                            {
                                // Convert to correct type if needed
                                if (value.GetType() != column.DataType)
                                {
                                    value = column.ConvertValue(value);
                                }

                                // Use cached property setter (avoids reflection)
                                column.SetPropertyValue(entity, value);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error loading column '{ColumnName}' for row", column.Name);
                        }
                    }

                    // Insert the entity into the in-memory table
                    await InsertAsync(entity, cancellationToken).ConfigureAwait(false);
                    rowsLoaded++;
                }
            }
        }

        // Mark all loaded data as unchanged (it's already in the database)
        AcceptChanges();

        _logger.LogInformation("Loaded {RowCount} rows from database table '{TableName}' into '{InMemoryTableName}'",
            rowsLoaded, tableName, _schema.TableName);

        return rowsLoaded;
    }

    #endregion

    private void CheckFlushRequired()
    {
        if (_config.MaxRowCount > 0 && _rows.Count >= _config.MaxRowCount)
        {
            FlushRequired?.Invoke(this, new FlushRequiredEventArgs(
                _schema.TableName,
                FlushReason.MaxRowCountReached,
                _rows.Count));
        }
    }

    /// <summary>
    /// Flushes current in-memory data to memory-mapped file.
    /// </summary>
    public async Task FlushToMemoryMappedFileAsync(CancellationToken cancellationToken = default)
    {
        if (_memoryMappedStore == null)
        {
            _logger.LogDebug("Memory-mapped file not configured, skipping flush");
            return;
        }

        ThrowIfDisposed();

        try
        {
            // Get all active rows as entities
            var entities = _rows.Values
                .Where(r => r.State != RowState.Deleted)
                .Select(r => r.ToEntity<TEntity>())
                .ToList();

            await _memoryMappedStore.SaveAsync(entities, cancellationToken);

                    _logger.LogDebug("Flushed {RowCount} rows to memory-mapped file '{FileName}'", 
                        entities.Count, _config.MemoryMappedFileName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to flush to memory-mapped file '{FileName}'", _config.MemoryMappedFileName);
                    throw;
                }
            }

            /// <summary>
            /// Deletes the memory-mapped file from disk.
            /// Warning: This will permanently delete the file. Ensure no other processes are accessing it.
            /// Only works if MemoryMappedFileName is configured.
            /// </summary>
            public void DeleteMemoryMappedFile()
            {
                if (_memoryMappedStore == null)
                {
                    _logger.LogDebug("Memory-mapped file not configured, nothing to delete");
                    return;
                }

                ThrowIfDisposed();

                try
                {
                    _memoryMappedStore.DeleteFile();
                    _logger.LogInformation("Deleted memory-mapped file '{FileName}'", _config.MemoryMappedFileName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to delete memory-mapped file '{FileName}'", _config.MemoryMappedFileName);
                    throw;
                }
            }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(InMemoryTable<TEntity>));
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            try
            {
                // Flush one last time if configured for immediate or batched mode
                if (_memoryMappedStore != null && 
                    (_config.SyncMode == MemoryMappedSyncMode.Immediate || _config.SyncMode == MemoryMappedSyncMode.Batched))
                {
                    FlushToMemoryMappedFileAsync().GetAwaiter().GetResult();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to flush memory-mapped file on dispose");
            }

            _syncTimer?.Dispose();
            _memoryMappedStore?.Dispose();
            _indexLock.Dispose();
            _disposed = true;
        }
    }

    #endregion
}

#region Supporting Types

/// <summary>
/// Records an operation for flush replay
/// </summary>
public sealed class OperationRecord
{
    public OperationType Operation { get; }
    public object PrimaryKey { get; }
    public InMemoryRow? RowSnapshot { get; }
    public DateTime Timestamp { get; }

    public OperationRecord(OperationType operation, object primaryKey, InMemoryRow? rowSnapshot)
    {
        Operation = operation;
        PrimaryKey = primaryKey;
        RowSnapshot = rowSnapshot;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Type of database operation
/// </summary>
public enum OperationType
{
    Insert,
    Update,
    Delete
}

/// <summary>
/// Event args for flush required event
/// </summary>
public sealed class FlushRequiredEventArgs : EventArgs
{
    public string TableName { get; }
    public FlushReason Reason { get; }
    public int CurrentRowCount { get; }

    public FlushRequiredEventArgs(string tableName, FlushReason reason, int currentRowCount)
    {
        TableName = tableName;
        Reason = reason;
        CurrentRowCount = currentRowCount;
    }
}

/// <summary>
/// Reason why a flush is required
/// </summary>
public enum FlushReason
{
    MaxRowCountReached,
    TimerElapsed,
    ManualRequest
}

#endregion
