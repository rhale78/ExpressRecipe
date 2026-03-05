using ExpressRecipe.RecipeParser.Models;
using ExpressRecipe.RecipeParser.Parsers;
using FluentAssertions;

namespace ExpressRecipe.RecipeParser.Tests.Parsers;

public class CookLangParserTests
{
    private readonly CookLangParser _parser = new();

    private const string BasicRecipe = """
        >> title: Pasta Carbonara
        >> servings: 2
        >> author: Chef Mario

        Bring a large pot of salted water to a boil. Cook @spaghetti{200%g} until al dente.

        While pasta cooks, fry @bacon{150%g} in a #pan{} until crispy.

        In a bowl, whisk together @eggs{3} and @parmesan{50%g}.

        Combine pasta with bacon, remove from heat, add egg mixture. Season with @salt and @black pepper.

        Serve immediately with extra parmesan.
        """;

    [Fact]
    public void Parse_BasicRecipe_ExtractsMetadata()
    {
        var result = _parser.Parse(BasicRecipe);
        result.Success.Should().BeTrue();
        result.Recipes.Should().HaveCount(1);
        result.Recipes[0].Title.Should().Be("Pasta Carbonara");
        result.Recipes[0].Yield.Should().Be("2");
        result.Recipes[0].Author.Should().Be("Chef Mario");
    }

    [Fact]
    public void Parse_BasicRecipe_ExtractsIngredients()
    {
        var result = _parser.Parse(BasicRecipe);
        var ingredients = result.Recipes[0].Ingredients;
        ingredients.Should().Contain(i => i.Name.Contains("spaghetti"));
        ingredients.Should().Contain(i => i.Name.Contains("bacon"));
        ingredients.Should().Contain(i => i.Name.Contains("eggs"));
    }

    [Fact]
    public void Parse_IngredientWithUnit_ParsesQuantityAndUnit()
    {
        var result = _parser.Parse(BasicRecipe);
        var spaghetti = result.Recipes[0].Ingredients.FirstOrDefault(i => i.Name.Contains("spaghetti"));
        spaghetti.Should().NotBeNull();
        spaghetti!.Quantity.Should().Be("200");
        spaghetti.Unit.Should().Be("g");
    }

    [Fact]
    public void Parse_ExtractsCookware()
    {
        var result = _parser.Parse(BasicRecipe);
        result.Recipes[0].Instructions.Should().Contain(i => i.Cookware.Contains("pan"));
    }

    [Fact]
    public void Parse_CommentOnly_ReturnsRecipeWithNoContent()
    {
        var result = _parser.Parse("-- just a comment");
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
    }

    [Fact]
    public void CanParse_CookFile_ReturnsTrue()
    {
        _parser.CanParse("@ingredient{1%cup}", "cook").Should().BeTrue();
    }

    [Fact]
    public void Parse_WithTimer_ExtractsTimerText()
    {
        const string timerRecipe = """
            >> title: Boiled Eggs
            Boil @eggs{2} for ~{10%minutes}.
            """;
        var result = _parser.Parse(timerRecipe);
        result.Recipes[0].Instructions.Should().Contain(i => i.TimerText != null);
    }
}
