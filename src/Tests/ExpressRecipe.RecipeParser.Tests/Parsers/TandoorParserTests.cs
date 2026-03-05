using ExpressRecipe.RecipeParser.Models;
using ExpressRecipe.RecipeParser.Parsers;
using FluentAssertions;

namespace ExpressRecipe.RecipeParser.Tests.Parsers;

public class TandoorParserTests
{
    private readonly TandoorParser _parser = new();

    private const string TandoorSample = """
        {
          "name": "Mushroom Risotto",
          "description": "Creamy Italian rice dish",
          "source_url": "https://example.com/mushroom-risotto",
          "servings": 4,
          "working_time": 20,
          "waiting_time": 30,
          "keywords": [
            {"name": "italian"},
            {"name": "vegetarian"},
            {"name": "rice"}
          ],
          "steps": [
            {
              "instruction": "Heat broth in a saucepan and keep warm.",
              "ingredients": [
                {"food": {"name": "vegetable broth"}, "amount": 4, "unit": {"name": "cups"}},
                {"food": {"name": "mushrooms"}, "amount": 300, "unit": {"name": "g"}, "note": "sliced"}
              ]
            },
            {
              "instruction": "Sauté onion and garlic until soft. Add arborio rice and toast 2 minutes.",
              "ingredients": [
                {"food": {"name": "arborio rice"}, "amount": 1.5, "unit": {"name": "cups"}},
                {"food": {"name": "onion"}, "amount": 1},
                {"food": {"name": "garlic"}, "amount": 2, "unit": {"name": "cloves"}}
              ]
            },
            {
              "instruction": "Add broth ladle by ladle, stirring constantly. Finish with parmesan.",
              "ingredients": [
                {"food": {"name": "parmesan"}, "amount": 50, "unit": {"name": "g"}},
                {"food": {"name": "butter"}, "amount": 2, "unit": {"name": "tbsp"}}
              ]
            }
          ]
        }
        """;

    [Fact]
    public void Parse_TandoorRecipe_ExtractsTitle()
    {
        var result = _parser.Parse(TandoorSample);
        result.Success.Should().BeTrue();
        result.Recipes.Should().HaveCount(1);
        result.Recipes[0].Title.Should().Be("Mushroom Risotto");
    }

    [Fact]
    public void Parse_TandoorRecipe_ExtractsTimes()
    {
        var result = _parser.Parse(TandoorSample);
        var recipe = result.Recipes[0];
        recipe.PrepTime.Should().Contain("20");
        recipe.CookTime.Should().Contain("30");
    }

    [Fact]
    public void Parse_TandoorRecipe_ExtractsIngredients()
    {
        var result = _parser.Parse(TandoorSample);
        var ings = result.Recipes[0].Ingredients;
        ings.Should().NotBeEmpty();
        ings.Should().Contain(i => i.Name.Contains("rice", StringComparison.OrdinalIgnoreCase));
        ings.Should().Contain(i => i.Name.Contains("mushroom", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Parse_TandoorRecipe_ExtractsInstructions()
    {
        var result = _parser.Parse(TandoorSample);
        result.Recipes[0].Instructions.Should().HaveCount(3);
        result.Recipes[0].Instructions[0].Text.Should().Contain("broth");
    }

    [Fact]
    public void Parse_TandoorRecipe_ExtractsTags()
    {
        var result = _parser.Parse(TandoorSample);
        result.Recipes[0].Tags.Should().Contain("italian");
        result.Recipes[0].Tags.Should().Contain("vegetarian");
    }

    [Fact]
    public void Parse_InvalidJson_DoesNotThrow()
    {
        var act = () => _parser.Parse("{invalid}");
        act.Should().NotThrow();
    }
}
