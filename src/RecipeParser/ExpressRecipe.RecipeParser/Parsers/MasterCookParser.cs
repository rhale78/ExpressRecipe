using System.Xml;
using ExpressRecipe.RecipeParser.Helpers;
using ExpressRecipe.RecipeParser.Models;

namespace ExpressRecipe.RecipeParser.Parsers;

/// <summary>Handles both MasterCook .mxp (plain text) and .mx2 (XML) formats.</summary>
public sealed class MasterCookParser : IRecipeParser
{
    public string FormatName => "MasterCook";

    public bool CanParse(string text, string? fileExtension = null)
    {
        if (fileExtension?.EndsWith("mxp", StringComparison.OrdinalIgnoreCase) == true) return true;
        if (fileExtension?.EndsWith("mx2", StringComparison.OrdinalIgnoreCase) == true) return true;
        return text.Contains("* Exported from MasterCook") ||
               text.Contains("<mx2") || text.Contains("<MX2");
    }

    public ParseResult Parse(string text, RecipeParseOptions? options = null)
    {
        var trimmed = text.TrimStart();
        if (trimmed.StartsWith("<") && (trimmed.Contains("<mx2") || trimmed.Contains("<MX2")))
            return ParseMx2Xml(text, options);
        return ParseMxpText(text, options);
    }

    /// <summary>
    /// Parse an MXP ingredient line: Amount(8) + Measure(14) + Ingredient[ -- Preparation]
    /// </summary>
    private static ParsedIngredient ParseMxpIngredientLine(string line)
    {
        var ing = new ParsedIngredient();
        if (string.IsNullOrWhiteSpace(line)) return ing;

        // MXP fixed-width: amount in first ~8 chars, measure next ~14 chars, then ingredient name
        // Also handle short lines from fallback parsing
        var span = line.AsSpan();
        const int amtWidth = 8;
        const int measureWidth = 14;

        if (span.Length >= amtWidth)
        {
            var amtPart = span[..amtWidth].Trim();
            if (!amtPart.IsEmpty)
                ing.Quantity = amtPart.ToString();

            if (span.Length >= amtWidth + measureWidth)
            {
                var measPart = span[amtWidth..(amtWidth + measureWidth)].Trim();
                if (!measPart.IsEmpty)
                    ing.Unit = measPart.ToString();

                var namePart = span[(amtWidth + measureWidth)..].Trim().ToString();
                if (namePart.Contains(" -- "))
                {
                    var parts = namePart.Split(" -- ", 2);
                    ing.Name = parts[0].Trim();
                    ing.Preparation = parts[1].Trim();
                }
                else
                {
                    ing.Name = namePart;
                }
            }
            else
            {
                ing.Name = span[amtWidth..].Trim().ToString();
            }
        }
        else
        {
            // Short line fallback
            var (qty, unit, name) = TextParserHelper.ParseIngredientLine(line);
            ing.Quantity = qty;
            ing.Unit = unit;
            ing.Name = name;
        }

        return ing;
    }

    private static ParseResult ParseMxpText(string text, RecipeParseOptions? options)
    {
        var result = new ParseResult { Format = "MasterCook" };
        var errors = new List<ParseError>();

        var blocks = new List<string>();
        var current = new List<string>();
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (line.StartsWith("* Exported from MasterCook") || line.StartsWith("RECIPE"))
            {
                if (current.Count > 0) { blocks.Add(string.Join("\n", current)); current.Clear(); }
            }
            current.Add(line);
        }
        if (current.Count > 0) blocks.Add(string.Join("\n", current));

        for (int i = 0; i < blocks.Count; i++)
        {
            try { result.Recipes.Add(ParseMxpBlock(blocks[i], options)); }
            catch (Exception ex) { LoggingHelper.LogRecipeError(null, errors, i, null, "Failed to parse MasterCook MXP block", ex); }
        }

        result.Success = result.Recipes.Count > 0 || errors.Count == 0;
        result.Errors = errors;
        return result;
    }

    private static ParsedRecipe ParseMxpBlock(string block, RecipeParseOptions? options)
    {
        var recipe = new ParsedRecipe { Format = "MasterCook" };
        if (options?.IncludeRawText == true) recipe.RawText = block;

        var lines = block.Split('\n');
        bool inIngredients = false;
        bool inInstructions = false;
        var instructionLines = new List<string>();

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');

            if (line.StartsWith("Recipe By:")) { recipe.Author = line["Recipe By:".Length..].Trim(); continue; }
            if (line.StartsWith("Serving Size:"))
            {
                recipe.Yield = line["Serving Size:".Length..].Trim();
                inIngredients = true;
                continue;
            }
            if (line.StartsWith("Preparation Time:")) { recipe.PrepTime = line["Preparation Time:".Length..].Trim(); continue; }
            if (line.StartsWith("Categories:")) { recipe.Category = line["Categories:".Length..].Trim(); continue; }

            if (line.TrimStart() == "Amount  Measure       Ingredient -- Preparation Method")
            {
                inIngredients = true; continue;
            }
            if (line.TrimStart().StartsWith("--------"))
            {
                inIngredients = true; continue;
            }

            if (inIngredients && string.IsNullOrWhiteSpace(line))
            {
                inIngredients = false; inInstructions = true; continue;
            }

            if (inIngredients)
            {
                var cols = IngredientColumnHelper.SplitColumns(line);
                int colNum = 1;
                foreach (var col in cols)
                {
                    if (!string.IsNullOrWhiteSpace(col))
                    {
                        var ing = ParseMxpIngredientLine(col);
                        if (!string.IsNullOrEmpty(ing.Name))
                            recipe.Ingredients.Add(ing);
                        colNum++;
                    }
                }
            }
            else if (inInstructions && !string.IsNullOrWhiteSpace(line))
            {
                instructionLines.Add(line.Trim());
            }
            else if (string.IsNullOrEmpty(recipe.Title) && !string.IsNullOrWhiteSpace(line) &&
                     !line.StartsWith("*") && !line.StartsWith("-"))
            {
                recipe.Title = line.Trim();
            }
        }

        if (instructionLines.Count > 0)
            recipe.Instructions.Add(new ParsedInstruction { Step = 1, Text = string.Join(" ", instructionLines) });

        return recipe;
    }

    private static ParseResult ParseMx2Xml(string text, RecipeParseOptions? options)
    {
        var result = new ParseResult { Format = "MasterCook" };
        var errors = new List<ParseError>();

        try
        {
            var doc = XmlParserHelper.LoadXml(text);
            var recipeNodes = XmlParserHelper.GetChildNodes(doc.DocumentElement, "//Recipe");
            if (recipeNodes.Count == 0)
                recipeNodes = XmlParserHelper.GetChildNodes(doc.DocumentElement, "Recipe");

            for (int i = 0; i < recipeNodes.Count; i++)
            {
                try
                {
                    var node = recipeNodes[i];
                    var recipe = new ParsedRecipe { Format = "MasterCook" };
                    recipe.Title = XmlParserHelper.GetAttributeValue(node, "name") ?? XmlParserHelper.GetElementText(node, "Title") ?? "";
                    recipe.Author = XmlParserHelper.GetAttributeValue(node, "author") ?? XmlParserHelper.GetElementText(node, "Author");
                    recipe.Yield = XmlParserHelper.GetAttributeValue(node, "servings") ?? XmlParserHelper.GetElementText(node, "Serving");
                    recipe.Category = XmlParserHelper.GetElementText(node, "Category");

                    var ingNodes = node.SelectNodes("IngredientList/Ingredient") ?? node.SelectNodes("Ingredient");
                    if (ingNodes != null)
                    {
                        foreach (XmlNode ing in ingNodes)
                        {
                            recipe.Ingredients.Add(new ParsedIngredient
                            {
                                Quantity = XmlParserHelper.GetAttributeValue(ing, "quantity"),
                                Unit = XmlParserHelper.GetAttributeValue(ing, "unit"),
                                Name = XmlParserHelper.GetAttributeValue(ing, "ingredient") ?? XmlParserHelper.GetElementText(ing, "Name") ?? "",
                                Preparation = XmlParserHelper.GetAttributeValue(ing, "preparation")
                            });
                        }
                    }

                    var directions = XmlParserHelper.GetElementText(node, "Directions") ?? XmlParserHelper.GetElementText(node, "Instructions");
                    if (!string.IsNullOrWhiteSpace(directions))
                        recipe.Instructions.Add(new ParsedInstruction { Step = 1, Text = directions });

                    result.Recipes.Add(recipe);
                }
                catch (Exception ex)
                {
                    LoggingHelper.LogRecipeError(null, errors, i, null, "Failed to parse MX2 recipe node", ex);
                }
            }
            result.Success = true;
        }
        catch (Exception ex)
        {
            LoggingHelper.LogBatchError(null, errors, "Failed to parse MX2 XML", ex);
        }

        result.Errors = errors;
        return result;
    }
}
