namespace ExpressRecipe.Shared.Units;

/// <summary>
/// Determines how numeric quantities are displayed.
/// </summary>
public enum NumberFormat { Fraction, Decimal }

/// <summary>
/// Formats decimal quantities as Unicode vulgar fractions or plain decimals.
/// </summary>
public static class FractionFormatter
{
    // Mapping: decimal value → Unicode char (sorted descending for nearest-match scan)
    private static readonly (decimal Value, char Symbol)[] Fractions =
    [
        (7m / 8m, '\u215E'), (5m / 6m, '\u215A'), (4m / 5m, '\u2158'), (3m / 4m, '\u00BE'),
        (2m / 3m, '\u2154'), (5m / 8m, '\u215D'), (3m / 5m, '\u2157'), (1m / 2m, '\u00BD'),
        (2m / 5m, '\u2156'), (3m / 8m, '\u215C'), (1m / 3m, '\u2153'), (1m / 4m, '\u00BC'),
        (1m / 5m, '\u2155'), (1m / 6m, '\u2159'), (1m / 8m, '\u215B')
    ];

    private const decimal MaxFractionError = 0.04m;

    /// <summary>
    /// Formats a decimal value using the specified number format.
    /// </summary>
    public static string Format(decimal value, NumberFormat format = NumberFormat.Fraction)
    {
        if (format == NumberFormat.Decimal) { return FormatDecimal(value); }

        // Values >= 100 or negative: use decimal
        if (value >= 100m || value < 0m) { return FormatDecimal(value); }
        if (value == 0m) { return "0"; }

        int whole = (int)Math.Floor(value);
        decimal frac = value - whole;

        if (frac == 0m) { return whole.ToString(); }

        // Find nearest vulgar fraction within tolerance
        char? fracChar = FindNearestFraction(frac);
        if (fracChar.HasValue) { return whole == 0 ? fracChar.Value.ToString() : $"{whole}{fracChar.Value}"; }

        // No suitable fraction → decimal
        return FormatDecimal(value);
    }

    private static char? FindNearestFraction(decimal frac)
    {
        char? best = null;
        decimal bestDiff = MaxFractionError;
        foreach ((decimal v, char sym) in Fractions)
        {
            decimal diff = Math.Abs(frac - v);
            if (diff < bestDiff) { bestDiff = diff; best = sym; }
        }
        return best;
    }

    private static string FormatDecimal(decimal value) =>
        value == Math.Floor(value) ? ((long)value).ToString() : value.ToString("G4");
}
