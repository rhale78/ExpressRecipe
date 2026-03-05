using System.Buffers;

namespace ExpressRecipe.RecipeParser.Helpers;

public static class TextParserHelper
{
    public static ReadOnlySpan<char> TrimSpan(ReadOnlySpan<char> span) => span.Trim();

    public static string[] SplitLines(string text)
        => text.Split('\n');

    public static bool StartsWithIgnoreCase(ReadOnlySpan<char> span, ReadOnlySpan<char> prefix)
        => span.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

    public static string NormalizeWhitespace(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var chars = ArrayPool<char>.Shared.Rent(input.Length);
        try
        {
            int pos = 0;
            bool lastWasSpace = false;
            foreach (char c in input)
            {
                if (char.IsWhiteSpace(c))
                {
                    if (!lastWasSpace && pos > 0) { chars[pos++] = ' '; lastWasSpace = true; }
                }
                else { chars[pos++] = c; lastWasSpace = false; }
            }
            return new string(chars, 0, pos).Trim();
        }
        finally { ArrayPool<char>.Shared.Return(chars); }
    }

    private static readonly HashSet<string> KnownUnits = new(StringComparer.OrdinalIgnoreCase)
    {
        "c","cup","cups","T","tbsp","tablespoon","tablespoons",
        "t","tsp","teaspoon","teaspoons","lb","lbs","pound","pounds",
        "oz","ounce","ounces","g","gram","grams","kg","ml","l","liter",
        "liters","pt","pint","qt","quart","gal","gallon","pkg","package",
        "sm","med","lg","small","medium","large","stick","sticks","bunch",
        "head","can","slice","slices","pinch","dash","drop","clove","cloves"
    };

    public static (string? quantity, string? unit, string name) ParseIngredientLine(string line)
    {
        line = line.Trim();
        if (string.IsNullOrEmpty(line)) return (null, null, line);

        var span = line.AsSpan();
        int idx = 0;

        while (idx < span.Length && span[idx] == ' ') idx++;

        int qStart = idx;
        int qEnd = idx;
        while (qEnd < span.Length && (char.IsDigit(span[qEnd]) || span[qEnd] == '/' || span[qEnd] == '.')) qEnd++;

        // Check for compound fractions like "1 1/2"
        if (qEnd > qStart && qEnd < span.Length && span[qEnd] == ' ')
        {
            int tempIdx = qEnd + 1;
            int tempEnd = tempIdx;
            while (tempEnd < span.Length && (char.IsDigit(span[tempEnd]) || span[tempEnd] == '/')) tempEnd++;
            if (tempEnd > tempIdx && span[tempIdx..tempEnd].Contains('/'))
            {
                qEnd = tempEnd;
            }
        }

        string? qty = qEnd > qStart ? new string(span[qStart..qEnd]).Trim() : null;

        if (qty == null) return (null, null, line);

        idx = qEnd;
        while (idx < span.Length && span[idx] == ' ') idx++;

        int uStart = idx;
        int uEnd = idx;
        while (uEnd < span.Length && span[uEnd] != ' ') uEnd++;

        string potentialUnit = new string(span[uStart..uEnd]);

        string? unit = KnownUnits.Contains(potentialUnit) ? potentialUnit : null;

        if (unit != null)
        {
            idx = uEnd;
            while (idx < span.Length && span[idx] == ' ') idx++;
        }
        else
        {
            idx = uStart;
        }

        string name = idx < span.Length ? new string(span[idx..]).Trim() : "";
        return (qty, unit, name);
    }

    public static string ExtractPreparation(ref string ingredientName)
    {
        int commaIdx = ingredientName.IndexOf(',');
        if (commaIdx > 0)
        {
            string prep = ingredientName[(commaIdx + 1)..].Trim();
            ingredientName = ingredientName[..commaIdx].Trim();
            return prep;
        }
        return string.Empty;
    }
}
