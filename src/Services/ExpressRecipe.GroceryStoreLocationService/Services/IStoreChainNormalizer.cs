namespace ExpressRecipe.GroceryStoreLocationService.Services;

/// <summary>
/// Normalizes raw store name variants to a canonical chain name.
/// Loads alias mappings from the StoreChain table via HybridCache (TTL 24h).
/// </summary>
public interface IStoreChainNormalizer
{
    /// <summary>
    /// Returns the canonical chain name for the given raw store name,
    /// or null if no matching chain is found.
    /// </summary>
    string? Normalize(string rawName);

    /// <summary>
    /// Refreshes the alias cache from the database.
    /// </summary>
    Task RefreshAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures the alias map is loaded. Should be called on application startup.
    /// </summary>
    Task EnsureLoadedAsync(CancellationToken cancellationToken = default);
}
