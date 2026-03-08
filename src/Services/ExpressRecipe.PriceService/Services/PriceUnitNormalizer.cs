namespace ExpressRecipe.PriceService.Services;

/// <summary>
/// Normalizes unit strings to a canonical set and computes per-unit price metrics.
/// Canonical units: oz, g, lb, kg, ml, l, each, 100g
/// </summary>
public interface IPriceUnitNormalizer
{
    /// <summary>
    /// Normalize an arbitrary unit string to the canonical form.
    /// Returns null when the unit cannot be recognized.
    /// </summary>
    string? NormalizeUnit(string? rawUnit);

    /// <summary>
    /// Compute per-unit price metrics (PricePerOz, PricePerHundredG) for the given price, unit, and quantity.
    /// Returns a new <see cref="PriceUnitMetrics"/> instance; does not mutate any input values.
    /// </summary>
    PriceUnitMetrics ComputeUnitPrices(decimal finalPrice, string? unit, decimal? quantity);
}

public sealed class PriceUnitMetrics
{
    public decimal? PricePerOz { get; init; }
    public decimal? PricePerHundredG { get; init; }
    public string? NormalizedUnit { get; init; }
    public decimal? TotalOz { get; init; }
    public decimal? TotalGrams { get; init; }
}

/// <inheritdoc />
public sealed class PriceUnitNormalizer : IPriceUnitNormalizer
{
    // Oz per other unit (for weight-based conversions)
    private const decimal OzPerLb = 16m;
    private const decimal OzPerKg = 35.274m;
    private const decimal OzPerG = 0.035274m;
    private const decimal GramsPerOz = 28.3495m;
    private const decimal GramsPerLb = 453.592m;
    private const decimal GramsPerKg = 1000m;
    // Ml per fluid unit
    private const decimal MlPerFlOz = 29.5735m;
    private const decimal MlPerL = 1000m;

    // Canonical unit tokens (lowercased)
    private static readonly Dictionary<string, string> UnitAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        { "oz", "oz" },
        { "fl oz", "oz" }, { "floz", "oz" }, { "fl.oz", "oz" }, { "fluid oz", "oz" }, { "fluid ounce", "oz" },
        { "ounce", "oz" }, { "ounces", "oz" },
        { "g", "g" }, { "gram", "g" }, { "grams", "g" },
        { "lb", "lb" }, { "lbs", "lb" }, { "pound", "lb" }, { "pounds", "lb" },
        { "kg", "kg" }, { "kilogram", "kg" }, { "kilograms", "kg" },
        { "ml", "ml" }, { "milliliter", "ml" }, { "millilitre", "ml" }, { "milliliters", "ml" }, { "millilitres", "ml" },
        { "l", "l" }, { "liter", "l" }, { "litre", "l" }, { "liters", "l" }, { "litres", "l" },
        { "each", "each" }, { "ea", "each" }, { "count", "each" }, { "ct", "each" }, { "pc", "each" }, { "pcs", "each" },
        { "100g", "100g" }
    };

    public string? NormalizeUnit(string? rawUnit)
    {
        if (string.IsNullOrWhiteSpace(rawUnit)) { return null; }

        var trimmed = rawUnit.Trim();
        if (UnitAliases.TryGetValue(trimmed, out var canonical)) { return canonical; }

        // Try lowercased
        var lower = trimmed.ToLowerInvariant();
        return UnitAliases.TryGetValue(lower, out canonical) ? canonical : null;
    }

    public PriceUnitMetrics ComputeUnitPrices(decimal finalPrice, string? unit, decimal? quantity)
    {
        var normalizedUnit = NormalizeUnit(unit);
        var qty = quantity ?? 1m;
        if (qty <= 0m) { qty = 1m; }

        decimal? totalOz = null;
        decimal? totalGrams = null;
        decimal? pricePerOz = null;
        decimal? pricePerHundredG = null;

        switch (normalizedUnit)
        {
            case "oz":
                totalOz = qty;
                totalGrams = qty * GramsPerOz;
                break;
            case "lb":
                totalOz = qty * OzPerLb;
                totalGrams = qty * GramsPerLb;
                break;
            case "kg":
                totalOz = qty * OzPerKg;
                totalGrams = qty * GramsPerKg;
                break;
            case "g":
                totalGrams = qty;
                totalOz = qty * OzPerG;
                break;
            case "100g":
                totalGrams = qty * 100m;
                totalOz = totalGrams * OzPerG;
                break;
            case "ml":
                // Treat ml as fluid oz equivalent for comparison (1 fl oz = 29.5735 ml)
                totalOz = qty / MlPerFlOz;
                break;
            case "l":
                totalOz = (qty * MlPerL) / MlPerFlOz;
                break;
        }

        if (totalOz.HasValue && totalOz.Value > 0m)
        {
            pricePerOz = finalPrice / totalOz.Value;
        }

        if (totalGrams.HasValue && totalGrams.Value > 0m)
        {
            pricePerHundredG = (finalPrice / totalGrams.Value) * 100m;
        }
        else if (totalOz.HasValue && totalOz.Value > 0m)
        {
            // Derive from oz: totalGrams = totalOz * GramsPerOz
            var derivedGrams = totalOz.Value * GramsPerOz;
            pricePerHundredG = (finalPrice / derivedGrams) * 100m;
        }

        return new PriceUnitMetrics
        {
            NormalizedUnit = normalizedUnit,
            TotalOz = totalOz,
            TotalGrams = totalGrams,
            PricePerOz = pricePerOz.HasValue ? Math.Round(pricePerOz.Value, 6) : null,
            PricePerHundredG = pricePerHundredG.HasValue ? Math.Round(pricePerHundredG.Value, 6) : null
        };
    }
}
