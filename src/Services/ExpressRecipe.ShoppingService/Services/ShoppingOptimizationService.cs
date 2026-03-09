using System.Net.Http.Json;
using System.Text.Json;
using ExpressRecipe.ShoppingService.Data;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("ExpressRecipe.ShoppingService.Tests")]

namespace ExpressRecipe.ShoppingService.Services;

public interface IShoppingOptimizationService
{
    Task<OptimizedShoppingPlan> OptimizeAsync(Guid listId, Guid userId, string strategy, CancellationToken ct = default);
}

public class ShoppingOptimizationService : IShoppingOptimizationService
{
    private readonly IShoppingRepository _repository;
    private readonly HttpClient _priceClient;
    private readonly ILogger<ShoppingOptimizationService> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public ShoppingOptimizationService(
        IShoppingRepository repository,
        IHttpClientFactory httpClientFactory,
        ILogger<ShoppingOptimizationService> logger)
    {
        _repository = repository;
        _priceClient = httpClientFactory.CreateClient("PriceService");
        _logger = logger;
    }

    public async Task<OptimizedShoppingPlan> OptimizeAsync(Guid listId, Guid userId, string strategy, CancellationToken ct = default)
    {
        // 1. Load list items
        List<ShoppingListItemDto> items = await _repository.GetListItemsAsync(listId, userId);
        if (items.Count == 0)
        {
            return new OptimizedShoppingPlan
            {
                Strategy = strategy,
                Warnings = new List<string> { "Shopping list is empty." }
            };
        }

        // 2. Load user category preferences
        List<UserStoreCategoryPreferenceDto> categoryPrefs = await _repository.GetUserCategoryPreferencesAsync(userId, ct);

        // 3. Batch-fetch prices from PriceService
        List<Guid> productIds = items
            .Where(i => i.ProductId.HasValue)
            .Select(i => i.ProductId!.Value)
            .Distinct()
            .ToList();

        Dictionary<Guid, List<StorePriceEntry>> pricesByProduct = await FetchBatchPricesAsync(productIds, ct);

        // 4. Apply strategy
        Dictionary<Guid, Guid> itemToStore = strategy switch
        {
            "CheapestOverall" => ApplyCheapestOverall(items, pricesByProduct),
            "PreferredStorePerCategory" => ApplyPreferredStorePerCategory(items, pricesByProduct, categoryPrefs),
            "MinimizeStores" => ApplyMinimizeStores(items, pricesByProduct),
            "Hybrid" => ApplyHybrid(items, pricesByProduct, categoryPrefs),
            _ => ApplySingleStore(items, categoryPrefs) // SingleStore default
        };

        // 5. Load store details for assigned stores
        HashSet<Guid> assignedStoreIds = itemToStore.Values.ToHashSet();
        Dictionary<Guid, StoreDto> storeDetails = new();
        foreach (Guid storeId in assignedStoreIds)
        {
            StoreDto? store = await _repository.GetStoreByIdAsync(storeId);
            if (store != null)
            {
                storeDetails[storeId] = store;
            }
        }

        // 6. Build store groups with aisle-sorted items
        Dictionary<Guid, List<OptimizedShoppingItem>> grouped = new();
        List<string> warnings = new();

        foreach (ShoppingListItemDto item in items)
        {
            if (!itemToStore.TryGetValue(item.Id, out Guid assignedStore))
            {
                warnings.Add($"No store found for '{item.CustomName ?? item.ProductName ?? item.Id.ToString()}'.");
                continue;
            }
            if (!grouped.ContainsKey(assignedStore))
            {
                grouped[assignedStore] = new List<OptimizedShoppingItem>();
            }

            List<StorePriceEntry>? pricingOptions = item.ProductId.HasValue
                && pricesByProduct.TryGetValue(item.ProductId.Value, out List<StorePriceEntry>? opts)
                ? opts : null;
            StorePriceEntry? storePrice = pricingOptions?.FirstOrDefault(p => p.StoreId == assignedStore);

            grouped[assignedStore].Add(new OptimizedShoppingItem
            {
                ShoppingListItemId = item.Id,
                Name = item.CustomName ?? item.ProductName ?? string.Empty,
                Quantity = item.Quantity,
                Unit = item.Unit,
                Aisle = item.Aisle,
                AisleOrder = item.OrderIndex,
                Price = storePrice?.Price ?? item.EstimatedPrice,
                HasDeal = storePrice?.HasDeal ?? item.HasDeal,
                DealDescription = storePrice?.DealDescription ?? item.DealDescription,
                Savings = storePrice?.HasDeal == true && storePrice.RegularPrice.HasValue
                    ? storePrice.RegularPrice.Value - storePrice.Price
                    : null
            });
        }

        // Sort items by AisleOrder within each group
        List<StoreShoppingGroup> storeGroups = grouped
            .Select(kvp =>
            {
                List<OptimizedShoppingItem> sortedItems = kvp.Value.OrderBy(i => i.AisleOrder).ToList();
                decimal subTotal = sortedItems.Sum(i => (i.Price ?? 0m) * i.Quantity);
                storeDetails.TryGetValue(kvp.Key, out StoreDto? store);
                return new StoreShoppingGroup
                {
                    StoreId = kvp.Key,
                    StoreName = store?.Name ?? kvp.Key.ToString(),
                    StoreAddress = store?.Address,
                    SubTotal = subTotal,
                    Items = sortedItems
                };
            })
            .ToList();

        decimal totalEstimate = storeGroups.Sum(g => g.SubTotal);
        decimal totalWithDeals = storeGroups.Sum(g => g.Items.Sum(i => (i.Price ?? 0m) * i.Quantity));

        OptimizedShoppingPlan plan = new()
        {
            StoreGroups = storeGroups,
            TotalEstimate = totalEstimate,
            TotalWithDeals = totalWithDeals,
            StoreCount = storeGroups.Count,
            Strategy = strategy,
            Warnings = warnings
        };

        // 7. Persist the result
        string resultJson = JsonSerializer.Serialize(plan, _jsonOptions);
        await _repository.SaveOptimizationResultAsync(listId, strategy, resultJson, totalEstimate, totalWithDeals, ct);

        return plan;
    }

    // ── Strategies ────────────────────────────────────────────────────────────

    internal static Dictionary<Guid, Guid> ApplySingleStore(
        List<ShoppingListItemDto> items,
        List<UserStoreCategoryPreferenceDto> prefs)
    {
        Guid? preferredStore = prefs.OrderBy(p => p.RankOrder).FirstOrDefault()?.PreferredStoreId;
        Dictionary<Guid, Guid> result = new();
        if (preferredStore.HasValue)
        {
            foreach (ShoppingListItemDto item in items)
            {
                result[item.Id] = preferredStore.Value;
            }
        }
        return result;
    }

    internal static Dictionary<Guid, Guid> ApplyCheapestOverall(
        List<ShoppingListItemDto> items,
        Dictionary<Guid, List<StorePriceEntry>> prices)
    {
        Dictionary<Guid, Guid> result = new();
        foreach (ShoppingListItemDto item in items)
        {
            if (!item.ProductId.HasValue) continue;
            if (!prices.TryGetValue(item.ProductId.Value, out List<StorePriceEntry>? opts) || opts.Count == 0) continue;
            result[item.Id] = opts.OrderBy(p => p.Price).First().StoreId;
        }
        return result;
    }

    internal static Dictionary<Guid, Guid> ApplyPreferredStorePerCategory(
        List<ShoppingListItemDto> items,
        Dictionary<Guid, List<StorePriceEntry>> prices,
        List<UserStoreCategoryPreferenceDto> prefs)
    {
        Dictionary<Guid, Guid> result = new();
        foreach (ShoppingListItemDto item in items)
        {
            Guid? assigned = null;
            string? category = item.Category;
            if (!string.IsNullOrEmpty(category))
            {
                List<UserStoreCategoryPreferenceDto> catPrefs = prefs
                    .Where(p => string.Equals(p.Category, category, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(p => p.RankOrder)
                    .ToList();

                foreach (UserStoreCategoryPreferenceDto pref in catPrefs)
                {
                    bool hasPrice = item.ProductId.HasValue
                        && prices.TryGetValue(item.ProductId.Value, out List<StorePriceEntry>? opts)
                        && opts.Any(p => p.StoreId == pref.PreferredStoreId);
                    if (hasPrice)
                    {
                        assigned = pref.PreferredStoreId;
                        break;
                    }
                }
            }

            if (!assigned.HasValue && item.ProductId.HasValue
                && prices.TryGetValue(item.ProductId.Value, out List<StorePriceEntry>? fallbackOpts)
                && fallbackOpts.Count > 0)
            {
                assigned = fallbackOpts.OrderBy(p => p.Price).First().StoreId;
            }

            if (assigned.HasValue)
            {
                result[item.Id] = assigned.Value;
            }
        }
        return result;
    }

    internal static Dictionary<Guid, Guid> ApplyMinimizeStores(
        List<ShoppingListItemDto> items,
        Dictionary<Guid, List<StorePriceEntry>> prices)
    {
        const decimal NewStorePenalty = 2.50m;
        HashSet<Guid> openedStores = new();
        Dictionary<Guid, Guid> result = new();

        foreach (ShoppingListItemDto item in items)
        {
            if (!item.ProductId.HasValue) continue;
            if (!prices.TryGetValue(item.ProductId.Value, out List<StorePriceEntry>? opts) || opts.Count == 0) continue;

            StorePriceEntry? best = null;
            decimal bestCost = decimal.MaxValue;

            foreach (StorePriceEntry entry in opts)
            {
                decimal effectiveCost = entry.Price + (openedStores.Contains(entry.StoreId) ? 0m : NewStorePenalty);
                if (effectiveCost < bestCost)
                {
                    bestCost = effectiveCost;
                    best = entry;
                }
            }

            if (best != null)
            {
                result[item.Id] = best.StoreId;
                openedStores.Add(best.StoreId);
            }
        }
        return result;
    }

    internal static Dictionary<Guid, Guid> ApplyHybrid(
        List<ShoppingListItemDto> items,
        Dictionary<Guid, List<StorePriceEntry>> prices,
        List<UserStoreCategoryPreferenceDto> prefs)
    {
        Dictionary<Guid, Guid> preferred = ApplyPreferredStorePerCategory(items, prices, prefs);
        Dictionary<Guid, Guid> cheapest = ApplyCheapestOverall(items, prices);

        Dictionary<Guid, Guid> result = new();
        foreach (ShoppingListItemDto item in items)
        {
            if (preferred.TryGetValue(item.Id, out Guid store))
            {
                result[item.Id] = store;
            }
            else if (cheapest.TryGetValue(item.Id, out Guid fallback))
            {
                result[item.Id] = fallback;
            }
        }
        return result;
    }

    // ── External price fetch ──────────────────────────────────────────────────

    private async Task<Dictionary<Guid, List<StorePriceEntry>>> FetchBatchPricesAsync(
        List<Guid> productIds, CancellationToken ct)
    {
        Dictionary<Guid, List<StorePriceEntry>> result = new();
        if (productIds.Count == 0) return result;

        try
        {
            string csvIds = string.Join(",", productIds);
            List<BatchPriceResult>? prices = await _priceClient
                .GetFromJsonAsync<List<BatchPriceResult>>(
                    $"api/prices/batch?productIds={Uri.EscapeDataString(csvIds)}",
                    _jsonOptions, ct);

            if (prices != null)
            {
                foreach (BatchPriceResult p in prices)
                {
                    if (!result.ContainsKey(p.ProductId))
                    {
                        result[p.ProductId] = new List<StorePriceEntry>();
                    }
                    result[p.ProductId].Add(new StorePriceEntry
                    {
                        StoreId = p.StoreId,
                        Price = p.Price,
                        RegularPrice = p.RegularPrice,
                        HasDeal = p.HasDeal,
                        DealDescription = p.DealDescription
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch batch prices from PriceService; optimization will use estimated prices.");
        }

        return result;
    }

    // ── Internal models ───────────────────────────────────────────────────────

    internal sealed class StorePriceEntry
    {
        public Guid StoreId { get; set; }
        public decimal Price { get; set; }
        public decimal? RegularPrice { get; set; }
        public bool HasDeal { get; set; }
        public string? DealDescription { get; set; }
    }

    private sealed class BatchPriceResult
    {
        public Guid ProductId { get; set; }
        public Guid StoreId { get; set; }
        public decimal Price { get; set; }
        public decimal? RegularPrice { get; set; }
        public bool HasDeal { get; set; }
        public string? DealDescription { get; set; }
    }
}
