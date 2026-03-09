using System.Globalization;
using System.Text.RegularExpressions;

namespace ExpressRecipe.Shared.Units;
public static class UnitParser
{
    private static readonly Dictionary<string, UnitCode> _aliases = BuildAliases();

    // Unicode fraction replacements: char → decimal string
    private static readonly (char Char, string Decimal)[] _unicodeFractions =
    [
        ('½', ".5"), ('¼', ".25"), ('¾', ".75"),
        ('⅓', ".3333"), ('⅔', ".6667"),
        ('⅛', ".125"), ('⅜', ".375"), ('⅝', ".625"), ('⅞', ".875"),
    ];

    // Mixed number: "2 1/2 cups"
    private static readonly Regex _mixedPattern =
        new(@"^(\d+)\s+(\d+)/(\d+)\s*(.*)?$", RegexOptions.Compiled);

    // Pure fraction: "1/4 cup"
    private static readonly Regex _fractionPattern =
        new(@"^(\d+)/(\d+)\s*(.*)?$", RegexOptions.Compiled);

    // Decimal/integer amount + unit: "2.5 cups" or "350 F"
    private static readonly Regex _amountUnitPattern =
        new(@"^(\d+\.?\d*)\s*(.*)?$", RegexOptions.Compiled);

    // Gas mark: "gas mark 4" or "gas mark4"
    private static readonly Regex _gasMarkPattern =
        new(@"^gas\s*mark\s*(\d+\.?\d*)\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Returns (amount, unitCode). Amount=0 for uncountable.
    public static (decimal Amount, UnitCode Unit) Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) { return (0m, UnitCode.Unknown); }
        string s = raw.Trim();

        // 1. Check for gas mark pattern before lowercasing (e.g. "gas mark 4")
        Match gasMatch = _gasMarkPattern.Match(s);
        if (gasMatch.Success)
        {
            return (decimal.Parse(gasMatch.Groups[1].Value, CultureInfo.InvariantCulture), UnitCode.GasMark);
        }

        string normalized = s.ToLowerInvariant();

        // 2. Check for special uncountable phrases
        if (normalized is "to taste" or "as needed" or "as required")
        {
            return (0m, UnitCode.ToTaste);
        }

        // 3. Check for "pinch of x", "dash of x" etc.
        if (normalized.StartsWith("pinch")) { return (0m, UnitCode.Pinch); }
        if (normalized.StartsWith("dash")) { return (0m, UnitCode.Dash); }
        if (normalized.StartsWith("smidgen")) { return (0m, UnitCode.Smidgen); }
        if (normalized.StartsWith("handful")) { return (0m, UnitCode.Handful); }
        if (normalized.StartsWith("sprig")) { return (0m, UnitCode.Sprig); }

        // 4. Replace unicode fractions in original string (case-sensitive char matching)
        string replaced = s;
        foreach ((char c, string dec) in _unicodeFractions)
        {
            replaced = replaced.Replace(c.ToString(), dec);
        }

        // 5. Try unicode-prefixed fraction: "2½ cups" → already replaced to "2.5 cups"
        //    But also handle "½ cup" → ".5 cup" — prefix with 0 if starts with decimal point
        if (replaced.Length > 0 && replaced[0] == '.')
        {
            replaced = "0" + replaced;
        }

        // 6. Try mixed number: "2 1/2 cups"
        Match mixedMatch = _mixedPattern.Match(replaced);
        if (mixedMatch.Success)
        {
            decimal whole = decimal.Parse(mixedMatch.Groups[1].Value, CultureInfo.InvariantCulture);
            decimal num = decimal.Parse(mixedMatch.Groups[2].Value, CultureInfo.InvariantCulture);
            decimal den = decimal.Parse(mixedMatch.Groups[3].Value, CultureInfo.InvariantCulture);
            decimal amount = den == 0m ? whole : whole + num / den;
            string unitStr = mixedMatch.Groups[4].Value.Trim();
            return (amount, LookupUnit(unitStr));
        }

        // 7. Try pure fraction: "1/4 cup"
        Match fracMatch = _fractionPattern.Match(replaced);
        if (fracMatch.Success)
        {
            decimal num = decimal.Parse(fracMatch.Groups[1].Value, CultureInfo.InvariantCulture);
            decimal den = decimal.Parse(fracMatch.Groups[2].Value, CultureInfo.InvariantCulture);
            decimal amount = den == 0m ? 0m : num / den;
            string unitStr = fracMatch.Groups[3].Value.Trim();
            return (amount, LookupUnit(unitStr));
        }

        // 8. Try amount + unit: "2.5 cups" or "2cups"
        Match amountMatch = _amountUnitPattern.Match(replaced);
        if (amountMatch.Success)
        {
            decimal amount = decimal.Parse(amountMatch.Groups[1].Value, CultureInfo.InvariantCulture);
            string unitStr = amountMatch.Groups[2].Value.Trim();
            return (amount, LookupUnit(unitStr));
        }

        // 9. No amount found — treat whole string as unit with amount=1
        UnitCode unitCode = LookupUnit(replaced);
        if (unitCode != UnitCode.Unknown)
        {
            return (1m, unitCode);
        }

        return (0m, UnitCode.Unknown);
    }

    /// <summary>Normalizes and looks up a unit string in the alias dictionary.</summary>
    private static UnitCode LookupUnit(string unitStr)
    {
        if (string.IsNullOrWhiteSpace(unitStr)) { return UnitCode.Each; }

        string trimmed = unitStr.Trim().TrimEnd('.').Trim();

        // Try exact (case-insensitive)
        if (_aliases.TryGetValue(trimmed, out UnitCode code)) { return code; }

        // Try lowercase
        string lower = trimmed.ToLowerInvariant();
        if (_aliases.TryGetValue(lower, out code)) { return code; }

        // Try stripping trailing 's' (pluralization)
        if (lower.Length > 1 && lower.EndsWith('s'))
        {
            string singular = lower[..^1];
            if (_aliases.TryGetValue(singular, out code)) { return code; }
        }

        // Try stripping trailing 'es' (pluralization)
        if (lower.Length > 2 && lower.EndsWith("es"))
        {
            string singular = lower[..^2];
            if (_aliases.TryGetValue(singular, out code)) { return code; }
        }

        return UnitCode.Unknown;
    }

    public static UnitDimension GetDimension(UnitCode code) => code switch
    {
        UnitCode.Gram or UnitCode.Kilogram or UnitCode.Milligram
            or UnitCode.Ounce or UnitCode.Pound => UnitDimension.Mass,
        UnitCode.Milliliter or UnitCode.Liter or UnitCode.Teaspoon or UnitCode.UkTeaspoon
            or UnitCode.Tablespoon or UnitCode.UkTablespoon or UnitCode.FluidOunce
            or UnitCode.UkFluidOunce or UnitCode.Cup or UnitCode.UkCup or UnitCode.MetricCup
            or UnitCode.UsPint or UnitCode.UkPint or UnitCode.UsQuart or UnitCode.UkQuart
            or UnitCode.UsGallon or UnitCode.UkGallon => UnitDimension.Volume,
        UnitCode.Celsius or UnitCode.Fahrenheit or UnitCode.GasMark => UnitDimension.Temperature,
        UnitCode.Each or UnitCode.Clove or UnitCode.Slice or UnitCode.Bunch => UnitDimension.Count,
        UnitCode.Pinch or UnitCode.Dash or UnitCode.Smidgen or UnitCode.ToTaste
            or UnitCode.Handful or UnitCode.Sprig => UnitDimension.Uncountable,
        _ => UnitDimension.Unknown
    };

    private static Dictionary<string, UnitCode> BuildAliases()
    {
        Dictionary<string, UnitCode> d = new(StringComparer.OrdinalIgnoreCase);
        // Mass
        foreach (string s in new[] { "g", "gram", "grams", "gr" }) { d[s] = UnitCode.Gram; }
        foreach (string s in new[] { "kg", "kilogram", "kilograms" }) { d[s] = UnitCode.Kilogram; }
        foreach (string s in new[] { "oz", "ounce", "ounces" }) { d[s] = UnitCode.Ounce; }
        foreach (string s in new[] { "lb", "lbs", "pound", "pounds" }) { d[s] = UnitCode.Pound; }
        foreach (string s in new[] { "mg", "milligram", "milligrams" }) { d[s] = UnitCode.Milligram; }
        // Volume
        foreach (string s in new[] { "ml", "milliliter", "milliliters", "cc" }) { d[s] = UnitCode.Milliliter; }
        foreach (string s in new[] { "l", "liter", "liters", "litre", "litres" }) { d[s] = UnitCode.Liter; }
        foreach (string s in new[] { "tsp", "t", "teaspoon", "teaspoons" }) { d[s] = UnitCode.Teaspoon; }
        foreach (string s in new[] { "tbsp", "tb", "tbl", "tablespoon", "tablespoons", "T" }) { d[s] = UnitCode.Tablespoon; }
        foreach (string s in new[] { "fl oz", "floz", "fluid ounce", "fluid ounces" }) { d[s] = UnitCode.FluidOunce; }
        foreach (string s in new[] { "c", "c.", "cup", "cups" }) { d[s] = UnitCode.Cup; }
        foreach (string s in new[] { "pt", "pint", "pints" }) { d[s] = UnitCode.UsPint; }
        foreach (string s in new[] { "qt", "quart", "quarts" }) { d[s] = UnitCode.UsQuart; }
        foreach (string s in new[] { "gal", "gallon", "gallons" }) { d[s] = UnitCode.UsGallon; }
        // Temperature
        foreach (string s in new[] { "°f", "fahrenheit" }) { d[s] = UnitCode.Fahrenheit; }
        foreach (string s in new[] { "°c", "celsius", "centigrade" }) { d[s] = UnitCode.Celsius; }
        foreach (string s in new[] { "gas mark", "gas", "gm" }) { d[s] = UnitCode.GasMark; }
        // Count
        foreach (string s in new[] { "each", "ea", "piece", "pieces", "item", "items", "" }) { d[s] = UnitCode.Each; }
        foreach (string s in new[] { "clove", "cloves" }) { d[s] = UnitCode.Clove; }
        foreach (string s in new[] { "slice", "slices" }) { d[s] = UnitCode.Slice; }
        foreach (string s in new[] { "bunch", "bunches" }) { d[s] = UnitCode.Bunch; }
        // Uncountable
        foreach (string s in new[] { "pinch", "pinches" }) { d[s] = UnitCode.Pinch; }
        foreach (string s in new[] { "dash", "dashes" }) { d[s] = UnitCode.Dash; }
        foreach (string s in new[] { "to taste", "as needed", "as required" }) { d[s] = UnitCode.ToTaste; }
        foreach (string s in new[] { "handful", "handfuls" }) { d[s] = UnitCode.Handful; }
        foreach (string s in new[] { "sprig", "sprigs" }) { d[s] = UnitCode.Sprig; }
        d["smidgen"] = UnitCode.Smidgen;
        return d;
    }
}
