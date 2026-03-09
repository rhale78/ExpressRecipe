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
        // 1. Get all group members that are substitutes for this ingredient
        List<SubstituteOption> options = new List<SubstituteOption>();

        var members = await _catalog.GetSubstitutesForIngredientAsync(ingredientId, ct);

        if (members.Count == 0)
        {
            return options;
        }

        // 2. Optionally filter by allergens via SafeForkService
        HashSet<string> userAllergens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (filterByAllergens)
        {
            userAllergens = await GetUserAllergensAsync(userId, householdId, ct);
        }

        // 3. Check inventory via InventoryService
        var ingredientIds = members
            .Where(m => m.IngredientId.HasValue)
            .Select(m => m.IngredientId!.Value)
            .Distinct()
            .ToList();

        HashSet<Guid> onHandIds = await GetInventoryOnHandAsync(userId, ingredientIds, ct);

        // 4. Load substitution history for this user
        Dictionary<Guid, List<SubstitutionHistoryDto>> historyBySubId =
            new Dictionary<Guid, List<SubstitutionHistoryDto>>();

        List<Guid> substituteIngredientIds = members
            .Where(m => m.IngredientId.HasValue)
            .Select(m => m.IngredientId!.Value)
            .Distinct()
            .ToList();

        await Task.WhenAll(substituteIngredientIds.Select(async subId =>
        {
            List<SubstitutionHistoryDto> history = await _catalog.GetUserSubstitutionHistoryAsync(userId, subId, ct);
            lock (historyBySubId)
            {
                historyBySubId[subId] = history;
            }
        }));

        // Build lookup: substituteIngredientId → (avg rating, used)
        Dictionary<Guid, (decimal AvgRating, bool Used)> historyLookup =
            new Dictionary<Guid, (decimal, bool)>();

        foreach (KeyValuePair<Guid, List<SubstitutionHistoryDto>> kv in historyBySubId)
        {
            if (kv.Value.Count > 0)
            {
                List<SubstitutionHistoryDto> rated = kv.Value.Where(h => h.UserRating.HasValue).ToList();
                decimal avgRating = rated.Count > 0
                    ? (decimal)rated.Average(h => h.UserRating!.Value)
                    : 0m;
                historyLookup[kv.Key] = (avgRating, true);
            }
        }

        // 5. Build options
        foreach (var member in members)
        {
            // Allergen filtering
            if (filterByAllergens && userAllergens.Count > 0 && member.AllergenFreeJson != null)
            {
                try
                {
                    var memberAllergenFree = JsonSerializer.Deserialize<string[]>(
                        member.AllergenFreeJson, JsonOptions) ?? Array.Empty<string>();

                    // If this substitute is NOT free of the user's allergens, skip it
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

            bool isOnHand = member.IngredientId.HasValue && onHandIds.Contains(member.IngredientId.Value);
            bool usedBefore = member.IngredientId.HasValue && historyLookup.ContainsKey(member.IngredientId.Value);
            decimal? avgRating = member.IngredientId.HasValue && historyLookup.TryGetValue(member.IngredientId.Value, out var h)
                ? h.AvgRating > 0m ? h.AvgRating : null
                : null;

            options.Add(new SubstituteOption
            {
                FoodGroupMemberId = member.Id,
                IngredientId = member.IngredientId,
                ProductId = member.ProductId,
                Name = member.CustomName ?? string.Empty,
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
    // Helpers – call external services gracefully (non-critical)
    // -----------------------------------------------------------------------

    private async Task<HashSet<string>> GetUserAllergensAsync(
        Guid userId, Guid? householdId, CancellationToken ct)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("SafeForkService");
            var url = $"api/safefork/allergens?userId={userId}";
            if (householdId.HasValue)
            {
                url += $"&householdId={householdId}";
            }

            var allergens = await client.GetFromJsonAsync<List<string>>(url, JsonOptions, ct);
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
            var client = _httpClientFactory.CreateClient("InventoryService");
            var ids = string.Join(",", ingredientIds);
            var url = $"api/inventory/check?userId={userId}&ingredientIds={ids}";

            var onHand = await client.GetFromJsonAsync<Dictionary<string, bool>>(url, JsonOptions, ct);
            if (onHand == null)
            {
                return new HashSet<Guid>();
            }

            var result = new HashSet<Guid>();
            foreach (var kvp in onHand)
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
