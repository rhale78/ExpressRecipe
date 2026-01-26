using StackExchange.Redis;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ExpressRecipe.Shared.Services
{
    /// <summary>
    /// Redis caching service wrapper
    /// </summary>
    public class CacheService
    {
        private readonly IConnectionMultiplexer? _redis;
        private readonly ILogger<CacheService> _logger;
        private readonly IDatabase? _db;

        public CacheService(IConnectionMultiplexer redis, ILogger<CacheService> logger)
        {
            _redis = redis;
            _logger = logger;
            _db = redis?.GetDatabase();
        }

        public async Task<T?> GetAsync<T>(string key)
        {
            if (_db == null)
            {
                return default;
            }

            try
            {
                RedisValue value = await _db.StringGetAsync(key);
                return value.IsNullOrEmpty ? default : JsonSerializer.Deserialize<T>((string)value!);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cached value for key {Key}", key);
                return default;
            }
        }

        public async Task<bool> SetAsync<T>(string key, T value, TimeSpan? expiry = null)
        {
            if (_db == null)
            {
                return false;
            }

            try
            {
                var serialized = JsonSerializer.Serialize(value);
                return expiry.HasValue ? await _db.StringSetAsync(key, serialized, expiry.Value) : await _db.StringSetAsync(key, serialized);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting cached value for key {Key}", key);
                return false;
            }
        }

        public async Task<bool> DeleteAsync(string key)
        {
            if (_db == null)
            {
                return false;
            }

            try
            {
                return await _db.KeyDeleteAsync(key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting cached key {Key}", key);
                return false;
            }
        }

        public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null)
        {
            T? cached = await GetAsync<T>(key);
            if (cached != null)
            {
                return cached;
            }

            T? value = await factory();
            await SetAsync(key, value, expiry);
            return value;
        }
    }
}
