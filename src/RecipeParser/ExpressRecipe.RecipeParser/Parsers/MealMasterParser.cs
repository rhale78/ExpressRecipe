using ExpressRecipe.RecipeParser.Helpers;
using ExpressRecipe.RecipeParser.Models;

namespace ExpressRecipe.RecipeParser.Parsers;

public sealed class MealMasterParser : IRecipeParser
{
    public string FormatName => "MealMaster";

    public bool CanParse(string text, string? fileExtension = null)
    {
        if (fileExtension?.EndsWith("mmf", StringComparison.OrdinalIgnoreCase) == true) return true;
        return text.Contains("MMMMM") || text.TrimStart().StartsWith("-----");
    }

    public ParseResult Parse(string text, RecipeParseOptions? options = null)
    {
        var result = new ParseResult { Format = FormatName };
        var errors = new List<ParseError>();

        try
        {
            var blocks = SplitIntoBlocks(text);
            for (int i = 0; i < blocks.Count; i++)
            {
                try
                {
                    var recipe = ParseBlock(blocks[i], options);
                    result.Recipes.Add(recipe);
                }
                catch (Exception ex)
                {
                    LoggingHelper.LogRecipeError(null, errors, i, null, $"Failed to parse MealMaster recipe block {i}", ex);
                }
            }
            result.Success = result.Recipes.Count > 0 || errors.Count == 0;
        }
        catch (Exception ex)
        {
            LoggingHelper.LogBatchError(null, errors, "Failed to parse MealMaster file", ex);
        }

        result.Errors = errors;
        return result;
    }

    private static List<string> SplitIntoBlocks(string text)
    {
        var blocks = new List<string>();
        var lines = text.Split('\n');
        var current = new List<string>();
        bool inRecipe = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');
            if (line.StartsWith("MMMMM-----") || line.StartsWith("-----"))
            {
                if (inRecipe && current.Count > 0)
                {
                    string headerContent = line.Contains("-----") ? line[(line.IndexOf("-----") + 5)..].Trim() : "";
                    if (headerContent.Length > 0 && !line.Contains("Recipe via") && !line.Contains("Meal-Master"))
                    {
                        current.Add(line);
                        continue;
                    }
                    blocks.Add(string.Join("\n", current));
                    current.Clear();
                }
                inRecipe = true;
                current.Add(line);
            }
            else if (line.Trim() == "MMMMM" || line.Trim() == "-----")
            {
                if (inRecipe)
                {
                    current.Add(line);
                    blocks.Add(string.Join("\n", current));
                    current.Clear();
                    inRecipe = false;
                }
            }
            else if (inRecipe)
            {
                current.Add(line);
            }
        }

        if (current.Count > 0) blocks.Add(string.Join("\n", current));
        return blocks;
    }

    private static ParsedRecipe ParseBlock(string block, RecipeParseOptions? options)
    {
        var recipe = new ParsedRecipe { Format = "MealMaster" };
        if (options?.IncludeRawText == true) recipe.RawText = block;

        var lines = block.Split('\n');
        bool inIngredients = false;
        bool inInstructions = false;
        bool hasSeenIngredients = false;
        var instructionLines = new List<string>();

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');

            if (line.StartsWith("MMMMM-----") || line.StartsWith("-----"))
            {
                string header = line.TrimStart('-').TrimStart('M').TrimEnd('-').Trim();
                if (header.Contains("Recipe via") || string.IsNullOrEmpty(header))
                {
                    inIngredients = false;
                    continue;
                }
                if (header.Contains("Instruction", StringComparison.OrdinalIgnoreCase) ||
                    header.Contains("Direction", StringComparison.OrdinalIgnoreCase) ||
                    header.Contains("Method", StringComparison.OrdinalIgnoreCase))
                {
                    inInstructions = true;
                    inIngredients = false;
                }
                else if (header.Contains("Ingredient", StringComparison.OrdinalIgnoreCase))
                {
                    inIngredients = true;
                    inInstructions = false;
                }
                continue;
            }

            if (line.Trim() == "MMMMM" || line.Trim() == "-----") break;

            if (line.TrimStart().StartsWith("Title:", StringComparison.OrdinalIgnoreCase))
            {
                recipe.Title = ExtractField(line, "Title:");
                continue;
            }
            if (line.TrimStart().StartsWith("Categories:", StringComparison.OrdinalIgnoreCase))
            {
                recipe.Category = ExtractField(line, "Categories:");
                continue;
            }
            if (line.TrimStart().StartsWith("Yield:", StringComparison.OrdinalIgnoreCase))
            {
                recipe.Yield = ExtractField(line, "Yield:");
                inIngredients = true;
                continue;
            }
            if (line.TrimStart().StartsWith("Servings:", StringComparison.OrdinalIgnoreCase))
            {
                recipe.Yield = ExtractField(line, "Servings:");
                inIngredients = true;
                continue;
            }
            if (line.TrimStart().StartsWith("Source:", StringComparison.OrdinalIgnoreCase))
            {
                recipe.Source = ExtractField(line, "Source:");
                continue;
            }
            if (line.TrimStart().StartsWith("Prep Time:", StringComparison.OrdinalIgnoreCase))
            {
                recipe.PrepTime = ExtractField(line, "Prep Time:");
                continue;
            }
            if (line.TrimStart().StartsWith("Cook Time:", StringComparison.OrdinalIgnoreCase))
            {
                recipe.CookTime = ExtractField(line, "Cook Time:");
                continue;
            }

            if (inIngredients)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    // Only transition to instructions if we've already parsed some ingredients
                    // The blank line right after Yield is just header spacing
                    if (hasSeenIngredients)
                    {
                        inIngredients = false;
                        inInstructions = true;
                    }
                    continue;
                }

                if (line.TrimStart().StartsWith("----- ") || line.Contains("  ----- "))
                {
                    string heading = line.Trim().Trim('-').Trim();
                    recipe.Ingredients.Add(new ParsedIngredient { GroupHeading = heading, Name = "" });
                    continue;
                }

                var columns = IngredientColumnHelper.SplitColumns(line);
                int colNum = 1;
                foreach (var col in columns)
                {
                    if (!string.IsNullOrWhiteSpace(col))
                    {
                        var ing = IngredientColumnHelper.ParseMealMasterColumn(col, colNum);
                        if (!string.IsNullOrWhiteSpace(ing.Name))
                        {
                            recipe.Ingredients.Add(ing);
                            hasSeenIngredients = true;
                        }
                        colNum++;
                    }
                }
            }
            else if (inInstructions || (!inIngredients && recipe.Title != null && !string.IsNullOrWhiteSpace(line)))
            {
                if (!string.IsNullOrWhiteSpace(line))
                    instructionLines.Add(line.Trim());
            }
        }

        if (instructionLines.Count > 0)
            recipe.Instructions.Add(new ParsedInstruction { Step = 1, Text = string.Join(" ", instructionLines) });

        return recipe;
    }

    private static string ExtractField(string line, string fieldName)
    {
        int idx = line.IndexOf(fieldName, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return line.Trim();
        return line[(idx + fieldName.Length)..].Trim();
    }
}
