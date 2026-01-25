using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace HighSpeedDAL.Core.Caching
{
    /// <summary>
    /// Query result cache with smart invalidation and LRU eviction.
    /// 
    /// Features:
    /// - LRU (Least Recently Used) eviction policy
    /// - Table-based invalidation tags
    /// - Configurable TTL per cache entry
    /// - SHA256 secure cache keys
    /// - Hit/miss statistics
    /// - Thread-safe operations
    /// - Automatic cleanup of expired entries
    /// 
    /// Thread-safe for concurrent operations.
    /// 
    /// Example usage:
    /// QueryResultCache cache = new QueryResultCache(logger, maxSize: 10000);
    /// 
    /// // Cache a query result
    /// await cache.SetAsync("Products", query, result, TimeSpan.FromMinutes(5), "Products");
    /// 
    /// // Retrieve from cache
    /// var cachedResult = await cache.GetAsync<List<Product>>("Products", query);
    /// 
    /// // Invalidate when table changes
    /// await cache.InvalidateByTableAsync("Products");
    /// 
    /// HighSpeedDAL Framework v0.1 - Phase 4
    /// </summary>
    public sealed class QueryResultCache : IDisposable
    {
        private readonly ILogger<QueryResultCache> _logger;
        private readonly int _maxSize;
        private readonly ConcurrentDictionary<string, CacheEntry> _cache;
        private readonly ConcurrentDictionary<string, HashSet<string>> _tableToKeys;
        private readonly LinkedList<string> _lruList;
        private readonly object _lruLock = new object();
        private readonly Timer _cleanupTimer;
        private long _hits;
        private long _misses;
        private bool _disposed;

        /// <summary>
        /// Default TTL for cache entries.
        /// </summary>
        public TimeSpan DefaultTtl { get; set; } = TimeSpan.FromMinutes(5);

        public QueryResultCache(
            ILogger<QueryResultCache> logger,
            int maxSize = 10000,
            int cleanupIntervalSeconds = 60)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _maxSize = maxSize > 0 ? maxSize : throw new ArgumentException("Max size must be positive", nameof(maxSize));
        
            _cache = new ConcurrentDictionary<string, CacheEntry>();
            _tableToKeys = new ConcurrentDictionary<string, HashSet<string>>();
            _lruList = new LinkedList<string>();

            // Start cleanup timer
            TimeSpan cleanupInterval = TimeSpan.FromSeconds(cleanupIntervalSeconds);
            _cleanupTimer = new Timer(
                state => CleanupExpiredEntries(),
                null,
                cleanupInterval,
                cleanupInterval);

            _logger.LogInformation(
                "Query Result Cache initialized with max size: {MaxSize}, cleanup interval: {Interval}s",
                maxSize, cleanupIntervalSeconds);
        }

        /// <summary>
        /// Gets a cached query result.
        /// </summary>
        /// <typeparam name="TResult">Type of the cached result</typeparam>
        /// <param name="tableName">Table name for cache key</param>
        /// <param name="query">Query string or identifier</param>
        /// <returns>Cached result or null if not found/expired</returns>
        public async Task<TResult?> GetAsync<TResult>(string tableName, string query)
            where TResult : class
        {
            if (string.IsNullOrWhiteSpace(tableName))
            {
                throw new ArgumentException("Table name cannot be null or empty", nameof(tableName));
            }
            if (string.IsNullOrWhiteSpace(query))
            {
                throw new ArgumentException("Query cannot be null or empty", nameof(query));
            }

            string cacheKey = GenerateCacheKey(tableName, query);

            if (_cache.TryGetValue(cacheKey, out CacheEntry? entry))
            {
                if (entry.ExpiresAt > DateTime.UtcNow)
                {
                    // Update LRU
                    UpdateLru(cacheKey);

                    Interlocked.Increment(ref _hits);

                    _logger.LogDebug(
                        "Cache HIT for table: {Table}, key: {Key}",
                        tableName, cacheKey.Substring(0, Math.Min(16, cacheKey.Length)));

                    return await Task.FromResult(entry.Value as TResult);
                }
                else
                {
                    // Expired - remove it
                    _cache.TryRemove(cacheKey, out _);
                    RemoveFromLru(cacheKey);
                }
            }

            Interlocked.Increment(ref _misses);

            _logger.LogDebug(
                "Cache MISS for table: {Table}, key: {Key}",
                tableName, cacheKey.Substring(0, Math.Min(16, cacheKey.Length)));

            return null;
        }

        /// <summary>
        /// Caches a query result.
        /// </summary>
        /// <typeparam name="TResult">Type of the result to cache</typeparam>
        /// <param name="tableName">Table name for invalidation</param>
        /// <param name="query">Query string or identifier</param>
        /// <param name="result">Result to cache</param>
        /// <param name="ttl">Time to live (optional, uses DefaultTtl if null)</param>
        /// <param name="additionalTables">Additional tables for invalidation tracking</param>
        public async Task SetAsync<TResult>(
            string tableName,
            string query,
            TResult result,
            TimeSpan? ttl = null,
            params string[] additionalTables)
            where TResult : class
        {
            if (string.IsNullOrWhiteSpace(tableName))
            {
                throw new ArgumentException("Table name cannot be null or empty", nameof(tableName));
            }
            if (string.IsNullOrWhiteSpace(query))
            {
                throw new ArgumentException("Query cannot be null or empty", nameof(query));
            }
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            string cacheKey = GenerateCacheKey(tableName, query);
            TimeSpan effectiveTtl = ttl ?? DefaultTtl;

            CacheEntry entry = new CacheEntry
            {
                Key = cacheKey,
                Value = result,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.Add(effectiveTtl),
                Tables = [tableName]
            };

            if (additionalTables != null && additionalTables.Length > 0)
            {
                foreach (string table in additionalTables)
                {
                    if (!string.IsNullOrWhiteSpace(table))
                    {
                        entry.Tables.Add(table);
                    }
                }
            }

            // Check if we need to evict
            if (_cache.Count >= _maxSize)
            {
                EvictLru();
            }

            // Add to cache
            _cache[cacheKey] = entry;

            // Track table associations
            foreach (string table in entry.Tables)
            {
                HashSet<string> keys = _tableToKeys.GetOrAdd(table, []);
                lock (keys)
                {
                    keys.Add(cacheKey);
                }
            }

            // Update LRU
            AddToLru(cacheKey);

            _logger.LogDebug(
                "Cached result for table: {Table}, key: {Key}, TTL: {Ttl}s",
                tableName, cacheKey.Substring(0, Math.Min(16, cacheKey.Length)), effectiveTtl.TotalSeconds);

            await Task.CompletedTask;
        }

        /// <summary>
        /// Invalidates all cache entries associated with a table.
        /// </summary>
        /// <param name="tableName">Table name to invalidate</param>
        public async Task InvalidateByTableAsync(string tableName)
        {
            if (string.IsNullOrWhiteSpace(tableName))
            {
                throw new ArgumentException("Table name cannot be null or empty", nameof(tableName));
            }

            if (_tableToKeys.TryGetValue(tableName, out HashSet<string>? keys))
            {
                int removedCount = 0;

                List<string> keysToRemove;
                lock (keys)
                {
                    keysToRemove = keys.ToList();
                }

                foreach (string key in keysToRemove)
                {
                    if (_cache.TryRemove(key, out _))
                    {
                        RemoveFromLru(key);
                        removedCount++;
                    }
                }

                lock (keys)
                {
                    keys.Clear();
                }

                _logger.LogInformation(
                    "Invalidated {Count} cache entries for table: {Table}",
                    removedCount, tableName);
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Clears all cache entries.
        /// </summary>
        public async Task ClearAsync()
        {
            int count = _cache.Count;

            _cache.Clear();
            _tableToKeys.Clear();

            lock (_lruLock)
            {
                _lruList.Clear();
            }

            Interlocked.Exchange(ref _hits, 0);
            Interlocked.Exchange(ref _misses, 0);

            _logger.LogInformation("Cleared {Count} cache entries", count);

            await Task.CompletedTask;
        }

        /// <summary>
        /// Gets cache statistics.
        /// </summary>
        /// <returns>Cache statistics</returns>
        public CacheStatistics GetStatistics()
        {
            long totalHits = Interlocked.Read(ref _hits);
            long totalMisses = Interlocked.Read(ref _misses);
            long totalRequests = totalHits + totalMisses;

            double hitRate = totalRequests > 0
                ? (double)totalHits / totalRequests * 100
                : 0;

            return new CacheStatistics
            {
                TotalEntries = _cache.Count,
                MaxSize = _maxSize,
                Hits = totalHits,
                Misses = totalMisses,
                HitRate = hitRate,
                TablesTracked = _tableToKeys.Count
            };
        }

        #region Private Methods

        private string GenerateCacheKey(string tableName, string query)
        {
            string input = $"{tableName}:{query}";
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return Convert.ToBase64String(hash);
        }

        private void UpdateLru(string key)
        {
            lock (_lruLock)
            {
                LinkedListNode<string>? node = _lruList.Find(key);
                if (node != null)
                {
                    _lruList.Remove(node);
                    _lruList.AddFirst(node);
                }
            }
        }

        private void AddToLru(string key)
        {
            lock (_lruLock)
            {
                _lruList.AddFirst(key);
            }
        }

        private void RemoveFromLru(string key)
        {
            lock (_lruLock)
            {
                _lruList.Remove(key);
            }
        }

        private void EvictLru()
        {
            string? keyToEvict = null;

            lock (_lruLock)
            {
                if (_lruList.Last != null)
                {
                    keyToEvict = _lruList.Last.Value;
                    _lruList.RemoveLast();
                }
            }

            if (keyToEvict != null)
            {
                if (_cache.TryRemove(keyToEvict, out CacheEntry? entry))
                {
                    // Remove from table tracking
                    foreach (string table in entry.Tables)
                    {
                        if (_tableToKeys.TryGetValue(table, out HashSet<string>? keys))
                        {
                            lock (keys)
                            {
                                keys.Remove(keyToEvict);
                            }
                        }
                    }

                    _logger.LogDebug("Evicted LRU cache entry: {Key}", keyToEvict.Substring(0, Math.Min(16, keyToEvict.Length)));
                }
            }
        }

        private void CleanupExpiredEntries()
        {
            try
            {
                DateTime now = DateTime.UtcNow;
                int removedCount = 0;

                List<string> expiredKeys = _cache
                    .Where(kvp => kvp.Value.ExpiresAt <= now)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (string key in expiredKeys)
                {
                    if (_cache.TryRemove(key, out CacheEntry? entry))
                    {
                        RemoveFromLru(key);

                        foreach (string table in entry.Tables)
                        {
                            if (_tableToKeys.TryGetValue(table, out HashSet<string>? keys))
                            {
                                lock (keys)
                                {
                                    keys.Remove(key);
                                }
                            }
                        }

                        removedCount++;
                    }
                }

                if (removedCount > 0)
                {
                    _logger.LogInformation("Cleanup removed {Count} expired cache entries", removedCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cache cleanup");
            }
        }

        #endregion

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _cleanupTimer?.Dispose();
            _disposed = true;

            _logger.LogInformation("Query Result Cache disposed");
        }
    }

    /// <summary>
    /// Cache entry with metadata.
    /// </summary>
    internal sealed class CacheEntry
    {
        public string Key { get; set; } = string.Empty;
        public object Value { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public HashSet<string> Tables { get; set; } = [];
    }

    /// <summary>
    /// Cache statistics.
    /// </summary>
    public sealed class CacheStatistics
    {
        public int TotalEntries { get; set; }
        public int MaxSize { get; set; }
        public long Hits { get; set; }
        public long Misses { get; set; }
        public double HitRate { get; set; }
        public int TablesTracked { get; set; }
    }
}
