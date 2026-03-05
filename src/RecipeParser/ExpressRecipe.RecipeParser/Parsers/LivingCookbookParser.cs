using System.Xml;
using ExpressRecipe.RecipeParser.Helpers;
using ExpressRecipe.RecipeParser.Models;

namespace ExpressRecipe.RecipeParser.Parsers;

public sealed class LivingCookbookParser : IRecipeParser
{
    public string FormatName => "LivingCookbook";

    public bool CanParse(string text, string? fileExtension = null)
    {
        if (fileExtension?.EndsWith("fdx", StringComparison.OrdinalIgnoreCase) == true) return true;
        return text.Contains("Living Cookbook") || text.Contains("<fdxz") || text.Contains("<FDX");
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
                    recipe.Title = XmlParserHelper.GetElementText(node, "RecipeName") ?? XmlParserHelper.GetAttributeValue(node, "name") ?? "";
                    recipe.Author = XmlParserHelper.GetElementText(node, "Author");
                    recipe.Yield = XmlParserHelper.GetElementText(node, "Servings");
                    recipe.Category = XmlParserHelper.GetElementText(node, "Categories");
                    recipe.Source = XmlParserHelper.GetElementText(node, "Source");
                    recipe.PrepTime = XmlParserHelper.GetElementText(node, "PrepTime");
                    recipe.CookTime = XmlParserHelper.GetElementText(node, "CookTime");
                    recipe.Description = XmlParserHelper.GetElementText(node, "Description");

                    int step = 1;
                    var ingNodes = node.SelectNodes(".//Ingredient") ?? node.SelectNodes(".//ingredient");
                    if (ingNodes != null)
                    {
                        foreach (XmlNode ing in ingNodes)
                            recipe.Ingredients.Add(new ParsedIngredient
                            {
                                Quantity = XmlParserHelper.GetAttributeValue(ing, "Quantity") ?? XmlParserHelper.GetElementText(ing, "Quantity"),
                                Unit = XmlParserHelper.GetAttributeValue(ing, "Unit") ?? XmlParserHelper.GetElementText(ing, "Unit"),
                                Name = XmlParserHelper.GetAttributeValue(ing, "Name") ?? XmlParserHelper.GetElementText(ing, "Name") ?? ing.InnerText.Trim()
                            });
                    }

                    var dirNodes = node.SelectNodes(".//Direction") ?? node.SelectNodes(".//Step");
                    if (dirNodes != null)
                    {
                        foreach (XmlNode dir in dirNodes)
                        {
                            var t = dir.InnerText.Trim();
                            if (!string.IsNullOrWhiteSpace(t)) recipe.Instructions.Add(new ParsedInstruction { Step = step++, Text = t });
                        }
                    }
                    if (recipe.Instructions.Count == 0)
                    {
                        var dir = XmlParserHelper.GetElementText(node, "Directions");
                        if (!string.IsNullOrWhiteSpace(dir))
                            recipe.Instructions.Add(new ParsedInstruction { Step = 1, Text = dir });
                    }
                    result.Recipes.Add(recipe);
                }
                catch (Exception ex) { LoggingHelper.LogRecipeError(null, errors, i, null, "Failed to parse LivingCookbook recipe", ex); }
                i++;
            }
            result.Success = true;
        }
        catch (Exception ex) { LoggingHelper.LogBatchError(null, errors, "Failed to parse LivingCookbook XML", ex); }
        result.Errors = errors;
        return result;
    }
}
