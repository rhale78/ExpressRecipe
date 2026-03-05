using System.Xml;
using ExpressRecipe.RecipeParser.Helpers;
using ExpressRecipe.RecipeParser.Models;

namespace ExpressRecipe.RecipeParser.Parsers;

public sealed class CooknParser : IRecipeParser
{
    public string FormatName => "Cookn";

    public bool CanParse(string text, string? fileExtension = null)
    {
        return text.Contains("<cooknrecipes") || text.Contains("<CooknRecipes") || text.Contains("Cook'n") || text.Contains("<cooknrecipe");
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
                    recipe.Title = XmlParserHelper.GetElementText(node, "title") ?? XmlParserHelper.GetAttributeValue(node, "name") ?? "";
                    recipe.Description = XmlParserHelper.GetElementText(node, "description") ?? XmlParserHelper.GetElementText(node, "notes");
                    recipe.Author = XmlParserHelper.GetElementText(node, "author");
                    recipe.Yield = XmlParserHelper.GetElementText(node, "servings") ?? XmlParserHelper.GetElementText(node, "yield");
                    recipe.Category = XmlParserHelper.GetElementText(node, "category") ?? XmlParserHelper.GetElementText(node, "categories");
                    recipe.Source = XmlParserHelper.GetElementText(node, "source");
                    recipe.PrepTime = XmlParserHelper.GetElementText(node, "preptime");
                    recipe.CookTime = XmlParserHelper.GetElementText(node, "cooktime");

                    var ingNodes = node.SelectNodes(".//ingredient") ?? node.SelectNodes(".//Ingredient");
                    if (ingNodes != null)
                    {
                        foreach (XmlNode ing in ingNodes)
                            recipe.Ingredients.Add(new ParsedIngredient
                            {
                                Quantity = XmlParserHelper.GetElementText(ing, "qty") ?? XmlParserHelper.GetAttributeValue(ing, "qty"),
                                Unit = XmlParserHelper.GetElementText(ing, "unit") ?? XmlParserHelper.GetAttributeValue(ing, "unit"),
                                Name = XmlParserHelper.GetElementText(ing, "name") ?? XmlParserHelper.GetAttributeValue(ing, "name") ?? ing.InnerText.Trim()
                            });
                    }

                    int step = 1;
                    var dirNodes = node.SelectNodes(".//direction") ?? node.SelectNodes(".//step");
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
                        var dir = XmlParserHelper.GetElementText(node, "directions") ?? XmlParserHelper.GetElementText(node, "instructions");
                        if (!string.IsNullOrWhiteSpace(dir))
                            recipe.Instructions.Add(new ParsedInstruction { Step = 1, Text = dir });
                    }
                    result.Recipes.Add(recipe);
                }
                catch (Exception ex) { LoggingHelper.LogRecipeError(null, errors, i, null, "Failed to parse Cook'n recipe", ex); }
                i++;
            }
            result.Success = true;
        }
        catch (Exception ex) { LoggingHelper.LogBatchError(null, errors, "Failed to parse Cook'n XML", ex); }
        result.Errors = errors;
        return result;
    }
}
