using ExpressRecipe.RecipeParser.Models;
using ExpressRecipe.RecipeParser.Parsers;
using FluentAssertions;

namespace ExpressRecipe.RecipeParser.Tests.Parsers;

public class EatDrinkFeedGoodParserTests
{
    private readonly EatDrinkFeedGoodParser _parser = new();

    private const string Sample = """
        RECIPE: Tomato Soup
        YIELD: 4 servings
        CATEGORY: Soups
        AUTHOR: Home Cook

        INGREDIENTS:
        - 2 lbs tomatoes, chopped
        - 1 onion, diced
        - 2 cloves garlic
        - 2 cups chicken broth
        - salt and pepper

        DIRECTIONS:
        - Sauté onion and garlic in olive oil.
        - Add tomatoes and broth, simmer 20 minutes.
        - Blend until smooth and season to taste.
        """;

    [Fact]
    public void Parse_ValidFormat_ExtractsTitle()
    {
        var result = _parser.Parse(Sample);
        result.Success.Should().BeTrue();
        result.Recipes.Should().NotBeEmpty();
        result.Recipes[0].Title.Should().Be("Tomato Soup");
    }

    [Fact]
    public void Parse_ValidFormat_ExtractsMetadata()
    {
        var result = _parser.Parse(Sample);
        result.Recipes[0].Yield.Should().Be("4 servings");
        result.Recipes[0].Category.Should().Be("Soups");
        result.Recipes[0].Author.Should().Be("Home Cook");
    }

    [Fact]
    public void Parse_ValidFormat_ExtractsIngredients()
    {
        var result = _parser.Parse(Sample);
        result.Recipes[0].Ingredients.Should().HaveCountGreaterThan(3);
        result.Recipes[0].Ingredients.Should().Contain(i => i.Name.Contains("tomato", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Parse_ValidFormat_ExtractsDirections()
    {
        var result = _parser.Parse(Sample);
        result.Recipes[0].Instructions.Should().NotBeEmpty();
        result.Recipes[0].Instructions[0].Text.Should().Contain("onion");
    }

    [Fact]
    public void Parse_EmptyInput_DoesNotThrow()
    {
        var act = () => _parser.Parse("");
        act.Should().NotThrow();
    }

    [Fact]
    public void CanParse_ContainsRecipeKeyword_ReturnsTrue()
    {
        _parser.CanParse("RECIPE: Test\nINGREDIENTS:\nDIRECTIONS:").Should().BeTrue();
    }
}
