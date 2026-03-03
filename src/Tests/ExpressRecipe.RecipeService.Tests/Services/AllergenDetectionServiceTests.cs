using ExpressRecipe.RecipeService.Data;
using ExpressRecipe.RecipeService.Parsers;
using ExpressRecipe.RecipeService.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ExpressRecipe.RecipeService.Tests.Services;

/// <summary>
/// Tests for AllergenDetectionService – covers keyword-based fallback detection
/// (exercised via a mock IAllergenRepository that returns empty results)
/// and static helper methods.
/// </summary>
public class AllergenDetectionServiceTests
{
    /// <summary>
    /// Build a service whose repository mock returns an empty list,
    /// forcing the keyword-based fallback path.
    /// </summary>
    private static AllergenDetectionService CreateServiceWithNoDbMatch()
    {
        var repoMock = new Mock<IAllergenRepository>();
        repoMock.Setup(r => r.FindAllergensByIngredientNameAsync(It.IsAny<string>()))
                .ReturnsAsync([]);
        repoMock.Setup(r => r.GetAllKnownAllergensAsync())
                .ReturnsAsync([]);
        return new AllergenDetectionService(repoMock.Object, NullLogger<AllergenDetectionService>.Instance);
    }

    /// <summary>
    /// Build a recipe with the given ingredient names.
    /// </summary>
    private static ParsedRecipe RecipeWith(params string[] ingredientNames)
    {
        var recipe = new ParsedRecipe { Name = "Test Recipe" };
        foreach (var name in ingredientNames)
            recipe.Ingredients.Add(new ParsedIngredient { IngredientName = name });
        return recipe;
    }

    // ── Empty / Null ──────────────────────────────────────────────────────────

    [Fact]
    public async Task DetectAllergens_EmptyIngredientList_ReturnsEmptyList()
    {
        var service = CreateServiceWithNoDbMatch();
        var recipe = new ParsedRecipe { Name = "Empty Recipe" };

        var result = await service.DetectAllergensAsync(recipe);

        result.Should().BeEmpty();
    }

    // ── Dairy ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("whole milk")]
    [InlineData("heavy cream")]
    [InlineData("unsalted butter")]
    [InlineData("cheddar cheese")]
    [InlineData("plain yogurt")]
    public async Task DetectAllergens_DairyIngredient_DetectsMilkAllergen(string ingredient)
    {
        var service = CreateServiceWithNoDbMatch();
        var recipe = RecipeWith(ingredient);

        var result = await service.DetectAllergensAsync(recipe);

        result.Should().Contain(a => a.AllergenName == "Milk",
            $"ingredient '{ingredient}' should trigger Milk allergen");
    }

    // ── Eggs ──────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("2 large eggs")]
    [InlineData("1 egg white")]
    [InlineData("mayonnaise")]
    public async Task DetectAllergens_EggIngredient_DetectsEggsAllergen(string ingredient)
    {
        var service = CreateServiceWithNoDbMatch();
        var recipe = RecipeWith(ingredient);

        var result = await service.DetectAllergensAsync(recipe);

        result.Should().Contain(a => a.AllergenName == "Eggs",
            $"ingredient '{ingredient}' should trigger Eggs allergen");
    }

    // ── Peanuts ───────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("peanut butter")]
    [InlineData("crushed peanuts")]
    public async Task DetectAllergens_PeanutIngredient_DetectsPeanutsAllergen(string ingredient)
    {
        var service = CreateServiceWithNoDbMatch();
        var recipe = RecipeWith(ingredient);

        var result = await service.DetectAllergensAsync(recipe);

        result.Should().Contain(a => a.AllergenName == "Peanuts",
            $"ingredient '{ingredient}' should trigger Peanuts allergen");
    }

    // ── Tree Nuts ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("sliced almonds")]
    [InlineData("walnut halves")]
    [InlineData("cashew pieces")]
    public async Task DetectAllergens_TreeNutIngredient_DetectsTreeNutsAllergen(string ingredient)
    {
        var service = CreateServiceWithNoDbMatch();
        var recipe = RecipeWith(ingredient);

        var result = await service.DetectAllergensAsync(recipe);

        result.Should().Contain(a => a.AllergenName == "Tree Nuts",
            $"ingredient '{ingredient}' should trigger Tree Nuts allergen");
    }

    // ── Wheat / Gluten ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("all-purpose wheat flour")]
    [InlineData("whole grain bread")]
    [InlineData("pasta noodles")]
    public async Task DetectAllergens_WheatIngredient_DetectsWheatAllergen(string ingredient)
    {
        var service = CreateServiceWithNoDbMatch();
        var recipe = RecipeWith(ingredient);

        var result = await service.DetectAllergensAsync(recipe);

        result.Should().Contain(a => a.AllergenName == "Wheat" || a.AllergenName == "Gluten",
            $"ingredient '{ingredient}' should trigger Wheat or Gluten allergen");
    }

    // ── Soy ───────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("soy sauce")]
    [InlineData("firm tofu")]
    [InlineData("edamame")]
    public async Task DetectAllergens_SoyIngredient_DetectsSoyAllergen(string ingredient)
    {
        var service = CreateServiceWithNoDbMatch();
        var recipe = RecipeWith(ingredient);

        var result = await service.DetectAllergensAsync(recipe);

        result.Should().Contain(a => a.AllergenName == "Soybeans",
            $"ingredient '{ingredient}' should trigger Soybeans allergen");
    }

    // ── Shellfish ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("peeled shrimp")]
    [InlineData("dungeness crab")]
    [InlineData("lobster tail")]
    public async Task DetectAllergens_ShellfishIngredient_DetectsShellfishAllergen(string ingredient)
    {
        var service = CreateServiceWithNoDbMatch();
        var recipe = RecipeWith(ingredient);

        var result = await service.DetectAllergensAsync(recipe);

        result.Should().Contain(a => a.AllergenName == "Shellfish",
            $"ingredient '{ingredient}' should trigger Shellfish allergen");
    }

    // ── Sesame ────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("sesame seeds")]
    [InlineData("tahini paste")]
    public async Task DetectAllergens_SesameIngredient_DetectsSesameAllergen(string ingredient)
    {
        var service = CreateServiceWithNoDbMatch();
        var recipe = RecipeWith(ingredient);

        var result = await service.DetectAllergensAsync(recipe);

        result.Should().Contain(a => a.AllergenName == "Sesame",
            $"ingredient '{ingredient}' should trigger Sesame allergen");
    }

    // ── No Duplicates ─────────────────────────────────────────────────────────

    [Fact]
    public async Task DetectAllergens_SameAllergenMultipleIngredients_NoDuplicates()
    {
        var service = CreateServiceWithNoDbMatch();
        var recipe = RecipeWith("whole milk", "cheddar cheese", "cream cheese");

        var result = await service.DetectAllergensAsync(recipe);

        var milkAllergens = result.Where(a => a.AllergenName == "Milk").ToList();
        milkAllergens.Should().HaveCount(1, "the same allergen should not appear twice");
    }

    // ── Safe Ingredients ──────────────────────────────────────────────────────

    [Fact]
    public async Task DetectAllergens_SafeIngredients_ReturnsNoMajorAllergens()
    {
        var service = CreateServiceWithNoDbMatch();
        var recipe = RecipeWith("salt", "black pepper", "olive oil", "garlic");

        var result = await service.DetectAllergensAsync(recipe);

        result.Should().NotContain(a =>
            a.AllergenName == "Milk" || a.AllergenName == "Eggs" || a.AllergenName == "Peanuts",
            "basic spices and oil should not trigger major allergens");
    }

    // ── Multiple Allergens ────────────────────────────────────────────────────

    [Fact]
    public async Task DetectAllergens_MultipleAllergenicIngredients_DetectsAll()
    {
        var service = CreateServiceWithNoDbMatch();
        var recipe = RecipeWith("butter", "eggs", "wheat flour", "peanut butter");

        var result = await service.DetectAllergensAsync(recipe);

        result.Should().Contain(a => a.AllergenName == "Milk");
        result.Should().Contain(a => a.AllergenName == "Eggs");
        result.Should().Contain(a => a.AllergenName == "Peanuts");
        result.Should().Contain(a => a.AllergenName == "Wheat" || a.AllergenName == "Gluten");
    }

    // ── DB match overrides keyword detection ─────────────────────────────────

    [Fact]
    public async Task DetectAllergens_WhenDbReturnsMatch_UsesDbResultsNotKeywords()
    {
        var dbAllergenId = Guid.NewGuid();
        var repoMock = new Mock<IAllergenRepository>();
        repoMock.Setup(r => r.FindAllergensByIngredientNameAsync("whole milk"))
                .ReturnsAsync([(dbAllergenId, "Dairy")]);

        var service = new AllergenDetectionService(
            repoMock.Object, NullLogger<AllergenDetectionService>.Instance);

        var recipe = RecipeWith("whole milk");
        var result = await service.DetectAllergensAsync(recipe);

        result.Should().ContainSingle();
        result[0].AllergenId.Should().Be(dbAllergenId);
        result[0].AllergenName.Should().Be("Dairy");
    }

    // ── DetectByKeywords static helper ────────────────────────────────────────

    [Theory]
    [InlineData("almond milk", "Tree Nuts")]
    [InlineData("cheddar cheese", "Milk")]
    [InlineData("scrambled eggs", "Eggs")]
    [InlineData("soy sauce", "Soybeans")]
    public void DetectByKeywords_KnownIngredient_FindsExpectedAllergen(string ingredient, string expectedAllergen)
    {
        var result = AllergenDetectionService.DetectByKeywords(ingredient);

        result.Should().Contain(r => r.AllergenName == expectedAllergen,
            $"'{ingredient}' should match allergen '{expectedAllergen}'");
    }

    [Fact]
    public void DetectByKeywords_SafeIngredient_ReturnsEmpty()
    {
        var result = AllergenDetectionService.DetectByKeywords("salt");

        result.Should().BeEmpty();
    }
}
