using ExpressRecipe.RecipeParser.Helpers;
using FluentAssertions;

namespace ExpressRecipe.RecipeParser.Tests.Parsers;

public class TextParserHelperTests
{
    [Theory]
    [InlineData("2 cups flour", "2", "cups", "flour")]
    [InlineData("1/2 cup sugar", "1/2", "cup", "sugar")]
    [InlineData("1 1/2 tsp salt", "1 1/2", "tsp", "salt")]
    [InlineData("3 eggs", "3", null, "eggs")]
    [InlineData("pinch of salt", null, null, "pinch of salt")]
    public void ParseIngredientLine_VariousInputs_ReturnsCorrectParsing(
        string line, string? expectedQty, string? expectedUnit, string expectedName)
    {
        var (qty, unit, name) = TextParserHelper.ParseIngredientLine(line);
        qty.Should().Be(expectedQty);
        unit.Should().Be(expectedUnit);
        name.Should().Be(expectedName);
    }

    [Fact]
    public void ParseIngredientLine_EmptyString_ReturnsEmptyName()
    {
        var (qty, unit, name) = TextParserHelper.ParseIngredientLine("");
        qty.Should().BeNull();
        unit.Should().BeNull();
        name.Should().BeEmpty();
    }

    [Fact]
    public void NormalizeWhitespace_MultipleSpaces_CollapseToSingle()
    {
        var result = TextParserHelper.NormalizeWhitespace("hello   world  foo");
        result.Should().Be("hello world foo");
    }

    [Fact]
    public void NormalizeWhitespace_WhitespaceOnly_ReturnsEmpty()
    {
        var result = TextParserHelper.NormalizeWhitespace("   \t  ");
        result.Should().BeEmpty();
    }

    [Fact]
    public void ExtractPreparation_WithComma_SplitsCorrectly()
    {
        var name = "garlic, minced";
        var prep = TextParserHelper.ExtractPreparation(ref name);
        name.Should().Be("garlic");
        prep.Should().Be("minced");
    }

    [Fact]
    public void ExtractPreparation_WithoutComma_ReturnsEmpty()
    {
        var name = "flour";
        var prep = TextParserHelper.ExtractPreparation(ref name);
        name.Should().Be("flour");
        prep.Should().BeEmpty();
    }

    [Fact]
    public void SplitLines_MultilineInput_SplitsCorrectly()
    {
        var lines = TextParserHelper.SplitLines("line1\nline2\nline3");
        lines.Should().HaveCount(3);
        lines[0].Should().Be("line1");
        lines[2].Should().Be("line3");
    }
}
