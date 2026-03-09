using ExpressRecipe.Shared.Units;
using FluentAssertions;
using Moq;

namespace ExpressRecipe.Shared.Tests.Units;

public class UnitConversionServiceTests
{
    private static IUnitConversionService CreateService(decimal? density = null)
    {
        Mock<IIngredientDensityResolver> mockResolver = new();
        mockResolver
            .Setup(r => r.GetDensityAsync(It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(density);
        return new UnitConversionService(mockResolver.Object);
    }

    [Fact]
    public async Task ToCanonicalAsync_250ml_NoDensity_Returns250ml()
    {
        IUnitConversionService svc = CreateService(density: null);
        ConversionResult result = await svc.ToCanonicalAsync(250m, UnitCode.Milliliter);
        result.Success.Should().BeTrue();
        result.Value.Should().Be(250m);
        result.Unit.Should().Be(UnitCode.Milliliter);
    }

    [Fact]
    public async Task ToCanonicalAsync_1Cup_WithFlourDensity_ReturnsGrams()
    {
        decimal flourDensity = 0.5292m; // g/ml
        IUnitConversionService svc = CreateService(density: flourDensity);
        ConversionResult result = await svc.ToCanonicalAsync(1m, UnitCode.Cup, ingredientName: "all-purpose flour");
        result.Success.Should().BeTrue();
        result.Unit.Should().Be(UnitCode.Gram);
        // 1 cup = 236.58824 ml × 0.5292 g/ml ≈ 125.16 g
        result.Value.Should().BeApproximately(125.12m, 0.5m);
    }

    [Fact]
    public async Task ToCanonicalAsync_Uncountable_ReturnsFailure()
    {
        IUnitConversionService svc = CreateService();
        ConversionResult result = await svc.ToCanonicalAsync(0m, UnitCode.Pinch);
        result.Success.Should().BeFalse();
        result.FailureReason.Should().Be(ConversionFailureReason.Uncountable);
    }

    [Fact]
    public void Scale_4to6Servings_Returns375()
    {
        IUnitConversionService svc = CreateService();
        decimal scaled = svc.Scale(250m, 4m, 6m);
        scaled.Should().Be(375m);
    }

    [Fact]
    public void Scale_ZeroFromServings_ReturnsSameAmount()
    {
        IUnitConversionService svc = CreateService();
        decimal scaled = svc.Scale(250m, 0m, 6m);
        scaled.Should().Be(250m);
    }

    [Fact]
    public void ToDisplay_125grams_Metric_Returns125g()
    {
        IUnitConversionService svc = CreateService();
        ConversionResult result = svc.ToDisplay(125m, UnitCode.Gram, UnitSystemPreference.Metric);
        result.Success.Should().BeTrue();
        result.Value.Should().BeApproximately(125m, 0.01m);
        result.Unit.Should().Be(UnitCode.Gram);
        result.DisplayString.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CompareAsync_SameUnit_ReturnsComparison()
    {
        IUnitConversionService svc = CreateService();
        int? comparison = await svc.CompareAsync(500m, UnitCode.Gram, 250m, UnitCode.Gram);
        comparison.Should().BeGreaterThan(0); // 500g > 250g
    }

    [Fact]
    public async Task CompareAsync_DifferentButCompatibleUnits_ReturnsComparison()
    {
        IUnitConversionService svc = CreateService();
        int? comparison = await svc.CompareAsync(1m, UnitCode.Kilogram, 500m, UnitCode.Gram);
        comparison.Should().BeGreaterThan(0); // 1kg > 500g
    }
}
