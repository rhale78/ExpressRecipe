using Microsoft.Extensions.Caching.Hybrid;
using System.Net.Http.Json;

namespace ExpressRecipe.UserService.Services;

public interface IIngredientFetchService
{
    /// <summary>
    /// Returns normalized lowercase ingredient names for a given product.
    /// Returns an empty list if the product is not found or has no ingredient data.
    /// </summary>
    Task<List<string>> GetNormalizedIngredientsAsync(Guid productId, CancellationToken ct = default);

    /// <summary>
    /// Returns the set of ingredient names from the household's non-reaction product history
    /// (last 180 days, cross-referenced with InventoryService).
    /// </summary>
    Task<HashSet<string>> GetSafeIngredientSetAsync(
        Guid householdId, Guid? memberId, int minUsageCount, CancellationToken ct = default);
}

public sealed class IngredientFetchService : IIngredientFetchService
{
    private readonly IHttpClientFactory _http;
    private readonly HybridCache _cache;
    private readonly ILogger<IngredientFetchService> _logger;

    public IngredientFetchService(
        IHttpClientFactory http,
        HybridCache cache,
        ILogger<IngredientFetchService> logger)
    {
        _http   = http;
        _cache  = cache;
        _logger = logger;
    }

    public async Task<List<string>> GetNormalizedIngredientsAsync(
        Guid productId, CancellationToken ct = default)
    {
        return await _cache.GetOrCreateAsync(
            $"prod-ing:{productId}",
            async innerCt =>
            {
                HttpClient client = _http.CreateClient("ProductService");
                try
                {
                    List<string>? ingredients = await client
                        .GetFromJsonAsync<List<string>>(
                            $"/api/products/{productId}/ingredients/normalized",
                            innerCt);
                    return ingredients ?? new List<string>();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Could not fetch ingredients for product {ProductId}", productId);
                    return new List<string>();
                }
            },
            new HybridCacheEntryOptions { Expiration = TimeSpan.FromHours(12) },
            cancellationToken: ct);
    }

    public async Task<HashSet<string>> GetSafeIngredientSetAsync(
        Guid householdId, Guid? memberId, int minUsageCount, CancellationToken ct = default)
    {
        string cacheKey = $"safe-ing:{householdId}:{memberId?.ToString() ?? "primary"}:min-{minUsageCount}";

        return await _cache.GetOrCreateAsync(
            cacheKey,
            async innerCt =>
            {
                HttpClient client = _http.CreateClient("InventoryService");
                try
                {
                    List<SafeProductUsage>? usages = await client
                        .GetFromJsonAsync<List<SafeProductUsage>>(
                            $"/api/inventory/safe-product-history/{householdId}?minCount={minUsageCount}",
                            innerCt);

                    if (usages is null)
                    {
                        return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    }

                    // Parallelize per-product ingredient fetches to avoid sequential N+1 latency.
                    IEnumerable<Task<List<string>>> tasks = usages
                        .Select(u => GetNormalizedIngredientsAsync(u.ProductId, innerCt));
                    List<string>[] results = await Task.WhenAll(tasks);

                    HashSet<string> safeSet = new(StringComparer.OrdinalIgnoreCase);
                    foreach (List<string> ingredients in results)
                    {
                        foreach (string ingredient in ingredients) { safeSet.Add(ingredient); }
                    }

                    return safeSet;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Could not fetch safe ingredient history for {HouseholdId}", householdId);
                    return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                }
            },
            new HybridCacheEntryOptions { Expiration = TimeSpan.FromMinutes(30) },
            cancellationToken: ct);
    }
}

public sealed record SafeProductUsage
{
    public Guid ProductId  { get; init; }
    public int  UsageCount { get; init; }
}
