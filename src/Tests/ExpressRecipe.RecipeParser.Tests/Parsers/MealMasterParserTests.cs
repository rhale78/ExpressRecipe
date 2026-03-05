using ExpressRecipe.RecipeParser.Models;
using ExpressRecipe.RecipeParser.Parsers;
using FluentAssertions;

namespace ExpressRecipe.RecipeParser.Tests.Parsers;

public class MealMasterParserTests
{
    private readonly MealMasterParser _parser = new();

    private const string SingleRecipe = """
        MMMMM----- Recipe via Meal-Master (tm) v8.05
             Title: Chocolate Chip Cookies
        Categories: Cookies, Desserts
             Yield: 48 Cookies

          2 1/4 c  All-purpose flour                1 t  Baking soda
              1 t  Salt                             1 c  Butter, softened
          3/4 c  Granulated sugar                 3/4 c  Packed brown sugar
              2 lg Eggs                              2 t  Vanilla extract
              2 c  Chocolate chips

        Preheat oven to 375 degrees F. Mix flour, baking soda and salt.
        Beat butter and sugars until creamy. Add eggs and vanilla.
        Gradually blend in the flour mixture. Stir in chocolate chips.
        Drop rounded tablespoon of dough onto ungreased baking sheets.
        Bake for 9 to 11 minutes or until golden brown.

        MMMMM
        """;

    [Fact]
    public void Parse_SingleRecipe_ReturnsOneRecipe()
    {
        var result = _parser.Parse(SingleRecipe);
        result.Success.Should().BeTrue();
        result.Recipes.Should().HaveCount(1);
        result.Recipes[0].Title.Should().Be("Chocolate Chip Cookies");
        result.Recipes[0].Category.Should().Contain("Cookies");
        result.Recipes[0].Yield.Should().Contain("48");
    }

    [Fact]
    public void Parse_MultiColumnIngredients_ParsesBothColumns()
    {
        var result = _parser.Parse(SingleRecipe);
        result.Recipes[0].Ingredients.Should().NotBeEmpty();
        result.Recipes[0].Ingredients.Should().Contain(i => i.Name.Contains("flour", StringComparison.OrdinalIgnoreCase));
        result.Recipes[0].Ingredients.Should().Contain(i => i.Name.Contains("soda", StringComparison.OrdinalIgnoreCase) || i.Name.Contains("Baking", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Parse_EmptyInput_ReturnsNoRecipes()
    {
        var result = _parser.Parse("");
        result.Recipes.Should().BeEmpty();
    }

    [Fact]
    public void Parse_BadData_DoesNotThrow()
    {
        var badData = "MMMMM-----\nThis is not valid meal master data\nMMMMM";
        var act = () => _parser.Parse(badData);
        act.Should().NotThrow();
    }

    [Fact]
    public void Parse_HasInstructions()
    {
        var result = _parser.Parse(SingleRecipe);
        result.Recipes[0].Instructions.Should().NotBeEmpty();
        result.Recipes[0].Instructions[0].Text.Should().Contain("oven");
    }

    [Fact]
    public void CanParse_MmfExtension_ReturnsTrue()
    {
        _parser.CanParse("some content", "mmf").Should().BeTrue();
    }

    [Fact]
    public void CanParse_ContainsMmmm_ReturnsTrue()
    {
        _parser.CanParse("MMMMM some content").Should().BeTrue();
    }
}
