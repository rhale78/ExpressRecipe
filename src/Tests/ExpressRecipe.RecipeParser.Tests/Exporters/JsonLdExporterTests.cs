using System.Text.Json;
using ExpressRecipe.RecipeParser.Exporters;
using ExpressRecipe.RecipeParser.Models;

namespace ExpressRecipe.RecipeParser.Tests.Exporters;

public class JsonLdExporterTests
{
    private readonly JsonLdRecipeExporter _exporter = new();

    private static ParsedRecipe MakeSampleRecipe() => new()
    {
        Title = "Pancakes",
        Description = "Fluffy pancakes",
        Author = "Chef Bob",
        Yield = "8 pancakes",
        PrepTime = "10 min",
        CookTime = "20 min",
        Category = "Breakfast",
        Cuisine = "American",
        Tags = ["breakfast", "easy"],
        Ingredients =
        [
            new ParsedIngredient { Quantity = "1", Unit = "cup", Name = "flour" },
            new ParsedIngredient { Quantity = "1", Unit = "cup", Name = "milk" }
        ],
        Instructions =
        [
            new ParsedInstruction { Step = 1, Text = "Mix flour and milk." },
            new ParsedInstruction { Step = 2, Text = "Cook on griddle." }
        ],
        Nutrition = new ParsedNutrition
        {
            Calories = "250",
            Fat = "5g",
            Carbohydrates = "45g",
            Protein = "8g",
            Fiber = "2g",
            Sugar = "10g",
            Sodium = "300mg",
            Cholesterol = "40mg"
        }
    };

    [Fact]
    public void Export_ProducesValidJson()
    {
        var recipe = MakeSampleRecipe();
        var output = _exporter.Export(recipe);
        var act = () => JsonDocument.Parse(output);
        act.Should().NotThrow();
    }

    [Fact]
    public void Export_ContainsSchemaContext()
    {
        var output = _exporter.Export(MakeSampleRecipe());
        output.Should().Contain("schema.org");
        output.Should().Contain("\"@type\"");
        output.Should().Contain("\"Recipe\"");
    }

    [Fact]
    public void Export_ContainsAllFields()
    {
        var output = _exporter.Export(MakeSampleRecipe());
        using var doc = JsonDocument.Parse(output);
        var root = doc.RootElement;

        root.GetProperty("name").GetString().Should().Be("Pancakes");
        root.GetProperty("description").GetString().Should().Be("Fluffy pancakes");
        root.GetProperty("recipeCategory").GetString().Should().Be("Breakfast");
        root.GetProperty("recipeCuisine").GetString().Should().Be("American");
        root.GetProperty("recipeIngredient").GetArrayLength().Should().Be(2);
        root.GetProperty("recipeInstructions").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public void Export_NutritionMapped()
    {
        var output = _exporter.Export(MakeSampleRecipe());
        using var doc = JsonDocument.Parse(output);
        var nut = doc.RootElement.GetProperty("nutrition");
        nut.GetProperty("calories").GetString().Should().Be("250");
        nut.GetProperty("fatContent").GetString().Should().Be("5g");
        nut.GetProperty("proteinContent").GetString().Should().Be("8g");
    }

    [Fact]
    public void Export_InstructionsAreHowToStep()
    {
        var output = _exporter.Export(MakeSampleRecipe());
        using var doc = JsonDocument.Parse(output);
        var insts = doc.RootElement.GetProperty("recipeInstructions");
        insts[0].GetProperty("@type").GetString().Should().Be("HowToStep");
        insts[0].GetProperty("text").GetString().Should().Be("Mix flour and milk.");
    }

    [Fact]
    public void ExportAll_ProducesGraph()
    {
        var recipes = new[] { MakeSampleRecipe(), MakeSampleRecipe() };
        var output = _exporter.ExportAll(recipes);
        using var doc = JsonDocument.Parse(output);
        doc.RootElement.GetProperty("@graph").GetArrayLength().Should().Be(2);
    }

    [Theory]
    [InlineData("PT30M", "PT30M")]
    [InlineData("PT1H30M", "PT1H30M")]
    [InlineData("30 min", "PT30M")]
    [InlineData("1 hour", "PT1H")]
    [InlineData("1 hour 30 min", "PT1H30M")]
    [InlineData("1h 30m", "PT1H30M")]
    [InlineData("90 min", "PT1H30M")]
    [InlineData("45 minutes", "PT45M")]
    public void ToIsoDuration_ConvertsCorrectly(string input, string expected)
        => JsonLdRecipeExporter.ToIsoDuration(input).Should().Be(expected);
}
