namespace ExpressRecipe.Shared.Units;

/// <summary>
/// Converts quantities between unit systems and formats them for display.
/// </summary>
public static class UnitConversionService
{
    /// <summary>
    /// Formats a quantity value with its unit for display.
    /// Uses <see cref="FractionFormatter"/> to render the numeric portion.
    /// </summary>
    public static string FormatWithFractions(decimal value, UnitCode unit,
        NumberFormat format = NumberFormat.Fraction)
    {
        string formattedValue = FractionFormatter.Format(value, format);
        string unitLabel = GetDisplayLabel(value, unit);
        return string.IsNullOrEmpty(unitLabel) ? formattedValue : $"{formattedValue} {unitLabel}";
    }

    /// <summary>
    /// Returns the plural/singular display label for a unit given the quantity.
    /// </summary>
    public static string GetDisplayLabel(decimal value, UnitCode unit)
    {
        bool plural = value != 1m;
        return unit switch
        {
            UnitCode.Teaspoon    => plural ? "tsp" : "tsp",
            UnitCode.Tablespoon  => plural ? "tbsp" : "tbsp",
            UnitCode.Cup         => plural ? "cups" : "cup",
            UnitCode.FluidOunce  => plural ? "fl oz" : "fl oz",
            UnitCode.Pint        => plural ? "pints" : "pint",
            UnitCode.Quart       => plural ? "quarts" : "quart",
            UnitCode.Gallon      => plural ? "gallons" : "gallon",
            UnitCode.Milliliter  => "ml",
            UnitCode.Liter       => plural ? "liters" : "liter",
            UnitCode.Ounce       => plural ? "oz" : "oz",
            UnitCode.Pound       => plural ? "lbs" : "lb",
            UnitCode.Gram        => "g",
            UnitCode.Kilogram    => "kg",
            UnitCode.Pinch       => plural ? "pinches" : "pinch",
            UnitCode.Dash        => plural ? "dashes" : "dash",
            UnitCode.Drop        => plural ? "drops" : "drop",
            UnitCode.Clove       => plural ? "cloves" : "clove",
            UnitCode.Slice       => plural ? "slices" : "slice",
            UnitCode.Piece       => plural ? "pieces" : "piece",
            UnitCode.Bunch       => plural ? "bunches" : "bunch",
            UnitCode.Head        => plural ? "heads" : "head",
            UnitCode.Stick       => plural ? "sticks" : "stick",
            UnitCode.Package     => plural ? "packages" : "package",
            UnitCode.Can         => plural ? "cans" : "can",
            UnitCode.Sprig       => plural ? "sprigs" : "sprig",
            UnitCode.Whole       => "",
            _                    => ""
        };
    }
}
