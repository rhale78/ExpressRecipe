using ExpressRecipe.RecipeParser.Models;
using ExpressRecipe.RecipeParser.Parsers;
using FluentAssertions;

namespace ExpressRecipe.RecipeParser.Tests.Parsers;

public class YamlRecipeParserTests
{
    private readonly YamlRecipeParser _parser = new();

    private const string SingleRecipeYaml = """
        title: Banana Bread
        description: Moist and delicious banana bread
        author: Home Baker
        servings: 8 slices
        prep_time: 15 minutes
        cook_time: 60 minutes
        category: Baking
        cuisine: American
        tags:
          - bread
          - banana
          - baking
        ingredients:
          - 3 ripe bananas, mashed
          - "1/3 cup melted butter"
          - name: sugar
            quantity: "3/4"
            unit: cup
          - name: egg
            quantity: "1"
          - name: baking soda
            quantity: "1"
            unit: tsp
          - name: flour
            quantity: "1 1/2"
            unit: cups
        instructions:
          - Preheat oven to 350°F (175°C).
          - Mix mashed bananas with melted butter.
          - Mix in sugar, egg, and baking soda.
          - Fold in flour until just combined.
          - Pour into greased loaf pan and bake 55-65 minutes.
        """;

    [Fact]
    public void Parse_SingleRecipe_ExtractsMetadata()
    {
        var result = _parser.Parse(SingleRecipeYaml);
        result.Success.Should().BeTrue();
        result.Recipes.Should().HaveCount(1);
        var recipe = result.Recipes[0];
        recipe.Title.Should().Be("Banana Bread");
        recipe.Author.Should().Be("Home Baker");
        recipe.PrepTime.Should().Be("15 minutes");
        recipe.CookTime.Should().Be("60 minutes");
    }

    [Fact]
    public void Parse_SingleRecipe_ExtractsIngredients()
    {
        var result = _parser.Parse(SingleRecipeYaml);
        var ings = result.Recipes[0].Ingredients;
        ings.Should().HaveCountGreaterThan(4);
        ings.Should().Contain(i => i.Name == "sugar");
        ings.Should().Contain(i => i.Name == "flour");
    }

    [Fact]
    public void Parse_SingleRecipe_ExtractsInstructions()
    {
        var result = _parser.Parse(SingleRecipeYaml);
        result.Recipes[0].Instructions.Should().HaveCount(5);
        result.Recipes[0].Instructions[0].Text.Should().Contain("350");
    }

    [Fact]
    public void Parse_Tags_ParsesAsList()
    {
        var result = _parser.Parse(SingleRecipeYaml);
        result.Recipes[0].Tags.Should().Contain("bread");
        result.Recipes[0].Tags.Should().Contain("banana");
    }

    [Fact]
    public void Parse_InvalidYaml_ReturnsFailureOrEmpty()
    {
        var result = _parser.Parse("title: [\nthis is invalid yaml: :");
        result.Should().NotBeNull();
        // Invalid YAML should either fail or produce errors — it should not silently succeed with recipes
        if (result.Success)
            result.Recipes.Should().BeEmpty();
        else
            result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void Parse_EmptyYaml_ReturnsSuccessWithNoRecipes()
    {
        var result = _parser.Parse("# empty yaml file");
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Recipes.Should().BeEmpty();
    }
}
