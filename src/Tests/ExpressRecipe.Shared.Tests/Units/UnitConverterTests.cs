using ExpressRecipe.Shared.Units;
using FluentAssertions;

namespace ExpressRecipe.Shared.Tests.Units;

public class UnitConverterTests
{
    [Fact]
    public void ToCelsius_Fahrenheit350_Returns176_67()
    {
        decimal celsius = UnitConverter.ToCelsius(350m, UnitCode.Fahrenheit);
        celsius.Should().BeApproximately(176.67m, 0.01m);
    }

    [Fact]
    public void ToCelsius_GasMark4_Returns180()
    {
        decimal celsius = UnitConverter.ToCelsius(4m, UnitCode.GasMark);
        celsius.Should().Be(180m);
    }

    [Fact]
    public void ToCelsius_Celsius_ReturnsSame()
    {
        decimal celsius = UnitConverter.ToCelsius(100m, UnitCode.Celsius);
        celsius.Should().Be(100m);
    }

    [Fact]
    public void ToMilliliters_Cup_Returns236_588()
    {
        decimal ml = UnitConverter.ToMilliliters(1m, UnitCode.Cup);
        ml.Should().BeApproximately(236.588m, 0.001m);
    }

    [Fact]
    public void ToMilliliters_UkCup_Returns284_131()
    {
        decimal ml = UnitConverter.ToMilliliters(1m, UnitCode.UkCup);
        ml.Should().BeApproximately(284.131m, 0.001m);
    }

    [Fact]
    public void ToMilliliters_Liter_Returns1000()
    {
        decimal ml = UnitConverter.ToMilliliters(1m, UnitCode.Liter);
        ml.Should().Be(1000m);
    }

    [Fact]
    public void ToGrams_Pound_Returns453_592()
    {
        decimal grams = UnitConverter.ToGrams(1m, UnitCode.Pound);
        grams.Should().BeApproximately(453.592m, 0.001m);
    }

    [Fact]
    public void ToGrams_Ounce_Returns28_35()
    {
        decimal grams = UnitConverter.ToGrams(1m, UnitCode.Ounce);
        grams.Should().BeApproximately(28.35m, 0.01m);
    }

    [Fact]
    public void ToGrams_Kilogram_Returns1000()
    {
        decimal grams = UnitConverter.ToGrams(1m, UnitCode.Kilogram);
        grams.Should().Be(1000m);
    }

    [Fact]
    public void ToCanonical_Uncountable_ReturnsFailure()
    {
        ConversionResult result = UnitConverter.ToCanonical(0m, UnitCode.Pinch);
        result.Success.Should().BeFalse();
        result.FailureReason.Should().Be(ConversionFailureReason.Uncountable);
    }

    [Fact]
    public void FromCanonical_GramsToOunce_CorrectRatio()
    {
        ConversionResult result = UnitConverter.FromCanonical(28.34952m, UnitCode.Gram, UnitCode.Ounce);
        result.Success.Should().BeTrue();
        result.Value.Should().BeApproximately(1m, 0.001m);
    }

    [Fact]
    public void FromCanonical_MlToCup_CorrectRatio()
    {
        ConversionResult result = UnitConverter.FromCanonical(236.58824m, UnitCode.Milliliter, UnitCode.Cup);
        result.Success.Should().BeTrue();
        result.Value.Should().BeApproximately(1m, 0.001m);
    }

    [Fact]
    public void FromCanonical_IncompatibleDimensions_ReturnsIncompatibleDimensionsReason()
    {
        ConversionResult result = UnitConverter.FromCanonical(100m, UnitCode.Gram, UnitCode.Milliliter);
        result.Success.Should().BeFalse();
        result.FailureReason.Should().Be(ConversionFailureReason.IncompatibleDimensions);
    }

    [Fact]
    public void ToCanonical_EachCount_ReturnsAmount()
    {
        ConversionResult result = UnitConverter.ToCanonical(3m, UnitCode.Each);
        result.Success.Should().BeTrue();
        result.Value.Should().Be(3m);
        result.Unit.Should().Be(UnitCode.Each);
    }
}
