using System.Xml;
using ExpressRecipe.RecipeParser.Helpers;
using ExpressRecipe.RecipeParser.Models;

namespace ExpressRecipe.RecipeParser.Parsers;

public sealed class HomeCookinParser : IRecipeParser
{
    public string FormatName => "HomeCookin";

    public bool CanParse(string text, string? fileExtension = null)
    {
        return text.Contains("<HomeCookin") || text.Contains("<homecookin") || text.Contains("<HOMECOOKIN");
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
                    recipe.Title = XmlParserHelper.GetElementText(node, "Name") ?? XmlParserHelper.GetElementText(node, "name") ?? XmlParserHelper.GetAttributeValue(node, "name") ?? "";
                    recipe.Description = XmlParserHelper.GetElementText(node, "Description") ?? XmlParserHelper.GetElementText(node, "Notes");
                    recipe.Author = XmlParserHelper.GetElementText(node, "Author");
                    recipe.Yield = XmlParserHelper.GetElementText(node, "Servings") ?? XmlParserHelper.GetElementText(node, "Yield");
                    recipe.Category = XmlParserHelper.GetElementText(node, "Category") ?? XmlParserHelper.GetElementText(node, "Categories");
                    recipe.Source = XmlParserHelper.GetElementText(node, "Source");
                    recipe.PrepTime = XmlParserHelper.GetElementText(node, "PrepTime");
                    recipe.CookTime = XmlParserHelper.GetElementText(node, "CookTime");

                    var ingNodes = node.SelectNodes(".//Ingredient") ?? node.SelectNodes(".//ingredient");
                    if (ingNodes != null)
                    {
                        foreach (XmlNode ing in ingNodes)
                        {
                            var ingText = ing.InnerText.Trim();
                            if (!string.IsNullOrWhiteSpace(ingText))
                            {
                                var qty = XmlParserHelper.GetAttributeValue(ing, "Qty") ?? XmlParserHelper.GetElementText(ing, "Qty");
                                var unit = XmlParserHelper.GetAttributeValue(ing, "Unit") ?? XmlParserHelper.GetElementText(ing, "Unit");
                                var name = XmlParserHelper.GetAttributeValue(ing, "Ingredient") ?? XmlParserHelper.GetElementText(ing, "Ingredient") ?? ingText;
                                recipe.Ingredients.Add(new ParsedIngredient { Quantity = qty, Unit = unit, Name = name });
                            }
                        }
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
                catch (Exception ex) { LoggingHelper.LogRecipeError(null, errors, i, null, "Failed to parse HomeCookin recipe", ex); }
                i++;
            }
            result.Success = true;
        }
        catch (Exception ex) { LoggingHelper.LogBatchError(null, errors, "Failed to parse HomeCookin XML", ex); }
        result.Errors = errors;
        return result;
    }
}
