using ExpressRecipe.RecipeParser.Models;
using ExpressRecipe.RecipeParser.Parsers;
using FluentAssertions;

namespace ExpressRecipe.RecipeParser.Tests.Parsers;

public class JsonRecipeParserTests
{
    private readonly JsonRecipeParser _parser = new();

    private const string SingleRecipeJson = """
        {
          "title": "Spaghetti Bolognese",
          "description": "Classic Italian pasta dish",
          "author": "Nonna Rosa",
          "servings": "4",
          "prepTime": "15 min",
          "cookTime": "45 min",
          "category": "Pasta",
          "cuisine": "Italian",
          "tags": ["dinner", "italian", "pasta"],
          "ingredients": [
            {"quantity": "400", "unit": "g", "name": "spaghetti"},
            {"quantity": "500", "unit": "g", "name": "ground beef"},
            {"quantity": "2", "unit": "cloves", "name": "garlic", "preparation": "minced"},
            "1 can tomatoes",
            "salt and pepper to taste"
          ],
          "instructions": [
            "Cook spaghetti according to package instructions.",
            "Brown the ground beef in a large pan.",
            "Add garlic and tomatoes, simmer 30 minutes.",
            "Combine and serve."
          ],
          "nutrition": {
            "calories": "520",
            "protein": "35g",
            "carbohydrates": "60g",
            "fat": "15g"
          }
        }
        """;

    private const string MultiRecipeJson = """
        [
          {"title": "Recipe One", "ingredients": ["1 cup flour"], "instructions": ["Mix well."]},
          {"title": "Recipe Two", "ingredients": ["2 eggs"], "instructions": ["Beat eggs."]}
        ]
        """;

    [Fact]
    public void Parse_SingleRecipe_ExtractsAllFields()
    {
        var result = _parser.Parse(SingleRecipeJson);
        result.Success.Should().BeTrue();
        result.Recipes.Should().HaveCount(1);
        var recipe = result.Recipes[0];
        recipe.Title.Should().Be("Spaghetti Bolognese");
        recipe.Author.Should().Be("Nonna Rosa");
        recipe.Yield.Should().Be("4");
        recipe.PrepTime.Should().Be("15 min");
        recipe.CookTime.Should().Be("45 min");
        recipe.Cuisine.Should().Be("Italian");
    }

    [Fact]
    public void Parse_SingleRecipe_ExtractsIngredients()
    {
        var result = _parser.Parse(SingleRecipeJson);
        result.Recipes[0].Ingredients.Should().HaveCountGreaterThan(3);
        result.Recipes[0].Ingredients.Should().Contain(i => i.Name == "spaghetti");
        result.Recipes[0].Ingredients.Should().Contain(i => i.Preparation == "minced");
    }

    [Fact]
    public void Parse_SingleRecipe_ExtractsInstructions()
    {
        var result = _parser.Parse(SingleRecipeJson);
        result.Recipes[0].Instructions.Should().HaveCount(4);
        result.Recipes[0].Instructions[0].Step.Should().Be(1);
    }

    [Fact]
    public void Parse_SingleRecipe_ExtractsNutrition()
    {
        var result = _parser.Parse(SingleRecipeJson);
        result.Recipes[0].Nutrition.Should().NotBeNull();
        result.Recipes[0].Nutrition!.Calories.Should().Be("520");
        result.Recipes[0].Nutrition.Protein.Should().Be("35g");
    }

    [Fact]
    public void Parse_MultipleRecipes_ReturnsAll()
    {
        var result = _parser.Parse(MultiRecipeJson);
        result.Success.Should().BeTrue();
        result.Recipes.Should().HaveCount(2);
        result.Recipes[0].Title.Should().Be("Recipe One");
        result.Recipes[1].Title.Should().Be("Recipe Two");
    }

    [Fact]
    public void Parse_InvalidJson_ReturnsFailure()
    {
        var result = _parser.Parse("{invalid json}");
        result.Success.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void Parse_Tags_ExtractsList()
    {
        var result = _parser.Parse(SingleRecipeJson);
        result.Recipes[0].Tags.Should().Contain("italian");
        result.Recipes[0].Tags.Should().Contain("pasta");
    }
}
