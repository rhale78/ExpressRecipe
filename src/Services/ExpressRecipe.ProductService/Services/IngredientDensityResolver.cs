using ExpressRecipe.Data.Common;
using ExpressRecipe.Shared.Services;
using ExpressRecipe.Shared.Units;
using Microsoft.Data.SqlClient;

namespace ExpressRecipe.ProductService.Services;

/// <summary>
/// Resolves ingredient density (g/ml) from the IngredientUnitDensity table.
/// Results are cached via HybridCache with a 60-minute TTL.
/// Match priority: IngredientId exact → IngredientName exact → IngredientName StartsWith.
/// </summary>
public sealed class IngredientDensityResolver : SqlHelper, IIngredientDensityResolver
{
    private readonly HybridCacheService _cache;
    private readonly ILogger<IngredientDensityResolver> _logger;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(60);

    public IngredientDensityResolver(
        string connectionString,
        HybridCacheService cache,
        ILogger<IngredientDensityResolver> logger) : base(connectionString)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<decimal?> GetDensityAsync(
        Guid? ingredientId, string? ingredientName, CancellationToken ct = default)
    {
        // 1. Try by IngredientId
        if (ingredientId.HasValue)
        {
            string key = $"density:id:{ingredientId}";
            decimal? byId = await _cache.GetOrSetAsync<decimal?>(
                key,
                async _ =>
                {
                    return await QueryDensityByIdAsync(ingredientId.Value);
                },
                CacheTtl,
                cancellationToken: ct);

            if (byId.HasValue) { return byId; }
        }

        // 2. Try by IngredientName
        if (!string.IsNullOrWhiteSpace(ingredientName))
        {
            string normalized = ingredientName.Trim().ToLowerInvariant();
            string key = $"density:name:{normalized}";
            decimal? byName = await _cache.GetOrSetAsync<decimal?>(
                key,
                async _ =>
                {
                    return await QueryDensityByNameAsync(normalized);
                },
                CacheTtl,
                cancellationToken: ct);

            if (byName.HasValue) { return byName; }
        }

        return null;
    }

    private async Task<decimal?> QueryDensityByIdAsync(Guid ingredientId)
    {
        const string sql = @"
            SELECT TOP 1 GramsPerMl
            FROM IngredientUnitDensity
            WHERE IngredientId = @IngredientId
              AND PreparationNote IS NULL
            ORDER BY IsVerified DESC, CreatedAt DESC";

        List<decimal> rows = await ExecuteReaderAsync(
            sql,
            reader => GetDecimal(reader, "GramsPerMl"),
            CreateParameter("@IngredientId", ingredientId));

        return rows.Count > 0 ? rows[0] : null;
    }

    private async Task<decimal?> QueryDensityByNameAsync(string normalizedName)
    {
        // Exact match first, then StartsWith
        const string sql = @"
            SELECT TOP 1 GramsPerMl
            FROM IngredientUnitDensity
            WHERE LOWER(IngredientName) = @ExactName
               OR LOWER(IngredientName) LIKE @StartsWithName
            ORDER BY
                CASE WHEN LOWER(IngredientName) = @ExactName THEN 0 ELSE 1 END,
                IsVerified DESC,
                CreatedAt DESC";

        List<decimal> rows = await ExecuteReaderAsync(
            sql,
            reader => GetDecimal(reader, "GramsPerMl"),
            CreateParameter("@ExactName", normalizedName),
            CreateParameter("@StartsWithName", normalizedName + "%"));

        return rows.Count > 0 ? rows[0] : null;
    }
}
