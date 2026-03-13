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
    /// Gets a value from cache if it exists; returns <c>default(T)</c> on a miss.
    /// <para>
    /// .NET 9 <see cref="HybridCache"/> does not expose a standalone "get-without-create" API,
    /// so this method calls <see cref="HybridCache.GetOrCreateAsync{T}"/> with a factory that
    /// returns <c>default(T)</c> and a 1-second TTL so that a cache miss never poisons the cache
    /// for longer than one second. For write-through scenarios prefer
    /// <see cref="GetOrSetAsync{T}"/> which inlines the data-source call in the factory.
    /// </para>
    /// </summary>
    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        // Short TTL so that a cache-miss null never blocks the real value for a full hour.
        return await _cache.GetOrCreateAsync<T>(
            key,
            ct => ValueTask.FromResult(default(T)!),
            new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromSeconds(1),
                LocalCacheExpiration = TimeSpan.FromSeconds(1)
            },
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

/// <summary>
/// Cache key constants for consistent key naming across services
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

    // Cookbooks
    public const string CookbookById = "cookbook:id:{0}";
    public const string CookbookBySlug = "cookbook:slug:{0}";
    public const string CookbookList = "cookbook:list:{0}";

    // User data
    public const string UserProfile = "user:profile:{0}";

    // Helper methods
    public static string FormatKey(string template, params object[] args)
    {
        return string.Format(template, args);
    }
}