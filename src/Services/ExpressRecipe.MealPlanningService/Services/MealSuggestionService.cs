using System.Net.Http.Json;
using ExpressRecipe.MealPlanningService.Data;
using ExpressRecipe.MealPlanningService.Logging;
using Microsoft.Extensions.Caching.Hybrid;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("ExpressRecipe.MealPlanningService.Tests")]

namespace ExpressRecipe.MealPlanningService.Services;

/// <summary>
/// Composite scoring formula weights.
/// </summary>
internal static class ScoringWeights
{
    // Rating contribution (0-50 points total from user + global)
    internal const decimal UserRatingWeight   = 8m;   // up to 40 pts (5 * 8)
    internal const decimal GlobalRatingWeight = 2m;   // up to 10 pts (5 * 2)

    // Inventory contribution (3-30 pts, weight modulated by slider)
    internal const decimal InventoryMin       = 3m;
    internal const decimal InventoryMax       = 30m;

    // Mode bonuses
    internal const decimal TriedAndTrueCookBonus = 2m;  // per cook (capped at 20)
    internal const decimal SomethingNewNoveltyBonus = 15m; // for CookCount = 0
}

public class MealSuggestionService : IMealSuggestionService
{
    // DTOs for external service responses
    private record RecipeCandidate(
        Guid Id,
        string Name,
        int? CookTimeMinutes,
        decimal? GlobalAverageRating,
        List<string>? Tags,
        List<RecipeIngredient>? Ingredients);

    private record RecipeIngredient(
        Guid? IngredientId,
        string Name,
        decimal Quantity,
        string? Unit);

    private record GlobalRatingEntry(Guid RecipeId, decimal AverageRating);

    private record InventoryItem(Guid? IngredientId, string Name, decimal QuantityOnHand, string? Unit);

    private record SafeForkResult(Dictionary<string, bool> Results);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMealPlanningRepository _repository;
    private readonly ILogger<MealSuggestionService> _logger;
    private readonly HybridCache? _hybridCache;

    // Service base URL config keys
    private const string RecipeServiceKey    = "Services:RecipeService";
    private const string InventoryServiceKey = "Services:InventoryService";
    private const string SafeForkServiceKey  = "Services:SafeForkService";

    private readonly string _recipeServiceUrl;
    private readonly string _inventoryServiceUrl;
    private readonly string _safeForkServiceUrl;

    public MealSuggestionService(
        IHttpClientFactory httpClientFactory,
        IMealPlanningRepository repository,
        IConfiguration configuration,
        ILogger<MealSuggestionService> logger,
        HybridCache? hybridCache = null)
    {
        _httpClientFactory = httpClientFactory;
        _repository = repository;
        _logger = logger;
        _hybridCache = hybridCache;
        _recipeServiceUrl    = configuration[RecipeServiceKey]    ?? "http://recipeservice";
        _inventoryServiceUrl = configuration[InventoryServiceKey] ?? "http://inventoryservice";
        _safeForkServiceUrl  = configuration[SafeForkServiceKey]  ?? "http://safeforkservice";
    }

    public async Task<List<MealSuggestion>> SuggestAsync(SuggestionRequest request, CancellationToken ct = default)
    {
        // 1. Fetch candidate recipes from RecipeService (cached)
        List<RecipeCandidate> candidates = await GetCandidatesAsync(request.UserId, request.MealType, ct);
        _logger.LogSuggestionCandidates(request.UserId, candidates.Count, request.MealType ?? "any");

        // Apply MaxCookMinutes filter early
        if (request.MaxCookMinutes > 0)
        {
            candidates = candidates
                .Where(c => c.CookTimeMinutes == null || c.CookTimeMinutes <= request.MaxCookMinutes)
                .ToList();
            _logger.LogSuggestionFiltered(request.UserId, candidates.Count, "MaxCookMinutes");
        }

        // Apply explicit exclude list
        if (request.ExcludeRecipeIds.Count > 0)
        {
            HashSet<Guid> excluded = new(request.ExcludeRecipeIds);
            candidates = candidates.Where(c => !excluded.Contains(c.Id)).ToList();
        }

        if (candidates.Count == 0)
        {
            return new List<MealSuggestion>();
        }

        // 2-3. User ratings and cook counts from repository
        Task<Dictionary<Guid, decimal>> userRatingsTask  = _repository.GetUserRecipeRatingsAsync(request.UserId, ct);
        Task<Dictionary<Guid, int>> cookCountsTask       = _repository.GetUserRecipeCookCountsAsync(request.UserId, ct);
        Task<List<Guid>> recentIdsTask                   = request.ExcludeRecentDays
            ? _repository.GetRecentlyCookedRecipeIdsAsync(request.UserId, request.RecentDaysCutoff, ct)
            : Task.FromResult(new List<Guid>());

        // 4. Fetch inventory from InventoryService
        Task<List<InventoryItem>> inventoryTask = GetInventoryAsync(request.UserId, ct);

        await Task.WhenAll(userRatingsTask, cookCountsTask, recentIdsTask, inventoryTask);

        Dictionary<Guid, decimal> userRatings  = userRatingsTask.Result;
        Dictionary<Guid, int> cookCounts       = cookCountsTask.Result;
        HashSet<Guid> recentIds                = new(recentIdsTask.Result);
        List<InventoryItem> inventory          = inventoryTask.Result;

        // 5. Filter allergen-unsafe recipes via SafeFork
        Guid[] candidateIds = candidates.Select(c => c.Id).ToArray();
        Dictionary<Guid, bool> safeForkResults = await GetSafeForkResultsAsync(candidateIds, request.UserId, ct);

        // 6. Score, filter and sort
        return ScoreCandidates(candidates, request, userRatings, cookCounts, recentIds, inventory, safeForkResults);
    }

    /// <summary>
    /// Scores a pre-filtered candidate list using the supplied pre-loaded user and inventory data.
    /// Shared by <see cref="SuggestAsync"/> and <see cref="SuggestForWeekAsync"/>.
    /// </summary>
    private List<MealSuggestion> ScoreCandidates(
        List<RecipeCandidate> candidates,
        SuggestionRequest request,
        Dictionary<Guid, decimal> userRatings,
        Dictionary<Guid, int> cookCounts,
        HashSet<Guid> recentIds,
        List<InventoryItem> inventory,
        Dictionary<Guid, bool> safeForkResults)
    {
        List<MealSuggestion> scored = new(candidates.Count);

        int allergenUnsafeCount = 0;
        int recentDaysExcludedCount = 0;

        foreach (RecipeCandidate candidate in candidates)
        {
            bool isAllergenSafe = !safeForkResults.TryGetValue(candidate.Id, out bool isSafe) || isSafe;

            if (!isAllergenSafe)
            {
                allergenUnsafeCount++;
                continue;  // Allergen-unsafe recipes are excluded entirely
            }

            if (request.ExcludeRecentDays && recentIds.Contains(candidate.Id))
            {
                recentDaysExcludedCount++;
                continue;  // Recently cooked — skip
            }

            decimal userRating  = userRatings.TryGetValue(candidate.Id, out decimal ur) ? ur : 0m;
            int cookCount       = cookCounts.TryGetValue(candidate.Id, out int cc) ? cc : 0;
            decimal globalRating = candidate.GlobalAverageRating ?? 0m;

            // Compute inventory match
            (decimal matchPct, List<string> missing) = ComputeInventoryMatch(candidate.Ingredients, inventory);

            // Base score components.
            // In SomethingNew mode the user has no rating for untried recipes (CookCount=0 means no experience),
            // so the user-rating weight is zeroed out to prevent rated-but-overused recipes from dominating.
            decimal effectiveUserRatingWeight = request.SuggestionMode == SuggestionModes.SomethingNew
                ? 0m
                : ScoringWeights.UserRatingWeight;

            decimal ratingScore    = (userRating * effectiveUserRatingWeight)
                                   + (globalRating * ScoringWeights.GlobalRatingWeight);
            decimal inventoryScore = ComputeInventoryScore(matchPct, request.InventorySlider);
            decimal modeBonus      = ComputeModeBonus(request.SuggestionMode, cookCount, userRating);

            decimal totalScore = ratingScore + inventoryScore + modeBonus;

            scored.Add(new MealSuggestion
            {
                RecipeId            = candidate.Id,
                RecipeName          = candidate.Name,
                CookMinutes         = candidate.CookTimeMinutes ?? 0,
                UserRating          = userRating,
                GlobalRating        = globalRating,
                UserCookCount       = cookCount,
                InventoryMatchPct   = matchPct,
                IsAllergenSafe      = true,
                Score               = totalScore,
                MissingIngredients  = missing,
                Tags                = candidate.Tags ?? new List<string>()
            });
        }

        _logger.LogSuggestionFiltered(request.UserId, candidates.Count - allergenUnsafeCount, "AllergenFilter");
        int afterBothFilters = candidates.Count - allergenUnsafeCount - recentDaysExcludedCount;
        _logger.LogSuggestionFiltered(request.UserId, afterBothFilters, "ExcludeRecentDays");

        return scored
            .OrderByDescending(s => s.Score)
            .Take(request.Count)
            .ToList();
    }

    public async Task<List<MealSuggestion>> SuggestForWeekAsync(
        Guid userId, Guid? householdId, SuggestionRequest baseRequest, CancellationToken ct = default)
    {
        string[] mealTypes = { "Breakfast", "Lunch", "Dinner" };
        HashSet<Guid> usedThisWeek = new();
        List<MealSuggestion> weekSuggestions = new();

        // Pre-fetch all user-specific data once to avoid 21 × 5 repeated I/O operations
        Task<Dictionary<Guid, decimal>> userRatingsTask  = _repository.GetUserRecipeRatingsAsync(userId, ct);
        Task<Dictionary<Guid, int>> cookCountsTask       = _repository.GetUserRecipeCookCountsAsync(userId, ct);
        Task<List<Guid>> recentIdsTask                   = baseRequest.ExcludeRecentDays
            ? _repository.GetRecentlyCookedRecipeIdsAsync(userId, baseRequest.RecentDaysCutoff, ct)
            : Task.FromResult(new List<Guid>());
        Task<List<InventoryItem>> inventoryTask          = GetInventoryAsync(userId, ct);

        await Task.WhenAll(userRatingsTask, cookCountsTask, recentIdsTask, inventoryTask);

        Dictionary<Guid, decimal> userRatings  = userRatingsTask.Result;
        Dictionary<Guid, int> cookCounts       = cookCountsTask.Result;
        HashSet<Guid> recentIds                = new(recentIdsTask.Result);
        List<InventoryItem> inventory          = inventoryTask.Result;

        for (int day = 0; day < 7; day++)
        {
            foreach (string mealType in mealTypes)
            {
                // Accumulate week's used recipes + any caller-supplied exclusions
                List<Guid> excludeIds = new(baseRequest.ExcludeRecipeIds);
                excludeIds.AddRange(usedThisWeek);

                // Build a per-slot request and score using the pre-fetched data
                SuggestionRequest dayRequest = new()
                {
                    UserId           = userId,
                    HouseholdId      = householdId,
                    MealType         = mealType,
                    SuggestionMode   = baseRequest.SuggestionMode,
                    InventorySlider  = baseRequest.InventorySlider,
                    MaxCookMinutes   = baseRequest.MaxCookMinutes,
                    Count            = 1,
                    ExcludeRecentDays = baseRequest.ExcludeRecentDays,
                    RecentDaysCutoff  = baseRequest.RecentDaysCutoff,
                    ExcludeRecipeIds  = excludeIds
                };

                List<MealSuggestion> suggestions = await ScoreWithPreloadedDataAsync(
                    dayRequest, userRatings, cookCounts, recentIds, inventory, ct);

                if (suggestions.Count > 0)
                {
                    MealSuggestion suggestion = suggestions[0];
                    usedThisWeek.Add(suggestion.RecipeId);
                    weekSuggestions.Add(suggestion);
                }
            }
        }

        return weekSuggestions;
    }

    /// <summary>
    /// Scores candidates using pre-fetched user data and inventory (avoids redundant I/O in loops).
    /// SafeFork is still called per slot because the candidate set may differ by meal type.
    /// </summary>
    private async Task<List<MealSuggestion>> ScoreWithPreloadedDataAsync(
        SuggestionRequest request,
        Dictionary<Guid, decimal> userRatings,
        Dictionary<Guid, int> cookCounts,
        HashSet<Guid> recentIds,
        List<InventoryItem> inventory,
        CancellationToken ct)
    {
        List<RecipeCandidate> candidates = await GetCandidatesAsync(request.UserId, request.MealType, ct);

        if (request.MaxCookMinutes > 0)
        {
            candidates = candidates
                .Where(c => c.CookTimeMinutes == null || c.CookTimeMinutes <= request.MaxCookMinutes)
                .ToList();
        }

        if (request.ExcludeRecipeIds.Count > 0)
        {
            HashSet<Guid> excluded = new(request.ExcludeRecipeIds);
            candidates = candidates.Where(c => !excluded.Contains(c.Id)).ToList();
        }

        if (candidates.Count == 0)
        {
            return new List<MealSuggestion>();
        }

        Guid[] candidateIds = candidates.Select(c => c.Id).ToArray();
        Dictionary<Guid, bool> safeForkResults = await GetSafeForkResultsAsync(candidateIds, request.UserId, ct);

        return ScoreCandidates(candidates, request, userRatings, cookCounts, recentIds, inventory, safeForkResults);
    }

    // ── Scoring helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Inventory score: at slider=0 (use on-hand) a full match yields 30 pts, no match 3 pts.
    /// At slider=100 (shop fresh) inventory contributes 0 pts.
    /// Linear interpolation between the two extremes.
    /// </summary>
    internal static decimal ComputeInventoryScore(decimal matchPct, int slider)
    {
        // inventoryWeight: 1.0 at slider=0, 0.0 at slider=100
        decimal inventoryWeight = 1m - (slider / 100m);
        decimal rawScore = ScoringWeights.InventoryMin
                         + ((ScoringWeights.InventoryMax - ScoringWeights.InventoryMin) * matchPct);
        return inventoryWeight * rawScore;
    }

    internal static decimal ComputeModeBonus(string mode, int cookCount, decimal userRating)
    {
        return mode switch
        {
            SuggestionModes.TriedAndTrue =>
                // Reward higher cook counts (capped at 20 pts) + user rating
                Math.Min(cookCount * ScoringWeights.TriedAndTrueCookBonus, 20m),

            SuggestionModes.SomethingNew =>
                // Heavily favour recipes the user has never cooked
                cookCount == 0 ? ScoringWeights.SomethingNewNoveltyBonus : 0m,

            _ => 0m  // Balanced — no special bonus
        };
    }

    /// <summary>
    /// Computes the fraction of recipe ingredients already in inventory.
    /// Returns (matchPct 0.0-1.0, missingIngredientNames).
    /// </summary>
    private static (decimal matchPct, List<string> missing) ComputeInventoryMatch(
        List<RecipeIngredient>? ingredients,
        List<InventoryItem> inventory)
    {
        if (ingredients == null || ingredients.Count == 0)
        {
            return (1m, new List<string>());
        }

        // Build lookup by ingredient id and name
        Dictionary<Guid, InventoryItem> byId = inventory
            .Where(i => i.IngredientId.HasValue)
            .ToDictionary(i => i.IngredientId!.Value, i => i);

        Dictionary<string, InventoryItem> byName = inventory
            .ToDictionary(i => i.Name.ToLowerInvariant(), i => i, StringComparer.OrdinalIgnoreCase);

        List<string> missing = new();
        int matched = 0;

        foreach (RecipeIngredient ing in ingredients)
        {
            bool found = (ing.IngredientId.HasValue && byId.ContainsKey(ing.IngredientId.Value))
                      || byName.ContainsKey(ing.Name.ToLowerInvariant());

            if (found)
            {
                matched++;
            }
            else
            {
                missing.Add(ing.Name);
            }
        }

        decimal matchPct = (decimal)matched / ingredients.Count;
        return (matchPct, missing);
    }

    // ── External service calls ────────────────────────────────────────────────

    private async Task<List<RecipeCandidate>> GetCandidatesAsync(
        Guid userId, string mealType, CancellationToken ct)
    {
        if (_hybridCache != null)
        {
            string cacheKey = $"suggestions:{userId}:candidates:{mealType}";
            // The factory delegate is only invoked on a true cache miss (L1 + L2 both absent).
            // HybridCache guarantees the factory is called at most once per key per miss window,
            // making the factoryInvoked flag a reliable miss indicator.
            bool factoryInvoked = false;
            List<RecipeCandidate> result = await _hybridCache.GetOrCreateAsync(
                cacheKey,
                async token =>
                {
                    factoryInvoked = true;
                    return await FetchCandidatesFromServiceAsync(mealType, token);
                },
                new HybridCacheEntryOptions { Expiration = TimeSpan.FromMinutes(5) },
                cancellationToken: ct);

            if (factoryInvoked)
                _logger.LogSuggestionCacheMiss(userId, cacheKey);
            else
                _logger.LogSuggestionCacheHit(userId, cacheKey);

            return result;
        }

        return await FetchCandidatesFromServiceAsync(mealType, ct);
    }

    private async Task<List<RecipeCandidate>> FetchCandidatesFromServiceAsync(
        string mealType, CancellationToken ct)
    {
        try
        {
            using HttpClient client = _httpClientFactory.CreateClient("RecipeService");
            List<RecipeCandidate>? result = await client.GetFromJsonAsync<List<RecipeCandidate>>(
                $"{_recipeServiceUrl}/api/recipes?category={Uri.EscapeDataString(mealType)}&limit=200",
                ct);
            return result ?? new List<RecipeCandidate>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch candidates from RecipeService for mealType {MealType}", mealType);
            return new List<RecipeCandidate>();
        }
    }

    private async Task<List<InventoryItem>> GetInventoryAsync(Guid userId, CancellationToken ct)
    {
        try
        {
            using HttpClient client = _httpClientFactory.CreateClient("InventoryService");
            List<InventoryItem>? result = await client.GetFromJsonAsync<List<InventoryItem>>(
                $"{_inventoryServiceUrl}/api/inventory?userId={userId}",
                ct);
            return result ?? new List<InventoryItem>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch inventory for user {UserId}", userId);
            return new List<InventoryItem>();
        }
    }

    private async Task<Dictionary<Guid, bool>> GetSafeForkResultsAsync(
        Guid[] recipeIds, Guid userId, CancellationToken ct)
    {
        if (recipeIds.Length == 0)
        {
            return new Dictionary<Guid, bool>();
        }

        try
        {
            using HttpClient client = _httpClientFactory.CreateClient("SafeForkService");
            HttpResponseMessage response = await client.PostAsJsonAsync(
                $"{_safeForkServiceUrl}/api/safefork/evaluate/batch",
                new { recipeIds, userId },
                ct);

            if (!response.IsSuccessStatusCode)
            {
                return new Dictionary<Guid, bool>();
            }

            Dictionary<string, bool>? raw = await response.Content
                .ReadFromJsonAsync<Dictionary<string, bool>>(ct);

            if (raw == null)
            {
                return new Dictionary<Guid, bool>();
            }

            return raw.ToDictionary(
                kvp => Guid.Parse(kvp.Key),
                kvp => kvp.Value);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch SafeFork results — assuming all recipes safe");
            // Default to all safe if service is unavailable
            return new Dictionary<Guid, bool>();
        }
    }
}
