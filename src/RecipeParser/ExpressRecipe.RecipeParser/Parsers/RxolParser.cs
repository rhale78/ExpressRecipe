using System.Xml;
using ExpressRecipe.RecipeParser.Helpers;
using ExpressRecipe.RecipeParser.Models;

namespace ExpressRecipe.RecipeParser.Parsers;

public sealed class RxolParser : IRecipeParser
{
    public string FormatName => "Rxol";

    public bool CanParse(string text, string? fileExtension = null)
    {
        return text.Contains("<RXOL") || text.Contains("<rxol");
    }

    public ParseResult Parse(string text, RecipeParseOptions? options = null)
    {
        var result = new ParseResult { Format = FormatName };
        var errors = new List<ParseError>();
        try
        {
            var doc = XmlParserHelper.LoadXml(text);
            int i = 0;
            var recipeNodes = doc.SelectNodes("//Recipe") ?? doc.SelectNodes("//recipe");
            if (recipeNodes == null) { result.Success = true; result.Errors = errors; return result; }

            foreach (XmlNode node in recipeNodes)
            {
                try
                {
                    var recipe = new ParsedRecipe { Format = FormatName };
                    recipe.Title = XmlParserHelper.GetElementText(node, "Title") ?? XmlParserHelper.GetAttributeValue(node, "name") ?? "";
                    recipe.Author = XmlParserHelper.GetElementText(node, "Author");
                    recipe.Yield = XmlParserHelper.GetElementText(node, "Servings") ?? XmlParserHelper.GetElementText(node, "Yield");
                    recipe.Category = XmlParserHelper.GetElementText(node, "Category");
                    recipe.Source = XmlParserHelper.GetElementText(node, "Source");

                    var ingNodes = node.SelectNodes(".//Ingredient") ?? node.SelectNodes(".//ingredient");
                    if (ingNodes != null)
                    {
                        foreach (XmlNode ing in ingNodes)
                            recipe.Ingredients.Add(new ParsedIngredient
                            {
                                Quantity = XmlParserHelper.GetElementText(ing, "Quantity") ?? XmlParserHelper.GetAttributeValue(ing, "qty"),
                                Unit = XmlParserHelper.GetElementText(ing, "Unit") ?? XmlParserHelper.GetAttributeValue(ing, "unit"),
                                Name = XmlParserHelper.GetElementText(ing, "Name") ?? XmlParserHelper.GetAttributeValue(ing, "name") ?? ing.InnerText.Trim()
                            });
                    }

                    int step = 1;
                    var dirNodes = node.SelectNodes(".//Direction") ?? node.SelectNodes(".//Step");
                    if (dirNodes != null && dirNodes.Count > 0)
                    {
                        foreach (XmlNode dir in dirNodes)
                        {
                            var t = dir.InnerText.Trim();
                            if (!string.IsNullOrWhiteSpace(t)) recipe.Instructions.Add(new ParsedInstruction { Step = step++, Text = t });
                        }
                    }
                    else
                    {
                        var dir = XmlParserHelper.GetElementText(node, "Directions") ?? XmlParserHelper.GetElementText(node, "Instructions");
                        if (!string.IsNullOrWhiteSpace(dir))
                            recipe.Instructions.Add(new ParsedInstruction { Step = 1, Text = dir });
                    }
                    result.Recipes.Add(recipe);
                }
                catch (Exception ex) { LoggingHelper.LogRecipeError(null, errors, i, null, "Failed to parse RXOL recipe", ex); }
                i++;
            }
            result.Success = true;
        }
        catch (Exception ex) { LoggingHelper.LogBatchError(null, errors, "Failed to parse RXOL XML", ex); }
        result.Errors = errors;
        return result;
    }
}
