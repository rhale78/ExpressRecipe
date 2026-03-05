using System.Xml;
using ExpressRecipe.RecipeParser.Helpers;
using ExpressRecipe.RecipeParser.Models;

namespace ExpressRecipe.RecipeParser.Parsers;

public sealed class BigOvenXmlParser : IRecipeParser
{
    public string FormatName => "BigOvenXml";

    public bool CanParse(string text, string? fileExtension = null)
    {
        return text.Contains("<BigOven") || (text.Contains("<Recipes>") && text.Contains("<Recipe>"));
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
                    recipe.Description = XmlParserHelper.GetElementText(node, "Description") ?? XmlParserHelper.GetElementText(node, "Notes");
                    recipe.Author = XmlParserHelper.GetElementText(node, "CreatedBy") ?? XmlParserHelper.GetElementText(node, "Author");
                    recipe.Yield = XmlParserHelper.GetElementText(node, "Yield") ?? XmlParserHelper.GetElementText(node, "Servings");
                    recipe.Category = XmlParserHelper.GetElementText(node, "Category") ?? XmlParserHelper.GetElementText(node, "Cuisine");
                    recipe.Cuisine = XmlParserHelper.GetElementText(node, "Cuisine");
                    recipe.Source = XmlParserHelper.GetElementText(node, "SourceURL") ?? XmlParserHelper.GetElementText(node, "Source");
                    recipe.PrepTime = XmlParserHelper.GetElementText(node, "PrepMinutes");
                    recipe.CookTime = XmlParserHelper.GetElementText(node, "CookMinutes");
                    recipe.TotalTime = XmlParserHelper.GetElementText(node, "TotalMinutes");

                    var ingNodes = node.SelectNodes(".//Ingredient") ?? node.SelectNodes(".//ingredient");
                    if (ingNodes != null)
                    {
                        foreach (XmlNode ing in ingNodes)
                        {
                            var qty = XmlParserHelper.GetElementText(ing, "Quantity") ?? XmlParserHelper.GetAttributeValue(ing, "qty");
                            var unit = XmlParserHelper.GetElementText(ing, "Unit") ?? XmlParserHelper.GetAttributeValue(ing, "unit");
                            var name = XmlParserHelper.GetElementText(ing, "Name") ?? XmlParserHelper.GetElementText(ing, "Ingredient") ?? ing.InnerText.Trim();
                            var prep = XmlParserHelper.GetElementText(ing, "PreparationNotes") ?? XmlParserHelper.GetElementText(ing, "Preparation");
                            recipe.Ingredients.Add(new ParsedIngredient { Quantity = qty, Unit = unit, Name = name, Preparation = prep });
                        }
                    }

                    int step = 1;
                    var instNodes = node.SelectNodes(".//Instruction") ?? node.SelectNodes(".//Direction");
                    if (instNodes != null && instNodes.Count > 0)
                    {
                        foreach (XmlNode inst in instNodes)
                        {
                            var t = inst.InnerText.Trim();
                            if (!string.IsNullOrWhiteSpace(t)) recipe.Instructions.Add(new ParsedInstruction { Step = step++, Text = t });
                        }
                    }
                    else
                    {
                        var inst = XmlParserHelper.GetElementText(node, "Instructions") ?? XmlParserHelper.GetElementText(node, "Directions");
                        if (!string.IsNullOrWhiteSpace(inst))
                            recipe.Instructions.Add(new ParsedInstruction { Step = 1, Text = inst });
                    }
                    result.Recipes.Add(recipe);
                }
                catch (Exception ex) { LoggingHelper.LogRecipeError(null, errors, i, null, "Failed to parse BigOven recipe", ex); }
                i++;
            }
            result.Success = true;
        }
        catch (Exception ex) { LoggingHelper.LogBatchError(null, errors, "Failed to parse BigOven XML", ex); }
        result.Errors = errors;
        return result;
    }
}
