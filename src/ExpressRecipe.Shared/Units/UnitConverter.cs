namespace ExpressRecipe.Shared.Units;
public static class UnitConverter
{
    public static ConversionResult ToCanonical(decimal amount, UnitCode fromUnit)
    {
        UnitDimension dim = UnitParser.GetDimension(fromUnit);
        return dim switch
        {
            UnitDimension.Mass        => ConversionResult.Ok(ToGrams(amount, fromUnit), UnitCode.Gram),
            UnitDimension.Volume      => ConversionResult.Ok(ToMilliliters(amount, fromUnit), UnitCode.Milliliter),
            UnitDimension.Temperature => ConversionResult.Ok(ToCelsius(amount, fromUnit), UnitCode.Celsius),
            UnitDimension.Count       => ConversionResult.Ok(amount, UnitCode.Each),
            UnitDimension.Uncountable => ConversionResult.Uncountable(fromUnit),
            _                         => ConversionResult.BadUnit(fromUnit.ToString())
        };
    }

    public static ConversionResult FromCanonical(decimal canonicalAmount, UnitCode canonicalUnit, UnitCode toUnit)
    {
        UnitDimension fromDim = UnitParser.GetDimension(canonicalUnit);
        UnitDimension toDim = UnitParser.GetDimension(toUnit);

        if (fromDim != toDim && toDim != UnitDimension.Unknown)
        {
            return ConversionResult.BadUnit($"Cannot convert {canonicalUnit} to {toUnit}: incompatible dimensions");
        }

        try
        {
            decimal result = fromDim switch
            {
                UnitDimension.Mass        => FromGrams(canonicalAmount, toUnit),
                UnitDimension.Volume      => FromMilliliters(canonicalAmount, toUnit),
                UnitDimension.Temperature => FromCelsius(canonicalAmount, toUnit),
                UnitDimension.Count       => canonicalAmount,
                _                         => canonicalAmount
            };
            return ConversionResult.Ok(result, toUnit);
        }
        catch (ArgumentException ex)
        {
            return ConversionResult.BadUnit(ex.Message);
        }
    }

    public static decimal ToGrams(decimal amount, UnitCode unit) => unit switch
    {
        UnitCode.Gram      => amount,
        UnitCode.Kilogram  => amount * 1000m,
        UnitCode.Milligram => amount / 1000m,
        UnitCode.Ounce     => amount * 28.34952m,
        UnitCode.Pound     => amount * 453.59237m,
        _ => throw new ArgumentException($"{unit} is not a mass unit")
    };

    private static decimal FromGrams(decimal grams, UnitCode toUnit) => toUnit switch
    {
        UnitCode.Gram      => grams,
        UnitCode.Kilogram  => grams / 1000m,
        UnitCode.Milligram => grams * 1000m,
        UnitCode.Ounce     => grams / 28.34952m,
        UnitCode.Pound     => grams / 453.59237m,
        _ => throw new ArgumentException($"{toUnit} is not a mass unit")
    };

    public static decimal ToMilliliters(decimal amount, UnitCode unit) => unit switch
    {
        UnitCode.Milliliter    => amount,
        UnitCode.Liter         => amount * 1000m,
        UnitCode.Teaspoon      => amount * 4.92892m,
        UnitCode.UkTeaspoon    => amount * 5.0m,
        UnitCode.Tablespoon    => amount * 14.78676m,
        UnitCode.UkTablespoon  => amount * 15.0m,
        UnitCode.FluidOunce    => amount * 29.57353m,
        UnitCode.UkFluidOunce  => amount * 28.41306m,
        UnitCode.Cup           => amount * 236.58824m,
        UnitCode.UkCup         => amount * 284.13063m,
        UnitCode.MetricCup     => amount * 250m,
        UnitCode.UsPint        => amount * 473.17647m,
        UnitCode.UkPint        => amount * 568.26125m,
        UnitCode.UsQuart       => amount * 946.35295m,
        UnitCode.UkQuart       => amount * 1136.5225m,
        UnitCode.UsGallon      => amount * 3785.41178m,
        UnitCode.UkGallon      => amount * 4546.09m,
        _ => throw new ArgumentException($"{unit} is not a volume unit")
    };

    private static decimal FromMilliliters(decimal ml, UnitCode toUnit) => toUnit switch
    {
        UnitCode.Milliliter    => ml,
        UnitCode.Liter         => ml / 1000m,
        UnitCode.Teaspoon      => ml / 4.92892m,
        UnitCode.UkTeaspoon    => ml / 5.0m,
        UnitCode.Tablespoon    => ml / 14.78676m,
        UnitCode.UkTablespoon  => ml / 15.0m,
        UnitCode.FluidOunce    => ml / 29.57353m,
        UnitCode.UkFluidOunce  => ml / 28.41306m,
        UnitCode.Cup           => ml / 236.58824m,
        UnitCode.UkCup         => ml / 284.13063m,
        UnitCode.MetricCup     => ml / 250m,
        UnitCode.UsPint        => ml / 473.17647m,
        UnitCode.UkPint        => ml / 568.26125m,
        UnitCode.UsQuart       => ml / 946.35295m,
        UnitCode.UkQuart       => ml / 1136.5225m,
        UnitCode.UsGallon      => ml / 3785.41178m,
        UnitCode.UkGallon      => ml / 4546.09m,
        _ => throw new ArgumentException($"{toUnit} is not a volume unit")
    };

    public static decimal ToCelsius(decimal amount, UnitCode unit) => unit switch
    {
        UnitCode.Celsius    => amount,
        UnitCode.Fahrenheit => (amount - 32m) * 5m / 9m,
        UnitCode.GasMark    => GasMarkToCelsius((int)amount),
        _ => throw new ArgumentException($"{unit} is not a temperature unit")
    };

    public static decimal FromCelsius(decimal celsius, UnitCode toUnit) => toUnit switch
    {
        UnitCode.Celsius    => celsius,
        UnitCode.Fahrenheit => (celsius * 9m / 5m) + 32m,
        UnitCode.GasMark    => CelsiusToGasMark(celsius),
        _ => throw new ArgumentException($"{toUnit} is not a temperature unit")
    };

    private static readonly (int GasMark, decimal Celsius)[] _gasMarkTable =
    [
        (0, 110m), (1, 140m), (2, 150m), (3, 170m), (4, 180m),
        (5, 190m), (6, 200m), (7, 220m), (8, 230m), (9, 240m)
    ];

    private static decimal GasMarkToCelsius(int gasMark)
    {
        foreach ((int gm, decimal c) in _gasMarkTable) { if (gm == gasMark) { return c; } }
        throw new ArgumentOutOfRangeException(nameof(gasMark), $"Gas Mark {gasMark} not in table");
    }

    private static decimal CelsiusToGasMark(decimal celsius)
    {
        (int bestGm, decimal bestDiff) = (0, decimal.MaxValue);
        foreach ((int gm, decimal c) in _gasMarkTable)
        {
            decimal diff = Math.Abs(celsius - c);
            if (diff < bestDiff) { bestDiff = diff; bestGm = gm; }
        }
        return bestGm;
    }
}
