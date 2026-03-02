using ExpressRecipe.Shared.DTOs.Product;
using ExpressRecipe.IngredientService.Data;
using System.Text.RegularExpressions;

namespace ExpressRecipe.IngredientService.Services.Parsing;

public interface IIngredientParser
{
    Task<ParsedIngredientResult> ParseIngredientStringAsync(string ingredientString);
    Task<Dictionary<string, ParsedIngredientResult>> BulkParseIngredientStringsAsync(IEnumerable<string> ingredientStrings);
}

public class IngredientParser : IIngredientParser
{
    private readonly IIngredientRepository _ingredientRepository;
    private readonly ILogger<IngredientParser> _logger;

    public IngredientParser(
        IIngredientRepository ingredientRepository,
        ILogger<IngredientParser> logger)
    {
        _ingredientRepository = ingredientRepository;
        _logger = logger;
    }

    public async Task<ParsedIngredientResult> ParseIngredientStringAsync(string ingredientString)
    {
        if (string.IsNullOrWhiteSpace(ingredientString))
        {
            return new ParsedIngredientResult { OriginalString = ingredientString ?? string.Empty };
        }

        var result = new ParsedIngredientResult
        {
            OriginalString = ingredientString
        };

        // Split by commas at the top level (not inside parentheses)
        var topLevelComponents = SplitRespectingParentheses(ingredientString, ',');

        for (int i = 0; i < topLevelComponents.Count; i++)
        {
            var component = ParseComponent(topLevelComponents[i].Trim(), i);
            if (component != null)
            {
                result.Components.Add(component);
            }
        }

        // Try to match components to existing ingredients in our DB
        await MatchComponentsToIngredientsAsync(result.Components);

        return result;
    }

    public async Task<Dictionary<string, ParsedIngredientResult>> BulkParseIngredientStringsAsync(IEnumerable<string> ingredientStrings)
    {
        var results = new Dictionary<string, ParsedIngredientResult>();
        foreach (var str in ingredientStrings.Distinct())
        {
            results[str] = await ParseIngredientStringAsync(str);
        }
        return results;
    }

    private ParsedIngredientComponent? ParseComponent(string componentString, int orderIndex)
    {
        if (string.IsNullOrWhiteSpace(componentString))
        {
            return null;
        }

        var component = new ParsedIngredientComponent
        {
            OrderIndex = orderIndex
        };

        // 1. Try to extract quantity and unit from the string
        var (quantity, unit, remaining) = ParseQuantityAndUnit(componentString);
        component.Quantity = quantity;
        component.Unit = unit;
        
        // 2. Check if remaining part has parenthetical sub-components
        var match = Regex.Match(remaining, @"^(.+?)\s*\((.+)\)$");

        if (match.Success)
        {
            component.Name = CleanIngredientName(match.Groups[1].Value);
            component.CleanName = component.Name;
            component.IsParenthetical = false;

            // Parse sub-components
            var subComponentsString = match.Groups[2].Value;
            var subComponentParts = SplitRespectingParentheses(subComponentsString, ',');

            component.SubComponents = new List<ParsedIngredientComponent>();
            for (int i = 0; i < subComponentParts.Count; i++)
            {
                var subComponent = new ParsedIngredientComponent
                {
                    Name = CleanIngredientName(subComponentParts[i].Trim()),
                    CleanName = CleanIngredientName(subComponentParts[i].Trim()),
                    OrderIndex = i,
                    IsParenthetical = true
                };
                component.SubComponents.Add(subComponent);
            }
        }
        else
        {
            component.Name = CleanIngredientName(remaining);
            component.CleanName = component.Name;
        }

        return component;
    }

    /// <summary>
    /// Parse quantity and unit from ingredient text
    /// </summary>
    private (decimal? quantity, string? unit, string remaining) ParseQuantityAndUnit(string text)
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

        // Try to extract unit (looking for word followed by space or end)
        var unitPattern = @"^([a-zA-Z]+\.?)\s+";
        var unitMatch = Regex.Match(remaining, unitPattern);

        string? unit = null;
        if (unitMatch.Success)
        {
            var potentialUnit = unitMatch.Groups[1].Value.TrimEnd('.');
            if (IsKnownUnit(potentialUnit))
            {
                unit = NormalizeUnit(potentialUnit);
                remaining = remaining.Substring(unitMatch.Length).Trim();
            }
        }

        return (quantity, unit, remaining);
    }

    private bool IsKnownUnit(string unit)
    {
        var knownUnits = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "cup", "cups", "tsp", "teaspoon", "teaspoons", "tbsp", "tablespoon", "tablespoons",
            "oz", "ounce", "ounces", "lb", "lbs", "pound", "pounds", "g", "gram", "grams",
            "kg", "kilogram", "kilograms", "ml", "milliliter", "milliliters", "l", "liter", "liters",
            "clove", "cloves", "can", "cans", "pkg", "package", "packages"
        };
        return knownUnits.Contains(unit);
    }

    private decimal? ParseQuantity(string quantityStr)
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

    private string NormalizeUnit(string unit)
    {
        unit = unit.ToLowerInvariant();
        return unit switch
        {
            "tsp" or "teaspoon" or "teaspoons" => "tsp",
            "tbsp" or "tablespoon" or "tablespoons" => "tbsp",
            "oz" or "ounce" or "ounces" => "oz",
            "lb" or "lbs" or "pound" or "pounds" => "lb",
            "g" or "gram" or "grams" => "g",
            "kg" or "kilogram" or "kilograms" => "kg",
            "ml" or "milliliter" or "milliliters" => "ml",
            "l" or "liter" or "liters" => "l",
            "clove" or "cloves" => "clove",
            "cup" or "cups" => "cup",
            _ => unit
        };
    }

    private async Task MatchComponentsToIngredientsAsync(List<ParsedIngredientComponent> components)
    {
        foreach (var component in components)
        {
            // Try matching with the CleanName if available
            var nameToMatch = component.CleanName ?? component.Name;
            var ingredient = await _ingredientRepository.GetIngredientByNameAsync(nameToMatch);

            if (ingredient != null)
            {
                component.BaseIngredientId = ingredient.Id;
                component.MatchedName = ingredient.Name;
            }

            // Match sub-components
            if (component.SubComponents != null)
            {
                await MatchComponentsToIngredientsAsync(component.SubComponents);
            }
        }
    }

    private string CleanIngredientName(string name)
    {
        name = name.Trim();
        name = Regex.Replace(name, @"\s+(and/or|or)\s*$", "", RegexOptions.IgnoreCase);
        name = Regex.Replace(name, @"\s*\([<>]?\s*\d+%\s*\)", "");
        name = name.Replace("*", "").Replace("†", "").Replace("‡", "");
        return name.Trim();
    }

    private List<string> SplitRespectingParentheses(string input, char delimiter)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        int parenthesesDepth = 0;

        foreach (char c in input)
        {
            if (c == '(')
            {
                parenthesesDepth++;
                current.Append(c);
            }
            else if (c == ')')
            {
                parenthesesDepth--;
                current.Append(c);
            }
            else if (c == delimiter && parenthesesDepth == 0)
            {
                if (current.Length > 0)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0)
        {
            result.Add(current.ToString());
        }

        return result;
    }
}
