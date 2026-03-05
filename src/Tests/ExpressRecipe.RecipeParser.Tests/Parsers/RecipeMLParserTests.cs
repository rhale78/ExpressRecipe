using ExpressRecipe.RecipeParser.Models;
using ExpressRecipe.RecipeParser.Parsers;
using FluentAssertions;

namespace ExpressRecipe.RecipeParser.Tests.Parsers;

public class RecipeMLParserTests
{
    private readonly RecipeMLParser _parser = new();

    private const string RecipeMLSample = """
        <?xml version="1.0" encoding="UTF-8"?>
        <recipeml version="0.5">
          <recipe>
            <head>
              <title>Grilled Salmon</title>
              <author>Chef Lee</author>
              <yield>4</yield>
              <preptime>10 minutes</preptime>
              <cooktime>15 minutes</cooktime>
              <categories>Seafood, Healthy</categories>
              <source>Home Kitchen</source>
            </head>
            <ingredients>
              <ingredient>
                <amt><qty>4</qty><unit>fillets</unit></amt>
                <item>Salmon fillets</item>
              </ingredient>
              <ingredient>
                <amt><qty>2</qty><unit>tbsp</unit></amt>
                <item>Olive oil</item>
              </ingredient>
              <ingredient>
                <item>Salt and pepper to taste</item>
              </ingredient>
            </ingredients>
            <directions>
              <step>Brush salmon with olive oil and season.</step>
              <step>Grill 5-7 minutes per side until cooked through.</step>
              <step>Serve with lemon wedges.</step>
            </directions>
          </recipe>
        </recipeml>
        """;

    [Fact]
    public void Parse_ValidRecipeML_ExtractsTitle()
    {
        var result = _parser.Parse(RecipeMLSample);
        result.Success.Should().BeTrue();
        result.Recipes.Should().HaveCount(1);
        result.Recipes[0].Title.Should().Be("Grilled Salmon");
    }

    [Fact]
    public void Parse_ValidRecipeML_ExtractsMetadata()
    {
        var result = _parser.Parse(RecipeMLSample);
        var recipe = result.Recipes[0];
        recipe.Author.Should().Be("Chef Lee");
        recipe.Yield.Should().Be("4");
        recipe.PrepTime.Should().Be("10 minutes");
        recipe.CookTime.Should().Be("15 minutes");
    }

    [Fact]
    public void Parse_ValidRecipeML_ExtractsIngredients()
    {
        var result = _parser.Parse(RecipeMLSample);
        result.Recipes[0].Ingredients.Should().HaveCount(3);
        result.Recipes[0].Ingredients.Should().Contain(i => i.Name.Contains("Salmon"));
        result.Recipes[0].Ingredients.Should().Contain(i => i.Unit == "tbsp");
    }

    [Fact]
    public void Parse_ValidRecipeML_ExtractsSteps()
    {
        var result = _parser.Parse(RecipeMLSample);
        result.Recipes[0].Instructions.Should().HaveCount(3);
        result.Recipes[0].Instructions[0].Step.Should().Be(1);
    }

    [Fact]
    public void Parse_InvalidXml_ReturnsFailure()
    {
        var result = _parser.Parse("<recipeml><recipe><title>Broken");
        result.Success.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void CanParse_RecipeMLExtension_ReturnsTrue()
    {
        _parser.CanParse("<recipeml>", "rml").Should().BeTrue();
    }
}
