using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace ExpressRecipe.Shared.Services;

/// <summary>
/// Modern Hybrid caching service using .NET 9+ native HybridCache.
/// Provides unified L1 (In-memory) and L2 (Redis) caching with stampede protection.
/// </summary>
public class HybridCacheService
{
    private readonly HybridCache _cache;
    private readonly ILogger<HybridCacheService> _logger;

    // Default cache durations
    private static readonly TimeSpan DefaultMemoryCacheDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan DefaultDistributedCacheDuration = TimeSpan.FromHours(1);

    public HybridCacheService(
        HybridCache cache,
        ILogger<HybridCacheService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Gets a value from cache or executes the factory and caches the result.
    /// HybridCache automatically handles L1/L2 lookups and stampede protection.
    /// </summary>
    public async Task<T?> GetOrSetAsync<T>(
        string key,
        Func<CancellationToken, ValueTask<T>> factory,
        TimeSpan? expiration = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        var options = new HybridCacheEntryOptions
        {
            Expiration = expiration ?? DefaultDistributedCacheDuration
        };

        return await _cache.GetOrCreateAsync<T>(
            key,
            async (ct) => await factory(ct),
            options,
            tags,
            cancellationToken);
    }

    /// <summary>
    /// Gets a value from cache if it exists.
    /// </summary>
    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        // HybridCache.GetAsync might be missing in some preview versions, 
        // we can use GetOrCreateAsync with a factory that returns default
        return await _cache.GetOrCreateAsync<T>(
            key,
            ct => ValueTask.FromResult(default(T)!),
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Sets a value in the cache explicitly.
    /// </summary>
    public async Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? expiration = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        var options = new HybridCacheEntryOptions
        {
            Expiration = expiration ?? DefaultDistributedCacheDuration
        };

        await _cache.SetAsync<T>(key, value, options, tags, cancellationToken);
    }

    /// <summary>
    /// Removes a value from cache by key.
    /// </summary>
    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        await _cache.RemoveAsync(key, cancellationToken);
    }

    /// <summary>
    /// Removes multiple values from cache using tags (very powerful for bulk invalidation).
    /// </summary>
    public async Task RemoveByTagAsync(string tag, CancellationToken cancellationToken = default)
    {
        await _cache.RemoveByTagAsync(tag, cancellationToken);
    }
}
