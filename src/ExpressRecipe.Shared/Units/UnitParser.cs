namespace ExpressRecipe.Shared.Units;

/// <summary>
/// Parses a combined quantity+unit string into a numeric value and a canonical <see cref="UnitCode"/>.
/// Delegates quantity parsing to <see cref="FractionParser"/>.
/// </summary>
public static class UnitParser
{
    private static readonly Dictionary<string, UnitCode> Aliases =
        new(StringComparer.OrdinalIgnoreCase)
    {
        // Teaspoon
        { "t", UnitCode.Teaspoon }, { "tsp", UnitCode.Teaspoon },
        { "teaspoon", UnitCode.Teaspoon }, { "teaspoons", UnitCode.Teaspoon },
        // Tablespoon
        { "T", UnitCode.Tablespoon }, { "tbsp", UnitCode.Tablespoon }, { "tbs", UnitCode.Tablespoon },
        { "tablespoon", UnitCode.Tablespoon }, { "tablespoons", UnitCode.Tablespoon },
        // Cup
        { "c", UnitCode.Cup }, { "cup", UnitCode.Cup }, { "cups", UnitCode.Cup },
        // Fluid ounce
        { "fl oz", UnitCode.FluidOunce }, { "floz", UnitCode.FluidOunce },
        { "fluid ounce", UnitCode.FluidOunce }, { "fluid ounces", UnitCode.FluidOunce },
        // Pint
        { "pt", UnitCode.Pint }, { "pint", UnitCode.Pint }, { "pints", UnitCode.Pint },
        // Quart
        { "qt", UnitCode.Quart }, { "quart", UnitCode.Quart }, { "quarts", UnitCode.Quart },
        // Gallon
        { "gal", UnitCode.Gallon }, { "gallon", UnitCode.Gallon }, { "gallons", UnitCode.Gallon },
        // Milliliter
        { "ml", UnitCode.Milliliter }, { "milliliter", UnitCode.Milliliter }, { "milliliters", UnitCode.Milliliter },
        { "millilitre", UnitCode.Milliliter }, { "millilitres", UnitCode.Milliliter },
        // Liter
        { "l", UnitCode.Liter }, { "liter", UnitCode.Liter }, { "liters", UnitCode.Liter },
        { "litre", UnitCode.Liter }, { "litres", UnitCode.Liter },
        // Ounce (weight)
        { "oz", UnitCode.Ounce }, { "ounce", UnitCode.Ounce }, { "ounces", UnitCode.Ounce },
        // Pound
        { "lb", UnitCode.Pound }, { "lbs", UnitCode.Pound }, { "pound", UnitCode.Pound }, { "pounds", UnitCode.Pound },
        // Gram
        { "g", UnitCode.Gram }, { "gram", UnitCode.Gram }, { "grams", UnitCode.Gram },
        // Kilogram
        { "kg", UnitCode.Kilogram }, { "kilogram", UnitCode.Kilogram }, { "kilograms", UnitCode.Kilogram },
        // Pinch / dash / drop
        { "pinch", UnitCode.Pinch }, { "pinches", UnitCode.Pinch },
        { "dash", UnitCode.Dash }, { "dashes", UnitCode.Dash },
        { "drop", UnitCode.Drop }, { "drops", UnitCode.Drop },
        // Count
        { "clove", UnitCode.Clove }, { "cloves", UnitCode.Clove },
        { "slice", UnitCode.Slice }, { "slices", UnitCode.Slice },
        { "piece", UnitCode.Piece }, { "pieces", UnitCode.Piece },
        { "whole", UnitCode.Whole },
        { "bunch", UnitCode.Bunch }, { "bunches", UnitCode.Bunch },
        { "head", UnitCode.Head }, { "heads", UnitCode.Head },
        { "stick", UnitCode.Stick }, { "sticks", UnitCode.Stick },
        { "pkg", UnitCode.Package }, { "package", UnitCode.Package }, { "packages", UnitCode.Package },
        { "can", UnitCode.Can }, { "cans", UnitCode.Can },
        { "sprig", UnitCode.Sprig }, { "sprigs", UnitCode.Sprig }
    };

    // Matches a leading quantity (digits, spaces, slashes, Unicode fractions) then captures the rest
    private static readonly System.Text.RegularExpressions.Regex LeadingQuantityPattern =
        new(@"^([\d\s/\.,½¼¾⅓⅔⅛⅜⅝⅞⅕⅖⅗⅘⅙⅚\u00BC-\u00BE\u2150-\u215E]+)\s*(.*?)$",
            System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>
    /// Parses a string such as "1½ cups" into a quantity and a <see cref="UnitCode"/>.
    /// Returns (null, Unknown) if no quantity is found; unit defaults to Unknown if not recognized.
    /// Never throws.
    /// </summary>
    public static (decimal? Quantity, UnitCode Unit) Parse(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) { return (null, UnitCode.Unknown); }

        // Normalize Unicode fractions first so the regex can match digits
        string normalized = FractionParser.Normalize(input.Trim());

        System.Text.RegularExpressions.Match m = LeadingQuantityPattern.Match(normalized);
        if (!m.Success) { return (null, UnitCode.Unknown); }

        string quantityPart = m.Groups[1].Value.Trim();
        string unitPart = m.Groups[2].Value.Trim().TrimEnd('.');

        decimal? quantity = FractionParser.ParseFraction(quantityPart);
        if (quantity == null) { return (null, UnitCode.Unknown); }

        UnitCode unit = Aliases.TryGetValue(unitPart, out UnitCode code) ? code : UnitCode.Unknown;
        return (quantity, unit);
    }
}
