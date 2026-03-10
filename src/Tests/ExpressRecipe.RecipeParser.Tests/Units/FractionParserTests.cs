using ExpressRecipe.Shared.Units;

namespace ExpressRecipe.RecipeParser.Tests.Units;

public class FractionParserTests
{
    [Theory]
    [InlineData("\u00BD", 0.5)]          // ½
    [InlineData("1/2", 0.5)]
    [InlineData("1 1/2", 1.5)]
    [InlineData("1\u00BD", 1.5)]         // 1½
    [InlineData("1 \u00BD", 1.5)]        // 1 ½
    [InlineData("\u00BC", 0.25)]         // ¼
    [InlineData("\u00BE", 0.75)]         // ¾
    [InlineData("\u215B", 0.125)]        // ⅛
    [InlineData("2.5", 2.5)]
    [InlineData(".5", 0.5)]
    [InlineData("3", 3.0)]
    [InlineData("0", 0.0)]
    public void ParseFraction_ValidInputs_ReturnsExpected(string input, double expected)
    {
        decimal? result = FractionParser.ParseFraction(input);
        result.Should().NotBeNull();
        result!.Value.Should().BeApproximately((decimal)expected, 0.001m);
    }

    [Theory]
    [InlineData("2\u2153", 2.333)] // 2⅓
    public void ParseFraction_TwoThirds_ReturnsApproximate(string input, double expected)
    {
        decimal? result = FractionParser.ParseFraction(input);
        result.Should().NotBeNull();
        result!.Value.Should().BeApproximately((decimal)expected, 0.001m);
    }

    [Theory]
    [InlineData("&frac12;", 0.5)]
    [InlineData("&#189;", 0.5)]
    [InlineData("&#xBD;", 0.5)]
    [InlineData("&frac14;", 0.25)]
    [InlineData("&frac34;", 0.75)]
    [InlineData("&#188;", 0.25)]
    public void ParseFraction_HtmlEntities_ReturnsExpected(string input, double expected)
    {
        decimal? result = FractionParser.ParseFraction(input);
        result.Should().NotBeNull();
        result!.Value.Should().BeApproximately((decimal)expected, 0.001m);
    }

    [Fact]
    public void ParseFraction_UnicodeFractionSlash_ReturnsHalf()
    {
        // U+2044 fraction slash between 1 and 2
        string input = "1\u20442";
        decimal? result = FractionParser.ParseFraction(input);
        result.Should().NotBeNull();
        result!.Value.Should().BeApproximately(0.5m, 0.001m);
    }

    [Fact]
    public void ParseFraction_Range_ReturnsAverage()
    {
        decimal? result = FractionParser.ParseFraction("2-3");
        result.Should().NotBeNull();
        result!.Value.Should().BeApproximately(2.5m, 0.001m);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("to taste")]
    [InlineData("pinch")]
    public void ParseFraction_InvalidInputs_ReturnsNull(string? input)
    {
        FractionParser.ParseFraction(input).Should().BeNull();
    }

    [Theory]
    [InlineData("1\u00BD", "1 1/2")]     // 1½ → 1 1/2
    [InlineData("2\u00BC", "2 1/4")]     // 2¼ → 2 1/4
    [InlineData("&frac12;", "1/2")]
    [InlineData("1/2", "1/2")]
    [InlineData("1 1/2", "1 1/2")]
    public void Normalize_VariousInputs_ReturnsAscii(string input, string expected)
    {
        FractionParser.Normalize(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("\u00BD", true)]
    [InlineData("&frac12;", true)]
    [InlineData("1/2", false)]
    [InlineData("1.5", false)]
    [InlineData(null, false)]
    public void ContainsFraction_ReturnsExpected(string? input, bool expected)
    {
        FractionParser.ContainsFraction(input).Should().Be(expected);
    }
}
