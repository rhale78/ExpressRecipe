using ExpressRecipe.RecipeService.Parsers;
using ExpressRecipe.RecipeService.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace ExpressRecipe.RecipeService.Tests.Services;

/// <summary>
/// Tests for AllergenDetectionService – specifically the keyword-based detection path
/// (DetectByKeywords is called as fallback when database is unavailable).
/// We test via DetectAllergensAsync with an invalid connection string which forces
/// the database call to fail and falls through to keyword matching.
/// </summary>
public class AllergenDetectionServiceTests
{
    private readonly AllergenDetectionService _service;

    public AllergenDetectionServiceTests()
    {
        // Use an invalid connection string with minimum timeout.
        // The DB call will fail quickly, triggering keyword-based fallback detection.
        // Note: ideally AllergenDetectionService would accept an injectable DB abstraction;
        // for now this exercises the keyword fallback path reliably.
        _service = new AllergenDetectionService(
            "Server=localhost;Database=NonExistent;Trusted_Connection=True;Connect Timeout=1;",
            NullLogger<AllergenDetectionService>.Instance);
    }

    private static ParsedRecipe RecipeWith(params string[] ingredientNames)
    {
        var recipe = new ParsedRecipe { Name = "Test Recipe" };
        foreach (var name in ingredientNames)
        {
            recipe.Ingredients.Add(new ParsedIngredient { IngredientName = name });
        }
        return recipe;
    }

    // ── Empty / Null ──────────────────────────────────────────────────────────

    [Fact]
    public async Task DetectAllergens_EmptyIngredientList_ReturnsEmptyList()
    {
        var recipe = new ParsedRecipe { Name = "Empty Recipe" };

        var result = await _service.DetectAllergensAsync(recipe);

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
        var recipe = RecipeWith(ingredient);

        var result = await _service.DetectAllergensAsync(recipe);

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
        var recipe = RecipeWith(ingredient);

        var result = await _service.DetectAllergensAsync(recipe);

        result.Should().Contain(a => a.AllergenName == "Eggs",
            $"ingredient '{ingredient}' should trigger Eggs allergen");
    }

    // ── Peanuts ───────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("peanut butter")]
    [InlineData("crushed peanuts")]
    public async Task DetectAllergens_PeanutIngredient_DetectsPeanutsAllergen(string ingredient)
    {
        var recipe = RecipeWith(ingredient);

        var result = await _service.DetectAllergensAsync(recipe);

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
        var recipe = RecipeWith(ingredient);

        var result = await _service.DetectAllergensAsync(recipe);

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
        var recipe = RecipeWith(ingredient);

        var result = await _service.DetectAllergensAsync(recipe);

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
        var recipe = RecipeWith(ingredient);

        var result = await _service.DetectAllergensAsync(recipe);

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
        var recipe = RecipeWith(ingredient);

        var result = await _service.DetectAllergensAsync(recipe);

        result.Should().Contain(a => a.AllergenName == "Shellfish",
            $"ingredient '{ingredient}' should trigger Shellfish allergen");
    }

    // ── Sesame ────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("sesame seeds")]
    [InlineData("tahini paste")]
    public async Task DetectAllergens_SesameIngredient_DetectsSesameAllergen(string ingredient)
    {
        var recipe = RecipeWith(ingredient);

        var result = await _service.DetectAllergensAsync(recipe);

        result.Should().Contain(a => a.AllergenName == "Sesame",
            $"ingredient '{ingredient}' should trigger Sesame allergen");
    }

    // ── No Duplicates ─────────────────────────────────────────────────────────

    [Fact]
    public async Task DetectAllergens_SameAllergenMultipleIngredients_NoDuplicates()
    {
        var recipe = RecipeWith("whole milk", "cheddar cheese", "cream cheese");

        var result = await _service.DetectAllergensAsync(recipe);

        var milkAllergens = result.Where(a => a.AllergenName == "Milk").ToList();
        milkAllergens.Should().HaveCount(1, "the same allergen should not appear twice");
    }

    // ── Safe Ingredients ──────────────────────────────────────────────────────

    [Fact]
    public async Task DetectAllergens_SafeIngredients_ReturnsEmptyOrNonAllergenWarnings()
    {
        var recipe = RecipeWith("salt", "black pepper", "olive oil", "garlic");

        var result = await _service.DetectAllergensAsync(recipe);

        result.Should().NotContain(a =>
            a.AllergenName == "Milk" || a.AllergenName == "Eggs" || a.AllergenName == "Peanuts",
            "basic spices and oil should not trigger major allergens");
    }

    // ── Multiple Allergens ────────────────────────────────────────────────────

    [Fact]
    public async Task DetectAllergens_MultipleAllergenicIngredients_DetectsAll()
    {
        var recipe = RecipeWith("butter", "eggs", "wheat flour", "peanut butter");

        var result = await _service.DetectAllergensAsync(recipe);

        result.Should().Contain(a => a.AllergenName == "Milk");
        result.Should().Contain(a => a.AllergenName == "Eggs");
        result.Should().Contain(a => a.AllergenName == "Peanuts");
        result.Should().Contain(a => a.AllergenName == "Wheat" || a.AllergenName == "Gluten");
    }
}
