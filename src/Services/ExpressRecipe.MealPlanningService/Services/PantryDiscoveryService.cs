using Microsoft.Extensions.Caching.Hybrid;
using System.Net.Http.Json;

namespace ExpressRecipe.MealPlanningService.Services;

// ── Options ──────────────────────────────────────────────────────────────────

public sealed record PantryDiscoveryOptions
{
    /// <summary>Minimum fraction of recipe ingredients that must be in the pantry (0.40 – 1.00).</summary>
    public decimal MinMatchPercent { get; init; } = 0.80m;
    /// <summary>Sort order: "match" | "rating" | "cookTime" | "added"</summary>
    public string? SortBy { get; init; } = "match";
    public int Limit { get; init; } = 24;
    public bool RespectDietaryRestrictions { get; init; } = true;
}

// ── Result DTOs ───────────────────────────────────────────────────────────────

public sealed record PantryDiscoveryResult
{
    public List<PantryRecipeMatch> Matches { get; init; } = new();
    public int TotalPantryIngredients { get; init; }
    public DateTime CachedAt { get; init; }
}

public sealed record PantryRecipeMatch
{
    public Guid RecipeId { get; init; }
    public string RecipeName { get; init; } = string.Empty;
    public string? ImageUrl { get; init; }
    public int CookTimeMinutes { get; init; }
    public decimal AverageRating { get; init; }
    public decimal MatchPercent { get; init; }
    public int MatchedIngredientCount { get; init; }
    public int TotalIngredientCount { get; init; }
    public List<string> MissingIngredients { get; init; } = new();
    public bool HasDietaryConflict { get; init; }
}

// ── Cross-service HTTP response DTOs ─────────────────────────────────────────

public sealed record PantryIngredientItem
{
    public Guid InventoryItemId { get; init; }
    public string NormalizedName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
}

public sealed record RecipeIngredientSummary
{
    public Guid RecipeId { get; init; }
    public string RecipeName { get; init; } = string.Empty;
    public string? ImageUrl { get; init; }
    public int CookTimeMinutes { get; init; }
    public decimal AverageRating { get; init; }
    public List<IngredientRef> Ingredients { get; init; } = new();
}

public sealed record IngredientRef
{
    public string NormalizedName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
}

// ── Interface ─────────────────────────────────────────────────────────────────

public interface IPantryDiscoveryService
{
    Task<PantryDiscoveryResult> DiscoverAsync(Guid householdId, Guid userId,
        PantryDiscoveryOptions options, CancellationToken ct = default);
}

// ── Implementation ────────────────────────────────────────────────────────────

public sealed class PantryDiscoveryService : IPantryDiscoveryService
{
    private readonly HybridCache _cache;
    private readonly IHttpClientFactory _http;
    private readonly ILogger<PantryDiscoveryService> _logger;

    public PantryDiscoveryService(
        HybridCache cache,
        IHttpClientFactory http,
        ILogger<PantryDiscoveryService> logger)
    {
        _cache  = cache;
        _http   = http;
        _logger = logger;
    }

    public async Task<PantryDiscoveryResult> DiscoverAsync(
        Guid householdId, Guid userId,
        PantryDiscoveryOptions options, CancellationToken ct = default)
    {
        // When RespectDietaryRestrictions is true the result is user-scoped (allergen filtering);
        // include the userId so different users in the same household don't receive each other's filtered results.
        string userScope = options.RespectDietaryRestrictions ? userId.ToString() : "shared";
        string cacheKey = $"pantry-discover:{householdId}:{userScope}:" +
                          $"{options.MinMatchPercent.ToString(System.Globalization.CultureInfo.InvariantCulture)}:" +
                          $"{options.Limit}:{options.SortBy}";

        return await _cache.GetOrCreateAsync(
            cacheKey,
            async innerCt => await ComputeDiscoveryAsync(householdId, userId, options, innerCt),
            new HybridCacheEntryOptions { Expiration = TimeSpan.FromMinutes(30) },
            cancellationToken: ct);
    }

    private async Task<PantryDiscoveryResult> ComputeDiscoveryAsync(
        Guid householdId, Guid userId, PantryDiscoveryOptions options, CancellationToken ct)
    {
        // ── 1. Fetch pantry inventory ──────────────────────────────────────────
        HttpClient inventoryClient = _http.CreateClient("InventoryService");
        List<PantryIngredientItem>? pantryItems;
        try
        {
            pantryItems = await inventoryClient.GetFromJsonAsync<List<PantryIngredientItem>>(
                $"/api/inventory/pantry-ingredients/{householdId}", ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not fetch pantry ingredients for household {HouseholdId}", householdId);
            pantryItems = null;
        }

        if (pantryItems is null || pantryItems.Count == 0)
        {
            return new PantryDiscoveryResult { CachedAt = DateTime.UtcNow };
        }

        HashSet<string> pantrySet = new(
            pantryItems.Select(p => p.NormalizedName),
            StringComparer.OrdinalIgnoreCase);

        // ── 2. Get dietary restrictions if requested ───────────────────────────
        List<string> allergenBlocks = new();
        if (options.RespectDietaryRestrictions)
        {
            try
            {
                HttpClient userClient = _http.CreateClient("UserService");
                allergenBlocks = await userClient
                    .GetFromJsonAsync<List<string>>(
                        $"/api/AllergyManagement/internal/{userId}/allergen-names", ct)
                    ?? new List<string>();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not fetch allergen names for user {UserId} — skipping dietary filter", userId);
            }
        }

        // ── 3. Get recipes from RecipeService with ingredient lists ────────────
        HttpClient recipeClient = _http.CreateClient("RecipeService");
        List<RecipeIngredientSummary>? recipes;
        try
        {
            recipes = await recipeClient.GetFromJsonAsync<List<RecipeIngredientSummary>>(
                "/api/recipes/with-ingredients?limit=500", ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not fetch recipes with ingredients");
            recipes = null;
        }

        if (recipes is null)
        {
            return new PantryDiscoveryResult { TotalPantryIngredients = pantrySet.Count, CachedAt = DateTime.UtcNow };
        }

        // ── 4. Score each recipe ───────────────────────────────────────────────
        List<PantryRecipeMatch> matches = new();
        foreach (RecipeIngredientSummary recipe in recipes)
        {
            if (recipe.Ingredients.Count == 0) { continue; }

            bool hasConflict = allergenBlocks.Any(allergen =>
                recipe.Ingredients.Any(i =>
                    ContainsWholeWord(i.NormalizedName, allergen)));

            // Exclude recipes that conflict with the user's dietary restrictions.
            if (hasConflict) { continue; }

            List<string> matched = recipe.Ingredients
                .Where(i => pantrySet.Contains(i.NormalizedName))
                .Select(i => i.DisplayName)
                .ToList();
            List<string> missing = recipe.Ingredients
                .Where(i => !pantrySet.Contains(i.NormalizedName))
                .Select(i => i.DisplayName)
                .ToList();

            decimal matchPct = (decimal)matched.Count / recipe.Ingredients.Count;
            if (matchPct < options.MinMatchPercent) { continue; }

            matches.Add(new PantryRecipeMatch
            {
                RecipeId               = recipe.RecipeId,
                RecipeName             = recipe.RecipeName,
                ImageUrl               = recipe.ImageUrl,
                CookTimeMinutes        = recipe.CookTimeMinutes,
                AverageRating          = recipe.AverageRating,
                MatchPercent           = matchPct,
                MatchedIngredientCount = matched.Count,
                TotalIngredientCount   = recipe.Ingredients.Count,
                MissingIngredients     = missing
            });
        }

        // ── 5. Sort ────────────────────────────────────────────────────────────
        List<PantryRecipeMatch> sorted = options.SortBy switch
        {
            "rating"   => matches.OrderByDescending(m => m.AverageRating).ToList(),
            "cookTime" => matches.OrderBy(m => m.CookTimeMinutes).ToList(),
            "added"    => matches.ToList(),
            _          => matches.OrderByDescending(m => m.MatchPercent)
                                 .ThenByDescending(m => m.AverageRating).ToList()
        };

        return new PantryDiscoveryResult
        {
            Matches                = sorted.Take(options.Limit).ToList(),
            TotalPantryIngredients = pantrySet.Count,
            CachedAt               = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Returns true when <paramref name="ingredientName"/> contains <paramref name="allergen"/> as a whole word.
    /// "peanuts" matches allergen "peanuts" or "nuts". "coconut" does NOT match allergen "nut" unless "nut" is
    /// a separate token. Comparison is case-insensitive.
    /// </summary>
    private static bool ContainsWholeWord(string ingredientName, string allergen)
    {
        if (string.IsNullOrWhiteSpace(allergen)) { return false; }

        string source = ingredientName.ToLowerInvariant();
        string target = allergen.ToLowerInvariant();

        // Exact match
        if (source == target) { return true; }

        // Word-boundary scan: check every position where target starts in source
        int start = 0;
        while (true)
        {
            int idx = source.IndexOf(target, start, StringComparison.Ordinal);
            if (idx < 0) { return false; }

            bool leftBoundary  = idx == 0 || !char.IsLetterOrDigit(source[idx - 1]);
            bool rightBoundary = idx + target.Length >= source.Length ||
                                 !char.IsLetterOrDigit(source[idx + target.Length]);

            if (leftBoundary && rightBoundary) { return true; }
            start = idx + 1;
        }
    }
}
