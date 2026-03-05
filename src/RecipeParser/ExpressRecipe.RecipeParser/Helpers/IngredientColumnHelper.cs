using ExpressRecipe.RecipeParser.Models;

namespace ExpressRecipe.RecipeParser.Helpers;

/// <summary>
/// Parses multi-column ingredient lines (e.g. MealMaster uses 2 columns of ~36 chars each).
/// </summary>
public static class IngredientColumnHelper
{
    private const int MealMasterColumnWidth = 36;

    public static List<string> SplitColumns(string line, int columnWidth = MealMasterColumnWidth)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(line)) return result;

        ReadOnlySpan<char> span = line.AsSpan();
        if (span.Length <= columnWidth)
        {
            string col = span.Trim().ToString();
            if (!string.IsNullOrEmpty(col)) result.Add(col);
            return result;
        }

        int pos = 0;
        while (pos < span.Length)
        {
            int end = Math.Min(pos + columnWidth, span.Length);
            var chunk = span[pos..end].TrimEnd().ToString();
            if (!string.IsNullOrWhiteSpace(chunk))
                result.Add(chunk);
            pos = end;
        }
        return result;
    }

    public static ParsedIngredient ParseMealMasterColumn(string col, int columnNumber)
    {
        var ingredient = new ParsedIngredient { Column = columnNumber };
        if (string.IsNullOrWhiteSpace(col)) return ingredient;

        ReadOnlySpan<char> span = col.AsSpan();

        if (span.Length >= 9)
        {
            var amtSpan = span[..7].Trim();
            var unitSpan = span.Length > 9 ? span[7..9].Trim() : ReadOnlySpan<char>.Empty;
            var nameSpan = span.Length > 10 ? span[9..].Trim() : ReadOnlySpan<char>.Empty;

            if (!amtSpan.IsEmpty && (char.IsDigit(amtSpan[0]) || amtSpan[0] == ' '))
            {
                ingredient.Quantity = amtSpan.IsEmpty ? null : amtSpan.ToString();
                ingredient.Unit = unitSpan.IsEmpty ? null : unitSpan.ToString();
                ingredient.Name = nameSpan.IsEmpty ? col.Trim() : nameSpan.ToString();

                string name = ingredient.Name;
                string prep = TextParserHelper.ExtractPreparation(ref name);
                ingredient.Name = name;
                if (!string.IsNullOrEmpty(prep)) ingredient.Preparation = prep;
                return ingredient;
            }
        }

        var (qty, unit, name2) = TextParserHelper.ParseIngredientLine(col);
        ingredient.Quantity = qty;
        ingredient.Unit = unit;
        ingredient.Name = name2;
        string prep2 = TextParserHelper.ExtractPreparation(ref name2);
        ingredient.Name = name2;
        if (!string.IsNullOrEmpty(prep2)) ingredient.Preparation = prep2;
        return ingredient;
    }

    public static int DetectColumnCount(IEnumerable<string> lines)
    {
        int maxLen = 0;
        foreach (var line in lines)
            if (line.Length > maxLen) maxLen = line.Length;

        if (maxLen > 100) return 3;
        if (maxLen > 50) return 2;
        return 1;
    }
}
