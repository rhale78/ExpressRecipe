using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace ExpressRecipe.Shared.Units;

/// <summary>
/// Resolves ingredient density by calling the ProductService catalog endpoint.
/// Used by non-Product services (Recipe, Inventory, Shopping, etc.).
/// </summary>
public sealed class HttpIngredientDensityResolver : IIngredientDensityResolver
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpIngredientDensityResolver> _logger;

    public HttpIngredientDensityResolver(HttpClient httpClient, ILogger<HttpIngredientDensityResolver> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<decimal?> GetDensityAsync(Guid? ingredientId, string? ingredientName, CancellationToken ct = default)
    {
        try
        {
            if (ingredientId.HasValue)
            {
                DensityResponse? response = await _httpClient.GetFromJsonAsync<DensityResponse>(
                    $"api/catalog/density/{ingredientId}", ct);
                if (response?.GramsPerMl is not null) { return response.GramsPerMl; }
            }

            if (!string.IsNullOrWhiteSpace(ingredientName))
            {
                string encoded = Uri.EscapeDataString(ingredientName);
                DensityResponse? response = await _httpClient.GetFromJsonAsync<DensityResponse>(
                    $"api/catalog/density/by-name/{encoded}", ct);
                if (response?.GramsPerMl is not null) { return response.GramsPerMl; }
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to fetch density for ingredient {IngredientId}/{IngredientName}",
                ingredientId, ingredientName);
        }
        catch (TaskCanceledException)
        {
            // Timeout or cancellation — return null to fall back to volumetric
        }

        return null;
    }

    private sealed class DensityResponse
    {
        public decimal? GramsPerMl { get; init; }
    }
}
