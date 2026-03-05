using ExpressRecipe.RecipeParser.Models;
using ExpressRecipe.RecipeParser.Parsers;
using FluentAssertions;

namespace ExpressRecipe.RecipeParser.Tests.Parsers;

public class NextcloudCookbookParserTests
{
    private readonly NextcloudCookbookParser _parser = new();

    private const string SchemaOrgRecipe = """
        {
          "@context": "https://schema.org",
          "@type": "Recipe",
          "name": "French Toast",
          "description": "Classic breakfast french toast",
          "author": {"@type": "Person", "name": "Breakfast Chef"},
          "recipeYield": "2 servings",
          "prepTime": "PT5M",
          "cookTime": "PT10M",
          "totalTime": "PT15M",
          "recipeCategory": "Breakfast",
          "recipeCuisine": "French",
          "keywords": "breakfast,quick,easy",
          "recipeIngredient": [
            "4 slices thick bread",
            "2 eggs",
            "1/2 cup milk",
            "1 tsp vanilla extract",
            "1/2 tsp cinnamon",
            "butter for cooking"
          ],
          "recipeInstructions": [
            {"@type": "HowToStep", "text": "Whisk together eggs, milk, vanilla, and cinnamon."},
            {"@type": "HowToStep", "text": "Dip bread slices into egg mixture."},
            {"@type": "HowToStep", "text": "Cook in buttered pan 2-3 minutes per side until golden."},
            {"@type": "HowToStep", "text": "Serve with maple syrup and powdered sugar."}
          ],
          "nutrition": {
            "calories": "320 calories",
            "proteinContent": "12g",
            "carbohydrateContent": "45g",
            "fatContent": "10g"
          }
        }
        """;

    [Fact]
    public void Parse_SchemaOrgRecipe_ExtractsTitle()
    {
        var result = _parser.Parse(SchemaOrgRecipe);
        result.Success.Should().BeTrue();
        result.Recipes.Should().HaveCount(1);
        result.Recipes[0].Title.Should().Be("French Toast");
    }

    [Fact]
    public void Parse_SchemaOrgRecipe_ExtractsMetadata()
    {
        var result = _parser.Parse(SchemaOrgRecipe);
        var recipe = result.Recipes[0];
        recipe.Author.Should().Be("Breakfast Chef");
        recipe.Yield.Should().Be("2 servings");
        recipe.Cuisine.Should().Be("French");
    }

    [Fact]
    public void Parse_SchemaOrgRecipe_ExtractsIngredients()
    {
        var result = _parser.Parse(SchemaOrgRecipe);
        result.Recipes[0].Ingredients.Should().HaveCount(6);
    }

    [Fact]
    public void Parse_SchemaOrgRecipe_ExtractsInstructions()
    {
        var result = _parser.Parse(SchemaOrgRecipe);
        result.Recipes[0].Instructions.Should().HaveCount(4);
        result.Recipes[0].Instructions[0].Text.Should().Contain("egg");
    }

    [Fact]
    public void Parse_SchemaOrgRecipe_ExtractsNutrition()
    {
        var result = _parser.Parse(SchemaOrgRecipe);
        result.Recipes[0].Nutrition.Should().NotBeNull();
        result.Recipes[0].Nutrition!.Protein.Should().Be("12g");
    }

    [Fact]
    public void Parse_Keywords_ExtractsTags()
    {
        var result = _parser.Parse(SchemaOrgRecipe);
        result.Recipes[0].Tags.Should().Contain("breakfast");
        result.Recipes[0].Tags.Should().Contain("quick");
    }
}
