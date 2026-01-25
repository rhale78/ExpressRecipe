using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using HighSpeedDAL.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace HighSpeedDAL.Core.Caching
{
    /// <summary>
    /// High-performance in-memory cache using ConcurrentDictionary
    /// </summary>
    public sealed class MemoryCacheManager<TEntity, TKey> : ICacheManager<TEntity, TKey>
        where TEntity : class
        where TKey : notnull
    {
        private readonly ConcurrentDictionary<TKey, CacheEntry<TEntity>> _cache;
        private readonly ILogger _logger;
        private readonly int _maxSize;
        private readonly TimeSpan _expiration;

        public bool IsEnabled { get; set; } = true;

        public MemoryCacheManager(ILogger logger, int maxSize = 0, int expirationSeconds = 0)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _maxSize = maxSize;
            _expiration = expirationSeconds > 0 ? TimeSpan.FromSeconds(expirationSeconds) : TimeSpan.Zero;
            _cache = new ConcurrentDictionary<TKey, CacheEntry<TEntity>>();
        }

        public Task<TEntity?> GetAsync(TKey key, CancellationToken cancellationToken = default)
        {
            if (!IsEnabled)
            {
                return Task.FromResult<TEntity?>(null);
            }

            if (_cache.TryGetValue(key, out CacheEntry<TEntity>? entry))
            {
                if (IsExpired(entry))
                {
                    _cache.TryRemove(key, out _);
                    _logger.LogDebug("Cache entry expired for key: {Key}", key);
                    return Task.FromResult<TEntity?>(null);
                }

                _logger.LogDebug("Cache hit for key: {Key}", key);
                return Task.FromResult<TEntity?>(entry.Value);
            }

                _logger.LogDebug("Cache miss for key: {Key}", key);
                return Task.FromResult<TEntity?>(null);
            }

            public Task SetAsync(TKey key, TEntity entity, CancellationToken cancellationToken = default)
            {
                if (!IsEnabled)
                {
                    return Task.CompletedTask;
                }

                if (entity == null)
                {
                    throw new ArgumentNullException(nameof(entity));
                }

                // Check size limit
                if (_maxSize > 0 && _cache.Count >= _maxSize && !_cache.ContainsKey(key))
                {
                    EvictOldestEntry();
                }

            CacheEntry<TEntity> entry = new CacheEntry<TEntity>
            {
                Value = entity,
                Timestamp = DateTime.UtcNow
            };

            _cache.AddOrUpdate(key, entry, (k, v) => entry);
            _logger.LogDebug("Cache entry added/updated for key: {Key}", key);

            return Task.CompletedTask;
        }

        public Task RemoveAsync(TKey key, CancellationToken cancellationToken = default)
        {
            if (_cache.TryRemove(key, out _))
            {
                _logger.LogDebug("Cache entry removed for key: {Key}", key);
            }

            return Task.CompletedTask;
        }

        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            _cache.Clear();
            _logger.LogInformation("Cache cleared");
            return Task.CompletedTask;
        }

        public Task<bool> ContainsAsync(TKey key, CancellationToken cancellationToken = default)
        {
            bool contains = _cache.ContainsKey(key);
            return Task.FromResult(contains);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsExpired(CacheEntry<TEntity> entry)
        {
            return _expiration == TimeSpan.Zero ? false : DateTime.UtcNow - entry.Timestamp > _expiration;
        }

        private void EvictOldestEntry()
        {
            DateTime oldestTimestamp = DateTime.MaxValue;
            TKey? oldestKey = default;

            foreach (KeyValuePair<TKey, CacheEntry<TEntity>> kvp in _cache)
            {
                if (kvp.Value.Timestamp < oldestTimestamp)
                {
                    oldestTimestamp = kvp.Value.Timestamp;
                    oldestKey = kvp.Key;
                }
            }

            if (oldestKey != null)
            {
                _cache.TryRemove(oldestKey, out _);
                _logger.LogDebug("Evicted oldest cache entry for key: {Key}", oldestKey);
            }
        }
    }

    /// <summary>
    /// Two-layer cache: lock-free Dictionary (L1) + ConcurrentDictionary (L2)
    /// L1 is read-only snapshot for ultra-fast reads
    /// L2 handles writes and periodic promotion to L1
    /// </summary>
    public sealed class TwoLayerCacheManager<TEntity, TKey> : ICacheManager<TEntity, TKey>
        where TEntity : class
        where TKey : notnull
    {
        private readonly ILogger _logger;
        private readonly int _maxSize;
        private readonly TimeSpan _expiration;
        private readonly TimeSpan _promotionInterval;

        // L1: Lock-free read cache (snapshot)
        private volatile Dictionary<TKey, CacheEntry<TEntity>> _l1Cache;

        // L2: Write cache
        private readonly ConcurrentDictionary<TKey, CacheEntry<TEntity>> _l2Cache;

        // Promotion control
        private readonly Timer _promotionTimer;
        private readonly object _promotionLock = new object();

        public bool IsEnabled { get; set; } = true;

        public TwoLayerCacheManager(
            ILogger logger,
            int maxSize = 0,
            int expirationSeconds = 0,
            int promotionIntervalSeconds = 5)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _maxSize = maxSize;
            _expiration = expirationSeconds > 0 ? TimeSpan.FromSeconds(expirationSeconds) : TimeSpan.Zero;
            _promotionInterval = TimeSpan.FromSeconds(promotionIntervalSeconds);

            _l1Cache = [];
            _l2Cache = new ConcurrentDictionary<TKey, CacheEntry<TEntity>>();

            _promotionTimer = new Timer(PromoteL2ToL1, null, _promotionInterval, _promotionInterval);
        }

        public Task<TEntity?> GetAsync(TKey key, CancellationToken cancellationToken = default)
        {
            if (!IsEnabled)
            {
                return Task.FromResult<TEntity?>(null);
            }

            // Try L1 first (lock-free read)
            Dictionary<TKey, CacheEntry<TEntity>> l1 = _l1Cache;
            if (l1.TryGetValue(key, out CacheEntry<TEntity>? entry))
            {
                if (!IsExpired(entry))
                {
                    _logger.LogDebug("L1 cache hit for key: {Key}", key);
                    return Task.FromResult<TEntity?>(entry.Value);
                }
            }

            // Try L2
            if (_l2Cache.TryGetValue(key, out entry))
            {
                if (!IsExpired(entry))
                {
                    _logger.LogDebug("L2 cache hit for key: {Key}", key);
                    return Task.FromResult<TEntity?>(entry.Value);
                }
                else
                {
                    _l2Cache.TryRemove(key, out _);
                }
            }

                _logger.LogDebug("Cache miss for key: {Key}", key);
                return Task.FromResult<TEntity?>(null);
            }

            public Task SetAsync(TKey key, TEntity entity, CancellationToken cancellationToken = default)
            {
                if (!IsEnabled)
                {
                    return Task.CompletedTask;
                }

                if (entity == null)
                {
                    throw new ArgumentNullException(nameof(entity));
                }

                // Check size limit
                if (_maxSize > 0 && _l2Cache.Count >= _maxSize && !_l2Cache.ContainsKey(key))
                {
                    EvictOldestEntry();
                }

            CacheEntry<TEntity> entry = new CacheEntry<TEntity>
            {
                Value = entity,
                Timestamp = DateTime.UtcNow
            };

            _l2Cache.AddOrUpdate(key, entry, (k, v) => entry);
            _logger.LogDebug("Cache entry added to L2 for key: {Key}", key);

            return Task.CompletedTask;
        }

        public Task RemoveAsync(TKey key, CancellationToken cancellationToken = default)
        {
            _l2Cache.TryRemove(key, out _);
            _logger.LogDebug("Cache entry removed from L2 for key: {Key}", key);

            return Task.CompletedTask;
        }

        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            _l2Cache.Clear();
            _l1Cache = [];
            _logger.LogInformation("Cache cleared (both layers)");

            return Task.CompletedTask;
        }

        public Task<bool> ContainsAsync(TKey key, CancellationToken cancellationToken = default)
        {
            bool contains = _l1Cache.ContainsKey(key) || _l2Cache.ContainsKey(key);
            return Task.FromResult(contains);
        }

        private void PromoteL2ToL1(object? state)
        {
            if (!Monitor.TryEnter(_promotionLock))
            {
                return; // Skip if already promoting
            }

            try
            {
                // Create new L1 snapshot from L2
                Dictionary<TKey, CacheEntry<TEntity>> newL1 = [];

                foreach (KeyValuePair<TKey, CacheEntry<TEntity>> kvp in _l2Cache)
                {
                    if (!IsExpired(kvp.Value))
                    {
                        newL1[kvp.Key] = kvp.Value;
                    }
                    else
                    {
                        _l2Cache.TryRemove(kvp.Key, out _);
                    }
                }

                // Atomic swap
                _l1Cache = newL1;

                _logger.LogDebug("Promoted {Count} entries from L2 to L1", newL1.Count);
            }
            finally
            {
                Monitor.Exit(_promotionLock);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsExpired(CacheEntry<TEntity> entry)
        {
            return _expiration == TimeSpan.Zero ? false : DateTime.UtcNow - entry.Timestamp > _expiration;
        }

        private void EvictOldestEntry()
        {
            DateTime oldestTimestamp = DateTime.MaxValue;
            TKey? oldestKey = default;

            foreach (KeyValuePair<TKey, CacheEntry<TEntity>> kvp in _l2Cache)
            {
                if (kvp.Value.Timestamp < oldestTimestamp)
                {
                    oldestTimestamp = kvp.Value.Timestamp;
                    oldestKey = kvp.Key;
                }
            }

            if (oldestKey != null)
            {
                _l2Cache.TryRemove(oldestKey, out _);
                _logger.LogDebug("Evicted oldest cache entry for key: {Key}", oldestKey);
            }
        }
    }

    /// <summary>
    /// Cache entry with timestamp for expiration tracking
    /// </summary>
    internal sealed class CacheEntry<TEntity>
    {
        public TEntity Value { get; set; } = default!;
        public DateTime Timestamp { get; set; }
    }
}
