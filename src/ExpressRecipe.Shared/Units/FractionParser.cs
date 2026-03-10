namespace ExpressRecipe.Shared.Units;

/// <summary>
/// Single authoritative parser for fraction strings.
/// All other parsers in the solution delegate here.
/// </summary>
public static class FractionParser
{
    private static readonly Dictionary<char, string> VulgarFractions = new()
    {
        { '\u00BC', "1/4" }, { '\u00BD', "1/2" }, { '\u00BE', "3/4" },
        { '\u2153', "1/3" }, { '\u2154', "2/3" },
        { '\u2155', "1/5" }, { '\u2156', "2/5" }, { '\u2157', "3/5" }, { '\u2158', "4/5" },
        { '\u2159', "1/6" }, { '\u215A', "5/6" },
        { '\u215B', "1/8" }, { '\u215C', "3/8" }, { '\u215D', "5/8" }, { '\u215E', "7/8" },
        { '\u2150', "1/7" }, { '\u2151', "1/9" }, { '\u2152', "1/10" }
    };

    private static readonly Dictionary<string, char> HtmlEntities =
        new(StringComparer.OrdinalIgnoreCase)
    {
        { "&frac14;", '\u00BC' }, { "&frac12;", '\u00BD' }, { "&frac34;", '\u00BE' },
        { "&frac13;", '\u2153' }, { "&frac23;", '\u2154' },
        { "&frac18;", '\u215B' }, { "&frac38;", '\u215C' }, { "&frac58;", '\u215D' }, { "&frac78;", '\u215E' },
        { "&#188;", '\u00BC' }, { "&#189;", '\u00BD' }, { "&#190;", '\u00BE' },
        { "&#8531;", '\u2153' }, { "&#8532;", '\u2154' },
        { "&#8539;", '\u215B' }, { "&#8540;", '\u215C' }, { "&#8541;", '\u215D' }, { "&#8542;", '\u215E' },
        { "&#xBC;", '\u00BC' }, { "&#xBD;", '\u00BD' }, { "&#xBE;", '\u00BE' },
        { "&#x00BC;", '\u00BC' }, { "&#x00BD;", '\u00BD' }, { "&#x00BE;", '\u00BE' },
        { "&#x2153;", '\u2153' }, { "&#x2154;", '\u2154' },
        { "&#x215B;", '\u215B' }, { "&#x215C;", '\u215C' }, { "&#x215D;", '\u215D' }, { "&#x215E;", '\u215E' }
    };

    private const char UnicodeFractionSlash = '\u2044';

    /// <summary>
    /// Parses any quantity string to decimal. Returns null for empty/unrecognized input.
    /// Never throws.
    /// </summary>
    public static decimal? ParseFraction(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) { return null; }
        string normalized = Normalize(raw);
        if (string.IsNullOrWhiteSpace(normalized)) { return null; }

        // Range "2-3" → average 2.5 (guard: ignore leading minus)
        if (normalized.Contains('-') && !normalized.StartsWith('-'))
        {
            string[] parts = normalized.Split('-', 2);
            if (decimal.TryParse(parts[0].Trim(), System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out decimal lo) &&
                decimal.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out decimal hi))
            { return (lo + hi) / 2m; }
        }

        // Mixed number "2 1/2"
        System.Text.RegularExpressions.Match m =
            System.Text.RegularExpressions.Regex.Match(normalized, @"^(\d+)\s+(\d+)/(\d+)$");
        if (m.Success)
        {
            int den = int.Parse(m.Groups[3].Value);
            if (den == 0) { return null; }
            return int.Parse(m.Groups[1].Value) + (decimal)int.Parse(m.Groups[2].Value) / den;
        }

        // Simple fraction "1/2"
        m = System.Text.RegularExpressions.Regex.Match(normalized, @"^(\d+)/(\d+)$");
        if (m.Success)
        {
            int den = int.Parse(m.Groups[2].Value);
            return den == 0 ? null : (decimal)int.Parse(m.Groups[1].Value) / den;
        }

        return decimal.TryParse(normalized, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out decimal result) ? result : null;
    }

    /// <summary>
    /// Normalizes to ASCII: replaces HTML entities, Unicode fraction slash, vulgar fraction chars,
    /// and inserts space between digit and adjacent vulgar fraction (e.g. "2½" → "2 1/2").
    /// </summary>
    public static string Normalize(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) { return string.Empty; }
        string s = ReplaceHtmlEntities(raw.Trim());
        s = s.Replace(UnicodeFractionSlash, '/');

        // Insert space before vulgar fraction if preceded by digit
        System.Text.StringBuilder sb = new(s.Length + 4);
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (i > 0 && VulgarFractions.ContainsKey(c) && char.IsDigit(s[i - 1])) { sb.Append(' '); }
            sb.Append(c);
        }
        s = sb.ToString();

        foreach ((char frac, string ascii) in VulgarFractions) { s = s.Replace(frac.ToString(), ascii); }
        return s.Trim();
    }

    /// <summary>
    /// Returns true if the string contains any vulgar fraction Unicode characters or HTML entities.
    /// </summary>
    public static bool ContainsFraction(string? s)
    {
        if (string.IsNullOrEmpty(s)) { return false; }
        foreach (char c in s) { if (VulgarFractions.ContainsKey(c)) { return true; } }
        return s.Contains("&frac", StringComparison.OrdinalIgnoreCase) ||
               s.Contains("&#18", StringComparison.Ordinal) ||
               s.Contains("&#853", StringComparison.Ordinal);
    }

    private static string ReplaceHtmlEntities(string s)
    {
        if (!s.Contains('&')) { return s; }
        foreach ((string entity, char replacement) in HtmlEntities)
        { s = s.Replace(entity, replacement.ToString(), StringComparison.OrdinalIgnoreCase); }
        return s;
    }
}
