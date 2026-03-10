using ExpressRecipe.Shared.Units;
using FluentAssertions;

namespace ExpressRecipe.Shared.Tests.Units;

public class UnitParserTests
{
    [Theory]
    [InlineData("2½ cups", 2.5, UnitCode.Cup)]
    [InlineData("1/4 tsp", 0.25, UnitCode.Teaspoon)]
    [InlineData("to taste", 0, UnitCode.ToTaste)]
    [InlineData("as needed", 0, UnitCode.ToTaste)]
    [InlineData("as required", 0, UnitCode.ToTaste)]
    public void Parse_CommonInputs_ReturnsExpected(string input, decimal expectedAmount, UnitCode expectedUnit)
    {
        (decimal amount, UnitCode unit) = UnitParser.Parse(input);
        amount.Should().BeApproximately(expectedAmount, 0.001m);
        unit.Should().Be(expectedUnit);
    }

    [Fact]
    public void Parse_GasMark4_ReturnsFourGasMark()
    {
        (decimal amount, UnitCode unit) = UnitParser.Parse("gas mark 4");
        amount.Should().Be(4m);
        unit.Should().Be(UnitCode.GasMark);
    }

    [Fact]
    public void Parse_350F_ReturnsFahrenheit()
    {
        (decimal amount, UnitCode unit) = UnitParser.Parse("350°F");
        amount.Should().Be(350m);
        unit.Should().Be(UnitCode.Fahrenheit);
    }

    [Fact]
    public void Parse_MixedNumber_ReturnsCorrectAmount()
    {
        (decimal amount, UnitCode unit) = UnitParser.Parse("2 1/2 cups");
        amount.Should().BeApproximately(2.5m, 0.001m);
        unit.Should().Be(UnitCode.Cup);
    }

    [Fact]
    public void Parse_UnicodeHalfCup_ReturnsCup()
    {
        (decimal amount, UnitCode unit) = UnitParser.Parse("½ cup");
        amount.Should().BeApproximately(0.5m, 0.001m);
        unit.Should().Be(UnitCode.Cup);
    }

    [Fact]
    public void Parse_PinchOfSalt_ReturnsPinch()
    {
        (decimal amount, UnitCode unit) = UnitParser.Parse("pinch of salt");
        amount.Should().Be(0m);
        unit.Should().Be(UnitCode.Pinch);
    }

    [Fact]
    public void Parse_DashOfPepper_ReturnsDash()
    {
        (decimal amount, UnitCode unit) = UnitParser.Parse("dash of pepper");
        amount.Should().Be(0m);
        unit.Should().Be(UnitCode.Dash);
    }

    [Fact]
    public void Parse_Null_ReturnsUnknown()
    {
        (decimal amount, UnitCode unit) = UnitParser.Parse(null);
        amount.Should().Be(0m);
        unit.Should().Be(UnitCode.Unknown);
    }

    [Fact]
    public void Parse_Empty_ReturnsUnknown()
    {
        (decimal amount, UnitCode unit) = UnitParser.Parse("");
        amount.Should().Be(0m);
        unit.Should().Be(UnitCode.Unknown);
    }

    [Theory]
    [InlineData("g", UnitDimension.Mass)]
    [InlineData("kg", UnitDimension.Mass)]
    [InlineData("oz", UnitDimension.Mass)]
    [InlineData("lb", UnitDimension.Mass)]
    [InlineData("ml", UnitDimension.Volume)]
    [InlineData("cup", UnitDimension.Volume)]
    [InlineData("tsp", UnitDimension.Volume)]
    [InlineData("tbsp", UnitDimension.Volume)]
    [InlineData("°C", UnitDimension.Temperature)]
    [InlineData("°F", UnitDimension.Temperature)]
    public void Parse_UnitString_CorrectDimension(string unitStr, UnitDimension expectedDim)
    {
        (_, UnitCode unit) = UnitParser.Parse($"1 {unitStr}");
        UnitParser.GetDimension(unit).Should().Be(expectedDim);
    }

    [Fact]
    public void Parse_3Eggs_ReturnsCount()
    {
        (decimal amount, UnitCode unit) = UnitParser.Parse("3 each");
        amount.Should().Be(3m);
        unit.Should().Be(UnitCode.Each);
    }

    [Fact]
    public void Parse_PlainNumber_ReturnsEach()
    {
        (decimal amount, UnitCode unit) = UnitParser.Parse("3");
        amount.Should().Be(3m);
        unit.Should().Be(UnitCode.Each);
    }

    [Fact]
    public void Parse_Grams_ReturnsGram()
    {
        (decimal amount, UnitCode unit) = UnitParser.Parse("250g");
        amount.Should().Be(250m);
        unit.Should().Be(UnitCode.Gram);
    }

    [Fact]
    public void Parse_Pound_ReturnsPound()
    {
        (decimal amount, UnitCode unit) = UnitParser.Parse("1 lb");
        amount.Should().Be(1m);
        unit.Should().Be(UnitCode.Pound);
    }
}
