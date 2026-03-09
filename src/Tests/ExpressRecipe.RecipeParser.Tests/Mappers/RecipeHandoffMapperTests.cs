using ExpressRecipe.RecipeParser.Mappers;
using ExpressRecipe.RecipeParser.Models;

namespace ExpressRecipe.RecipeParser.Tests.Mappers;

public class RecipeHandoffMapperTests
{
    [Fact]
    public void ToHandoffDto_BasicMapping()
    {
        var recipe = new ParsedRecipe
        {
            Title = "Pasta",
            Description = "Simple pasta",
            Author = "Chef",
            Yield = "4 servings",
            PrepTime = "15 min",
            CookTime = "20 min",
            Category = "Main, Italian",
            Cuisine = "Italian",
            Tags = ["easy", "quick"],
            Format = "JSON",
            Url = "https://example.com/pasta",
            Ingredients =
            [
                new ParsedIngredient { Name = "pasta", Quantity = "200", Unit = "g" },
                new ParsedIngredient { Name = "tomatoes", Quantity = "2", IsOptional = true }
            ],
            Instructions =
            [
                new ParsedInstruction { Step = 1, Text = "Boil water." },
                new ParsedInstruction { Step = 2, Text = "Cook pasta." }
            ],
            Nutrition = new ParsedNutrition { Calories = "350 kcal", Protein = "12.5g", Carbohydrates = "60g", Fat = "5g" }
        };

        var dto = recipe.ToHandoffDto();

        dto.Name.Should().Be("Pasta");
        dto.Description.Should().Be("Simple pasta");
        dto.Author.Should().Be("Chef");
        dto.Cuisine.Should().Be("Italian");
        dto.SourceFormat.Should().Be("JSON");
        dto.SourceUrl.Should().Be("https://example.com/pasta");
        dto.Tags.Should().Contain("easy");
        dto.Categories.Should().Contain("Main");
        dto.Categories.Should().Contain("Italian");
        dto.Servings.Should().Be(4);
        dto.PrepTimeMinutes.Should().Be(15);
        dto.CookTimeMinutes.Should().Be(20);
        dto.Ingredients.Should().HaveCount(2);
        dto.Ingredients[0].Name.Should().Be("pasta");
        dto.Ingredients[1].IsOptional.Should().BeTrue();
        dto.Instructions.Should().HaveCount(2);
        dto.Instructions[0].StepNumber.Should().Be(1);
        dto.Nutrition.Should().NotBeNull();
        dto.Nutrition!.Calories.Should().Be(350);
        dto.Nutrition.Protein.Should().BeApproximately(12.5m, 0.01m);
    }

    [Theory]
    [InlineData("PT30M", 30)]
    [InlineData("PT1H", 60)]
    [InlineData("PT1H30M", 90)]
    [InlineData("P1DT2H", 1560)]
    [InlineData("30 min", 30)]
    [InlineData("1 hour", 60)]
    [InlineData("1 hour 30 min", 90)]
    [InlineData("1h 30m", 90)]
    [InlineData("45 minutes", 45)]
    [InlineData("2 hr", 120)]
    [InlineData("45", 45)]
    public void ParseTimeToMinutes_VariousFormats(string input, int expected)
        => RecipeHandoffMapper.ParseTimeToMinutes(input).Should().Be(expected);

    [Fact]
    public void ParseTimeToMinutes_NullInput_ReturnsNull()
        => RecipeHandoffMapper.ParseTimeToMinutes(null).Should().BeNull();

    [Fact]
    public void ParseTimeToMinutes_EmptyInput_ReturnsNull()
        => RecipeHandoffMapper.ParseTimeToMinutes("").Should().BeNull();

    [Theory]
    [InlineData("1/2", 0.5)]
    [InlineData("1 1/2", 1.5)]
    [InlineData("2 3/4", 2.75)]
    [InlineData("1", 1.0)]
    [InlineData("0.5", 0.5)]
    [InlineData("3", 3.0)]
    [InlineData("\u00BD", 0.5)]       // ½
    [InlineData("\u00BE", 0.75)]      // ¾
    [InlineData("&frac34;", 0.75)]    // HTML entity
    public void ParseQuantity_VariousFormats(string input, double expected)
        => RecipeHandoffMapper.ParseQuantity(input).Should().BeApproximately((decimal)expected, 0.001m);

    [Fact]
    public void ParseQuantity_NullInput_ReturnsZero()
        => RecipeHandoffMapper.ParseQuantity(null).Should().Be(0m);

    [Theory]
    [InlineData("4 servings", 4)]
    [InlineData("6", 6)]
    [InlineData("makes 12 cookies", 12)]
    [InlineData(null, 1)]
    [InlineData("", 1)]
    [InlineData("several", 1)]
    public void ParseServings_VariousFormats(string? input, int expected)
        => RecipeHandoffMapper.ParseServings(input).Should().Be(expected);

    [Fact]
    public void ToHandoffDto_NullNutrition_NutritionIsNull()
    {
        var recipe = new ParsedRecipe { Title = "Test" };
        var dto = recipe.ToHandoffDto();
        dto.Nutrition.Should().BeNull();
    }

    [Fact]
    public void ToHandoffDto_EmptyRecipe_DefaultsAreCorrect()
    {
        var recipe = new ParsedRecipe { Title = "Empty" };
        var dto = recipe.ToHandoffDto();
        dto.Name.Should().Be("Empty");
        dto.Servings.Should().Be(1);
        dto.Difficulty.Should().Be("Medium");
        dto.Categories.Should().BeEmpty();
        dto.Tags.Should().BeEmpty();
        dto.Ingredients.Should().BeEmpty();
        dto.Instructions.Should().BeEmpty();
    }
}
