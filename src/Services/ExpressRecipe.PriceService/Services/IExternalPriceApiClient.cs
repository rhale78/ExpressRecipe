namespace ExpressRecipe.PriceService.Services;

/// <summary>
/// Common contract for all optional external price API clients.
/// Implementations must read their own Enabled flag from config and return empty
/// lists immediately when disabled — no HTTP calls made.
/// </summary>
public interface IExternalPriceApiClient
{
    /// <summary>Whether this client is enabled by configuration.</summary>
    bool IsEnabled { get; }

    /// <summary>Fetch current prices for the given UPC/barcode.</summary>
    Task<List<ExternalPriceResult>> GetPricesAsync(string upc, CancellationToken ct);

    /// <summary>Search for prices by product name and optional zip code.</summary>
    Task<List<ExternalPriceResult>> SearchByNameAsync(string name, string? zipCode, CancellationToken ct);
}

public sealed class ExternalPriceResult
{
    public string ProductName { get; init; } = string.Empty;
    public string? Upc { get; init; }
    public string? StoreName { get; init; }
    public string? StoreChain { get; init; }
    public decimal Price { get; init; }
    public decimal? RegularPrice { get; init; }
    public string? Unit { get; init; }
    public string DataSource { get; init; } = string.Empty;
    public string? ExternalId { get; init; }
    public DateTimeOffset ObservedAt { get; init; }
}
