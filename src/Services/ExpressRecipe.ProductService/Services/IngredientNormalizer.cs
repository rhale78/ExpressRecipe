using System.Text.RegularExpressions;

namespace ExpressRecipe.ProductService.Services;

/// <summary>
/// Normalizes raw ingredient list strings into a flat list of lowercase ingredient tokens.
/// </summary>
public static class IngredientNormalizer
{
    // Words that are fillers / conjunctions, not ingredient names
    private static readonly HashSet<string> Fillers =
        new(StringComparer.OrdinalIgnoreCase) { "and", "or", "contains", "with" };

    /// <summary>
    /// Normalizes a single raw ingredient string.
    /// </summary>
    /// <remarks>
    /// Rules applied in order:
    /// 1. Lowercase all text.
    /// 2. Expand parenthetical sub-ingredient lists, keeping both the outer name and each inner token.
    /// 3. Split on ',' and ';'.
    /// 4. Trim whitespace and trailing punctuation.
    /// 5. Remove single-character tokens and common fillers.
    /// </remarks>
    public static List<string> Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) { return new List<string>(); }

        // Lowercase first
        string input = raw.ToLowerInvariant();

        // Expand parentheses: for each "(…)" group keep the text before it as a token
        // and also include the inner comma-separated tokens individually.
        // E.g. "enriched flour (wheat flour, niacin)" →
        //      outer "enriched flour" + inner "wheat flour", "niacin"
        List<string> expanded = ExpandParentheses(input);

        HashSet<string> result = new(StringComparer.OrdinalIgnoreCase);

        foreach (string segment in expanded)
        {
            // Split on commas and semicolons
            foreach (string part in segment.Split(new[] { ',', ';' },
                         StringSplitOptions.RemoveEmptyEntries))
            {
                string token = CleanToken(part);
                if (IsValidToken(token)) { result.Add(token); }
            }
        }

        return result.ToList();
    }

    /// <summary>
    /// Normalizes all ingredient names for a product from a collection of
    /// <see cref="ExpressRecipe.Shared.DTOs.Product.ProductIngredientDto"/> name strings.
    /// </summary>
    public static List<string> NormalizeAll(IEnumerable<string?> rawIngredients)
    {
        HashSet<string> combined = new(StringComparer.OrdinalIgnoreCase);
        foreach (string? raw in rawIngredients)
        {
            foreach (string token in Normalize(raw)) { combined.Add(token); }
        }
        return combined.ToList();
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Expands parenthetical sub-ingredient groups recursively.
    /// "enriched flour (wheat flour, niacin)" produces:
    ///   ["enriched flour", "wheat flour, niacin"]
    /// The caller then splits on commas.
    /// </summary>
    private static List<string> ExpandParentheses(string input)
    {
        List<string> result = new();
        int pos = 0;

        while (pos < input.Length)
        {
            int open = input.IndexOf('(', pos);
            if (open < 0)
            {
                // No more parentheses — append remainder
                string tail = input[pos..].Trim();
                if (tail.Length > 0) { result.Add(tail); }
                break;
            }

            // Text before the '(' (the "outer" name segment)
            string before = input[pos..open].Trim().TrimEnd(',', ';').Trim();
            if (before.Length > 0) { result.Add(before); }

            // Find matching closing ')'
            int close = FindMatchingClose(input, open);
            if (close < 0)
            {
                // Unmatched '(' — treat everything to end of string as inner content
                string inner = input[(open + 1)..].Trim();
                if (inner.Length > 0) { result.AddRange(ExpandParentheses(inner)); }
                break;
            }

            // Inner content — expand recursively
            string innerContent = input[(open + 1)..close].Trim();
            if (innerContent.Length > 0)
            {
                result.AddRange(ExpandParentheses(innerContent));
            }

            pos = close + 1;
        }

        return result;
    }

    private static int FindMatchingClose(string input, int openIndex)
    {
        int depth = 0;
        for (int i = openIndex; i < input.Length; i++)
        {
            if (input[i] == '(') { depth++; }
            else if (input[i] == ')') { depth--; if (depth == 0) { return i; } }
        }
        return -1;
    }

    private static string CleanToken(string token)
        => token.Trim().TrimEnd('.', ':', ';', ',').Trim();

    private static bool IsValidToken(string token)
        => token.Length > 1 && !Fillers.Contains(token);
}
