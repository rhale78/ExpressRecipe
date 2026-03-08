using ExpressRecipe.PriceService.Data;

namespace ExpressRecipe.PriceService.Services;

/// <summary>
/// Computes the effective (net) per-unit price after applying BOGO, coupon, rebate,
/// and other structured deal types from the <see cref="DealDto"/> model.
/// </summary>
public interface IEffectivePriceCalculator
{
    /// <summary>
    /// Returns the effective price per unit after applying the best available deal.
    /// </summary>
    EffectivePriceResult Calculate(decimal basePrice, DealDto? deal, int quantity);
}

public sealed class EffectivePriceResult
{
    public decimal BasePrice { get; init; }
    public decimal EffectivePricePerUnit { get; init; }
    public decimal TotalCost { get; init; }
    public decimal Savings { get; init; }
    public decimal SavingsPct { get; init; }
    public string? AppliedDealType { get; init; }
}

/// <inheritdoc />
public sealed class EffectivePriceCalculator : IEffectivePriceCalculator
{
    public EffectivePriceResult Calculate(decimal basePrice, DealDto? deal, int quantity)
    {
        if (quantity <= 0) { quantity = 1; }

        if (deal == null)
        {
            return new EffectivePriceResult
            {
                BasePrice = basePrice,
                EffectivePricePerUnit = basePrice,
                TotalCost = basePrice * quantity,
                Savings = 0,
                SavingsPct = 0
            };
        }

        decimal totalCost;
        decimal effectivePerUnit;

        var discountType = deal.DiscountType ?? deal.DealType;

        switch (discountType)
        {
            case "BuyOneGetOne":
            {
                var buyQty = deal.BuyQuantity ?? 1;
                var getQty = deal.GetQuantity ?? 1;
                var cycleSize = buyQty + getQty;
                var fullCycles = quantity / cycleSize;
                var remainder = quantity % cycleSize;
                var paidUnits = (fullCycles * buyQty) + Math.Min(remainder, buyQty);
                totalCost = paidUnits * basePrice;
                effectivePerUnit = quantity > 0 ? totalCost / quantity : basePrice;
                break;
            }
            case "BuyNGetMFree":
            {
                var buyQty = deal.BuyQuantity ?? 1;
                var getQty = deal.GetQuantity ?? 1;
                var cycleSize = buyQty + getQty;
                var fullCycles = quantity / cycleSize;
                var remainder = quantity % cycleSize;
                var paidUnits = (fullCycles * buyQty) + Math.Min(remainder, buyQty);
                totalCost = paidUnits * basePrice;
                effectivePerUnit = quantity > 0 ? totalCost / quantity : basePrice;
                break;
            }
            case "InstantRebate":
            case "StoreRebate":
            case "Coupon":
            {
                var rebate = deal.RebateAmount ?? 0m;
                effectivePerUnit = Math.Max(0m, basePrice - rebate);
                totalCost = effectivePerUnit * quantity;
                break;
            }
            default:
            {
                // FlyerSale / DigitalCoupon / Clearance / GetPercentOff
                if (deal.GetPercentOff.HasValue && deal.GetPercentOff.Value > 0m)
                {
                    var clampedPct = Math.Clamp(deal.GetPercentOff.Value, 0m, 100m);
                    effectivePerUnit = Math.Max(0m, basePrice * (1m - clampedPct / 100m));
                }
                else if (deal.RebateAmount.HasValue && deal.RebateAmount.Value > 0m)
                {
                    effectivePerUnit = Math.Max(0m, basePrice - deal.RebateAmount.Value);
                }
                else
                {
                    // Fall back to SalePrice from the deal record
                    effectivePerUnit = deal.SalePrice > 0m ? deal.SalePrice : basePrice;
                }
                totalCost = effectivePerUnit * quantity;
                break;
            }
        }

        var savings = (basePrice - effectivePerUnit) * quantity;
        var savingsPct = basePrice > 0m ? ((basePrice - effectivePerUnit) / basePrice) * 100m : 0m;

        return new EffectivePriceResult
        {
            BasePrice = basePrice,
            EffectivePricePerUnit = Math.Round(effectivePerUnit, 4),
            TotalCost = Math.Round(totalCost, 4),
            Savings = Math.Round(savings, 4),
            SavingsPct = Math.Round(savingsPct, 2),
            AppliedDealType = discountType
        };
    }
}
