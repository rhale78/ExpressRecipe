using ExpressRecipe.RecipeParser.Models;
using ExpressRecipe.RecipeParser.Parsers;
using FluentAssertions;

namespace ExpressRecipe.RecipeParser.Tests.Parsers;

public class RecipeParserEngineTests
{
    [Fact]
    public void ParseText_MealMasterFormat_DetectsAndParses()
    {
        var text = """
            MMMMM----- Recipe via Meal-Master (tm) v8.05
                 Title: Test Recipe
            Categories: Test
                 Yield: 4

            1 c  Flour
            1 t  Baking powder

            Mix and bake.

            MMMMM
            """;

        var result = RecipeParserEngine.ParseText(text);
        result.Success.Should().BeTrue();
        result.Recipes.Should().NotBeEmpty();
        result.Recipes[0].Title.Should().Be("Test Recipe");
    }

    [Fact]
    public void ParseText_JsonFormat_DetectsAndParses()
    {
        var json = """{"title": "Test", "ingredients": ["1 cup flour"], "instructions": ["Mix."]}""";
        var result = RecipeParserEngine.ParseText(json);
        result.Success.Should().BeTrue();
        result.Recipes.Should().HaveCount(1);
        result.Recipes[0].Title.Should().Be("Test");
    }

    [Fact]
    public void ParseText_EmptyText_ReturnsFailure()
    {
        var result = RecipeParserEngine.ParseText("   ");
        result.Success.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void ParseText_WithForceFormat_UsesSpecifiedParser()
    {
        var json = """{"title": "Forced", "ingredients": [], "instructions": []}""";
        var options = new RecipeParseOptions { ForceFormat = "Json" };
        var result = RecipeParserEngine.ParseText(json, null, options);
        result.Success.Should().BeTrue();
        result.Recipes[0].Title.Should().Be("Forced");
    }

    [Fact]
    public void ParseText_CookLangFormat_DetectsAndParses()
    {
        var text = """
            >> title: Quick Eggs
            Fry @eggs{2} in a #pan{} with @butter{1%tbsp}.
            """;
        var result = RecipeParserEngine.ParseText(text);
        result.Success.Should().BeTrue();
        result.Recipes[0].Title.Should().Be("Quick Eggs");
    }

    [Fact]
    public void ParseText_YamlFormat_DetectsAndParses()
    {
        var yaml = """
            title: Simple Soup
            servings: 2
            ingredients:
              - 2 cups broth
              - 1 cup vegetables
            instructions:
              - Combine and heat.
            """;
        var result = RecipeParserEngine.ParseText(yaml);
        result.Success.Should().BeTrue();
        result.Recipes[0].Title.Should().Be("Simple Soup");
    }

    [Fact]
    public void ParseText_RecipeML_DetectsAndParses()
    {
        var xml = """
            <?xml version="1.0"?>
            <recipeml version="0.5">
              <recipe>
                <head><title>XML Recipe</title></head>
                <ingredients>
                  <ingredient><item>1 cup flour</item></ingredient>
                </ingredients>
                <directions><step>Mix.</step></directions>
              </recipe>
            </recipeml>
            """;
        var result = RecipeParserEngine.ParseText(xml);
        result.Success.Should().BeTrue();
        result.Recipes[0].Title.Should().Be("XML Recipe");
    }

    [Fact]
    public async Task ParseFileAsync_NonExistentFile_ReturnsFailure()
    {
        var result = await RecipeParserEngine.ParseFileAsync("/non/existent/file.mmf");
        result.Success.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void ParseStream_ValidJson_ParsesCorrectly()
    {
        var json = """{"title": "Stream Test", "ingredients": [], "instructions": []}""";
        using var stream = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        var result = RecipeParserEngine.ParseStream(stream, "json");
        result.Success.Should().BeTrue();
        result.Recipes[0].Title.Should().Be("Stream Test");
    }
}
