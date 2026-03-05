using ExpressRecipe.RecipeParser.Models;
using ExpressRecipe.RecipeParser.Parsers;
using FluentAssertions;

namespace ExpressRecipe.RecipeParser.Tests.Parsers;

public class ConnoisseurParserTests
{
    private readonly ConnoisseurParser _parser = new();

    private const string SingleRecipe = """
        @@@@@
        Title: Caesar Salad
        Yield: 4 servings
        Category: Salads
        Author: Chef Anna

        Ingredients:
        - 1 head romaine lettuce, chopped
        - 1/2 cup parmesan cheese, grated
        - 1 cup croutons
        - 3 tbsp Caesar dressing

        Directions:
        Toss lettuce with dressing. Add parmesan and croutons. Serve immediately.
        @@@@@
        """;

    private const string MultipleRecipes = """
        @@@@@
        Title: Green Salad
        Ingredients:
        - mixed greens
        - tomatoes
        Directions:
        Toss together.
        @@@@@
        Title: Fruit Salad
        Ingredients:
        - apples
        - oranges
        Directions:
        Mix and serve.
        @@@@@
        """;

    [Fact]
    public void Parse_SingleRecipe_ExtractsTitle()
    {
        var result = _parser.Parse(SingleRecipe);
        result.Success.Should().BeTrue();
        result.Recipes.Should().HaveCount(1);
        result.Recipes[0].Title.Should().Be("Caesar Salad");
    }

    [Fact]
    public void Parse_SingleRecipe_ExtractsMetadata()
    {
        var result = _parser.Parse(SingleRecipe);
        result.Recipes[0].Yield.Should().Be("4 servings");
        result.Recipes[0].Category.Should().Be("Salads");
        result.Recipes[0].Author.Should().Be("Chef Anna");
    }

    [Fact]
    public void Parse_SingleRecipe_ExtractsIngredients()
    {
        var result = _parser.Parse(SingleRecipe);
        result.Recipes[0].Ingredients.Should().HaveCountGreaterThan(2);
        result.Recipes[0].Ingredients.Should().Contain(i => i.Name.Contains("romaine", StringComparison.OrdinalIgnoreCase) || i.Name.Contains("lettuce", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Parse_MultipleRecipes_ReturnsAll()
    {
        var result = _parser.Parse(MultipleRecipes);
        result.Success.Should().BeTrue();
        result.Recipes.Should().HaveCount(2);
    }

    [Fact]
    public void Parse_BadData_DoesNotThrow()
    {
        var act = () => _parser.Parse("@@@@@\nNo fields here\n@@@@@");
        act.Should().NotThrow();
    }

    [Fact]
    public void CanParse_ContainsAtSigns_ReturnsTrue()
    {
        _parser.CanParse("@@@@@\nrecipe data").Should().BeTrue();
    }
}
