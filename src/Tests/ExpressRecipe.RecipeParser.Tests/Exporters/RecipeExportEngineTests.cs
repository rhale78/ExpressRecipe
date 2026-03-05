using ExpressRecipe.RecipeParser.Exporters;
using ExpressRecipe.RecipeParser.Models;
using ExpressRecipe.RecipeParser.Parsers;

namespace ExpressRecipe.RecipeParser.Tests.Exporters;

public class RecipeExportEngineTests
{
    private static ParsedRecipe MakeSampleRecipe() => new()
    {
        Title = "Test Recipe",
        Description = "A test",
        Author = "Chef Test",
        Yield = "4 servings",
        PrepTime = "15 min",
        CookTime = "30 min",
        Category = "Main",
        Cuisine = "American",
        Tags = ["test", "sample"],
        Ingredients =
        [
            new ParsedIngredient { Quantity = "2", Unit = "cups", Name = "flour" },
            new ParsedIngredient { Quantity = "1", Unit = "cup", Name = "sugar" }
        ],
        Instructions =
        [
            new ParsedInstruction { Step = 1, Text = "Mix ingredients together." },
            new ParsedInstruction { Step = 2, Text = "Bake at 350F for 30 min." }
        ],
        Nutrition = new ParsedNutrition { Calories = "300", Protein = "5g", Fat = "10g", Carbohydrates = "45g" }
    };

    [Fact]
    public void SupportedFormats_ContainsAllExpected()
    {
        var formats = RecipeExportEngine.SupportedFormats;
        formats.Should().Contain("GoogleStructuredData");
        formats.Should().Contain("JSON");
        formats.Should().Contain("YAML");
        formats.Should().Contain("OpenRecipeFormat");
        formats.Should().Contain("RecipeML");
        formats.Should().Contain("MealMaster");
        formats.Should().Contain("MasterCook");
        formats.Should().Contain("CookLang");
    }

    [Theory]
    [InlineData("GoogleStructuredData")]
    [InlineData("JSON")]
    [InlineData("YAML")]
    [InlineData("OpenRecipeFormat")]
    [InlineData("RecipeML")]
    [InlineData("MealMaster")]
    [InlineData("MasterCook")]
    [InlineData("CookLang")]
    public void Export_AllFormats_ProducesNonEmptyOutput(string format)
    {
        var recipe = MakeSampleRecipe();
        var output = RecipeExportEngine.Export(recipe, format);
        output.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData("GoogleStructuredData")]
    [InlineData("JSON")]
    [InlineData("YAML")]
    [InlineData("OpenRecipeFormat")]
    [InlineData("RecipeML")]
    [InlineData("MealMaster")]
    [InlineData("MasterCook")]
    [InlineData("CookLang")]
    public void ExportAll_AllFormats_ProducesNonEmptyOutput(string format)
    {
        var recipes = new[] { MakeSampleRecipe(), MakeSampleRecipe() };
        var output = RecipeExportEngine.ExportAll(recipes, format);
        output.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Export_UnsupportedFormat_ThrowsNotSupported()
    {
        var recipe = MakeSampleRecipe();
        var act = () => RecipeExportEngine.Export(recipe, "NonExistentFormat");
        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void RoundTrip_JsonLd_ParseExportParse()
    {
        var original = MakeSampleRecipe();
        var exported = RecipeExportEngine.Export(original, "GoogleStructuredData");

        var parser = new GoogleStructuredDataParser();
        var reimported = parser.Parse(exported);

        reimported.Success.Should().BeTrue();
        reimported.Recipes.Should().HaveCount(1);
        reimported.Recipes[0].Title.Should().Be(original.Title);
        reimported.Recipes[0].Ingredients.Should().HaveCount(original.Ingredients.Count);
        reimported.Recipes[0].Instructions.Should().HaveCount(original.Instructions.Count);
    }
}
