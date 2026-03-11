using FluentAssertions;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Net;
using System.Text.Json;
using ExpressRecipe.MealPlanningService.Services;

namespace ExpressRecipe.MealPlanningService.Tests.Services;

/// <summary>
/// Unit tests for <see cref="PantryDiscoveryService"/>.
/// Uses an in-process <see cref="HybridCache"/> and fake HTTP handlers to avoid real I/O.
/// </summary>
public class PantryDiscoveryServiceTests
{
    // ── helpers ───────────────────────────────────────────────────────────────

    private static HybridCache CreateHybridCache()
    {
#pragma warning disable EXTEXP0018
        ServiceCollection services = new();
        services.AddHybridCache();
#pragma warning restore EXTEXP0018
        return services.BuildServiceProvider().GetRequiredService<HybridCache>();
    }

    private static HttpClient BuildHttpClient(
        string baseAddress,
        Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        DiscoveryFakeHandler handler = new(responder);
        return new HttpClient(handler) { BaseAddress = new Uri(baseAddress) };
    }

    private PantryDiscoveryService CreateService(
        Func<HttpRequestMessage, HttpResponseMessage>? inventoryHandler = null,
        Func<HttpRequestMessage, HttpResponseMessage>? recipeHandler = null,
        Func<HttpRequestMessage, HttpResponseMessage>? userHandler = null)
    {
        HybridCache cache = CreateHybridCache();
        Mock<IHttpClientFactory> factoryMock = new();

        factoryMock.Setup(f => f.CreateClient("InventoryService"))
            .Returns(BuildHttpClient("http://inventory",
                inventoryHandler ?? (_ => JsonResponse(new List<PantryIngredientItem>()))));

        factoryMock.Setup(f => f.CreateClient("RecipeService"))
            .Returns(BuildHttpClient("http://recipe",
                recipeHandler ?? (_ => JsonResponse(new List<RecipeIngredientSummary>()))));

        factoryMock.Setup(f => f.CreateClient("UserService"))
            .Returns(BuildHttpClient("http://user",
                userHandler ?? (_ => JsonResponse(new List<string>()))));

        return new PantryDiscoveryService(
            cache,
            factoryMock.Object,
            NullLogger<PantryDiscoveryService>.Instance);
    }

    private static HttpResponseMessage JsonResponse<T>(T value, HttpStatusCode status = HttpStatusCode.OK)
    {
        string json = JsonSerializer.Serialize(value);
        HttpResponseMessage msg = new(status)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };
        return msg;
    }

    // ── tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DiscoverAsync_EmptyPantry_ReturnsEmptyResultWithZeroIngredients()
    {
        PantryDiscoveryService svc = CreateService(
            inventoryHandler: _ => JsonResponse(new List<PantryIngredientItem>()));

        PantryDiscoveryResult result = await svc.DiscoverAsync(
            Guid.NewGuid(), Guid.NewGuid(), new PantryDiscoveryOptions());

        result.Matches.Should().BeEmpty();
        result.TotalPantryIngredients.Should().Be(0);
    }

    [Fact]
    public async Task DiscoverAsync_AllIngredientsMatch_IncludesRecipe()
    {
        Guid recipeId = Guid.NewGuid();
        List<PantryIngredientItem> pantry = new()
        {
            new() { InventoryItemId = Guid.NewGuid(), NormalizedName = "chicken", DisplayName = "Chicken" },
            new() { InventoryItemId = Guid.NewGuid(), NormalizedName = "garlic",  DisplayName = "Garlic" }
        };
        List<RecipeIngredientSummary> recipes = new()
        {
            new()
            {
                RecipeId        = recipeId,
                RecipeName      = "Garlic Chicken",
                CookTimeMinutes = 30,
                AverageRating   = 4.5m,
                Ingredients     = new()
                {
                    new() { NormalizedName = "chicken", DisplayName = "Chicken" },
                    new() { NormalizedName = "garlic",  DisplayName = "Garlic" }
                }
            }
        };

        PantryDiscoveryService svc = CreateService(
            inventoryHandler: _ => JsonResponse(pantry),
            recipeHandler:    _ => JsonResponse(recipes));

        PantryDiscoveryResult result = await svc.DiscoverAsync(
            Guid.NewGuid(), Guid.NewGuid(), new PantryDiscoveryOptions { MinMatchPercent = 0.80m });

        result.Matches.Should().HaveCount(1);
        PantryRecipeMatch match = result.Matches[0];
        match.RecipeId.Should().Be(recipeId);
        match.MatchPercent.Should().Be(1.0m);
        match.MissingIngredients.Should().BeEmpty();
        match.HasDietaryConflict.Should().BeFalse();
        result.TotalPantryIngredients.Should().Be(2);
    }

    [Fact]
    public async Task DiscoverAsync_PartialMatch_ExcludedByMinMatchPercent()
    {
        List<PantryIngredientItem> pantry = new()
        {
            new() { InventoryItemId = Guid.NewGuid(), NormalizedName = "chicken", DisplayName = "Chicken" }
        };
        List<RecipeIngredientSummary> recipes = new()
        {
            new()
            {
                RecipeId    = Guid.NewGuid(),
                RecipeName  = "Chicken Stew",
                Ingredients = new()
                {
                    new() { NormalizedName = "chicken",   DisplayName = "Chicken" },
                    new() { NormalizedName = "potato",    DisplayName = "Potato" },
                    new() { NormalizedName = "carrot",    DisplayName = "Carrot" },
                    new() { NormalizedName = "celery",    DisplayName = "Celery" },
                    new() { NormalizedName = "onion",     DisplayName = "Onion" },
                    new() { NormalizedName = "thyme",     DisplayName = "Thyme" },
                    new() { NormalizedName = "broth",     DisplayName = "Broth" },
                    new() { NormalizedName = "butter",    DisplayName = "Butter" },
                    new() { NormalizedName = "bay leaf",  DisplayName = "Bay Leaf" },
                    new() { NormalizedName = "parsley",   DisplayName = "Parsley" }
                }
            }
        };

        PantryDiscoveryService svc = CreateService(
            inventoryHandler: _ => JsonResponse(pantry),
            recipeHandler:    _ => JsonResponse(recipes));

        PantryDiscoveryResult result = await svc.DiscoverAsync(
            Guid.NewGuid(), Guid.NewGuid(), new PantryDiscoveryOptions { MinMatchPercent = 0.80m });

        result.Matches.Should().BeEmpty("recipe match is 10% which is below 80% threshold");
    }

    [Fact]
    public async Task DiscoverAsync_PartialMatch_IncludedWhenThresholdLowered()
    {
        List<PantryIngredientItem> pantry = new()
        {
            new() { InventoryItemId = Guid.NewGuid(), NormalizedName = "chicken", DisplayName = "Chicken" },
            new() { InventoryItemId = Guid.NewGuid(), NormalizedName = "garlic",  DisplayName = "Garlic" },
            new() { InventoryItemId = Guid.NewGuid(), NormalizedName = "onion",   DisplayName = "Onion" },
            new() { InventoryItemId = Guid.NewGuid(), NormalizedName = "thyme",   DisplayName = "Thyme" },
            new() { InventoryItemId = Guid.NewGuid(), NormalizedName = "salt",    DisplayName = "Salt" },
            new() { InventoryItemId = Guid.NewGuid(), NormalizedName = "pepper",  DisplayName = "Pepper" },
            new() { InventoryItemId = Guid.NewGuid(), NormalizedName = "broth",   DisplayName = "Broth" }
        };
        List<RecipeIngredientSummary> recipes = new()
        {
            new()
            {
                RecipeId    = Guid.NewGuid(),
                RecipeName  = "Chicken Stew",
                Ingredients = new()
                {
                    new() { NormalizedName = "chicken", DisplayName = "Chicken" },
                    new() { NormalizedName = "garlic",  DisplayName = "Garlic" },
                    new() { NormalizedName = "onion",   DisplayName = "Onion" },
                    new() { NormalizedName = "thyme",   DisplayName = "Thyme" },
                    new() { NormalizedName = "salt",    DisplayName = "Salt" },
                    new() { NormalizedName = "pepper",  DisplayName = "Pepper" },
                    new() { NormalizedName = "broth",   DisplayName = "Broth" },
                    new() { NormalizedName = "potato",  DisplayName = "Potato" },
                    new() { NormalizedName = "carrot",  DisplayName = "Carrot" },
                    new() { NormalizedName = "celery",  DisplayName = "Celery" }
                }
            }
        };

        PantryDiscoveryService svc = CreateService(
            inventoryHandler: _ => JsonResponse(pantry),
            recipeHandler:    _ => JsonResponse(recipes));

        PantryDiscoveryResult result = await svc.DiscoverAsync(
            Guid.NewGuid(), Guid.NewGuid(), new PantryDiscoveryOptions { MinMatchPercent = 0.70m });

        result.Matches.Should().HaveCount(1, "7 of 10 ingredients match = 70% which meets threshold");
        result.Matches[0].MatchPercent.Should().Be(0.70m);
        result.Matches[0].MissingIngredients.Should().HaveCount(3);
    }

    [Fact]
    public async Task DiscoverAsync_AllergenConflict_ExcludesConflictingRecipe()
    {
        Guid userId = Guid.NewGuid();
        List<PantryIngredientItem> pantry = new()
        {
            new() { InventoryItemId = Guid.NewGuid(), NormalizedName = "peanuts", DisplayName = "Peanuts" }
        };
        List<RecipeIngredientSummary> recipes = new()
        {
            new()
            {
                RecipeId    = Guid.NewGuid(),
                RecipeName  = "Peanut Butter Cookies",
                Ingredients = new()
                {
                    new() { NormalizedName = "peanuts", DisplayName = "Peanuts" },
                    new() { NormalizedName = "flour",   DisplayName = "Flour" },
                    new() { NormalizedName = "sugar",   DisplayName = "Sugar" }
                }
            }
        };

        PantryDiscoveryService svc = CreateService(
            inventoryHandler: _ => JsonResponse(pantry),
            recipeHandler:    _ => JsonResponse(recipes),
            userHandler:      _ => JsonResponse(new List<string> { "peanuts" }));

        PantryDiscoveryResult result = await svc.DiscoverAsync(
            Guid.NewGuid(), userId,
            new PantryDiscoveryOptions { MinMatchPercent = 0.30m, RespectDietaryRestrictions = true });

        result.Matches.Should().BeEmpty("recipe contains a known allergen and should be excluded");
    }

    [Fact]
    public async Task DiscoverAsync_AllergenConflict_NonConflictingRecipeStillIncluded()
    {
        Guid userId = Guid.NewGuid();
        Guid safeRecipeId = Guid.NewGuid();
        List<PantryIngredientItem> pantry = new()
        {
            new() { InventoryItemId = Guid.NewGuid(), NormalizedName = "chicken", DisplayName = "Chicken" }
        };
        List<RecipeIngredientSummary> recipes = new()
        {
            new()
            {
                RecipeId    = Guid.NewGuid(),
                RecipeName  = "Peanut Butter Cookies",
                Ingredients = new()
                {
                    new() { NormalizedName = "peanuts", DisplayName = "Peanuts" },
                    new() { NormalizedName = "chicken",  DisplayName = "Chicken" }
                }
            },
            new()
            {
                RecipeId    = safeRecipeId,
                RecipeName  = "Grilled Chicken",
                Ingredients = new()
                {
                    new() { NormalizedName = "chicken", DisplayName = "Chicken" }
                }
            }
        };

        PantryDiscoveryService svc = CreateService(
            inventoryHandler: _ => JsonResponse(pantry),
            recipeHandler:    _ => JsonResponse(recipes),
            userHandler:      _ => JsonResponse(new List<string> { "peanuts" }));

        PantryDiscoveryResult result = await svc.DiscoverAsync(
            Guid.NewGuid(), userId,
            new PantryDiscoveryOptions { MinMatchPercent = 0.50m, RespectDietaryRestrictions = true });

        result.Matches.Should().HaveCount(1, "only the allergen-free recipe should be included");
        result.Matches[0].RecipeId.Should().Be(safeRecipeId);
    }

    [Fact]
    public async Task DiscoverAsync_SortByCookTime_OrdersAscending()
    {
        Guid recipeId1 = Guid.NewGuid();
        Guid recipeId2 = Guid.NewGuid();
        List<PantryIngredientItem> pantry = new()
        {
            new() { InventoryItemId = Guid.NewGuid(), NormalizedName = "egg", DisplayName = "Egg" }
        };
        List<RecipeIngredientSummary> recipes = new()
        {
            new() { RecipeId = recipeId1, RecipeName = "Slow Roast", CookTimeMinutes = 120,
                Ingredients = new() { new() { NormalizedName = "egg", DisplayName = "Egg" } } },
            new() { RecipeId = recipeId2, RecipeName = "Scrambled Eggs", CookTimeMinutes = 5,
                Ingredients = new() { new() { NormalizedName = "egg", DisplayName = "Egg" } } }
        };

        PantryDiscoveryService svc = CreateService(
            inventoryHandler: _ => JsonResponse(pantry),
            recipeHandler:    _ => JsonResponse(recipes));

        PantryDiscoveryResult result = await svc.DiscoverAsync(
            Guid.NewGuid(), Guid.NewGuid(), new PantryDiscoveryOptions { MinMatchPercent = 1.0m, SortBy = "cookTime" });

        result.Matches.Should().HaveCount(2);
        result.Matches[0].RecipeId.Should().Be(recipeId2, "5-minute recipe should come first");
        result.Matches[1].RecipeId.Should().Be(recipeId1);
    }

    [Fact]
    public async Task DiscoverAsync_SortByRating_OrdersDescending()
    {
        Guid recipeId1 = Guid.NewGuid();
        Guid recipeId2 = Guid.NewGuid();
        List<PantryIngredientItem> pantry = new()
        {
            new() { InventoryItemId = Guid.NewGuid(), NormalizedName = "egg", DisplayName = "Egg" }
        };
        List<RecipeIngredientSummary> recipes = new()
        {
            new() { RecipeId = recipeId1, RecipeName = "Recipe A", AverageRating = 3.5m,
                Ingredients = new() { new() { NormalizedName = "egg", DisplayName = "Egg" } } },
            new() { RecipeId = recipeId2, RecipeName = "Recipe B", AverageRating = 4.8m,
                Ingredients = new() { new() { NormalizedName = "egg", DisplayName = "Egg" } } }
        };

        PantryDiscoveryService svc = CreateService(
            inventoryHandler: _ => JsonResponse(pantry),
            recipeHandler:    _ => JsonResponse(recipes));

        PantryDiscoveryResult result = await svc.DiscoverAsync(
            Guid.NewGuid(), Guid.NewGuid(), new PantryDiscoveryOptions { MinMatchPercent = 1.0m, SortBy = "rating" });

        result.Matches.Should().HaveCount(2);
        result.Matches[0].RecipeId.Should().Be(recipeId2, "higher-rated recipe should come first");
    }

    [Fact]
    public async Task DiscoverAsync_LimitApplied_ReturnsNoMoreThanLimit()
    {
        List<PantryIngredientItem> pantry = new()
        {
            new() { InventoryItemId = Guid.NewGuid(), NormalizedName = "egg", DisplayName = "Egg" }
        };

        List<RecipeIngredientSummary> recipes = Enumerable.Range(1, 20)
            .Select(i => new RecipeIngredientSummary
            {
                RecipeId   = Guid.NewGuid(),
                RecipeName = $"Recipe {i}",
                Ingredients = new() { new() { NormalizedName = "egg", DisplayName = "Egg" } }
            })
            .ToList();

        PantryDiscoveryService svc = CreateService(
            inventoryHandler: _ => JsonResponse(pantry),
            recipeHandler:    _ => JsonResponse(recipes));

        PantryDiscoveryResult result = await svc.DiscoverAsync(
            Guid.NewGuid(), Guid.NewGuid(), new PantryDiscoveryOptions { MinMatchPercent = 1.0m, Limit = 5 });

        result.Matches.Should().HaveCount(5);
    }

    [Fact]
    public async Task DiscoverAsync_InventoryServiceDown_ReturnsEmptyResultGracefully()
    {
        PantryDiscoveryService svc = CreateService(
            inventoryHandler: _ => throw new HttpRequestException("Connection refused"));

        Func<Task> act = () => svc.DiscoverAsync(Guid.NewGuid(), Guid.NewGuid(), new PantryDiscoveryOptions());
        await act.Should().NotThrowAsync("service should degrade gracefully");

        PantryDiscoveryResult result = await svc.DiscoverAsync(
            Guid.NewGuid(), Guid.NewGuid(), new PantryDiscoveryOptions());
        result.Matches.Should().BeEmpty();
    }

    [Fact]
    public async Task DiscoverAsync_RecipeServiceDown_ReturnsEmptyMatchesWithPantryCount()
    {
        List<PantryIngredientItem> pantry = new()
        {
            new() { InventoryItemId = Guid.NewGuid(), NormalizedName = "egg", DisplayName = "Egg" }
        };

        PantryDiscoveryService svc = CreateService(
            inventoryHandler: _ => JsonResponse(pantry),
            recipeHandler:    _ => throw new HttpRequestException("Connection refused"));

        PantryDiscoveryResult result = await svc.DiscoverAsync(
            Guid.NewGuid(), Guid.NewGuid(), new PantryDiscoveryOptions());

        result.Matches.Should().BeEmpty();
        result.TotalPantryIngredients.Should().Be(1);
    }
}

// ── Fake HTTP message handler ─────────────────────────────────────────────────

internal sealed class DiscoveryFakeHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

    public DiscoveryFakeHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        => _responder = responder;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(_responder(request));
}
