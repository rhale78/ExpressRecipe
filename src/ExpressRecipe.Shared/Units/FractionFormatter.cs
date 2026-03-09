namespace ExpressRecipe.Shared.Units;

/// <summary>Specifies the output format for numeric values.</summary>
public enum NumberFormat
{
    /// <summary>Standard decimal notation, e.g. <c>1.5</c>.</summary>
    Decimal,
    /// <summary>Unicode vulgar fraction notation, e.g. <c>1½</c>.</summary>
    Fraction
}

/// <summary>
/// Formats decimal quantities as human-readable fraction strings using Unicode vulgar fraction characters.
/// </summary>
public static class FractionFormatter
{
    private static readonly IReadOnlyDictionary<(int Num, int Den), string> VulgarFractions =
        new Dictionary<(int, int), string>
        {
            { (1, 4),  "¼" },
            { (1, 2),  "½" },
            { (3, 4),  "¾" },
            { (1, 3),  "⅓" },
            { (2, 3),  "⅔" },
            { (1, 8),  "⅛" },
            { (3, 8),  "⅜" },
            { (5, 8),  "⅝" },
            { (7, 8),  "⅞" }
        };

    /// <summary>
    /// Formats a decimal value according to the specified <paramref name="format"/>.
    /// </summary>
    public static string Format(decimal value, NumberFormat format)
    {
        if (format != NumberFormat.Fraction) { return value.ToString("G"); }

        int wholeNumber = (int)Math.Floor(value);
        decimal remainder = value - wholeNumber;

        if (remainder == 0m) { return wholeNumber.ToString(); }

        // Try to match a common vulgar fraction
        foreach ((int num, int den) key in VulgarFractions.Keys)
        {
            decimal fraction = (decimal)key.num / key.den;
            if (Math.Abs(remainder - fraction) < 0.01m)
            {
                string vulgar = VulgarFractions[key];
                return wholeNumber > 0 ? $"{wholeNumber}{vulgar}" : vulgar;
            }
        }

        // Fallback: display as decimal
        return value.ToString("G4");
    }
}
