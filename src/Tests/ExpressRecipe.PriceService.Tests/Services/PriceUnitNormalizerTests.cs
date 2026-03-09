using ExpressRecipe.PriceService.Services;
using FluentAssertions;
using Xunit;

namespace ExpressRecipe.PriceService.Tests.Services;

/// <summary>
/// Tests for <see cref="PriceUnitNormalizer"/> — unit normalization and per-unit price computation.
/// </summary>
public class PriceUnitNormalizerTests
{
    private readonly PriceUnitNormalizer _sut = new();

    // ── Unit normalization ────────────────────────────────────────────────────

    [Theory]
    [InlineData("oz", "oz")]
    [InlineData("Oz", "oz")]
    [InlineData("OZ", "oz")]
    [InlineData("fl oz", "oz")]
    [InlineData("fluid oz", "oz")]
    [InlineData("ounce", "oz")]
    [InlineData("ounces", "oz")]
    [InlineData("g", "g")]
    [InlineData("gram", "g")]
    [InlineData("lb", "lb")]
    [InlineData("lbs", "lb")]
    [InlineData("pound", "lb")]
    [InlineData("kg", "kg")]
    [InlineData("ml", "ml")]
    [InlineData("l", "l")]
    [InlineData("liter", "l")]
    [InlineData("each", "each")]
    [InlineData("ea", "each")]
    [InlineData("ct", "each")]
    [InlineData("100g", "100g")]
    public void NormalizeUnit_KnownUnit_ReturnsCanonical(string input, string expected)
    {
        _sut.NormalizeUnit(input).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("banana")]
    public void NormalizeUnit_UnknownUnit_ReturnsNull(string? input)
    {
        _sut.NormalizeUnit(input).Should().BeNull();
    }

    // ── 12-pack of 12-oz cans → 144 oz total → price/oz ─────────────────────

    [Fact]
    public void ComputeUnitPrices_12PackOf12OzCans_PricePerOzCorrect()
    {
        // A 12-pack of 12 oz cans = 144 oz total. If priced at $7.99
        // the price/oz should be $7.99 / 144 ≈ 0.055486
        const decimal price = 7.99m;
        const decimal quantity = 144m;
        const string unit = "oz";

        var result = _sut.ComputeUnitPrices(price, unit, quantity);

        result.PricePerOz.Should().BeApproximately(price / quantity, 0.0001m);
        result.NormalizedUnit.Should().Be("oz");
    }

    // ── 2-liter bottle → 67.63 fl oz → price/oz ──────────────────────────────

    [Fact]
    public void ComputeUnitPrices_2LiterBottle_PricePerOzApprox67Oz()
    {
        // 2 liters = 2000 ml / 29.5735 ml per fl oz ≈ 67.63 oz
        const decimal price = 1.89m;
        const decimal quantity = 2m;
        const string unit = "l";

        var result = _sut.ComputeUnitPrices(price, unit, quantity);

        // Expected: price / 67.63 fl oz
        result.PricePerOz.Should().BeApproximately(price / 67.63m, 0.001m);
        result.NormalizedUnit.Should().Be("l");
    }

    // ── Per-100g calculation ──────────────────────────────────────────────────

    [Fact]
    public void ComputeUnitPrices_500g_PricePerHundredGCorrect()
    {
        const decimal price = 3.49m;
        const decimal quantity = 500m;
        const string unit = "g";

        var result = _sut.ComputeUnitPrices(price, unit, quantity);

        // price/500g * 100 = price/5
        result.PricePerHundredG.Should().BeApproximately(price / 5m, 0.0001m);
    }

    // ── No unit (each) returns no per-oz price ────────────────────────────────

    [Fact]
    public void ComputeUnitPrices_Each_NoOzPrice()
    {
        var result = _sut.ComputeUnitPrices(2.99m, "each", 1m);
        result.PricePerOz.Should().BeNull();
        result.PricePerHundredG.Should().BeNull();
        result.NormalizedUnit.Should().Be("each");
    }

    // ── Pound-based unit ──────────────────────────────────────────────────────

    [Fact]
    public void ComputeUnitPrices_OnePound_PricePerOzCorrect()
    {
        const decimal price = 4.00m;
        const decimal quantity = 1m;
        const string unit = "lb";

        var result = _sut.ComputeUnitPrices(price, unit, quantity);

        // 1 lb = 16 oz → price/oz = 4.00 / 16 = 0.25
        result.PricePerOz.Should().BeApproximately(0.25m, 0.0001m);
    }
}
