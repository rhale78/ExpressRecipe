using System.Xml;
using ExpressRecipe.RecipeParser.Helpers;
using ExpressRecipe.RecipeParser.Models;

namespace ExpressRecipe.RecipeParser.Parsers;

public sealed class RecipeBookXmlParser : IRecipeParser
{
    public string FormatName => "RecipeBookXml";

    public bool CanParse(string text, string? fileExtension = null)
    {
        return text.Contains("<RecipeBook") || text.Contains("<cookbook") || text.Contains("<Cookbook");
    }

    public ParseResult Parse(string text, RecipeParseOptions? options = null)
    {
        var result = new ParseResult { Format = FormatName };
        var errors = new List<ParseError>();
        try
        {
            var doc = XmlParserHelper.LoadXml(text);
            int i = 0;
            var recipeNodes = doc.SelectNodes("//recipe") ?? doc.SelectNodes("//Recipe");
            if (recipeNodes == null) { result.Success = true; result.Errors = errors; return result; }

            foreach (XmlNode node in recipeNodes)
            {
                try
                {
                    var recipe = new ParsedRecipe { Format = FormatName };
                    recipe.Title = XmlParserHelper.GetElementText(node, "title") ?? XmlParserHelper.GetElementText(node, "name")
                        ?? XmlParserHelper.GetAttributeValue(node, "name") ?? "";
                    recipe.Description = XmlParserHelper.GetElementText(node, "description") ?? XmlParserHelper.GetElementText(node, "notes");
                    recipe.Author = XmlParserHelper.GetElementText(node, "author");
                    recipe.Yield = XmlParserHelper.GetElementText(node, "servings") ?? XmlParserHelper.GetElementText(node, "yield");
                    recipe.Category = XmlParserHelper.GetElementText(node, "category") ?? XmlParserHelper.GetElementText(node, "categories");
                    recipe.Source = XmlParserHelper.GetElementText(node, "source");

                    var ingNodes = node.SelectNodes(".//ingredient") ?? node.SelectNodes(".//Ingredient");
                    if (ingNodes != null)
                    {
                        foreach (XmlNode ing in ingNodes)
                        {
                            var ingText = ing.InnerText.Trim();
                            if (!string.IsNullOrWhiteSpace(ingText))
                            {
                                var qty = XmlParserHelper.GetAttributeValue(ing, "quantity") ?? XmlParserHelper.GetElementText(ing, "quantity");
                                var unit = XmlParserHelper.GetAttributeValue(ing, "unit") ?? XmlParserHelper.GetElementText(ing, "unit");
                                var name = XmlParserHelper.GetAttributeValue(ing, "name") ?? XmlParserHelper.GetElementText(ing, "name") ?? ingText;
                                recipe.Ingredients.Add(new ParsedIngredient { Quantity = qty, Unit = unit, Name = name });
                            }
                        }
                    }

                    int step = 1;
                    var dirNodes = node.SelectNodes(".//step") ?? node.SelectNodes(".//direction");
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
                        var dir = XmlParserHelper.GetElementText(node, "directions") ?? XmlParserHelper.GetElementText(node, "instructions")
                            ?? XmlParserHelper.GetElementText(node, "method");
                        if (!string.IsNullOrWhiteSpace(dir))
                            recipe.Instructions.Add(new ParsedInstruction { Step = 1, Text = dir });
                    }
                    result.Recipes.Add(recipe);
                }
                catch (Exception ex) { LoggingHelper.LogRecipeError(null, errors, i, null, "Failed to parse RecipeBookXml recipe", ex); }
                i++;
            }
            result.Success = true;
        }
        catch (Exception ex) { LoggingHelper.LogBatchError(null, errors, "Failed to parse RecipeBookXml", ex); }
        result.Errors = errors;
        return result;
    }
}
