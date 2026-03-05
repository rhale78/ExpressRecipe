using ExpressRecipe.RecipeParser.Helpers;
using ExpressRecipe.RecipeParser.Models;

namespace ExpressRecipe.RecipeParser.Parsers;

/// <summary>
/// Parses simple text-based recipe format:
/// RECIPE: Title
/// INGREDIENTS:
/// - ingredient lines
/// DIRECTIONS:
/// - direction text
/// </summary>
public sealed class EatDrinkFeedGoodParser : IRecipeParser
{
    public string FormatName => "EatDrinkFeedGood";

    public bool CanParse(string text, string? fileExtension = null)
    {
        return text.Contains("RECIPE:") || (text.Contains("INGREDIENTS:") && text.Contains("DIRECTIONS:"));
    }

    public ParseResult Parse(string text, RecipeParseOptions? options = null)
    {
        var result = new ParseResult { Format = FormatName };
        var errors = new List<ParseError>();

        try
        {
            // Split by double blank line or RECIPE: delimiter
            var blocks = SplitBlocks(text);
            for (int i = 0; i < blocks.Count; i++)
            {
                try
                {
                    var recipe = ParseBlock(blocks[i], options);
                    if (!string.IsNullOrWhiteSpace(recipe.Title) || recipe.Ingredients.Count > 0)
                        result.Recipes.Add(recipe);
                }
                catch (Exception ex) { LoggingHelper.LogRecipeError(null, errors, i, null, "Failed to parse EDFG block", ex); }
            }
            result.Success = true;
        }
        catch (Exception ex) { LoggingHelper.LogBatchError(null, errors, "Failed to parse EatDrinkFeedGood", ex); }

        result.Errors = errors;
        return result;
    }

    private static List<string> SplitBlocks(string text)
    {
        var blocks = new List<string>();
        var current = new List<string>();
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (line.StartsWith("RECIPE:", StringComparison.OrdinalIgnoreCase) && current.Count > 0)
            {
                blocks.Add(string.Join("\n", current));
                current.Clear();
            }
            current.Add(line);
        }
        if (current.Count > 0) blocks.Add(string.Join("\n", current));
        return blocks;
    }

    private static ParsedRecipe ParseBlock(string block, RecipeParseOptions? options)
    {
        var recipe = new ParsedRecipe { Format = "EatDrinkFeedGood" };
        if (options?.IncludeRawText == true) recipe.RawText = block;

        var lines = block.Split('\n');
        string section = "header";
        var instructionLines = new List<string>();

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r').Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;

            if (line.StartsWith("RECIPE:", StringComparison.OrdinalIgnoreCase))
            {
                recipe.Title = line["RECIPE:".Length..].Trim();
                section = "header";
                continue;
            }
            if (line.Equals("INGREDIENTS:", StringComparison.OrdinalIgnoreCase) || line.StartsWith("INGREDIENTS:", StringComparison.OrdinalIgnoreCase))
            {
                section = "ingredients";
                continue;
            }
            if (line.Equals("DIRECTIONS:", StringComparison.OrdinalIgnoreCase) || line.StartsWith("DIRECTIONS:", StringComparison.OrdinalIgnoreCase)
                || line.Equals("INSTRUCTIONS:", StringComparison.OrdinalIgnoreCase))
            {
                section = "directions";
                continue;
            }
            if (line.StartsWith("YIELD:", StringComparison.OrdinalIgnoreCase)) { recipe.Yield = line["YIELD:".Length..].Trim(); continue; }
            if (line.StartsWith("SOURCE:", StringComparison.OrdinalIgnoreCase)) { recipe.Source = line["SOURCE:".Length..].Trim(); continue; }
            if (line.StartsWith("CATEGORY:", StringComparison.OrdinalIgnoreCase)) { recipe.Category = line["CATEGORY:".Length..].Trim(); continue; }
            if (line.StartsWith("AUTHOR:", StringComparison.OrdinalIgnoreCase)) { recipe.Author = line["AUTHOR:".Length..].Trim(); continue; }

            if (section == "ingredients")
            {
                var ingLine = line.TrimStart('-', '*', '•', ' ');
                if (!string.IsNullOrWhiteSpace(ingLine))
                {
                    var (qty, unit, name) = TextParserHelper.ParseIngredientLine(ingLine);
                    string n = name;
                    string p = TextParserHelper.ExtractPreparation(ref n);
                    recipe.Ingredients.Add(new ParsedIngredient
                    {
                        Quantity = qty,
                        Unit = unit,
                        Name = n,
                        Preparation = string.IsNullOrEmpty(p) ? null : p
                    });
                }
            }
            else if (section == "directions")
            {
                var dirLine = line.TrimStart('-', '*', '•', ' ');
                if (!string.IsNullOrWhiteSpace(dirLine))
                    instructionLines.Add(dirLine);
            }
        }

        if (instructionLines.Count > 0)
            recipe.Instructions.Add(new ParsedInstruction { Step = 1, Text = string.Join(" ", instructionLines) });

        return recipe;
    }
}
