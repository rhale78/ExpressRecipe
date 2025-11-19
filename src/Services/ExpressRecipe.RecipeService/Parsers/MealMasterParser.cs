using System.Text;

namespace ExpressRecipe.RecipeService.Parsers;

/// <summary>
/// Parser for MealMaster recipe format (.mmf, .mm)
/// MealMaster is a classic recipe format with specific structure
/// </summary>
public class MealMasterParser : RecipeParserBase
{
    public override string ParserName => "MealMasterParser";
    public override string SourceType => "MealMaster";

    public override bool CanParse(string content, ParserContext context)
    {
        // MealMaster files typically start with "MMMMM" or contain "-----" separators
        return content.Contains("MMMMM") ||
               (content.Contains("-----") && content.Contains("Title:"));
    }

    public override async Task<List<ParsedRecipe>> ParseAsync(string content, ParserContext context)
    {
        var recipes = new List<ParsedRecipe>();

        // MealMaster files can contain multiple recipes separated by "MMMMM"
        var recipeSections = content.Split(new[] { "MMMMM", "-----" }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var section in recipeSections)
        {
            if (string.IsNullOrWhiteSpace(section))
                continue;

            var recipe = await ParseSingleRecipeAsync(section);
            if (recipe != null)
            {
                recipes.Add(recipe);
            }
        }

        return recipes;
    }

    private Task<ParsedRecipe?> ParseSingleRecipeAsync(string content)
    {
        var recipe = new ParsedRecipe();
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        var currentSection = "header";
        var ingredientOrder = 0;
        var stepNumber = 0;
        string? currentIngredientSection = null;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line))
                continue;

            // Parse header fields
            if (line.StartsWith("Title:", StringComparison.OrdinalIgnoreCase))
            {
                recipe.Name = line.Substring(6).Trim();
            }
            else if (line.StartsWith("Categories:", StringComparison.OrdinalIgnoreCase))
            {
                var categories = line.Substring(11).Split(',');
                recipe.Categories.AddRange(categories.Select(c => c.Trim()));
            }
            else if (line.StartsWith("Yield:", StringComparison.OrdinalIgnoreCase) ||
                     line.StartsWith("Servings:", StringComparison.OrdinalIgnoreCase))
            {
                var yieldText = line.Split(':', 2)[1].Trim();
                if (int.TryParse(new string(yieldText.Where(char.IsDigit).ToArray()), out var servings))
                {
                    recipe.Servings = servings;
                }
            }
            else if (line.StartsWith("Preparation Time:", StringComparison.OrdinalIgnoreCase) ||
                     line.StartsWith("Prep Time:", StringComparison.OrdinalIgnoreCase))
            {
                var timeText = line.Split(':', 2)[1].Trim();
                recipe.PrepTimeMinutes = ParseTime(timeText);
            }
            else if (line.StartsWith("Cook Time:", StringComparison.OrdinalIgnoreCase))
            {
                var timeText = line.Split(':', 2)[1].Trim();
                recipe.CookTimeMinutes = ParseTime(timeText);
            }
            else if (line.StartsWith("Source:", StringComparison.OrdinalIgnoreCase) ||
                     line.StartsWith("From:", StringComparison.OrdinalIgnoreCase))
            {
                recipe.Source = line.Split(':', 2)[1].Trim();
            }
            // Ingredient section headers (often in CAPS or with dashes)
            else if (line.All(c => char.IsUpper(c) || char.IsWhiteSpace(c) || c == '-') && line.Length > 3 && line.Length < 50)
            {
                currentIngredientSection = line.Trim('-').Trim();
            }
            // Parse ingredients (typically have quantities at the start)
            else if (char.IsDigit(line[0]) || line.StartsWith("  "))
            {
                var ingredient = ParseIngredientLine(line, ingredientOrder++, currentIngredientSection);
                if (ingredient != null)
                {
                    recipe.Ingredients.Add(ingredient);
                }
            }
            // Everything else is likely instructions
            else if (line.Length > 20 && !line.Contains(":"))
            {
                currentSection = "instructions";
                recipe.Instructions.Add(new ParsedInstruction
                {
                    StepNumber = ++stepNumber,
                    InstructionText = CleanText(line)
                });
            }
        }

        // Calculate total time
        if (recipe.PrepTimeMinutes.HasValue || recipe.CookTimeMinutes.HasValue)
        {
            recipe.TotalTimeMinutes = (recipe.PrepTimeMinutes ?? 0) + (recipe.CookTimeMinutes ?? 0);
        }

        return Task.FromResult(string.IsNullOrWhiteSpace(recipe.Name) ? null : recipe);
    }

    private ParsedIngredient? ParseIngredientLine(string line, int order, string? sectionName)
    {
        line = line.Trim();
        if (string.IsNullOrWhiteSpace(line))
            return null;

        var (quantity, unit, remaining) = ParseQuantityAndUnit(line);

        if (string.IsNullOrWhiteSpace(remaining))
            return null;

        var (ingredient, preparation) = ExtractPreparation(remaining);

        return new ParsedIngredient
        {
            Order = order,
            SectionName = sectionName,
            Quantity = quantity,
            Unit = unit,
            IngredientName = ingredient,
            Preparation = preparation,
            IsOptional = IsOptionalIngredient(line),
            OriginalText = line
        };
    }
}
