using ExpressRecipe.PriceService.Data;
using ExpressRecipe.PriceService.Services;
using FluentAssertions;
using Xunit;

namespace ExpressRecipe.PriceService.Tests.Services;

/// <summary>
/// Tests for <see cref="EffectivePriceCalculator"/> — deal effective price computation.
/// </summary>
public class EffectivePriceCalculatorTests
{
    private readonly EffectivePriceCalculator _sut = new();

    private static DealDto BuildDeal(
        string discountType,
        decimal basePrice,
        decimal salePrice = 0,
        int? buyQty = null,
        int? getQty = null,
        decimal? getPercentOff = null,
        decimal? rebateAmount = null)
    {
        return new DealDto
        {
            ProductId = Guid.NewGuid(),
            StoreId = Guid.NewGuid(),
            DealType = discountType,
            DiscountType = discountType,
            OriginalPrice = basePrice,
            SalePrice = salePrice > 0 ? salePrice : basePrice,
            BuyQuantity = buyQty,
            GetQuantity = getQty,
            GetPercentOff = getPercentOff,
            RebateAmount = rebateAmount
        };
    }

    // ── BOGO — buy 1 get 1 free: qty=1 → 50% effective ─────────────────────

    [Fact]
    public void Calculate_BOGO_Qty1_EffectiveIs50Pct()
    {
        var deal = BuildDeal("BuyOneGetOne", basePrice: 3.00m, buyQty: 1, getQty: 1);
        var result = _sut.Calculate(3.00m, deal, 2);

        result.EffectivePricePerUnit.Should().Be(1.50m);
        result.TotalCost.Should().Be(3.00m); // 1 paid × $3
        result.SavingsPct.Should().Be(50m);
    }

    // ── B2G1 free — buy 3 units: 33% savings ─────────────────────────────────

    [Fact]
    public void Calculate_B2G1Free_Qty3_33PctSavings()
    {
        var deal = BuildDeal("BuyNGetMFree", basePrice: 2.00m, buyQty: 2, getQty: 1);
        var result = _sut.Calculate(2.00m, deal, 3);

        // Pay for 2 out of 3 → total = 4.00; effective per unit = 4/3 ≈ 1.333
        result.TotalCost.Should().Be(4.00m);
        result.SavingsPct.Should().BeApproximately(33.33m, 0.01m);
    }

    // ── Coupon $1.00 off ─────────────────────────────────────────────────────

    [Fact]
    public void Calculate_Coupon_1DollarOff_PriceReducedBy1()
    {
        var deal = BuildDeal("Coupon", basePrice: 5.00m, rebateAmount: 1.00m);
        var result = _sut.Calculate(5.00m, deal, 1);

        result.EffectivePricePerUnit.Should().Be(4.00m);
        result.Savings.Should().Be(1.00m);
    }

    // ── GetPercentOff 25% ─────────────────────────────────────────────────────

    [Fact]
    public void Calculate_GetPercentOff25_PriceReducedBy25Pct()
    {
        var deal = BuildDeal("FlyerSale", basePrice: 4.00m, getPercentOff: 25m);
        var result = _sut.Calculate(4.00m, deal, 1);

        result.EffectivePricePerUnit.Should().Be(3.00m);
        result.SavingsPct.Should().Be(25m);
    }

    // ── No deal — returns base price unchanged ────────────────────────────────

    [Fact]
    public void Calculate_NoDeal_ReturnsBasePrice()
    {
        var result = _sut.Calculate(2.50m, null, 4);

        result.EffectivePricePerUnit.Should().Be(2.50m);
        result.TotalCost.Should().Be(10.00m);
        result.Savings.Should().Be(0m);
        result.AppliedDealType.Should().BeNull();
    }

    // ── InstantRebate $0.50 ──────────────────────────────────────────────────

    [Fact]
    public void Calculate_InstantRebate_ReducesByRebateAmount()
    {
        var deal = BuildDeal("InstantRebate", basePrice: 3.00m, rebateAmount: 0.50m);
        var result = _sut.Calculate(3.00m, deal, 2);

        result.EffectivePricePerUnit.Should().Be(2.50m);
        result.TotalCost.Should().Be(5.00m);
    }
}
