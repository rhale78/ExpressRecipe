using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using ExpressRecipe.ShoppingService.Data;
using ExpressRecipe.ShoppingService.Services;

namespace ExpressRecipe.ShoppingService.Tests.Services;

public class ShoppingOptimizationServiceStrategyTests
{
    private static readonly Guid StorePublix = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid StoreCostco = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid StoreLowesFood = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static readonly Guid ItemA = Guid.Parse("aaaa0001-0000-0000-0000-000000000000");
    private static readonly Guid ItemB = Guid.Parse("aaaa0002-0000-0000-0000-000000000000");
    private static readonly Guid ItemC = Guid.Parse("aaaa0003-0000-0000-0000-000000000000");

    private static readonly Guid ProdA = Guid.Parse("bbbb0001-0000-0000-0000-000000000000");
    private static readonly Guid ProdB = Guid.Parse("bbbb0002-0000-0000-0000-000000000000");
    private static readonly Guid ProdC = Guid.Parse("bbbb0003-0000-0000-0000-000000000000");

    // ── CheapestOverall ───────────────────────────────────────────────────────

    [Fact]
    public void CheapestOverall_AssignsEachItemToCheapestStore()
    {
        // Arrange: item A cheapest at Publix, item B cheapest at Costco
        List<ShoppingListItemDto> items = new()
        {
            new() { Id = ItemA, ProductId = ProdA, Category = "Produce" },
            new() { Id = ItemB, ProductId = ProdB, Category = "Dairy" }
        };

        Dictionary<Guid, List<ShoppingOptimizationService.StorePriceEntry>> prices = new()
        {
            [ProdA] = new()
            {
                new() { StoreId = StorePublix, Price = 2.00m },
                new() { StoreId = StoreCostco, Price = 3.50m }
            },
            [ProdB] = new()
            {
                new() { StoreId = StorePublix, Price = 4.00m },
                new() { StoreId = StoreCostco, Price = 1.50m }
            }
        };

        // Act
        Dictionary<Guid, Guid> result = ShoppingOptimizationService.ApplyCheapestOverall(items, prices);

        // Assert
        result[ItemA].Should().Be(StorePublix);
        result[ItemB].Should().Be(StoreCostco);
    }

    [Fact]
    public void CheapestOverall_SkipsItemsWithNoProductId()
    {
        List<ShoppingListItemDto> items = new()
        {
            new() { Id = ItemA, ProductId = null, CustomName = "Custom item" }
        };
        Dictionary<Guid, List<ShoppingOptimizationService.StorePriceEntry>> prices = new();

        Dictionary<Guid, Guid> result = ShoppingOptimizationService.ApplyCheapestOverall(items, prices);

        result.Should().BeEmpty();
    }

    // ── MinimizeStores ────────────────────────────────────────────────────────

    [Fact]
    public void MinimizeStores_KeepsItemAtExistingStoreWhenPenaltyMakesNewStoreExpensive()
    {
        // Item A → Publix ($2). Item C also at Publix ($2.10) and Costco ($2.00).
        // Without penalty: Costco is cheaper for C ($2.00 < $2.10).
        // With penalty ($2.50 new store): effective Costco cost = $4.50 > $2.10 → stays Publix.
        List<ShoppingListItemDto> items = new()
        {
            new() { Id = ItemA, ProductId = ProdA },
            new() { Id = ItemC, ProductId = ProdC }
        };

        Dictionary<Guid, List<ShoppingOptimizationService.StorePriceEntry>> prices = new()
        {
            [ProdA] = new()
            {
                new() { StoreId = StorePublix, Price = 2.00m }
            },
            [ProdC] = new()
            {
                new() { StoreId = StorePublix, Price = 2.10m },
                new() { StoreId = StoreCostco, Price = 2.00m }
            }
        };

        // Act
        Dictionary<Guid, Guid> result = ShoppingOptimizationService.ApplyMinimizeStores(items, prices);

        // Assert: Both items go to Publix because $2.10 < $2.00 + $2.50 penalty
        result[ItemA].Should().Be(StorePublix);
        result[ItemC].Should().Be(StorePublix);
    }

    [Fact]
    public void MinimizeStores_OpensNewStoreWhenOnlyStoreCarryingItem()
    {
        // Item A → Publix. Item C → Costco only (no Publix price).
        // Opening Costco is required even with penalty.
        List<ShoppingListItemDto> items = new()
        {
            new() { Id = ItemA, ProductId = ProdA },
            new() { Id = ItemC, ProductId = ProdC }
        };

        Dictionary<Guid, List<ShoppingOptimizationService.StorePriceEntry>> prices = new()
        {
            [ProdA] = new()
            {
                new() { StoreId = StorePublix, Price = 2.00m }
            },
            [ProdC] = new()
            {
                new() { StoreId = StoreCostco, Price = 1.00m }
            }
        };

        Dictionary<Guid, Guid> result = ShoppingOptimizationService.ApplyMinimizeStores(items, prices);

        result[ItemA].Should().Be(StorePublix);
        result[ItemC].Should().Be(StoreCostco);
    }

    // ── PreferredStorePerCategory ─────────────────────────────────────────────

    [Fact]
    public void PreferredStorePerCategory_AssignsToPreferredStoreWhenPriceAvailable()
    {
        // Category=Produce preference=LowesFood; item has LowesFood price → assigned to LowesFood
        List<ShoppingListItemDto> items = new()
        {
            new() { Id = ItemA, ProductId = ProdA, Category = "Produce" }
        };

        Dictionary<Guid, List<ShoppingOptimizationService.StorePriceEntry>> prices = new()
        {
            [ProdA] = new()
            {
                new() { StoreId = StoreLowesFood, Price = 2.50m },
                new() { StoreId = StorePublix, Price = 2.00m }
            }
        };

        List<UserStoreCategoryPreferenceDto> prefs = new()
        {
            new() { Category = "Produce", PreferredStoreId = StoreLowesFood, RankOrder = 1 }
        };

        Dictionary<Guid, Guid> result = ShoppingOptimizationService.ApplyPreferredStorePerCategory(items, prices, prefs);

        // Should be LowesFood even though it's not the cheapest
        result[ItemA].Should().Be(StoreLowesFood);
    }

    [Fact]
    public void PreferredStorePerCategory_FallsBackToRankOrder2WhenRankOrder1HasNoPrice()
    {
        // RankOrder=1 store has no price data → fall back to RankOrder=2
        List<ShoppingListItemDto> items = new()
        {
            new() { Id = ItemA, ProductId = ProdA, Category = "Produce" }
        };

        Dictionary<Guid, List<ShoppingOptimizationService.StorePriceEntry>> prices = new()
        {
            [ProdA] = new()
            {
                // Only Publix has price data (RankOrder=2 store)
                new() { StoreId = StorePublix, Price = 2.00m }
            }
        };

        List<UserStoreCategoryPreferenceDto> prefs = new()
        {
            new() { Category = "Produce", PreferredStoreId = StoreLowesFood, RankOrder = 1 },
            new() { Category = "Produce", PreferredStoreId = StorePublix, RankOrder = 2 }
        };

        Dictionary<Guid, Guid> result = ShoppingOptimizationService.ApplyPreferredStorePerCategory(items, prices, prefs);

        // LowesFood (RankOrder=1) has no price data → fall back to Publix (RankOrder=2)
        result[ItemA].Should().Be(StorePublix);
    }

    [Fact]
    public void PreferredStorePerCategory_FallsBackToCheapestWhenNoPreferenceOrPrice()
    {
        // No category preference → falls back to cheapest
        List<ShoppingListItemDto> items = new()
        {
            new() { Id = ItemA, ProductId = ProdA, Category = "Bakery" }
        };

        Dictionary<Guid, List<ShoppingOptimizationService.StorePriceEntry>> prices = new()
        {
            [ProdA] = new()
            {
                new() { StoreId = StoreCostco, Price = 1.00m },
                new() { StoreId = StorePublix, Price = 2.00m }
            }
        };

        List<UserStoreCategoryPreferenceDto> prefs = new()
        {
            // No preference for "Bakery"
            new() { Category = "Produce", PreferredStoreId = StoreLowesFood, RankOrder = 1 }
        };

        Dictionary<Guid, Guid> result = ShoppingOptimizationService.ApplyPreferredStorePerCategory(items, prices, prefs);

        result[ItemA].Should().Be(StoreCostco);
    }

    // ── Hybrid ────────────────────────────────────────────────────────────────

    [Fact]
    public void Hybrid_UsesPreferredStorePerCategoryFirst_ThenCheapest()
    {
        // Item A has category=Produce pref=Publix AND price at Publix → uses Publix (preferred)
        // Item B has no category pref → uses cheapest (Costco)
        List<ShoppingListItemDto> items = new()
        {
            new() { Id = ItemA, ProductId = ProdA, Category = "Produce" },
            new() { Id = ItemB, ProductId = ProdB, Category = "Dairy" }
        };

        Dictionary<Guid, List<ShoppingOptimizationService.StorePriceEntry>> prices = new()
        {
            [ProdA] = new()
            {
                new() { StoreId = StorePublix, Price = 3.00m },
                new() { StoreId = StoreCostco, Price = 2.00m }
            },
            [ProdB] = new()
            {
                new() { StoreId = StorePublix, Price = 4.00m },
                new() { StoreId = StoreCostco, Price = 1.50m }
            }
        };

        List<UserStoreCategoryPreferenceDto> prefs = new()
        {
            new() { Category = "Produce", PreferredStoreId = StorePublix, RankOrder = 1 }
        };

        Dictionary<Guid, Guid> result = ShoppingOptimizationService.ApplyHybrid(items, prices, prefs);

        result[ItemA].Should().Be(StorePublix);  // preferred store despite higher price
        result[ItemB].Should().Be(StoreCostco);  // cheapest (no category pref)
    }

    // ── SingleStore ───────────────────────────────────────────────────────────

    [Fact]
    public void SingleStore_AssignsAllItemsToFirstPreferredStore()
    {
        List<ShoppingListItemDto> items = new()
        {
            new() { Id = ItemA },
            new() { Id = ItemB }
        };

        List<UserStoreCategoryPreferenceDto> prefs = new()
        {
            new() { Category = "Produce", PreferredStoreId = StorePublix, RankOrder = 1 }
        };

        Dictionary<Guid, Guid> result = ShoppingOptimizationService.ApplySingleStore(items, prefs);

        result[ItemA].Should().Be(StorePublix);
        result[ItemB].Should().Be(StorePublix);
    }

    [Fact]
    public void SingleStore_ReturnsEmptyWhenNoPreferences()
    {
        List<ShoppingListItemDto> items = new()
        {
            new() { Id = ItemA }
        };

        Dictionary<Guid, Guid> result = ShoppingOptimizationService.ApplySingleStore(items, new List<UserStoreCategoryPreferenceDto>());

        result.Should().BeEmpty();
    }
}
