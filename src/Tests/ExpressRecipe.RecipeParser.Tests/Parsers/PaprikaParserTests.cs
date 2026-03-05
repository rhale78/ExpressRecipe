using ExpressRecipe.RecipeParser.Models;
using ExpressRecipe.RecipeParser.Parsers;
using FluentAssertions;

namespace ExpressRecipe.RecipeParser.Tests.Parsers;

public class PaprikaParserTests
{
    private readonly PaprikaParser _parser = new();

    private const string PaprikaJson = """
        {
          "name": "Chicken Tikka Masala",
          "description": "Rich and creamy Indian curry",
          "source": "Family Recipe",
          "source_url": "https://example.com/ctm",
          "servings": "4",
          "prep_time": "20 minutes",
          "cook_time": "40 minutes",
          "categories": "Indian, Dinner",
          "ingredients": "500g chicken breast, cubed\n2 cups tikka masala sauce\n1 cup heavy cream\n2 tbsp oil\nsalt to taste",
          "directions": "Marinate chicken in spices.\nCook chicken until browned.\nAdd sauce and simmer 20 minutes.\nStir in cream and heat through."
        }
        """;

    [Fact]
    public void Parse_PaprikaJson_ExtractsTitle()
    {
        var result = _parser.Parse(PaprikaJson);
        result.Success.Should().BeTrue();
        result.Recipes.Should().HaveCount(1);
        result.Recipes[0].Title.Should().Be("Chicken Tikka Masala");
    }

    [Fact]
    public void Parse_PaprikaJson_ExtractsMetadata()
    {
        var result = _parser.Parse(PaprikaJson);
        var recipe = result.Recipes[0];
        recipe.PrepTime.Should().Be("20 minutes");
        recipe.CookTime.Should().Be("40 minutes");
        recipe.Source.Should().Be("Family Recipe");
    }

    [Fact]
    public void Parse_PaprikaJson_ExtractsIngredients()
    {
        var result = _parser.Parse(PaprikaJson);
        result.Recipes[0].Ingredients.Should().HaveCountGreaterThan(3);
        result.Recipes[0].Ingredients.Should().Contain(i => i.Name.Contains("chicken", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Parse_PaprikaJson_ExtractsInstructions()
    {
        var result = _parser.Parse(PaprikaJson);
        result.Recipes[0].Instructions.Should().HaveCount(4);
        result.Recipes[0].Instructions[0].Text.Should().Contain("Marinate");
    }

    [Fact]
    public void Parse_InvalidJson_ReturnsFailure()
    {
        var result = _parser.Parse("{bad json");
        result.Success.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void CanParse_PaprikaExtension_ReturnsTrue()
    {
        _parser.CanParse("{}", "paprika").Should().BeTrue();
    }
}
