# Caching Quick Reference Guide

## Cache Durations by Operation

| Operation | Memory (L1) | Redis (L2) | Use Case |
|-----------|-------------|------------|----------|
| Ingredient by ID | 30 min | 2 hr | Moderate stability |
| Ingredient by Name | 12 hr | 24 hr | Very stable mapping |
| Product by ID | 15 min | 1 hr | Moderate updates |
| Product by Barcode | 15 min | 2 hr | Scanner usage |
| Product Search | 5 min | 15 min | Freshness priority |

## Cache Key Patterns

```csharp
// Ingredients
ingredient:id:{guid}              // GetByIdAsync
ingredient:name:{lowercase}       // GetIngredientIdsByNamesAsync

// Products  
product:id:{guid}                 // GetByIdAsync
product:barcode:{barcode}         // GetByBarcodeAsync
product:ingredients:{guid}        // Product's ingredients
product:allergens:{guid}          // Product's allergens

// Search (composite key)
product:search:{category}:{brand}:{letter}:{approved}:{page}:{size}:{sort}
```

## Expected Performance Gains

### Bulk Import (100K products)
- **Before**: 1,000,000 ingredient lookups
- **After**: ~1,500 lookups (99.85% cache hit rate)
- **Improvement**: 1000x faster

### Barcode Scanner
- **Before**: 10ms per scan (DB query)
- **After**: 1ms per scan (cache hit)
- **Improvement**: 10x faster

### Product Search (pagination)
- **Before**: Every page = DB query
- **After**: Cached pages = instant
- **Improvement**: 5-10x faster

## Cache Invalidation Rules

### On Product Create
```csharp
await InvalidateSearchCachesAsync();
```

### On Product Update
```csharp
await _cache.RemoveAsync($"product:id:{id}");
await _cache.RemoveAsync($"product:ingredients:{id}");
await _cache.RemoveAsync($"product:allergens:{id}");
await _cache.RemoveAsync($"product:barcode:{barcode}");
await InvalidateSearchCachesAsync();
```

### On Product Delete
```csharp
// Get product first to find barcode
var product = await GetByIdAsync(id);

// Delete from DB
await DeleteAsync(id);

// Invalidate all caches
await _cache.RemoveAsync($"product:id:{id}");
await _cache.RemoveAsync($"product:ingredients:{id}");
await _cache.RemoveAsync($"product:allergens:{id}");
if (product?.Barcode != null)
    await _cache.RemoveAsync($"product:barcode:{product.Barcode}");
await InvalidateSearchCachesAsync();
```

### On Ingredient Create
```csharp
await _cache.RemoveAsync($"ingredient:name:{name.ToLowerInvariant()}");
```

### On Ingredient Update
```csharp
await _cache.RemoveAsync($"ingredient:id:{id}");
await _cache.RemoveAsync($"ingredient:name:{name.ToLowerInvariant()}");
```

## Smart Cache-First Lookup Pattern

**Best for**: Bulk operations with repeated lookups (ingredient name?ID mapping)

```csharp
// 1. Check cache for each item
var uncachedItems = new List<string>();
foreach (var item in items)
{
    var cachedValue = await _cache.GetAsync<T>(cacheKey);
    if (cachedValue != null)
        results[item] = cachedValue; // Cache hit!
    else
        uncachedItems.Add(item); // Cache miss
}

// 2. Log stats
_logger.LogDebug("Cache: {Hits} hits, {Misses} misses", 
    results.Count, uncachedItems.Count);

// 3. Query DB only for uncached items
var dbResults = await QueryDatabase(uncachedItems);

// 4. Cache each result individually for future lookups
foreach (var result in dbResults)
{
    await _cache.SetAsync(cacheKey, result.Value, 
        memoryExpiry: TimeSpan.FromHours(12),
        distributedExpiry: TimeSpan.FromHours(24));
    results[result.Key] = result.Value;
}
```

## When to Cache vs. When to Skip

### ? Cache These
- **Ingredient lookups**: Very stable data, high reuse
- **Product by ID/Barcode**: Moderate updates, frequent reads
- **Simple searches**: Category, brand, letter filters
- **Static data**: Categories, brands (if separate tables)

### ? Skip Cache for These
- **Complex searches**: Dietary restrictions (combinatorial explosion)
- **Search terms**: Too many variations, low hit rate
- **One-time queries**: Admin reports, analytics
- **Real-time data**: Live inventory, pricing

## Monitoring Cache Health

### Check Hit Rate in Logs
```
2024-01-15 10:23:45 [DEBUG] Ingredient cache: 485 hits, 15 misses (97% hit rate)
```

### Redis Statistics
```bash
redis-cli
> INFO stats          # Get cache stats
> KEYS product:*      # List product cache keys
> KEYS ingredient:*   # List ingredient cache keys
> TTL product:id:{id} # Check TTL for specific key
```

### Expected Hit Rates
- **Ingredients**: >95% after first batch
- **Products**: >80% for scanner usage
- **Search**: >50% for common filters

## Troubleshooting

### Problem: Low cache hit rate
**Check**: Cache key generation (must be deterministic)
```csharp
// ? Good: Deterministic
var key = $"product:id:{productId}";

// ? Bad: Includes timestamp
var key = $"product:id:{productId}:{DateTime.Now}";
```

### Problem: Stale data returned
**Check**: Cache invalidation on writes
```csharp
// ? Always invalidate after DB write
await UpdateDatabase(id, data);
await _cache.RemoveAsync($"product:id:{id}");

// ? Missing invalidation = stale data
await UpdateDatabase(id, data);
// Forgot to invalidate cache!
```

### Problem: High memory usage
**Check**: TTLs and cache key count
```bash
# Count cache keys
redis-cli
> DBSIZE

# Check memory usage
> INFO memory
```

## Adding Cache to New Repository

### Step 1: Add dependencies
```csharp
public class MyRepository : SqlHelper
{
    private readonly HybridCacheService? _cache;
    private readonly ILogger<MyRepository>? _logger;
    
    public MyRepository(
        string connectionString,
        HybridCacheService? cache = null,
        ILogger<MyRepository>? logger = null) : base(connectionString)
    {
        _cache = cache;
        _logger = logger;
    }
}
```

### Step 2: Wrap read operations
```csharp
public async Task<MyDto?> GetByIdAsync(Guid id)
{
    if (_cache != null)
    {
        var cacheKey = CacheKeys.FormatKey("myentity:id:{0}", id);
        return await _cache.GetOrSetAsync(
            cacheKey,
            async () => await GetByIdFromDbAsync(id),
            memoryExpiry: TimeSpan.FromMinutes(15),
            distributedExpiry: TimeSpan.FromHours(1));
    }
    
    return await GetByIdFromDbAsync(id);
}

private async Task<MyDto?> GetByIdFromDbAsync(Guid id)
{
    // Actual DB query here
}
```

### Step 3: Add invalidation
```csharp
public async Task<bool> UpdateAsync(Guid id, UpdateRequest request)
{
    var success = await UpdateInDatabase(id, request);
    
    if (success && _cache != null)
    {
        await _cache.RemoveAsync(CacheKeys.FormatKey("myentity:id:{0}", id));
    }
    
    return success;
}
```

### Step 4: Update DI registration
```csharp
builder.Services.AddScoped<IMyRepository>(sp => 
{
    var cache = sp.GetRequiredService<HybridCacheService>();
    var logger = sp.GetRequiredService<ILogger<MyRepository>>();
    return new MyRepository(connectionString, cache, logger);
});
```

## Best Practices Summary

1. ? **Cache-aside pattern**: Check cache first, populate on miss
2. ? **Individual item caching**: Cache each item separately for high reuse
3. ? **Appropriate TTLs**: Balance freshness vs. performance
4. ? **Comprehensive invalidation**: Remove all related caches on writes
5. ? **Logging**: Track hit/miss rates for validation
6. ? **Backward compatibility**: Nullable cache parameters
7. ? **Smart caching**: Skip caching for low-hit-rate queries
8. ? **Deterministic keys**: Same input = same cache key

## Files to Reference

- **HybridCacheService**: `src/ExpressRecipe.Shared/Services/HybridCacheService.cs`
- **CacheKeys**: `src/ExpressRecipe.Shared/Services/CacheKeys.cs`
- **Example Implementation**: `src/Services/ExpressRecipe.ProductService/Data/IngredientRepository.cs`
- **Full Documentation**: `CACHING_PERFORMANCE_OPTIMIZATION_COMPLETE.md`
