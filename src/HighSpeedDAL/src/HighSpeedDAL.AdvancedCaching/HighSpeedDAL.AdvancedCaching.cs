using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HighSpeedDAL.Core.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace HighSpeedDAL.AdvancedCaching
{
    // ============================================================================
    // CORE INTERFACES
    // ============================================================================

    /// <summary>
    /// Defines the contract for read-through cache operations
    /// </summary>
    /// <typeparam name="TEntity">The entity type being cached</typeparam>
    public interface IReadThroughCache<TEntity> where TEntity : class, IEntityCloneable<TEntity>
    {
        /// <summary>
        /// Gets an entity by key, loading from database if not in cache
        /// </summary>
        Task<TEntity?> GetAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets multiple entities by keys, loading missing ones from database
        /// </summary>
        Task<Dictionary<string, TEntity>> GetManyAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default);

        /// <summary>
        /// Warms the cache with frequently accessed data
        /// </summary>
        Task WarmCacheAsync(Expression<Func<TEntity, bool>>? filter = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Refreshes cached data in the background
        /// </summary>
        Task RefreshAsync(string key, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Defines the contract for write-through cache operations
    /// </summary>
    /// <typeparam name="TEntity">The entity type being cached</typeparam>
    public interface IWriteThroughCache<TEntity> where TEntity : class, IEntityCloneable<TEntity>
    {
        /// <summary>
        /// Writes to both cache and database synchronously
        /// </summary>
        Task<bool> WriteAsync(string key, TEntity entity, CancellationToken cancellationToken = default);

        /// <summary>
        /// Writes multiple entities to both cache and database
        /// </summary>
        Task<bool> WriteManyAsync(Dictionary<string, TEntity> entities, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes from both cache and database
        /// </summary>
        Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates an entity in both cache and database with transaction support
        /// </summary>
        Task<bool> UpdateAsync(string key, TEntity entity, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Defines the contract for cache-aside pattern operations
    /// </summary>
    /// <typeparam name="TEntity">The entity type being cached</typeparam>
    public interface ICacheAsidePattern<TEntity> where TEntity : class, IEntityCloneable<TEntity>
    {
        /// <summary>
        /// Gets an entity from cache, returns null if not cached
        /// </summary>
        Task<TEntity?> GetFromCacheAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets an entity in cache with optional TTL
        /// </summary>
        Task SetInCacheAsync(string key, TEntity entity, TimeSpan? ttl = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Invalidates cache for a specific key
        /// </summary>
        Task InvalidateAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Invalidates cache based on a pattern
        /// </summary>
        Task InvalidatePatternAsync(string pattern, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets cache statistics
        /// </summary>
        CacheStatistics GetStatistics();
    }

    /// <summary>
    /// Defines the contract for distributed cache coordination
    /// </summary>
    public interface IDistributedCacheCoordinator
    {
        /// <summary>
        /// Acquires a distributed lock
        /// </summary>
        Task<IDistributedLock?> AcquireLockAsync(string key, TimeSpan timeout, CancellationToken cancellationToken = default);

        /// <summary>
        /// Prevents cache stampede by coordinating concurrent requests
        /// </summary>
        Task<TResult> PreventStampedeAsync<TResult>(string key, Func<Task<TResult>> loader, TimeSpan timeout, CancellationToken cancellationToken = default);

        /// <summary>
        /// Writes to cache asynchronously (write-behind)
        /// </summary>
        Task EnqueueWriteAsync<TEntity>(string key, TEntity entity, CancellationToken cancellationToken = default) where TEntity : class;

        /// <summary>
        /// Flushes pending writes to database
        /// </summary>
        Task FlushPendingWritesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Coordinates cache invalidation across instances
        /// </summary>
        Task BroadcastInvalidationAsync(string key, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Represents a distributed lock
    /// </summary>
    public interface IDistributedLock : IDisposable
    {
        /// <summary>
        /// Gets whether the lock is currently held
        /// </summary>
        bool IsAcquired { get; }

        /// <summary>
        /// Releases the lock
        /// </summary>
        Task ReleaseAsync();
    }

    // ============================================================================
    // CONFIGURATION CLASSES
    // ============================================================================

    /// <summary>
    /// Configuration options for read-through cache
    /// </summary>
    public class ReadThroughCacheOptions
    {
        /// <summary>
        /// Default TTL for cached items
        /// </summary>
        public TimeSpan DefaultTtl { get; set; } = TimeSpan.FromMinutes(15);

        /// <summary>
        /// Maximum number of items to cache
        /// </summary>
        public int MaxCacheSize { get; set; } = 10000;

        /// <summary>
        /// Enable background refresh
        /// </summary>
        public bool EnableBackgroundRefresh { get; set; } = true;

        /// <summary>
        /// Refresh interval for background refresh
        /// </summary>
        public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromMinutes(10);

        /// <summary>
        /// Enable cache warming on startup
        /// </summary>
        public bool EnableCacheWarming { get; set; } = false;

        /// <summary>
        /// Maximum concurrent loads from database
        /// </summary>
        public int MaxConcurrentLoads { get; set; } = 10;
    }

    /// <summary>
    /// Configuration options for write-through cache
    /// </summary>
    public class WriteThroughCacheOptions
    {
        /// <summary>
        /// Enable transaction support
        /// </summary>
        public bool EnableTransactions { get; set; } = true;

        /// <summary>
        /// Timeout for write operations
        /// </summary>
        public TimeSpan WriteTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Retry count on failure
        /// </summary>
        public int RetryCount { get; set; } = 3;

        /// <summary>
        /// Delay between retries
        /// </summary>
        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(100);

        /// <summary>
        /// Enable rollback on cache write failure
        /// </summary>
        public bool RollbackOnCacheFailure { get; set; } = false;
    }

    /// <summary>
    /// Configuration options for cache-aside pattern
    /// </summary>
    public class CacheAsideOptions
    {
        /// <summary>
        /// Default TTL for cached items
        /// </summary>
        public TimeSpan DefaultTtl { get; set; } = TimeSpan.FromMinutes(15);

        /// <summary>
        /// Enable sliding expiration
        /// </summary>
        public bool UseSlidingExpiration { get; set; } = false;

        /// <summary>
        /// Maximum cache size
        /// </summary>
        public int MaxCacheSize { get; set; } = 10000;

        /// <summary>
        /// Enable statistics tracking
        /// </summary>
        public bool EnableStatistics { get; set; } = true;

        /// <summary>
        /// Eviction policy (LRU, LFU, FIFO)
        /// </summary>
        public EvictionPolicy EvictionPolicy { get; set; } = EvictionPolicy.LRU;
    }

    /// <summary>
    /// Configuration options for distributed cache coordination
    /// </summary>
    public class DistributedCacheOptions
    {
        /// <summary>
        /// Redis connection string
        /// </summary>
        public string RedisConnectionString { get; set; } = "localhost:6379";

        /// <summary>
        /// Default lock timeout
        /// </summary>
        public TimeSpan DefaultLockTimeout { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Enable cache stampede prevention
        /// </summary>
        public bool EnableStampedePrevention { get; set; } = true;

        /// <summary>
        /// Write-behind buffer size
        /// </summary>
        public int WriteBufferSize { get; set; } = 1000;

        /// <summary>
        /// Write-behind flush interval
        /// </summary>
        public TimeSpan FlushInterval { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Enable cross-instance invalidation
        /// </summary>
        public bool EnableInvalidationBroadcast { get; set; } = true;

        /// <summary>
        /// Channel name for invalidation broadcasts
        /// </summary>
        public string InvalidationChannel { get; set; } = "cache:invalidations";
    }

    /// <summary>
    /// Cache eviction policies
    /// </summary>
    public enum EvictionPolicy
    {
        /// <summary>
        /// Least Recently Used
        /// </summary>
        LRU,

        /// <summary>
        /// Least Frequently Used
        /// </summary>
        LFU,

        /// <summary>
        /// First In First Out
        /// </summary>
        FIFO
    }

    /// <summary>
    /// Cache statistics
    /// </summary>
    public class CacheStatistics
    {
        /// <summary>
        /// Total number of cache hits
        /// </summary>
        public long Hits { get; set; }

        /// <summary>
        /// Total number of cache misses
        /// </summary>
        public long Misses { get; set; }

        /// <summary>
        /// Cache hit ratio (0-1)
        /// </summary>
        public double HitRatio => Hits + Misses > 0 ? (double)Hits / (Hits + Misses) : 0;

        /// <summary>
        /// Current number of items in cache
        /// </summary>
        public int ItemCount { get; set; }

        /// <summary>
        /// Total number of evictions
        /// </summary>
        public long Evictions { get; set; }

        /// <summary>
        /// Average access time in milliseconds
        /// </summary>
        public double AverageAccessTimeMs { get; set; }

        /// <summary>
        /// Total cache size in bytes
        /// </summary>
        public long TotalSizeBytes { get; set; }
    }

    // ============================================================================
    // INTERNAL MODELS
    // ============================================================================

    /// <summary>
    /// Represents a cached item with metadata
    /// </summary>
    internal class CacheEntry<TEntity> where TEntity : class
    {
        public TEntity Value { get; set; } = default!;
        public DateTime CreatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public DateTime LastAccessedAt { get; set; }
        public int AccessCount { get; set; }
        public long SizeBytes { get; set; }
    }

    /// <summary>
    /// Represents a pending write operation
    /// </summary>
    internal class PendingWrite<TEntity> where TEntity : class
    {
        public string Key { get; set; } = string.Empty;
        public TEntity Entity { get; set; } = default!;
        public DateTime QueuedAt { get; set; }
        public int RetryCount { get; set; }
    }

    /// <summary>
    /// Distributed lock implementation
    /// </summary>
    internal class RedisDistributedLock : IDistributedLock
    {
        private readonly IDatabase _redis;
        private readonly string _key;
        private readonly string _token;
        private readonly ILogger _logger;
        private bool _isAcquired;
        private bool _disposed;

        public bool IsAcquired => _isAcquired && !_disposed;

        public RedisDistributedLock(IDatabase redis, string key, string token, ILogger logger)
        {
            _redis = redis;
            _key = key;
            _token = token;
            _logger = logger;
            _isAcquired = true;
        }

        public async Task ReleaseAsync()
        {
            if (!_isAcquired || _disposed)
            {
                return;
            }

            try
            {
                string script = @"
                    if redis.call('get', KEYS[1]) == ARGV[1] then
                        return redis.call('del', KEYS[1])
                    else
                        return 0
                    end";

                await _redis.ScriptEvaluateAsync(script, new RedisKey[] { _key }, new RedisValue[] { _token });
                _isAcquired = false;
                _logger.LogDebug("Released distributed lock for key: {Key}", _key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error releasing distributed lock for key: {Key}", _key);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            ReleaseAsync().GetAwaiter().GetResult();
            _disposed = true;
        }
    }

    // ============================================================================
    // READ-THROUGH CACHE IMPLEMENTATION
    // ============================================================================

    /// <summary>
    /// Implements read-through cache pattern with automatic database loading
    /// </summary>
    public class ReadThroughCache<TEntity> : IReadThroughCache<TEntity> where TEntity : class, IEntityCloneable<TEntity>
    {
        private readonly ConcurrentDictionary<string, CacheEntry<TEntity>> _cache;
        private readonly SemaphoreSlim _loadSemaphore;
        private readonly ILogger<ReadThroughCache<TEntity>> _logger;
        private readonly ReadThroughCacheOptions _options;
        private readonly Func<string, Task<TEntity?>> _loader;
        private readonly Func<IEnumerable<string>, Task<Dictionary<string, TEntity>>> _bulkLoader;
        private readonly Timer? _refreshTimer;
        private readonly ConcurrentDictionary<string, DateTime> _refreshSchedule;

        /// <summary>
        /// Initializes a new instance of the ReadThroughCache class
        /// </summary>
        public ReadThroughCache(
            Func<string, Task<TEntity?>> loader,
            Func<IEnumerable<string>, Task<Dictionary<string, TEntity>>> bulkLoader,
            ReadThroughCacheOptions options,
            ILogger<ReadThroughCache<TEntity>> logger)
        {
            _loader = loader ?? throw new ArgumentNullException(nameof(loader));
            _bulkLoader = bulkLoader ?? throw new ArgumentNullException(nameof(bulkLoader));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _cache = new ConcurrentDictionary<string, CacheEntry<TEntity>>();
            _loadSemaphore = new SemaphoreSlim(_options.MaxConcurrentLoads);
            _refreshSchedule = new ConcurrentDictionary<string, DateTime>();

            if (_options.EnableBackgroundRefresh)
            {
                _refreshTimer = new Timer(BackgroundRefreshCallback, null, _options.RefreshInterval, _options.RefreshInterval);
            }

            _logger.LogInformation("ReadThroughCache initialized for {EntityType} with TTL: {TTL}, MaxSize: {MaxSize}",
                typeof(TEntity).Name, _options.DefaultTtl, _options.MaxCacheSize);
        }

        /// <summary>
        /// Gets an entity by key, loading from database if not in cache
        /// </summary>
        public async Task<TEntity?> GetAsync(string key, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Key cannot be null or whitespace", nameof(key));
            }

            Stopwatch stopwatch = Stopwatch.StartNew();

            try
            {
                // Check cache first
                if (_cache.TryGetValue(key, out CacheEntry<TEntity>? entry))
                {
                    // Check expiration
                    if (entry.ExpiresAt.HasValue && entry.ExpiresAt.Value < DateTime.UtcNow)
                    {
                        // Expired, remove from cache
                        _cache.TryRemove(key, out CacheEntry<TEntity>? _);
                        _logger.LogDebug("Cache entry expired for key: {Key}", key);
                    }
                    else
                    {
                        // Update access metadata
                        entry.LastAccessedAt = DateTime.UtcNow;
                        entry.AccessCount++;

                        _logger.LogDebug("Cache HIT for key: {Key} in {ElapsedMs}ms", key, stopwatch.ElapsedMilliseconds);
                        return entry.Value.ShallowClone(); // Defensive copy to prevent cache corruption
                    }
                }

                // Cache miss - load from database
                _logger.LogDebug("Cache MISS for key: {Key}, loading from database", key);

                await _loadSemaphore.WaitAsync(cancellationToken);
                try
                {
                    // Double-check cache after acquiring semaphore
                    if (_cache.TryGetValue(key, out entry))
                    {
                        entry.LastAccessedAt = DateTime.UtcNow;
                        entry.AccessCount++;
                        return entry.Value.ShallowClone(); // Defensive copy to prevent cache corruption
                    }

                    // Load from database
                    TEntity? entity = await _loader(key);

                    if (entity != null)
                    {
                        // Add to cache
                        CacheEntry<TEntity> newEntry = new CacheEntry<TEntity>
                        {
                            Value = entity,
                            CreatedAt = DateTime.UtcNow,
                            ExpiresAt = DateTime.UtcNow.Add(_options.DefaultTtl),
                            LastAccessedAt = DateTime.UtcNow,
                            AccessCount = 1,
                            SizeBytes = EstimateSize(entity)
                        };

                        _cache.TryAdd(key, newEntry);
                        EvictIfNecessary();

                        // Schedule refresh if enabled
                        if (_options.EnableBackgroundRefresh)
                        {
                            _refreshSchedule.TryAdd(key, DateTime.UtcNow.Add(_options.RefreshInterval));
                        }

                                _logger.LogInformation("Loaded entity from database and added to cache: {Key} in {ElapsedMs}ms",
                                    key, stopwatch.ElapsedMilliseconds);
                            }

                            return entity != null ? entity.ShallowClone() : null; // Defensive copy to prevent cache corruption
                        }
                finally
                {
                    _loadSemaphore.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting entity from cache for key: {Key}", key);
                throw;
            }
        }

        /// <summary>
        /// Gets multiple entities by keys, loading missing ones from database
        /// </summary>
        public async Task<Dictionary<string, TEntity>> GetManyAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
        {
            if (keys == null)
            {
                throw new ArgumentNullException(nameof(keys));
            }

            List<string> keyList = keys.ToList();
            if (keyList.Count == 0)
            {
                return [];
            }

            Stopwatch stopwatch = Stopwatch.StartNew();
            Dictionary<string, TEntity> result = [];
            List<string> missingKeys = [];

            try
            {
                // Check cache for each key
                foreach (string key in keyList)
                {
                    if (_cache.TryGetValue(key, out CacheEntry<TEntity>? entry))
                    {
                        if (!entry.ExpiresAt.HasValue || entry.ExpiresAt.Value >= DateTime.UtcNow)
                        {
                            entry.LastAccessedAt = DateTime.UtcNow;
                            entry.AccessCount++;
                            result[key] = entry.Value.ShallowClone(); // Defensive copy to prevent cache corruption
                        }
                        else
                        {
                            _cache.TryRemove(key, out CacheEntry<TEntity>? _);
                            missingKeys.Add(key);
                        }
                    }
                    else
                    {
                        missingKeys.Add(key);
                    }
                }

                _logger.LogDebug("GetMany - Cache hits: {Hits}, misses: {Misses}", result.Count, missingKeys.Count);

                // Load missing keys from database
                if (missingKeys.Count > 0)
                {
                    await _loadSemaphore.WaitAsync(cancellationToken);
                    try
                    {
                        Dictionary<string, TEntity> loaded = await _bulkLoader(missingKeys);

                        foreach (KeyValuePair<string, TEntity> kvp in loaded)
                        {
                            CacheEntry<TEntity> newEntry = new CacheEntry<TEntity>
                            {
                                Value = kvp.Value,
                                CreatedAt = DateTime.UtcNow,
                                ExpiresAt = DateTime.UtcNow.Add(_options.DefaultTtl),
                                LastAccessedAt = DateTime.UtcNow,
                                AccessCount = 1,
                                SizeBytes = EstimateSize(kvp.Value)
                            };

                            _cache.TryAdd(kvp.Key, newEntry);
                            result[kvp.Key] = kvp.Value.ShallowClone(); // Defensive copy to prevent cache corruption

                            if (_options.EnableBackgroundRefresh)
                            {
                                _refreshSchedule.TryAdd(kvp.Key, DateTime.UtcNow.Add(_options.RefreshInterval));
                            }
                        }

                        EvictIfNecessary();

                        _logger.LogInformation("Loaded {Count} entities from database in {ElapsedMs}ms",
                            loaded.Count, stopwatch.ElapsedMilliseconds);
                    }
                    finally
                    {
                        _loadSemaphore.Release();
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting multiple entities from cache");
                throw;
            }
        }

        /// <summary>
        /// Warms the cache with frequently accessed data
        /// </summary>
        public async Task WarmCacheAsync(Expression<Func<TEntity, bool>>? filter = null, CancellationToken cancellationToken = default)
        {
            if (!_options.EnableCacheWarming)
            {
                _logger.LogWarning("Cache warming is disabled");
                return;
            }

            Stopwatch stopwatch = Stopwatch.StartNew();

            try
            {
                _logger.LogInformation("Starting cache warming for {EntityType}", typeof(TEntity).Name);

                // Note: In a real implementation, you would need a way to query all entities
                // This is a simplified version that assumes the bulk loader can handle null/empty keys
                Dictionary<string, TEntity> entities = await _bulkLoader(Enumerable.Empty<string>());

                int warmedCount = 0;
                foreach (KeyValuePair<string, TEntity> kvp in entities)
                {
                    if (_cache.Count >= _options.MaxCacheSize)
                    {
                        break;
                    }

                    CacheEntry<TEntity> entry = new CacheEntry<TEntity>
                    {
                        Value = kvp.Value,
                        CreatedAt = DateTime.UtcNow,
                        ExpiresAt = DateTime.UtcNow.Add(_options.DefaultTtl),
                        LastAccessedAt = DateTime.UtcNow,
                        AccessCount = 0,
                        SizeBytes = EstimateSize(kvp.Value)
                    };

                    if (_cache.TryAdd(kvp.Key, entry))
                    {
                        warmedCount++;
                    }
                }

                _logger.LogInformation("Cache warming completed: {Count} entities in {ElapsedMs}ms",
                    warmedCount, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cache warming");
                throw;
            }
        }

        /// <summary>
        /// Refreshes cached data in the background
        /// </summary>
        public async Task RefreshAsync(string key, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Key cannot be null or whitespace", nameof(key));
            }

            try
            {
                TEntity? entity = await _loader(key);

                if (entity != null)
                {
                    if (_cache.TryGetValue(key, out CacheEntry<TEntity>? existingEntry))
                    {
                        // Update existing entry
                        existingEntry.Value = entity;
                        existingEntry.ExpiresAt = DateTime.UtcNow.Add(_options.DefaultTtl);
                        existingEntry.SizeBytes = EstimateSize(entity);

                        _logger.LogDebug("Refreshed cache entry for key: {Key}", key);
                    }
                    else
                    {
                        // Add new entry
                        CacheEntry<TEntity> newEntry = new CacheEntry<TEntity>
                        {
                            Value = entity,
                            CreatedAt = DateTime.UtcNow,
                            ExpiresAt = DateTime.UtcNow.Add(_options.DefaultTtl),
                            LastAccessedAt = DateTime.UtcNow,
                            AccessCount = 0,
                            SizeBytes = EstimateSize(entity)
                        };

                        _cache.TryAdd(key, newEntry);
                    }

                    // Update refresh schedule
                    _refreshSchedule.AddOrUpdate(key, DateTime.UtcNow.Add(_options.RefreshInterval), (k, v) => DateTime.UtcNow.Add(_options.RefreshInterval));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing cache entry for key: {Key}", key);
            }
        }

        /// <summary>
        /// Background refresh callback
        /// </summary>
        private void BackgroundRefreshCallback(object? state)
        {
            try
            {
                DateTime now = DateTime.UtcNow;
                List<string> keysToRefresh = [];

                foreach (KeyValuePair<string, DateTime> kvp in _refreshSchedule)
                {
                    if (kvp.Value <= now)
                    {
                        keysToRefresh.Add(kvp.Key);
                    }
                }

                if (keysToRefresh.Count > 0)
                {
                    _logger.LogDebug("Background refresh: {Count} keys scheduled for refresh", keysToRefresh.Count);

                    foreach (string key in keysToRefresh)
                    {
                        Task.Run(async () => await RefreshAsync(key));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in background refresh callback");
            }
        }

        /// <summary>
        /// Evicts entries if cache size exceeds maximum
        /// </summary>
        private void EvictIfNecessary()
        {
            if (_cache.Count <= _options.MaxCacheSize)
            {
                return;
            }

            try
            {
                // Evict least recently used entries
                List<KeyValuePair<string, CacheEntry<TEntity>>> entries = _cache.OrderBy(x => x.Value.LastAccessedAt).Take(_cache.Count - _options.MaxCacheSize).ToList();

                foreach (KeyValuePair<string, CacheEntry<TEntity>> kvp in entries)
                {
                    _cache.TryRemove(kvp.Key, out CacheEntry<TEntity>? _);
                    _refreshSchedule.TryRemove(kvp.Key, out DateTime _);
                }

                _logger.LogDebug("Evicted {Count} cache entries", entries.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cache eviction");
            }
        }

        /// <summary>
        /// Estimates the size of an entity in bytes
        /// </summary>
        private long EstimateSize(TEntity entity)
        {
            try
            {
                string json = JsonSerializer.Serialize(entity);
                return System.Text.Encoding.UTF8.GetByteCount(json);
            }
            catch
            {
                return 1024; // Default estimate
            }
        }
    }

    // ============================================================================
    // WRITE-THROUGH CACHE IMPLEMENTATION
    // ============================================================================

    /// <summary>
    /// Implements write-through cache pattern with synchronous cache and database writes
    /// </summary>
    public class WriteThroughCache<TEntity> : IWriteThroughCache<TEntity> where TEntity : class, IEntityCloneable<TEntity>
    {
        private readonly ConcurrentDictionary<string, CacheEntry<TEntity>> _cache;
        private readonly ILogger<WriteThroughCache<TEntity>> _logger;
        private readonly WriteThroughCacheOptions _options;
        private readonly Func<string, TEntity, Task<bool>> _writer;
        private readonly Func<string, Task<bool>> _deleter;
        private readonly IDatabase? _redisCache;

        /// <summary>
        /// Initializes a new instance of the WriteThroughCache class
        /// </summary>
        public WriteThroughCache(
            Func<string, TEntity, Task<bool>> writer,
            Func<string, Task<bool>> deleter,
            WriteThroughCacheOptions options,
            ILogger<WriteThroughCache<TEntity>> logger,
            IDatabase? redisCache = null)
        {
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
            _deleter = deleter ?? throw new ArgumentNullException(nameof(deleter));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _redisCache = redisCache;

            _cache = new ConcurrentDictionary<string, CacheEntry<TEntity>>();

            _logger.LogInformation("WriteThroughCache initialized for {EntityType} with transactions: {EnableTransactions}",
                typeof(TEntity).Name, _options.EnableTransactions);
        }

        /// <summary>
        /// Writes to both cache and database synchronously
        /// </summary>
        public async Task<bool> WriteAsync(string key, TEntity entity, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Key cannot be null or whitespace", nameof(key));
            }

            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            Stopwatch stopwatch = Stopwatch.StartNew();
            int retryCount = 0;

            while (retryCount <= _options.RetryCount)
            {
                try
                {
                    // Write to database first
                    bool dbSuccess = await _writer(key, entity);

                    if (!dbSuccess)
                    {
                        _logger.LogWarning("Database write failed for key: {Key}", key);

                        if (retryCount < _options.RetryCount)
                        {
                            retryCount++;
                            await Task.Delay(_options.RetryDelay, cancellationToken);
                            continue;
                        }

                        return false;
                    }

                    // Write to local cache (clone to prevent caller mutations from corrupting cache)
                    CacheEntry<TEntity> entry = new CacheEntry<TEntity>
                    {
                        Value = entity.ShallowClone(), // Defensive copy to prevent cache corruption
                        CreatedAt = DateTime.UtcNow,
                        ExpiresAt = null, // No expiration for write-through
                        LastAccessedAt = DateTime.UtcNow,
                        AccessCount = 0,
                        SizeBytes = EstimateSize(entity)
                    };

                    _cache.AddOrUpdate(key, entry, (k, v) => entry);

                    // Write to distributed cache if available
                    if (_redisCache != null)
                    {
                        try
                        {
                            string json = JsonSerializer.Serialize(entity);
                            await _redisCache.StringSetAsync(key, json);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to write to distributed cache for key: {Key}", key);

                            if (_options.RollbackOnCacheFailure)
                            {
                                // Rollback database write
                                await _deleter(key);
                                _cache.TryRemove(key, out CacheEntry<TEntity>? _);
                                return false;
                            }
                        }
                    }

                    _logger.LogInformation("Write-through completed for key: {Key} in {ElapsedMs}ms",
                        key, stopwatch.ElapsedMilliseconds);

                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in write-through for key: {Key}, retry: {Retry}",
                        key, retryCount);

                    if (retryCount < _options.RetryCount)
                    {
                        retryCount++;
                        await Task.Delay(_options.RetryDelay, cancellationToken);
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Writes multiple entities to both cache and database
        /// </summary>
        public async Task<bool> WriteManyAsync(Dictionary<string, TEntity> entities, CancellationToken cancellationToken = default)
        {
            if (entities == null || entities.Count == 0)
            {
                throw new ArgumentException("Entities cannot be null or empty", nameof(entities));
            }

            Stopwatch stopwatch = Stopwatch.StartNew();
            List<string> successfulKeys = [];

            try
            {
                foreach (KeyValuePair<string, TEntity> kvp in entities)
                {
                    bool success = await WriteAsync(kvp.Key, kvp.Value, cancellationToken);

                    if (success)
                    {
                        successfulKeys.Add(kvp.Key);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to write entity for key: {Key}", kvp.Key);

                        if (_options.EnableTransactions)
                        {
                            // Rollback all successful writes
                            foreach (string successfulKey in successfulKeys)
                            {
                                await DeleteAsync(successfulKey, cancellationToken);
                            }

                            return false;
                        }
                    }
                }

                _logger.LogInformation("Write-through many completed: {SuccessCount}/{TotalCount} in {ElapsedMs}ms",
                    successfulKeys.Count, entities.Count, stopwatch.ElapsedMilliseconds);

                return successfulKeys.Count == entities.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in write-through many operation");

                if (_options.EnableTransactions)
                {
                    // Rollback successful writes
                    foreach (string successfulKey in successfulKeys)
                    {
                        try
                        {
                            await DeleteAsync(successfulKey, cancellationToken);
                        }
                        catch (Exception rollbackEx)
                        {
                            _logger.LogError(rollbackEx, "Error rolling back write for key: {Key}", successfulKey);
                        }
                    }
                }

                throw;
            }
        }

        /// <summary>
        /// Deletes from both cache and database
        /// </summary>
        public async Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Key cannot be null or whitespace", nameof(key));
            }

            Stopwatch stopwatch = Stopwatch.StartNew();

            try
            {
                // Delete from database first
                bool dbSuccess = await _deleter(key);

                if (!dbSuccess)
                {
                    _logger.LogWarning("Database delete failed for key: {Key}", key);
                    return false;
                }

                // Remove from local cache
                _cache.TryRemove(key, out CacheEntry<TEntity>? _);

                // Remove from distributed cache if available
                if (_redisCache != null)
                {
                    try
                    {
                        await _redisCache.KeyDeleteAsync(key);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete from distributed cache for key: {Key}", key);
                    }
                }

                _logger.LogInformation("Delete completed for key: {Key} in {ElapsedMs}ms",
                    key, stopwatch.ElapsedMilliseconds);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting key: {Key}", key);
                throw;
            }
        }

        /// <summary>
        /// Updates an entity in both cache and database with transaction support
        /// </summary>
        public async Task<bool> UpdateAsync(string key, TEntity entity, CancellationToken cancellationToken = default)
        {
            // Update is the same as write for write-through pattern
            return await WriteAsync(key, entity, cancellationToken);
        }

        /// <summary>
        /// Estimates the size of an entity in bytes
        /// </summary>
        private long EstimateSize(TEntity entity)
        {
            try
            {
                string json = JsonSerializer.Serialize(entity);
                return System.Text.Encoding.UTF8.GetByteCount(json);
            }
            catch
            {
                return 1024; // Default estimate
            }
        }
    }

    // ============================================================================
    // CACHE-ASIDE PATTERN IMPLEMENTATION
    // ============================================================================

    /// <summary>
    /// Implements cache-aside pattern with manual cache management
    /// </summary>
    public class CacheAsidePattern<TEntity> : ICacheAsidePattern<TEntity> where TEntity : class, IEntityCloneable<TEntity>
    {
        private readonly ConcurrentDictionary<string, CacheEntry<TEntity>> _cache;
        private readonly ILogger<CacheAsidePattern<TEntity>> _logger;
        private readonly CacheAsideOptions _options;
        private readonly IDatabase? _redisCache;
        private long _hits;
        private long _misses;
        private long _evictions;
        private readonly ConcurrentQueue<double> _accessTimes;

        /// <summary>
        /// Initializes a new instance of the CacheAsidePattern class
        /// </summary>
        public CacheAsidePattern(
            CacheAsideOptions options,
            ILogger<CacheAsidePattern<TEntity>> logger,
            IDatabase? redisCache = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _redisCache = redisCache;

            _cache = new ConcurrentDictionary<string, CacheEntry<TEntity>>();
            _accessTimes = new ConcurrentQueue<double>();

            _logger.LogInformation("CacheAsidePattern initialized for {EntityType} with eviction policy: {EvictionPolicy}",
                typeof(TEntity).Name, _options.EvictionPolicy);
        }

        /// <summary>
        /// Gets an entity from cache, returns null if not cached
        /// </summary>
        public async Task<TEntity?> GetFromCacheAsync(string key, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Key cannot be null or whitespace", nameof(key));
            }

            Stopwatch stopwatch = Stopwatch.StartNew();

            try
            {
                // Check local cache first
                if (_cache.TryGetValue(key, out CacheEntry<TEntity>? entry))
                {
                    // Check expiration
                    if (entry.ExpiresAt.HasValue && entry.ExpiresAt.Value < DateTime.UtcNow)
                    {
                        _cache.TryRemove(key, out CacheEntry<TEntity>? _);
                        Interlocked.Increment(ref _misses);
                        _logger.LogDebug("Cache entry expired for key: {Key}", key);
                        return null;
                    }

                    // Update access metadata
                    entry.LastAccessedAt = DateTime.UtcNow;
                    entry.AccessCount++;

                    if (_options.UseSlidingExpiration && entry.ExpiresAt.HasValue)
                    {
                        entry.ExpiresAt = DateTime.UtcNow.Add(_options.DefaultTtl);
                    }

                    Interlocked.Increment(ref _hits);
                    RecordAccessTime(stopwatch.Elapsed.TotalMilliseconds);

                    _logger.LogDebug("Cache HIT for key: {Key} in {ElapsedMs}ms", key, stopwatch.ElapsedMilliseconds);
                    return entry.Value.ShallowClone(); // Defensive copy to prevent cache corruption
                }

                // Check distributed cache if available
                if (_redisCache != null)
                {
                    try
                    {
                        RedisValue value = await _redisCache.StringGetAsync(key);

                        if (!value.IsNullOrEmpty)
                        {
                            TEntity? entity = JsonSerializer.Deserialize<TEntity>(value.ToString());

                            if (entity != null)
                            {
                                // Add to local cache
                                CacheEntry<TEntity> newEntry = new CacheEntry<TEntity>
                                {
                                    Value = entity.ShallowClone(), // Store clone in local cache
                                    CreatedAt = DateTime.UtcNow,
                                    ExpiresAt = DateTime.UtcNow.Add(_options.DefaultTtl),
                                    LastAccessedAt = DateTime.UtcNow,
                                    AccessCount = 1,
                                    SizeBytes = EstimateSize(entity)
                                };

                                _cache.TryAdd(key, newEntry);
                                EvictIfNecessary();

                                Interlocked.Increment(ref _hits);
                                RecordAccessTime(stopwatch.Elapsed.TotalMilliseconds);

                                _logger.LogDebug("Distributed cache HIT for key: {Key}", key);
                                return entity.ShallowClone(); // Defensive copy to prevent cache corruption
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error accessing distributed cache for key: {Key}", key);
                    }
                }

                Interlocked.Increment(ref _misses);
                RecordAccessTime(stopwatch.Elapsed.TotalMilliseconds);

                _logger.LogDebug("Cache MISS for key: {Key}", key);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting from cache for key: {Key}", key);
                throw;
            }
        }

        /// <summary>
        /// Sets an entity in cache with optional TTL
        /// </summary>
        public async Task SetInCacheAsync(string key, TEntity entity, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Key cannot be null or whitespace", nameof(key));
            }

            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            try
            {
                TimeSpan effectiveTtl = ttl ?? _options.DefaultTtl;

                CacheEntry<TEntity> entry = new CacheEntry<TEntity>
                {
                    Value = entity.ShallowClone(), // Clone to prevent caller mutations from corrupting cache
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.Add(effectiveTtl),
                    LastAccessedAt = DateTime.UtcNow,
                    AccessCount = 0,
                    SizeBytes = EstimateSize(entity)
                };

                _cache.AddOrUpdate(key, entry, (k, v) => entry);
                EvictIfNecessary();

                // Set in distributed cache if available
                if (_redisCache != null)
                {
                    try
                    {
                        string json = JsonSerializer.Serialize(entity);
                        await _redisCache.StringSetAsync(key, json, effectiveTtl);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to set in distributed cache for key: {Key}", key);
                    }
                }

                _logger.LogDebug("Set in cache: {Key} with TTL: {TTL}", key, effectiveTtl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting in cache for key: {Key}", key);
                throw;
            }
        }

        /// <summary>
        /// Invalidates cache for a specific key
        /// </summary>
        public async Task InvalidateAsync(string key, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Key cannot be null or whitespace", nameof(key));
            }

            try
            {
                _cache.TryRemove(key, out CacheEntry<TEntity>? _);

                if (_redisCache != null)
                {
                    try
                    {
                        await _redisCache.KeyDeleteAsync(key);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to invalidate distributed cache for key: {Key}", key);
                    }
                }

                _logger.LogDebug("Invalidated cache for key: {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating cache for key: {Key}", key);
                throw;
            }
        }

        /// <summary>
        /// Invalidates cache based on a pattern
        /// </summary>
        public async Task InvalidatePatternAsync(string pattern, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                throw new ArgumentException("Pattern cannot be null or whitespace", nameof(pattern));
            }

            try
            {
                List<string> keysToRemove = [];

                foreach (string key in _cache.Keys)
                {
                    if (MatchesPattern(key, pattern))
                    {
                        keysToRemove.Add(key);
                    }
                }

                foreach (string key in keysToRemove)
                {
                    _cache.TryRemove(key, out CacheEntry<TEntity>? _);
                }

                // Invalidate in distributed cache if available
                if (_redisCache != null)
                {
                    try
                    {
                        // Note: Pattern-based deletion in Redis requires scanning keys
                        // This is a simplified version
                        foreach (string key in keysToRemove)
                        {
                            await _redisCache.KeyDeleteAsync(key);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to invalidate distributed cache for pattern: {Pattern}", pattern);
                    }
                }

                _logger.LogInformation("Invalidated {Count} cache entries matching pattern: {Pattern}",
                    keysToRemove.Count, pattern);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating cache for pattern: {Pattern}", pattern);
                throw;
            }
        }

        /// <summary>
        /// Gets cache statistics
        /// </summary>
        public CacheStatistics GetStatistics()
        {
            long totalSize = 0;

            foreach (CacheEntry<TEntity> entry in _cache.Values)
            {
                totalSize += entry.SizeBytes;
            }

            double avgAccessTime = 0;
            if (_accessTimes.Count > 0)
            {
                avgAccessTime = _accessTimes.Average();
            }

            return new CacheStatistics
            {
                Hits = _hits,
                Misses = _misses,
                ItemCount = _cache.Count,
                Evictions = _evictions,
                AverageAccessTimeMs = avgAccessTime,
                TotalSizeBytes = totalSize
            };
        }

        /// <summary>
        /// Evicts entries if cache size exceeds maximum
        /// </summary>
        private void EvictIfNecessary()
        {
            if (_cache.Count <= _options.MaxCacheSize)
            {
                return;
            }

            try
            {
                List<KeyValuePair<string, CacheEntry<TEntity>>> entries;

                switch (_options.EvictionPolicy)
                {
                    case EvictionPolicy.LRU:
                        entries = _cache.OrderBy(x => x.Value.LastAccessedAt).Take(_cache.Count - _options.MaxCacheSize).ToList();
                        break;

                    case EvictionPolicy.LFU:
                        entries = _cache.OrderBy(x => x.Value.AccessCount).Take(_cache.Count - _options.MaxCacheSize).ToList();
                        break;

                    case EvictionPolicy.FIFO:
                        entries = _cache.OrderBy(x => x.Value.CreatedAt).Take(_cache.Count - _options.MaxCacheSize).ToList();
                        break;

                    default:
                        entries = _cache.OrderBy(x => x.Value.LastAccessedAt).Take(_cache.Count - _options.MaxCacheSize).ToList();
                        break;
                }

                foreach (KeyValuePair<string, CacheEntry<TEntity>> kvp in entries)
                {
                    _cache.TryRemove(kvp.Key, out CacheEntry<TEntity>? _);
                    Interlocked.Increment(ref _evictions);
                }

                _logger.LogDebug("Evicted {Count} cache entries using {Policy} policy", entries.Count, _options.EvictionPolicy);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cache eviction");
            }
        }

        /// <summary>
        /// Checks if a key matches a wildcard pattern
        /// </summary>
        private bool MatchesPattern(string key, string pattern)
        {
            // Simple wildcard matching (* and ?)
            string regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
            return System.Text.RegularExpressions.Regex.IsMatch(key, regexPattern, System.Text.RegularExpressions.RegexOptions.Compiled);
        }

        /// <summary>
        /// Records access time for statistics
        /// </summary>
        private void RecordAccessTime(double milliseconds)
        {
            if (!_options.EnableStatistics)
            {
                return;
            }

            _accessTimes.Enqueue(milliseconds);

            // Keep only last 1000 access times
            while (_accessTimes.Count > 1000)
            {
                _accessTimes.TryDequeue(out double _);
            }
        }

        /// <summary>
        /// Estimates the size of an entity in bytes
        /// </summary>
        private long EstimateSize(TEntity entity)
        {
            try
            {
                string json = JsonSerializer.Serialize(entity);
                return System.Text.Encoding.UTF8.GetByteCount(json);
            }
            catch
            {
                return 1024; // Default estimate
            }
        }
    }

    // ============================================================================
    // DISTRIBUTED CACHE COORDINATOR IMPLEMENTATION
    // ============================================================================

    /// <summary>
    /// Coordinates distributed caching operations across multiple instances
    /// </summary>
    public class DistributedCacheCoordinator : IDistributedCacheCoordinator, IDisposable
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly IDatabase _redisDb;
        private readonly ISubscriber _subscriber;
        private readonly ILogger<DistributedCacheCoordinator> _logger;
        private readonly DistributedCacheOptions _options;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _stampedeLocks;
        private readonly ConcurrentQueue<PendingWrite<object>> _writeBuffer;
        private readonly Timer _flushTimer;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the DistributedCacheCoordinator class
        /// </summary>
        public DistributedCacheCoordinator(
            DistributedCacheOptions options,
            ILogger<DistributedCacheCoordinator> logger)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _redis = ConnectionMultiplexer.Connect(_options.RedisConnectionString);
            _redisDb = _redis.GetDatabase();
            _subscriber = _redis.GetSubscriber();

            _stampedeLocks = new ConcurrentDictionary<string, SemaphoreSlim>();
            _writeBuffer = new ConcurrentQueue<PendingWrite<object>>();

            _flushTimer = new Timer(FlushTimerCallback, null, _options.FlushInterval, _options.FlushInterval);

            if (_options.EnableInvalidationBroadcast)
            {
                _subscriber.Subscribe(_options.InvalidationChannel, OnInvalidationMessage);
            }

            _logger.LogInformation("DistributedCacheCoordinator initialized with Redis: {RedisConnection}",
                _options.RedisConnectionString);
        }

        /// <summary>
        /// Acquires a distributed lock
        /// </summary>
        public async Task<IDistributedLock?> AcquireLockAsync(string key, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Key cannot be null or whitespace", nameof(key));
            }

            Stopwatch stopwatch = Stopwatch.StartNew();
            string lockKey = $"lock:{key}";
            string token = Guid.NewGuid().ToString();

            try
            {
                while (stopwatch.Elapsed < timeout)
                {
                    bool acquired = await _redisDb.StringSetAsync(lockKey, token, timeout, When.NotExists);

                    if (acquired)
                    {
                        _logger.LogDebug("Acquired distributed lock for key: {Key} in {ElapsedMs}ms",
                            key, stopwatch.ElapsedMilliseconds);

                        return new RedisDistributedLock(_redisDb, lockKey, token, _logger);
                    }

                    // Wait a bit before retrying
                    await Task.Delay(50, cancellationToken);
                }

                _logger.LogWarning("Failed to acquire distributed lock for key: {Key} within timeout: {Timeout}",
                    key, timeout);

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error acquiring distributed lock for key: {Key}", key);
                throw;
            }
        }

        /// <summary>
        /// Prevents cache stampede by coordinating concurrent requests
        /// </summary>
        public async Task<TResult> PreventStampedeAsync<TResult>(string key, Func<Task<TResult>> loader, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Key cannot be null or whitespace", nameof(key));
            }

            if (loader == null)
            {
                throw new ArgumentNullException(nameof(loader));
            }

            if (!_options.EnableStampedePrevention)
            {
                return await loader();
            }

            try
            {
                // Get or create a semaphore for this key
                SemaphoreSlim semaphore = _stampedeLocks.GetOrAdd(key, k => new SemaphoreSlim(1, 1));

                // Try to acquire the semaphore
                bool acquired = await semaphore.WaitAsync(timeout, cancellationToken);

                if (!acquired)
                {
                    _logger.LogWarning("Stampede prevention timeout for key: {Key}", key);
                    throw new TimeoutException($"Failed to acquire stampede lock for key: {key}");
                }

                try
                {
                    // Check if value is in cache (another thread might have loaded it)
                    string? cachedValue = await _redisDb.StringGetAsync(key);

                    if (!string.IsNullOrEmpty(cachedValue))
                    {
                        TResult? result = JsonSerializer.Deserialize<TResult>(cachedValue);

                        if (result != null)
                        {
                            _logger.LogDebug("Stampede prevention - cache hit after wait for key: {Key}", key);
                            return result;
                        }
                    }

                    // Load the value
                    _logger.LogDebug("Stampede prevention - loading value for key: {Key}", key);
                    TResult loadedValue = await loader();

                    // Cache the loaded value
                    string json = JsonSerializer.Serialize(loadedValue);
                    await _redisDb.StringSetAsync(key, json, TimeSpan.FromMinutes(15));

                    return loadedValue;
                }
                finally
                {
                    semaphore.Release();

                    // Clean up old semaphores
                    if (_stampedeLocks.Count > 1000)
                    {
                        List<string> keysToRemove = _stampedeLocks.Keys.Take(100).ToList();
                        foreach (string k in keysToRemove)
                        {
                            if (_stampedeLocks.TryRemove(k, out SemaphoreSlim? oldSem))
                            {
                                oldSem.Dispose();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in stampede prevention for key: {Key}", key);
                throw;
            }
        }

        /// <summary>
        /// Writes to cache asynchronously (write-behind)
        /// </summary>
        public async Task EnqueueWriteAsync<TEntity>(string key, TEntity entity, CancellationToken cancellationToken = default) where TEntity : class
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Key cannot be null or whitespace", nameof(key));
            }

            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            try
            {
                PendingWrite<object> pendingWrite = new PendingWrite<object>
                {
                    Key = key,
                    Entity = entity,
                    QueuedAt = DateTime.UtcNow,
                    RetryCount = 0
                };

                _writeBuffer.Enqueue(pendingWrite);

                // Immediately write to cache (write-behind for DB, write-through for cache)
                string json = JsonSerializer.Serialize(entity);
                await _redisDb.StringSetAsync(key, json);

                _logger.LogDebug("Enqueued write-behind operation for key: {Key}, buffer size: {BufferSize}",
                    key, _writeBuffer.Count);

                // Flush if buffer is full
                if (_writeBuffer.Count >= _options.WriteBufferSize)
                {
                    await FlushPendingWritesAsync(cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enqueueing write-behind operation for key: {Key}", key);
                throw;
            }
        }

        /// <summary>
        /// Flushes pending writes to database
        /// </summary>
        public async Task FlushPendingWritesAsync(CancellationToken cancellationToken = default)
        {
            if (_writeBuffer.IsEmpty)
            {
                return;
            }

            Stopwatch stopwatch = Stopwatch.StartNew();
            List<PendingWrite<object>> batch = [];

            try
            {
                // Dequeue all pending writes
                while (_writeBuffer.TryDequeue(out PendingWrite<object>? write))
                {
                    batch.Add(write);

                    if (batch.Count >= _options.WriteBufferSize)
                    {
                        break;
                    }
                }

                if (batch.Count == 0)
                {
                    return;
                }

                _logger.LogInformation("Flushing {Count} pending writes to database", batch.Count);

                // Process each write
                // Note: In a real implementation, you would batch these to the actual database
                foreach (PendingWrite<object> write in batch)
                {
                    try
                    {
                        // Simulate database write
                        await Task.Delay(1, cancellationToken);

                        _logger.LogDebug("Flushed write for key: {Key}", write.Key);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error flushing write for key: {Key}", write.Key);

                        // Retry logic
                        if (write.RetryCount < 3)
                        {
                            write.RetryCount++;
                            _writeBuffer.Enqueue(write);
                        }
                        else
                        {
                            _logger.LogError("Failed to flush write for key: {Key} after 3 retries", write.Key);
                        }
                    }
                }

                _logger.LogInformation("Flushed {Count} writes in {ElapsedMs}ms",
                    batch.Count, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error flushing pending writes");

                // Re-enqueue failed writes
                foreach (PendingWrite<object> write in batch)
                {
                    _writeBuffer.Enqueue(write);
                }

                throw;
            }
        }

        /// <summary>
        /// Coordinates cache invalidation across instances
        /// </summary>
        public async Task BroadcastInvalidationAsync(string key, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Key cannot be null or whitespace", nameof(key));
            }

            if (!_options.EnableInvalidationBroadcast)
            {
                return;
            }

            try
            {
                await _subscriber.PublishAsync(_options.InvalidationChannel, key);
                _logger.LogDebug("Broadcast cache invalidation for key: {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting cache invalidation for key: {Key}", key);
                throw;
            }
        }

        /// <summary>
        /// Flush timer callback
        /// </summary>
        private void FlushTimerCallback(object? state)
        {
            try
            {
                Task.Run(async () => await FlushPendingWritesAsync());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in flush timer callback");
            }
        }

        /// <summary>
        /// Handles invalidation messages from other instances
        /// </summary>
        private void OnInvalidationMessage(RedisChannel channel, RedisValue message)
        {
            try
            {
                string key = message.ToString();
                _logger.LogDebug("Received cache invalidation broadcast for key: {Key}", key);

                // Note: In a real implementation, you would invalidate local caches here
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling invalidation message");
            }
        }

        /// <summary>
        /// Disposes resources
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                _flushTimer?.Dispose();
                FlushPendingWritesAsync().GetAwaiter().GetResult();

                foreach (SemaphoreSlim semaphore in _stampedeLocks.Values)
                {
                    semaphore?.Dispose();
                }

                _redis?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing DistributedCacheCoordinator");
            }

            _disposed = true;
        }
    }

    // ============================================================================
    // ADVANCED CACHING MANAGER (FACADE)
    // ============================================================================

    /// <summary>
    /// Facade that provides unified access to all caching patterns
    /// </summary>
    public class AdvancedCachingManager<TEntity> : IDisposable where TEntity : class, IEntityCloneable<TEntity>
    {
        private readonly ReadThroughCache<TEntity> _readThroughCache;
        private readonly WriteThroughCache<TEntity> _writeThroughCache;
        private readonly CacheAsidePattern<TEntity> _cacheAsidePattern;
        private readonly DistributedCacheCoordinator _distributedCoordinator;
        private readonly ILogger<AdvancedCachingManager<TEntity>> _logger;
        private bool _disposed;

        /// <summary>
        /// Gets the read-through cache instance
        /// </summary>
        public IReadThroughCache<TEntity> ReadThrough => _readThroughCache;

        /// <summary>
        /// Gets the write-through cache instance
        /// </summary>
        public IWriteThroughCache<TEntity> WriteThrough => _writeThroughCache;

        /// <summary>
        /// Gets the cache-aside pattern instance
        /// </summary>
        public ICacheAsidePattern<TEntity> CacheAside => _cacheAsidePattern;

        /// <summary>
        /// Gets the distributed cache coordinator instance
        /// </summary>
        public IDistributedCacheCoordinator Distributed => _distributedCoordinator;

        /// <summary>
        /// Initializes a new instance of the AdvancedCachingManager class
        /// </summary>
        public AdvancedCachingManager(
            Func<string, Task<TEntity?>> loader,
            Func<IEnumerable<string>, Task<Dictionary<string, TEntity>>> bulkLoader,
            Func<string, TEntity, Task<bool>> writer,
            Func<string, Task<bool>> deleter,
            ReadThroughCacheOptions readThroughOptions,
            WriteThroughCacheOptions writeThroughOptions,
            CacheAsideOptions cacheAsideOptions,
            DistributedCacheOptions distributedOptions,
            ILogger<AdvancedCachingManager<TEntity>> logger,
            ILogger<ReadThroughCache<TEntity>> readThroughLogger,
            ILogger<WriteThroughCache<TEntity>> writeThroughLogger,
            ILogger<CacheAsidePattern<TEntity>> cacheAsideLogger,
            ILogger<DistributedCacheCoordinator> distributedLogger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Initialize distributed coordinator first
            _distributedCoordinator = new DistributedCacheCoordinator(distributedOptions, distributedLogger);

            // Get Redis database for other components
            IConnectionMultiplexer redis = ConnectionMultiplexer.Connect(distributedOptions.RedisConnectionString);
            IDatabase redisDb = redis.GetDatabase();

            // Initialize caching components
            _readThroughCache = new ReadThroughCache<TEntity>(loader, bulkLoader, readThroughOptions, readThroughLogger);
            _writeThroughCache = new WriteThroughCache<TEntity>(writer, deleter, writeThroughOptions, writeThroughLogger, redisDb);
            _cacheAsidePattern = new CacheAsidePattern<TEntity>(cacheAsideOptions, cacheAsideLogger, redisDb);

            _logger.LogInformation("AdvancedCachingManager initialized for {EntityType}", typeof(TEntity).Name);
        }

        /// <summary>
        /// Disposes resources
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                _distributedCoordinator?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing AdvancedCachingManager");
            }

            _disposed = true;
        }
    }
}
