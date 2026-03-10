using ExpressRecipe.Data.Common;
using ExpressRecipe.Shared.Services;
using ExpressRecipe.Shared.Units;

namespace ExpressRecipe.ProductService.Services;

/// <summary>
/// Resolves ingredient density (g/ml) from the IngredientUnitDensity table.
/// Results are cached via HybridCache with a 60-minute TTL.
/// Match priority: IngredientId exact → IngredientName exact → IngredientName StartsWith.
/// </summary>
public sealed class IngredientDensityResolver : SqlHelper, IIngredientDensityResolver
{
    private readonly HybridCacheService _cache;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(60);

    public IngredientDensityResolver(
        string connectionString,
        HybridCacheService cache) : base(connectionString)
    {
        _cache = cache;
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
        // Match on IngredientName (case-insensitive collation handles lower), preferring
        // rows with no preparation note (the "plain" density), then verified rows first.
        // Avoids LOWER() so the IX_IngredientDensity_Name index remains sargable.
        const string sql = @"
            SELECT TOP 1 GramsPerMl
            FROM IngredientUnitDensity
            WHERE IngredientName = @ExactName
               OR IngredientName LIKE @StartsWithName
            ORDER BY
                CASE WHEN IngredientName = @ExactName THEN 0 ELSE 1 END,
                CASE WHEN PreparationNote IS NULL THEN 0 ELSE 1 END,
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
