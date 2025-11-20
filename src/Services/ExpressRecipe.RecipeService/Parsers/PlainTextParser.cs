using System.Text.RegularExpressions;

namespace ExpressRecipe.RecipeService.Parsers;

/// <summary>
/// Parser for plain text recipes
/// Uses heuristics to identify recipe components
/// </summary>
public class PlainTextParser : RecipeParserBase
{
    public override string ParserName => "PlainTextParser";
    public override string SourceType => "Text";

    public override bool CanParse(string content, ParserContext context)
    {
        // Always can attempt to parse plain text
        return !string.IsNullOrWhiteSpace(content);
    }

    public override Task<List<ParsedRecipe>> ParseAsync(string content, ParserContext context)
    {
        var recipe = new ParsedRecipe
        {
            Name = context.FileName ?? "Imported Recipe"
        };

        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        if (lines.Count == 0)
        {
            return Task.FromResult(new List<ParsedRecipe>());
        }

        // First non-empty line is likely the title
        recipe.Name = lines[0];

        var currentSection = DetectSection(lines[0]);
        var ingredientOrder = 0;
        var stepNumber = 0;

        for (int i = 1; i < lines.Count; i++)
        {
            var line = lines[i];

            // Check for section headers
            var newSection = DetectSection(line);
            if (newSection != "unknown")
            {
                currentSection = newSection;
                continue;
            }

            // Parse based on current section
            if (currentSection == "ingredients" || IsLikelyIngredient(line))
            {
                var ingredient = ParseIngredientLine(line, ingredientOrder++);
                if (ingredient != null)
                {
                    recipe.Ingredients.Add(ingredient);
                }
            }
            else if (currentSection == "instructions" || IsLikelyInstruction(line))
            {
                recipe.Instructions.Add(new ParsedInstruction
                {
                    StepNumber = ++stepNumber,
                    InstructionText = CleanText(line)
                });
            }
            else if (currentSection == "description")
            {
                recipe.Description = (recipe.Description ?? "") + " " + line;
            }
            else if (line.Contains("servings", StringComparison.OrdinalIgnoreCase) ||
                     line.Contains("serves", StringComparison.OrdinalIgnoreCase))
            {
                var match = Regex.Match(line, @"(\d+)\s*(?:servings?|serves)", RegexOptions.IgnoreCase);
                if (match.Success && int.TryParse(match.Groups[1].Value, out var servings))
                {
                    recipe.Servings = servings;
                }
            }
            else if (line.Contains("prep", StringComparison.OrdinalIgnoreCase) ||
                     line.Contains("preparation", StringComparison.OrdinalIgnoreCase))
            {
                recipe.PrepTimeMinutes = ParseTime(line);
            }
            else if (line.Contains("cook", StringComparison.OrdinalIgnoreCase) &&
                     !line.Contains("cookbook", StringComparison.OrdinalIgnoreCase))
            {
                recipe.CookTimeMinutes = ParseTime(line);
            }
        }

        // Calculate total time
        if (recipe.PrepTimeMinutes.HasValue || recipe.CookTimeMinutes.HasValue)
        {
            recipe.TotalTimeMinutes = (recipe.PrepTimeMinutes ?? 0) + (recipe.CookTimeMinutes ?? 0);
        }

        return Task.FromResult(new List<ParsedRecipe> { recipe });
    }

    private string DetectSection(string line)
    {
        var lower = line.ToLower();

        if (Regex.IsMatch(lower, @"^ingredients?:?\s*$"))
            return "ingredients";

        if (Regex.IsMatch(lower, @"^(?:instructions?|directions?|steps?|method|procedure):?\s*$"))
            return "instructions";

        if (Regex.IsMatch(lower, @"^(?:description|about|intro):?\s*$"))
            return "description";

        return "unknown";
    }

    private bool IsLikelyIngredient(string line)
    {
        // Ingredients typically start with a quantity
        if (char.IsDigit(line[0]))
            return true;

        // Or common quantity words
        if (Regex.IsMatch(line, @"^\s*(?:a|an|the|some|half|quarter)\s+", RegexOptions.IgnoreCase))
            return true;

        // Or have measurement units
        var units = new[] { "cup", "tsp", "tbsp", "oz", "lb", "gram", "liter", "ml", "kg", "pinch", "dash" };
        foreach (var unit in units)
        {
            if (line.Contains(unit, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private bool IsLikelyInstruction(string line)
    {
        // Instructions often start with action verbs
        var verbs = new[]
        {
            "preheat", "heat", "cook", "bake", "boil", "simmer", "fry", "sauté",
            "mix", "stir", "whisk", "blend", "combine", "add", "pour", "spread",
            "place", "arrange", "transfer", "remove", "drain", "rinse", "wash",
            "chop", "dice", "mince", "slice", "cut", "peel", "grate", "shred"
        };

        var lower = line.ToLower();
        foreach (var verb in verbs)
        {
            if (lower.StartsWith(verb))
                return true;
        }

        // Or numbered steps
        if (Regex.IsMatch(line, @"^\d+\.\s"))
            return true;

        // Or descriptive cooking instructions
        if (line.Length > 40 && (line.Contains("until") || line.Contains("for") && line.Contains("minute")))
            return true;

        return false;
    }

    private ParsedIngredient? ParseIngredientLine(string line, int order)
    {
        // Remove leading bullet points or dashes
        line = Regex.Replace(line, @"^[-•*]\s+", "");

        var (quantity, unit, remaining) = ParseQuantityAndUnit(line);

        if (string.IsNullOrWhiteSpace(remaining))
            remaining = line; // Use full line if we couldn't parse quantity

        var (ingredient, preparation) = ExtractPreparation(remaining);

        if (string.IsNullOrWhiteSpace(ingredient))
            return null;

        return new ParsedIngredient
        {
            Order = order,
            Quantity = quantity,
            Unit = unit,
            IngredientName = ingredient,
            Preparation = preparation,
            IsOptional = IsOptionalIngredient(line),
            OriginalText = line
        };
    }
}
