using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ExpressRecipe.Shared.Services
{
    /// <summary>
    /// Hybrid caching service that uses both in-memory cache (L1) and distributed cache/Redis (L2)
    /// for optimal performance and scalability
    /// </summary>
    public class HybridCacheService
    {
        private readonly IMemoryCache _memoryCache;
        private readonly IDistributedCache _distributedCache;
        private readonly ILogger<HybridCacheService> _logger;

        // Default cache durations
        private static readonly TimeSpan DefaultMemoryCacheDuration = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan DefaultDistributedCacheDuration = TimeSpan.FromHours(1);

        public HybridCacheService(
            IMemoryCache memoryCache,
            IDistributedCache distributedCache,
            ILogger<HybridCacheService> logger)
        {
            _memoryCache = memoryCache;
            _distributedCache = distributedCache;
            _logger = logger;
        }

        /// <summary>
        /// Gets a value from cache (checks memory first, then distributed)
        /// </summary>
        public async Task<T?> GetAsync<T>(string key)
        {
            // L1: Check memory cache first (fastest)
            if (_memoryCache.TryGetValue(key, out T? memoryValue))
            {
                _logger.LogDebug("Cache hit (memory) for key: {Key}", key);
                return memoryValue;
            }

            // L2: Check distributed cache (Redis)
            try
            {
                var distributedValue = await _distributedCache.GetStringAsync(key);
                if (!string.IsNullOrEmpty(distributedValue))
                {
                    _logger.LogDebug("Cache hit (distributed) for key: {Key}", key);
                    T? value = JsonSerializer.Deserialize<T>(distributedValue);

                    // Populate L1 cache
                    if (value != null)
                    {
                        _memoryCache.Set(key, value, DefaultMemoryCacheDuration);
                    }

                    return value;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error reading from distributed cache for key: {Key}", key);
            }

            _logger.LogDebug("Cache miss for key: {Key}", key);
            return default;
        }

        /// <summary>
        /// Sets a value in both memory and distributed cache
        /// </summary>
        public async Task SetAsync<T>(string key, T value, TimeSpan? memoryExpiry = null, TimeSpan? distributedExpiry = null)
        {
            try
            {
                // Set in memory cache (L1)
                _memoryCache.Set(key, value, memoryExpiry ?? DefaultMemoryCacheDuration);

                // Set in distributed cache (L2)
                var serialized = JsonSerializer.Serialize(value);
                DistributedCacheEntryOptions options = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = distributedExpiry ?? DefaultDistributedCacheDuration
                };

                await _distributedCache.SetStringAsync(key, serialized, options);
                _logger.LogDebug("Cached value for key: {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting cache for key: {Key}", key);
            }
        }

        /// <summary>
        /// Gets a value from cache or executes the factory and caches the result
        /// </summary>
        public async Task<T> GetOrSetAsync<T>(
            string key,
            Func<Task<T>> factory,
            TimeSpan? memoryExpiry = null,
            TimeSpan? distributedExpiry = null)
        {
            T? cached = await GetAsync<T>(key);
            if (cached != null && !EqualityComparer<T>.Default.Equals(cached, default))
            {
                return cached;
            }

            T? value = await factory();
            if (value != null && !EqualityComparer<T>.Default.Equals(value, default))
            {
                await SetAsync(key, value, memoryExpiry, distributedExpiry);
            }

            return value;
        }

        /// <summary>
        /// Removes a value from both memory and distributed cache
        /// </summary>
        public async Task RemoveAsync(string key)
        {
            try
            {
                _memoryCache.Remove(key);
                await _distributedCache.RemoveAsync(key);
                _logger.LogDebug("Removed cache for key: {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cache for key: {Key}", key);
            }
        }

        /// <summary>
        /// Removes multiple keys matching a pattern (prefix)
        /// </summary>
        public async Task RemoveByPrefixAsync(string prefix)
        {
            try
            {
                // Note: Memory cache doesn't support prefix removal easily
                // For distributed cache, this would require Redis-specific commands
                _logger.LogWarning("RemoveByPrefix not fully implemented for prefix: {Prefix}", prefix);
                // TODO: Implement Redis SCAN for pattern-based deletion if needed
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cache by prefix: {Prefix}", prefix);
            }
        }
    }

    /// <summary>
    /// Cache key constants for consistent key naming
    /// </summary>
    public static class CacheKeys
    {
        // Products
        public const string ProductById = "product:id:{0}";
        public const string ProductByBarcode = "product:barcode:{0}";
        public const string ProductSearch = "product:search:{0}";
        public const string ProductLetterCounts = "product:lettercounts:{0}";

        // Ingredients
        public const string IngredientById = "ingredient:id:{0}";
        public const string IngredientByName = "ingredient:name:{0}";
        public const string IngredientsByNames = "ingredients:names:{0}";
        public const string BaseIngredientByName = "baseingredient:name:{0}";

        // Recipes
        public const string RecipeById = "recipe:id:{0}";
        public const string RecipeSearch = "recipe:search:{0}";

        // User data
        public const string UserProfile = "user:profile:{0}";

        // Helper methods
        public static string FormatKey(string template, params object[] args)
        {
            return string.Format(template, args);
        }
    }
}
