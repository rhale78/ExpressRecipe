using ExpressRecipe.RecipeParser.Helpers;
using ExpressRecipe.RecipeParser.Models;

namespace ExpressRecipe.RecipeParser.Parsers;

/// <summary>
/// Parses Connoisseur recipe format - recipes separated by @@@@@ or @@@ delimiters.
/// Fields identified by keywords like "Title:", "Ingredients:", "Directions:".
/// </summary>
public sealed class ConnoisseurParser : IRecipeParser
{
    public string FormatName => "Connoisseur";

    public bool CanParse(string text, string? fileExtension = null)
    {
        return text.Contains("@@@@@") || text.TrimStart().StartsWith("@@@");
    }

    public ParseResult Parse(string text, RecipeParseOptions? options = null)
    {
        var result = new ParseResult { Format = FormatName };
        var errors = new List<ParseError>();

        try
        {
            var blocks = SplitBlocks(text);
            for (int i = 0; i < blocks.Count; i++)
            {
                try
                {
                    var recipe = ParseBlock(blocks[i], options);
                    if (!string.IsNullOrWhiteSpace(recipe.Title) || recipe.Ingredients.Count > 0)
                        result.Recipes.Add(recipe);
                }
                catch (Exception ex) { LoggingHelper.LogRecipeError(null, errors, i, null, "Failed to parse Connoisseur block", ex); }
            }
            result.Success = true;
        }
        catch (Exception ex) { LoggingHelper.LogBatchError(null, errors, "Failed to parse Connoisseur", ex); }

        result.Errors = errors;
        return result;
    }

    private static List<string> SplitBlocks(string text)
    {
        var blocks = new List<string>();
        var current = new List<string>();
        bool inBlock = false;

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (line.StartsWith("@@@"))
            {
                if (inBlock && current.Count > 0)
                {
                    blocks.Add(string.Join("\n", current));
                    current.Clear();
                }
                inBlock = true;
                // Don't add the delimiter line
                continue;
            }
            if (inBlock)
                current.Add(line);
        }
        if (current.Count > 0) blocks.Add(string.Join("\n", current));
        return blocks;
    }

    private static ParsedRecipe ParseBlock(string block, RecipeParseOptions? options)
    {
        var recipe = new ParsedRecipe { Format = "Connoisseur" };
        if (options?.IncludeRawText == true) recipe.RawText = block;

        var lines = block.Split('\n');
        string section = "header";
        var instructionLines = new List<string>();

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r').Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;

            if (line.StartsWith("Title:", StringComparison.OrdinalIgnoreCase)) { recipe.Title = line["Title:".Length..].Trim(); continue; }
            if (line.StartsWith("Yield:", StringComparison.OrdinalIgnoreCase)) { recipe.Yield = line["Yield:".Length..].Trim(); continue; }
            if (line.StartsWith("Servings:", StringComparison.OrdinalIgnoreCase)) { recipe.Yield = line["Servings:".Length..].Trim(); continue; }
            if (line.StartsWith("Source:", StringComparison.OrdinalIgnoreCase)) { recipe.Source = line["Source:".Length..].Trim(); continue; }
            if (line.StartsWith("Category:", StringComparison.OrdinalIgnoreCase) || line.StartsWith("Categories:", StringComparison.OrdinalIgnoreCase))
            {
                int colon = line.IndexOf(':');
                recipe.Category = line[(colon + 1)..].Trim();
                continue;
            }
            if (line.StartsWith("Prep Time:", StringComparison.OrdinalIgnoreCase)) { recipe.PrepTime = line["Prep Time:".Length..].Trim(); continue; }
            if (line.StartsWith("Cook Time:", StringComparison.OrdinalIgnoreCase)) { recipe.CookTime = line["Cook Time:".Length..].Trim(); continue; }
            if (line.StartsWith("Author:", StringComparison.OrdinalIgnoreCase)) { recipe.Author = line["Author:".Length..].Trim(); continue; }
            if (line.StartsWith("Description:", StringComparison.OrdinalIgnoreCase)) { recipe.Description = line["Description:".Length..].Trim(); continue; }

            if (line.Equals("Ingredients:", StringComparison.OrdinalIgnoreCase) || line.StartsWith("Ingredients:", StringComparison.OrdinalIgnoreCase))
            { section = "ingredients"; continue; }
            if (line.Equals("Directions:", StringComparison.OrdinalIgnoreCase) || line.Equals("Instructions:", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("Directions:", StringComparison.OrdinalIgnoreCase))
            { section = "directions"; continue; }

            if (section == "ingredients")
            {
                var ingLine = line.TrimStart('-', '*', ' ');
                if (!string.IsNullOrWhiteSpace(ingLine))
                {
                    var (qty, unit, name) = TextParserHelper.ParseIngredientLine(ingLine);
                    string n = name;
                    string p = TextParserHelper.ExtractPreparation(ref n);
                    recipe.Ingredients.Add(new ParsedIngredient { Quantity = qty, Unit = unit, Name = n, Preparation = string.IsNullOrEmpty(p) ? null : p });
                }
            }
            else if (section == "directions")
            {
                var dirLine = line.TrimStart('-', '*', ' ');
                if (!string.IsNullOrWhiteSpace(dirLine))
                    instructionLines.Add(dirLine);
            }
            else if (section == "header" && string.IsNullOrEmpty(recipe.Title) && !string.IsNullOrWhiteSpace(line))
            {
                recipe.Title = line;
            }
        }

        if (instructionLines.Count > 0)
            recipe.Instructions.Add(new ParsedInstruction { Step = 1, Text = string.Join(" ", instructionLines) });

        return recipe;
    }
}
