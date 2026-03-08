using ExpressRecipe.Shared.Services;
using System.Text.Json;

namespace ExpressRecipe.GroceryStoreLocationService.Services;

/// <summary>
/// Normalizes raw store name variants to a canonical chain name.
/// Alias mappings are loaded from StoreChain table via HybridCache (key: "storechain:all", TTL 24h).
/// Matching is case-insensitive; aliases are stored as a JSON array in StoreChain.Aliases.
/// Uses IServiceScopeFactory to avoid captive-dependency issues (singleton using scoped repo).
/// </summary>
public class StoreChainNormalizer : IStoreChainNormalizer
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly HybridCacheService? _cache;
    private readonly ILogger<StoreChainNormalizer> _logger;

    private const string CacheKey = "storechain:all";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);

    private Dictionary<string, string>? _aliasMap;
    private readonly SemaphoreSlim _loadLock = new(1, 1);

    public StoreChainNormalizer(
        IServiceScopeFactory scopeFactory,
        ILogger<StoreChainNormalizer> logger,
        HybridCacheService? cache = null)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _cache = cache;
    }

    public string? Normalize(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName)) return null;

        if (_aliasMap == null)
        {
            // Map not yet loaded; return null (caller should await EnsureLoadedAsync first)
            return null;
        }

        var key = rawName.Trim().ToUpperInvariant();
        return _aliasMap.TryGetValue(key, out var canonical) ? canonical : null;
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        await _loadLock.WaitAsync(cancellationToken);
        try
        {
            _aliasMap = await BuildAliasMapAsync(cancellationToken);
            _logger.LogDebug("StoreChainNormalizer refreshed: {Count} alias entries loaded", _aliasMap.Count);
        }
        finally
        {
            _loadLock.Release();
        }
    }

    private async Task<Dictionary<string, string>> BuildAliasMapAsync(CancellationToken cancellationToken)
    {
        List<Data.StoreChainDto> chains;

        if (_cache != null)
        {
            chains = await _cache.GetOrSetAsync(
                CacheKey,
                async (ct) => await FetchChainsAsync(ct),
                expiration: CacheTtl,
                cancellationToken: cancellationToken) ?? new List<Data.StoreChainDto>();
        }
        else
        {
            chains = await FetchChainsAsync(cancellationToken);
        }

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var chain in chains)
        {
            var canonical = chain.CanonicalName;
            map[canonical.ToUpperInvariant()] = canonical;

            if (!string.IsNullOrWhiteSpace(chain.Aliases))
            {
                try
                {
                    var aliases = System.Text.Json.JsonSerializer.Deserialize<string[]>(chain.Aliases);
                    if (aliases != null)
                    {
                        foreach (var alias in aliases)
                        {
                            if (!string.IsNullOrWhiteSpace(alias))
                            {
                                map[alias.ToUpperInvariant()] = canonical;
                            }
                        }
                    }
                }
                catch (System.Text.Json.JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse aliases JSON for chain {Chain}", canonical);
                }
            }
        }

        return map;
    }

    private async Task<List<Data.StoreChainDto>> FetchChainsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<Data.IGroceryStoreRepository>();
        return await repo.GetAllChainsAsync();
    }

    /// <summary>
    /// Ensures the alias map is warmed. Call this from a hosted service on startup.
    /// </summary>
    public async Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
    {
        if (_aliasMap != null) return;
        await RefreshAsync(cancellationToken);
    }
}
