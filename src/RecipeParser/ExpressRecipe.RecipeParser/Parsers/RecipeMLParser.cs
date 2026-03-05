using System.Xml;
using ExpressRecipe.RecipeParser.Helpers;
using ExpressRecipe.RecipeParser.Models;

namespace ExpressRecipe.RecipeParser.Parsers;

public sealed class RecipeMLParser : IRecipeParser
{
    public string FormatName => "RecipeML";

    public bool CanParse(string text, string? fileExtension = null)
    {
        if (fileExtension?.EndsWith("rml", StringComparison.OrdinalIgnoreCase) == true) return true;
        return text.Contains("<RecipeML") || text.Contains("<recipeml") || text.Contains("<recipe ");
    }

    public ParseResult Parse(string text, RecipeParseOptions? options = null)
    {
        var result = new ParseResult { Format = FormatName };
        var errors = new List<ParseError>();

        try
        {
            var doc = XmlParserHelper.LoadXml(text);
            var recipeNodes = doc.SelectNodes("//recipe") ?? doc.SelectNodes("//Recipe");

            if (recipeNodes == null || recipeNodes.Count == 0)
            {
                if (doc.DocumentElement?.LocalName.Equals("recipe", StringComparison.OrdinalIgnoreCase) == true)
                    recipeNodes = doc.SelectNodes("/*");
            }

            if (recipeNodes == null)
            {
                result.Success = true;
                result.Errors = errors;
                return result;
            }

            int i = 0;
            foreach (XmlNode node in recipeNodes)
            {
                try
                {
                    var recipe = new ParsedRecipe { Format = FormatName };
                    recipe.Title = XmlParserHelper.GetElementText(node, "head/title")
                        ?? XmlParserHelper.GetAttributeValue(node, "name")
                        ?? XmlParserHelper.GetElementText(node, "title") ?? "";
                    recipe.Description = XmlParserHelper.GetElementText(node, "description");
                    recipe.Author = XmlParserHelper.GetElementText(node, "head/author");
                    recipe.Yield = XmlParserHelper.GetElementText(node, "head/yield") ?? XmlParserHelper.GetElementText(node, "yield");
                    recipe.PrepTime = XmlParserHelper.GetElementText(node, "head/preptime");
                    recipe.CookTime = XmlParserHelper.GetElementText(node, "head/cooktime");
                    recipe.Category = XmlParserHelper.GetElementText(node, "head/categories");
                    recipe.Source = XmlParserHelper.GetElementText(node, "head/source");

                    var ingNodes = node.SelectNodes(".//ingredient");
                    if (ingNodes != null)
                    {
                        foreach (XmlNode ing in ingNodes)
                        {
                            recipe.Ingredients.Add(new ParsedIngredient
                            {
                                Quantity = XmlParserHelper.GetElementText(ing, "amt/qty") ?? XmlParserHelper.GetAttributeValue(ing, "qty"),
                                Unit = XmlParserHelper.GetElementText(ing, "amt/unit") ?? XmlParserHelper.GetAttributeValue(ing, "unit"),
                                Name = XmlParserHelper.GetElementText(ing, "item") ?? ing.InnerText.Trim(),
                                Preparation = XmlParserHelper.GetAttributeValue(ing, "preparation")
                            });
                        }
                    }

                    int step = 1;
                    var stepNodes = node.SelectNodes(".//step");
                    if (stepNodes != null)
                    {
                        foreach (XmlNode stepNode in stepNodes)
                        {
                            var stepText = stepNode.InnerText.Trim();
                            if (!string.IsNullOrWhiteSpace(stepText))
                                recipe.Instructions.Add(new ParsedInstruction { Step = step++, Text = stepText });
                        }
                    }

                    if (recipe.Instructions.Count == 0)
                    {
                        var directions = XmlParserHelper.GetElementText(node, "directions");
                        if (!string.IsNullOrWhiteSpace(directions))
                            recipe.Instructions.Add(new ParsedInstruction { Step = 1, Text = directions });
                    }

                    result.Recipes.Add(recipe);
                }
                catch (Exception ex)
                {
                    LoggingHelper.LogRecipeError(null, errors, i, null, "Failed to parse RecipeML node", ex);
                }
                i++;
            }
            result.Success = true;
        }
        catch (Exception ex)
        {
            LoggingHelper.LogBatchError(null, errors, "Failed to parse RecipeML", ex);
        }

        result.Errors = errors;
        return result;
    }
}
