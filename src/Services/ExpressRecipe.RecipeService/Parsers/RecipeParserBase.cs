using System.Text.RegularExpressions;

namespace ExpressRecipe.RecipeService.Parsers;

/// <summary>
/// Base class for recipe parsers with common utility methods
/// </summary>
public abstract class RecipeParserBase : IRecipeParser
{
    public abstract string ParserName { get; }
    public abstract string SourceType { get; }

    public abstract Task<List<ParsedRecipe>> ParseAsync(string content, ParserContext context);
    public abstract bool CanParse(string content, ParserContext context);

    /// <summary>
    /// Parse quantity and unit from ingredient text
    /// Examples: "2 cups", "1/2 tsp", "3-4 cloves"
    /// </summary>
    protected (decimal? quantity, string? unit, string remaining) ParseQuantityAndUnit(string text)
    {
        text = text.Trim();

        // Match patterns like: "2", "1/2", "2-3", "2 1/2"
        var quantityPattern = @"^(\d+(?:\s+\d+/\d+|\.\d+|/\d+)?(?:\s*-\s*\d+(?:\.\d+)?)?)\s*";
        var match = Regex.Match(text, quantityPattern);

        if (!match.Success)
        {
            return (null, null, text);
        }

        var quantityStr = match.Groups[1].Value.Trim();
        var quantity = ParseQuantity(quantityStr);
        var remaining = text.Substring(match.Length).Trim();

        // Try to extract unit
        var unitPattern = @"^([a-zA-Z]+\.?)\s+";
        var unitMatch = Regex.Match(remaining, unitPattern);

        string? unit = null;
        if (unitMatch.Success)
        {
            unit = NormalizeUnit(unitMatch.Groups[1].Value);
            remaining = remaining.Substring(unitMatch.Length).Trim();
        }

        return (quantity, unit, remaining);
    }

    /// <summary>
    /// Parse quantity string (handles fractions, ranges, mixed numbers)
    /// </summary>
    protected decimal? ParseQuantity(string quantityStr)
    {
        quantityStr = quantityStr.Trim();

        // Handle ranges (take average): "2-3" -> 2.5
        if (quantityStr.Contains('-'))
        {
            var parts = quantityStr.Split('-');
            if (parts.Length == 2 &&
                decimal.TryParse(parts[0].Trim(), out var min) &&
                decimal.TryParse(parts[1].Trim(), out var max))
            {
                return (min + max) / 2;
            }
        }

        // Handle mixed fractions: "2 1/2" -> 2.5
        var mixedMatch = Regex.Match(quantityStr, @"(\d+)\s+(\d+)/(\d+)");
        if (mixedMatch.Success)
        {
            var whole = int.Parse(mixedMatch.Groups[1].Value);
            var numerator = int.Parse(mixedMatch.Groups[2].Value);
            var denominator = int.Parse(mixedMatch.Groups[3].Value);
            return whole + ((decimal)numerator / denominator);
        }

        // Handle simple fractions: "1/2" -> 0.5
        var fractionMatch = Regex.Match(quantityStr, @"(\d+)/(\d+)");
        if (fractionMatch.Success)
        {
            var numerator = int.Parse(fractionMatch.Groups[1].Value);
            var denominator = int.Parse(fractionMatch.Groups[2].Value);
            return (decimal)numerator / denominator;
        }

        // Handle decimal: "2.5"
        if (decimal.TryParse(quantityStr, out var result))
        {
            return result;
        }

        return null;
    }

    /// <summary>
    /// Normalize unit abbreviations to standard forms
    /// </summary>
    protected string NormalizeUnit(string unit)
    {
        unit = unit.ToLower().Trim().TrimEnd('.');

        return unit switch
        {
            "c" or "cup" or "cups" => "cup",
            "t" or "tsp" or "teaspoon" or "teaspoons" => "tsp",
            "tbsp" or "tablespoon" or "tablespoons" or "tbs" => "tbsp",
            "oz" or "ounce" or "ounces" => "oz",
            "lb" or "lbs" or "pound" or "pounds" => "lb",
            "g" or "gram" or "grams" => "g",
            "kg" or "kilogram" or "kilograms" => "kg",
            "ml" or "milliliter" or "milliliters" => "ml",
            "l" or "liter" or "liters" => "l",
            "pt" or "pint" or "pints" => "pint",
            "qt" or "quart" or "quarts" => "quart",
            "gal" or "gallon" or "gallons" => "gallon",
            "pinch" or "pinches" => "pinch",
            "dash" or "dashes" => "dash",
            "clove" or "cloves" => "clove",
            "slice" or "slices" => "slice",
            "piece" or "pieces" => "piece",
            "whole" => "whole",
            _ => unit
        };
    }

    /// <summary>
    /// Extract preparation method from ingredient text
    /// Examples: "diced", "minced", "chopped", "melted"
    /// </summary>
    protected (string ingredient, string? preparation) ExtractPreparation(string text)
    {
        var preparationWords = new[]
        {
            "diced", "minced", "chopped", "sliced", "crushed", "grated",
            "shredded", "julienned", "melted", "softened", "beaten",
            "whisked", "sifted", "toasted", "roasted", "blanched",
            "peeled", "seeded", "cored", "trimmed", "halved", "quartered",
            "cubed", "crumbled", "thawed", "frozen", "fresh", "dried",
            "cooked", "uncooked", "raw"
        };

        // Check if text contains a comma (common separator for preparation)
        if (text.Contains(','))
        {
            var parts = text.Split(',', 2);
            var ingredient = parts[0].Trim();
            var preparation = parts[1].Trim();
            return (ingredient, preparation);
        }

        // Check for parentheses
        var parenMatch = Regex.Match(text, @"^(.+?)\s*\((.+?)\)\s*$");
        if (parenMatch.Success)
        {
            return (parenMatch.Groups[1].Value.Trim(), parenMatch.Groups[2].Value.Trim());
        }

        // Look for preparation words at the end
        foreach (var word in preparationWords)
        {
            var pattern = $@"\s+{word}(\s|$)";
            if (Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase))
            {
                var parts = Regex.Split(text, pattern, RegexOptions.IgnoreCase);
                if (parts.Length >= 2)
                {
                    var ingredient = parts[0].Trim();
                    var preparation = text.Substring(ingredient.Length).Trim();
                    return (ingredient, preparation);
                }
            }
        }

        return (text, null);
    }

    /// <summary>
    /// Parse time duration from text
    /// Examples: "30 minutes", "1 hour", "2 hrs 30 min"
    /// </summary>
    protected int? ParseTime(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var totalMinutes = 0;

        // Match hours
        var hourMatch = Regex.Match(text, @"(\d+(?:\.\d+)?)\s*(?:hour|hr|hrs?|h)\b", RegexOptions.IgnoreCase);
        if (hourMatch.Success && decimal.TryParse(hourMatch.Groups[1].Value, out var hours))
        {
            totalMinutes += (int)(hours * 60);
        }

        // Match minutes
        var minMatch = Regex.Match(text, @"(\d+)\s*(?:minute|min|mins?|m)\b", RegexOptions.IgnoreCase);
        if (minMatch.Success && int.TryParse(minMatch.Groups[1].Value, out var minutes))
        {
            totalMinutes += minutes;
        }

        // If only a number without unit, assume minutes
        if (totalMinutes == 0 && int.TryParse(text.Trim(), out var directMinutes))
        {
            totalMinutes = directMinutes;
        }

        return totalMinutes > 0 ? totalMinutes : null;
    }

    /// <summary>
    /// Parse temperature from text
    /// Examples: "350F", "180C", "350 degrees"
    /// </summary>
    protected (int? temperature, string? unit) ParseTemperature(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return (null, null);

        // Match patterns like "350F", "350 F", "350°F", "350 degrees F"
        var match = Regex.Match(text, @"(\d+)\s*(?:°|degrees?)?\s*([FCfc])", RegexOptions.IgnoreCase);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var temp))
        {
            var unit = match.Groups[2].Value.ToUpper();
            return (temp, unit);
        }

        // If just a number, assume F (more common in recipes)
        if (int.TryParse(text.Trim(), out var directTemp) && directTemp >= 100 && directTemp <= 600)
        {
            return (directTemp, "F");
        }

        return (null, null);
    }

    /// <summary>
    /// Clean and normalize text
    /// </summary>
    protected string CleanText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        // Remove extra whitespace
        text = Regex.Replace(text, @"\s+", " ");

        // Trim
        text = text.Trim();

        return text;
    }

    /// <summary>
    /// Check if ingredient is optional
    /// </summary>
    protected bool IsOptionalIngredient(string text)
    {
        return text.Contains("optional", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("(optional)", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("if desired", StringComparison.OrdinalIgnoreCase);
    }
}
