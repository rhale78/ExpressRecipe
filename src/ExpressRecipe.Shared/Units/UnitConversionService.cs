using System.Text;

namespace ExpressRecipe.Shared.Units;
public sealed class UnitConversionService : IUnitConversionService
{
    private readonly IIngredientDensityResolver _density;
    public UnitConversionService(IIngredientDensityResolver density) { _density = density; }

    public async Task<ConversionResult> ToCanonicalAsync(decimal amount, UnitCode fromUnit,
        Guid? ingredientId = null, string? ingredientName = null, CancellationToken ct = default)
    {
        UnitDimension dim = UnitParser.GetDimension(fromUnit);
        if (dim is UnitDimension.Mass or UnitDimension.Temperature or UnitDimension.Count)
        {
            return UnitConverter.ToCanonical(amount, fromUnit);
        }
        if (dim == UnitDimension.Uncountable) { return ConversionResult.Uncountable(fromUnit); }
        if (dim == UnitDimension.Volume)
        {
            decimal ml = UnitConverter.ToMilliliters(amount, fromUnit);
            decimal? density = await _density.GetDensityAsync(ingredientId, ingredientName, ct);
            return density.HasValue
                ? ConversionResult.Ok(ml * density.Value, UnitCode.Gram)
                : ConversionResult.Ok(ml, UnitCode.Milliliter);
        }
        return ConversionResult.BadUnit(fromUnit.ToString());
    }

    public ConversionResult ToDisplay(decimal canonicalAmount, UnitCode canonicalUnit,
        UnitSystemPreference preference, UnitCode? forceUnit = null)
    {
        UnitCode displayUnit = forceUnit ?? ChooseDisplayUnit(canonicalUnit, canonicalAmount, preference);
        ConversionResult result = UnitConverter.FromCanonical(canonicalAmount, canonicalUnit, displayUnit);
        if (!result.Success) { return result; }
        string display = FormatWithFractions(result.Value!.Value, displayUnit);
        return result with { DisplayString = display };
    }

    public decimal Scale(decimal canonicalAmount, decimal fromServings, decimal toServings)
    {
        if (fromServings == 0m) { return canonicalAmount; }
        return canonicalAmount * (toServings / fromServings);
    }

    public async Task<int?> CompareAsync(decimal amount1, UnitCode unit1, decimal amount2, UnitCode unit2,
        Guid? ingredientId = null, string? ingredientName = null, CancellationToken ct = default)
    {
        ConversionResult r1 = await ToCanonicalAsync(amount1, unit1, ingredientId, ingredientName, ct);
        ConversionResult r2 = await ToCanonicalAsync(amount2, unit2, ingredientId, ingredientName, ct);
        if (!r1.Success || !r2.Success || r1.Unit != r2.Unit) { return null; }
        return r1.Value!.Value.CompareTo(r2.Value!.Value);
    }

    private static UnitCode ChooseDisplayUnit(UnitCode canonicalUnit, decimal amount,
        UnitSystemPreference preference)
    {
        if (canonicalUnit == UnitCode.Gram)
        {
            return preference switch
            {
                UnitSystemPreference.US => amount >= 453m ? UnitCode.Pound : UnitCode.Ounce,
                _ => amount >= 1000m ? UnitCode.Kilogram : UnitCode.Gram
            };
        }
        if (canonicalUnit == UnitCode.Milliliter)
        {
            if (preference == UnitSystemPreference.Metric)
            {
                return amount >= 1000m ? UnitCode.Liter : UnitCode.Milliliter;
            }
            if (preference == UnitSystemPreference.UK)
            {
                return amount >= 284m ? UnitCode.UkCup : amount >= 15m ? UnitCode.UkTablespoon : UnitCode.UkTeaspoon;
            }
            return amount >= 236m ? UnitCode.Cup : amount >= 29.6m ? UnitCode.FluidOunce :
                   amount >= 14.8m ? UnitCode.Tablespoon : UnitCode.Teaspoon;
        }
        if (canonicalUnit == UnitCode.Celsius)
        {
            return preference == UnitSystemPreference.US ? UnitCode.Fahrenheit : UnitCode.Celsius;
        }
        return canonicalUnit;
    }

    private static string FormatWithFractions(decimal value, UnitCode unit)
    {
        string unitLabel = UnitCodeToLabel(unit);

        // For large values or temperature, use decimal only
        if (value >= 100m || unit is UnitCode.Celsius or UnitCode.Fahrenheit or UnitCode.GasMark)
        {
            return $"{value:G6} {unitLabel}".Trim();
        }

        // Split into whole and fractional parts
        decimal absValue = Math.Abs(value);
        long whole = (long)Math.Floor(absValue);
        decimal frac = absValue - whole;

        // Round fraction to nearest 1/8
        int eighths = (int)Math.Round(frac * 8m);

        // Handle rollover (e.g. 7/8 rounds to 8/8 = 1)
        if (eighths >= 8)
        {
            whole++;
            eighths = 0;
        }

        string? fracStr = eighths switch
        {
            0 => null,
            1 => "⅛",
            2 => "¼",
            3 => "⅜",
            4 => "½",
            5 => "⅝",
            6 => "¾",
            7 => "⅞",
            _ => null
        };

        StringBuilder sb = new();
        if (value < 0m) { sb.Append('-'); }
        if (whole > 0) { sb.Append(whole); }
        if (fracStr != null) { sb.Append(fracStr); }
        if (whole == 0 && fracStr == null) { sb.Append('0'); }

        if (!string.IsNullOrEmpty(unitLabel))
        {
            sb.Append(' ');
            sb.Append(unitLabel);
        }
        return sb.ToString().Trim();
    }

    private static string UnitCodeToLabel(UnitCode code) => code switch
    {
        UnitCode.Gram        => "g",
        UnitCode.Kilogram    => "kg",
        UnitCode.Milligram   => "mg",
        UnitCode.Ounce       => "oz",
        UnitCode.Pound       => "lb",
        UnitCode.Milliliter  => "ml",
        UnitCode.Liter       => "L",
        UnitCode.Teaspoon    => "tsp",
        UnitCode.UkTeaspoon  => "tsp",
        UnitCode.Tablespoon  => "tbsp",
        UnitCode.UkTablespoon => "tbsp",
        UnitCode.FluidOunce  => "fl oz",
        UnitCode.UkFluidOunce => "fl oz",
        UnitCode.Cup         => "cup",
        UnitCode.UkCup       => "cup",
        UnitCode.MetricCup   => "cup",
        UnitCode.UsPint      => "pt",
        UnitCode.UkPint      => "pt",
        UnitCode.UsQuart     => "qt",
        UnitCode.UkQuart     => "qt",
        UnitCode.UsGallon    => "gal",
        UnitCode.UkGallon    => "gal",
        UnitCode.Celsius     => "°C",
        UnitCode.Fahrenheit  => "°F",
        UnitCode.GasMark     => "Gas Mark",
        UnitCode.Each        => "",
        _                    => code.ToString()
    };
}
