using ExpressRecipe.ProductService.Data;
using ExpressRecipe.Shared.DTOs.Product;
using System.Text.Json;

namespace ExpressRecipe.ProductService.Services;

public class FoodSubstitutionService : IFoodSubstitutionService
{
    private readonly IFoodCatalogRepository _catalog;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<FoodSubstitutionService> _logger;

    private static readonly JsonSerializerOptions JsonOptions =
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

    public FoodSubstitutionService(
        IFoodCatalogRepository catalog,
        IHttpClientFactory httpClientFactory,
        ILogger<FoodSubstitutionService> logger)
    {
        _catalog = catalog;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<List<SubstituteOption>> GetSubstitutesAsync(
        Guid ingredientId,
        Guid userId,
        Guid? householdId,
        bool filterByAllergens,
        CancellationToken ct = default)
    {
        List<SubstituteOption> options = new List<SubstituteOption>();

        // 1. Get all group members that are substitutes for this ingredient
        List<FoodGroupMemberDto> members = await _catalog.GetSubstitutesForIngredientAsync(ingredientId, ct);

        if (members.Count == 0)
        {
            return options;
        }

        // 2. Optionally filter by allergens via SafeForkService
        HashSet<string> userAllergens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (filterByAllergens && userId != Guid.Empty)
        {
            userAllergens = await GetUserAllergensAsync(userId, householdId, ct);
        }

        // 3. Check inventory via InventoryService (only when a real user is known)
        List<Guid> ingredientIds = members
            .Where(m => m.IngredientId.HasValue)
            .Select(m => m.IngredientId!.Value)
            .Distinct()
            .ToList();

        HashSet<Guid> onHandIds = userId != Guid.Empty
            ? await GetInventoryOnHandAsync(userId, ingredientIds, ct)
            : new HashSet<Guid>();

        // 4. Load substitution history in a single bulk query (avoids N+1 per substitute)
        Dictionary<Guid, (decimal AvgRating, bool Used)> historyLookup =
            new Dictionary<Guid, (decimal, bool)>();

        if (userId != Guid.Empty && ingredientIds.Count > 0)
        {
            List<SubstitutionHistoryDto> allHistory =
                await _catalog.GetUserSubstitutionHistoryBulkAsync(userId, ingredientIds, ct);

            // Only count rows where this ingredient was the *substitute* for the *original* we started with
            foreach (SubstitutionHistoryDto row in allHistory)
            {
                if (row.SubstituteIngredientId == null) { continue; }
                if (row.OriginalIngredientId != ingredientId) { continue; }

                Guid subId = row.SubstituteIngredientId.Value;
                if (!historyLookup.TryGetValue(subId, out (decimal AvgRating, bool Used) existing))
                {
                    existing = (0m, true);
                }

                decimal newAvg = row.UserRating.HasValue
                    ? (existing.AvgRating + row.UserRating.Value) / 2m
                    : existing.AvgRating;

                historyLookup[subId] = (newAvg, true);
            }
        }

        // 5. Build options
        foreach (FoodGroupMemberDto member in members)
        {
            // Allergen filtering: when enabled, members with missing allergen metadata are
            // treated as "unknown" and excluded to protect users with allergen concerns.
            if (filterByAllergens && userAllergens.Count > 0)
            {
                if (member.AllergenFreeJson == null)
                {
                    // Unknown allergen profile – skip when user has active allergen filters
                    continue;
                }

                try
                {
                    string[] memberAllergenFree = JsonSerializer.Deserialize<string[]>(
                        member.AllergenFreeJson, JsonOptions) ?? Array.Empty<string>();

                    bool safe = userAllergens.All(a =>
                        memberAllergenFree.Contains(a, StringComparer.OrdinalIgnoreCase));

                    if (!safe)
                    {
                        continue;
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse AllergenFreeJson for member {MemberId}", member.Id);
                    // Treat parse failure as unknown – exclude when filter is active
                    continue;
                }
            }

            string[] allergenFreeArr = Array.Empty<string>();
            if (member.AllergenFreeJson != null)
            {
                try
                {
                    allergenFreeArr = JsonSerializer.Deserialize<string[]>(
                        member.AllergenFreeJson, JsonOptions) ?? Array.Empty<string>();
                }
                catch (JsonException)
                {
                    allergenFreeArr = Array.Empty<string>();
                }
            }

            // Resolve a non-empty display name from CustomName -> IngredientId label -> ProductId label
            string displayName = !string.IsNullOrWhiteSpace(member.CustomName)
                ? member.CustomName
                : member.IngredientId.HasValue
                    ? $"Ingredient {member.IngredientId.Value}"
                    : member.ProductId.HasValue
                        ? $"Product {member.ProductId.Value}"
                        : "Unnamed item";

            bool isOnHand = member.IngredientId.HasValue && onHandIds.Contains(member.IngredientId.Value);
            bool usedBefore = member.IngredientId.HasValue && historyLookup.ContainsKey(member.IngredientId.Value);
            decimal? avgRating = member.IngredientId.HasValue && historyLookup.TryGetValue(member.IngredientId.Value, out (decimal AvgRating, bool Used) h)
                ? h.AvgRating > 0m ? h.AvgRating : null
                : null;

            options.Add(new SubstituteOption
            {
                FoodGroupMemberId = member.Id,
                IngredientId = member.IngredientId,
                ProductId = member.ProductId,
                Name = displayName,
                SubstitutionRatio = member.SubstitutionRatio,
                SubstitutionNotes = member.SubstitutionNotes,
                BestFor = member.BestFor,
                NotSuitableFor = member.NotSuitableFor,
                RankOrder = member.RankOrder,
                AllergenFree = allergenFreeArr,
                IsOnHand = isOnHand,
                UserHistoryRating = avgRating,
                UserUsedBefore = usedBefore,
                HasHomemadeRecipe = member.IsHomemadeRecipeAvailable,
                HomemadeRecipeId = member.HomemadeRecipeId
            });
        }

        // 6. Sort: IsOnHand DESC, UserUsedBefore DESC, UserHistoryRating DESC, RankOrder ASC
        options = options
            .OrderByDescending(o => o.IsOnHand)
            .ThenByDescending(o => o.UserUsedBefore)
            .ThenByDescending(o => o.UserHistoryRating ?? 0m)
            .ThenBy(o => o.RankOrder)
            .ToList();

        return options;
    }

    // -----------------------------------------------------------------------
    // Helpers - call external services gracefully (non-critical)
    // -----------------------------------------------------------------------

    private async Task<HashSet<string>> GetUserAllergensAsync(
        Guid userId, Guid? householdId, CancellationToken ct)
    {
        try
        {
            HttpClient client = _httpClientFactory.CreateClient("SafeForkService");
            string url = $"api/safefork/allergens?userId={userId}";
            if (householdId.HasValue)
            {
                url += $"&householdId={householdId}";
            }

            List<string>? allergens = await client.GetFromJsonAsync<List<string>>(url, JsonOptions, ct);
            return allergens != null
                ? new HashSet<string>(allergens, StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve user allergens from SafeForkService for user {UserId}", userId);
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private async Task<HashSet<Guid>> GetInventoryOnHandAsync(
        Guid userId, List<Guid> ingredientIds, CancellationToken ct)
    {
        if (ingredientIds.Count == 0)
        {
            return new HashSet<Guid>();
        }

        try
        {
            HttpClient client = _httpClientFactory.CreateClient("InventoryService");
            string ids = string.Join(",", ingredientIds);
            string url = $"api/inventory/check?userId={userId}&ingredientIds={ids}";

            Dictionary<string, bool>? onHand = await client.GetFromJsonAsync<Dictionary<string, bool>>(url, JsonOptions, ct);
            if (onHand == null)
            {
                return new HashSet<Guid>();
            }

            HashSet<Guid> result = new HashSet<Guid>();
            foreach (KeyValuePair<string, bool> kvp in onHand)
            {
                if (kvp.Value && Guid.TryParse(kvp.Key, out Guid id))
                {
                    result.Add(id);
                }
            }
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve inventory from InventoryService for user {UserId}", userId);
            return new HashSet<Guid>();
        }
    }
}
