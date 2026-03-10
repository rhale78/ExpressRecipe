using ExpressRecipe.Shared.Units;

namespace ExpressRecipe.RecipeParser.Tests.Units;

public class FractionFormatterTests
{
    [Theory]
    [InlineData(0.5, "\u00BD")]      // ½
    [InlineData(0.25, "\u00BC")]     // ¼
    [InlineData(0.75, "\u00BE")]     // ¾
    [InlineData(0.125, "\u215B")]    // ⅛
    [InlineData(1.5, "1\u00BD")]     // 1½
    [InlineData(3.0, "3")]
    [InlineData(0.0, "0")]
    public void Format_FractionMode_ReturnsExpected(double value, string expected)
    {
        FractionFormatter.Format((decimal)value, NumberFormat.Fraction).Should().Be(expected);
    }

    [Theory]
    [InlineData(0.333, "\u2153")]    // ⅓ (within tolerance)
    [InlineData(0.667, "\u2154")]    // ⅔ (within tolerance)
    [InlineData(2.333, "2\u2153")]   // 2⅓
    public void Format_ApproximateFractions_ReturnsNearestSymbol(double value, string expected)
    {
        FractionFormatter.Format((decimal)value, NumberFormat.Fraction).Should().Be(expected);
    }

    [Fact]
    public void Format_ValueWithinTolerance_ReturnsNearestFraction()
    {
        // 0.6 is exactly 3/5 → ⅗
        string result = FractionFormatter.Format(0.6m, NumberFormat.Fraction);
        result.Should().Be("\u2157"); // ⅗
    }

    [Theory]
    [InlineData(100.5, "100.5")]
    [InlineData(-1.0, "-1")]
    public void Format_LargeOrNegativeValues_UsesDecimal(double value, string expected)
    {
        FractionFormatter.Format((decimal)value, NumberFormat.Fraction).Should().Be(expected);
    }

    [Theory]
    [InlineData(0.5, "0.5")]
    [InlineData(0.25, "0.25")]
    [InlineData(0.75, "0.75")]
    [InlineData(0.125, "0.125")]
    [InlineData(1.5, "1.5")]
    [InlineData(3.0, "3")]
    [InlineData(0.333, "0.333")]
    [InlineData(0.667, "0.667")]
    [InlineData(100.5, "100.5")]
    public void Format_DecimalMode_ReturnsDecimalString(double value, string expected)
    {
        FractionFormatter.Format((decimal)value, NumberFormat.Decimal).Should().Be(expected);
    }
}
