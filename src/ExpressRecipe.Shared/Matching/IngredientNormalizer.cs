namespace ExpressRecipe.Shared.Matching;

public static class IngredientNormalizer
{
    private static readonly HashSet<string> _sizeModifiers =
        new(StringComparer.OrdinalIgnoreCase) { "large","small","medium","big","extra-large","xl" };
    private static readonly HashSet<string> _stateModifiers =
        new(StringComparer.OrdinalIgnoreCase) { "cold","warm","hot","room","temperature","chilled",
            "fresh","dried","frozen","thawed","raw","cooked","uncooked","ripe","overripe" };
    private static readonly HashSet<string> _qualityModifiers =
        new(StringComparer.OrdinalIgnoreCase) { "organic","natural","pure","unbleached","bleached",
            "enriched","unsalted","salted","sweetened","unsweetened","reduced-fat","low-fat",
            "fat-free","nonfat","whole","skim","2%","1%","store-bought","homemade","brand" };
    private static readonly HashSet<string> _prepWords =
        new(StringComparer.OrdinalIgnoreCase) { "softened","melted","beaten","whisked","sifted",
            "chopped","diced","minced","sliced","grated","shredded","crushed","peeled","seeded",
            "cored","trimmed","halved","quartered" };

    public static string Normalize(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) { return string.Empty; }
        string s = raw.Trim().ToLowerInvariant();
        s = System.Text.RegularExpressions.Regex.Replace(s, @"\([^)]*\)", " ");
        int comma = s.IndexOf(',');
        if (comma > 0) { s = s[..comma]; }
        s = System.Text.RegularExpressions.Regex.Replace(s,
            @"^\d[\d\s/½¼¾⅓⅔⅛.]*\s*(cups?|tbsp|tsp|oz|lbs?|g|kg|ml|l|pints?|quarts?|gallons?)?\s*", "");

        string[] tokens = s.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
        System.Collections.Generic.List<string> kept = new(tokens.Length);
        foreach (string token in tokens)
        {
            string clean = token.Trim('-').Trim('/');
            if (string.IsNullOrEmpty(clean)) { continue; }
            if (_sizeModifiers.Contains(clean) || _stateModifiers.Contains(clean) ||
                _qualityModifiers.Contains(clean) || _prepWords.Contains(clean)) { continue; }
            kept.Add(clean);
        }
        if (kept.Count == 0) { return string.Empty; }

        // Naive singular: strip trailing 's' if > 4 chars and not ending in 'ss'
        string last = kept[^1];
        if (last.Length > 4 && last.EndsWith('s') && !last.EndsWith("ss")) { kept[^1] = last[..^1]; }
        return string.Join(" ", kept).Trim();
    }

    public static HashSet<string> Tokenize(string normalized) =>
        new(normalized.Split(' ', System.StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Lightweight normalization that matches the DB computed column <c>NormalizedName AS LOWER(Name)</c>
    /// and the seeded <c>NormalizedAlias = LOWER(LTRIM(RTRIM(...)))</c>.
    /// Used for Exact and Alias stage lookups so the key aligns with what is stored in the database.
    /// </summary>
    public static string SimpleLower(string raw) => raw.Trim().ToLowerInvariant();

    public static decimal JaccardSimilarity(HashSet<string> a, HashSet<string> b)
    {
        if (a.Count == 0 && b.Count == 0) { return 1.0m; }
        if (a.Count == 0 || b.Count == 0) { return 0.0m; }
        int intersection = 0;
        foreach (string token in a) { if (b.Contains(token)) { intersection++; } }
        int union = a.Count + b.Count - intersection;
        return union == 0 ? 0m : (decimal)intersection / union;
    }

    public static int EditDistance(string a, string b)
    {
        if (string.IsNullOrEmpty(a)) { return b?.Length ?? 0; }
        if (string.IsNullOrEmpty(b)) { return a.Length; }
        int[,] dp = new int[a.Length + 1, b.Length + 1];
        for (int i = 0; i <= a.Length; i++) { dp[i, 0] = i; }
        for (int j = 0; j <= b.Length; j++) { dp[0, j] = j; }
        for (int i = 1; i <= a.Length; i++)
        {
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                dp[i, j] = Math.Min(Math.Min(dp[i-1,j]+1, dp[i,j-1]+1), dp[i-1,j-1]+cost);
            }
        }
        return dp[a.Length, b.Length];
    }
}
