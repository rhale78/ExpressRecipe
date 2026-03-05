using System.Text.Json;
using System.Xml;
using ExpressRecipe.RecipeParser.Helpers;
using ExpressRecipe.RecipeParser.Models;

namespace ExpressRecipe.RecipeParser.Parsers;

public sealed class ChickenPingParser : IRecipeParser
{
    public string FormatName => "ChickenPing";

    public bool CanParse(string text, string? fileExtension = null)
    {
        return text.Contains("<ChickenPing") || text.Contains("\"ChickenPing\"");
    }

    public ParseResult Parse(string text, RecipeParseOptions? options = null)
    {
        var result = new ParseResult { Format = FormatName };
        var errors = new List<ParseError>();
        try
        {
            var trimmed = text.TrimStart();
            if (trimmed.StartsWith("{") || trimmed.StartsWith("["))
                return ParseJson(text, options);

            var doc = XmlParserHelper.LoadXml(text);
            int i = 0;
            var recipeNodes = doc.SelectNodes("//Recipe") ?? doc.SelectNodes("//recipe");
            if (recipeNodes == null) { result.Success = true; result.Errors = errors; return result; }

            foreach (XmlNode node in recipeNodes)
            {
                try
                {
                    var recipe = new ParsedRecipe { Format = FormatName };
                    recipe.Title = XmlParserHelper.GetElementText(node, "Name") ?? XmlParserHelper.GetAttributeValue(node, "name") ?? "";
                    recipe.Description = XmlParserHelper.GetElementText(node, "Description");
                    recipe.Author = XmlParserHelper.GetElementText(node, "Author");
                    recipe.Yield = XmlParserHelper.GetElementText(node, "Servings");
                    recipe.Category = XmlParserHelper.GetElementText(node, "Category");
                    recipe.Source = XmlParserHelper.GetElementText(node, "Source");

                    var ingNodes = node.SelectNodes(".//Ingredient") ?? node.SelectNodes(".//ingredient");
                    if (ingNodes != null)
                    {
                        foreach (XmlNode ing in ingNodes)
                            recipe.Ingredients.Add(new ParsedIngredient
                            {
                                Quantity = XmlParserHelper.GetAttributeValue(ing, "Qty") ?? XmlParserHelper.GetElementText(ing, "Qty"),
                                Unit = XmlParserHelper.GetAttributeValue(ing, "Unit") ?? XmlParserHelper.GetElementText(ing, "Unit"),
                                Name = XmlParserHelper.GetAttributeValue(ing, "Name") ?? XmlParserHelper.GetElementText(ing, "Name") ?? ing.InnerText.Trim()
                            });
                    }

                    int step = 1;
                    var dirNodes = node.SelectNodes(".//Step") ?? node.SelectNodes(".//Direction");
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
                catch (Exception ex) { LoggingHelper.LogRecipeError(null, errors, i, null, "Failed to parse ChickenPing recipe", ex); }
                i++;
            }
            result.Success = true;
        }
        catch (Exception ex) { LoggingHelper.LogBatchError(null, errors, "Failed to parse ChickenPing", ex); }
        result.Errors = errors;
        return result;
    }

    private static ParseResult ParseJson(string text, RecipeParseOptions? options)
    {
        var result = new ParseResult { Format = "ChickenPing" };
        var errors = new List<ParseError>();
        try
        {
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            var recipes = root.ValueKind == JsonValueKind.Array ? root : default;

            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("recipes", out var arr))
                recipes = arr;

            if (recipes.ValueKind == JsonValueKind.Array)
            {
                int i = 0;
                foreach (var elem in recipes.EnumerateArray())
                {
                    try
                    {
                        result.Recipes.Add(ParseJsonElement(elem));
                    }
                    catch (Exception ex) { LoggingHelper.LogRecipeError(null, errors, i, null, "Failed to parse ChickenPing JSON element", ex); }
                    i++;
                }
            }
            else if (root.ValueKind == JsonValueKind.Object)
            {
                result.Recipes.Add(ParseJsonElement(root));
            }
            result.Success = true;
        }
        catch (Exception ex) { LoggingHelper.LogBatchError(null, errors, "Failed to parse ChickenPing JSON", ex); }
        result.Errors = errors;
        return result;
    }

    private static ParsedRecipe ParseJsonElement(JsonElement elem)
    {
        var recipe = new ParsedRecipe { Format = "ChickenPing" };
        recipe.Title = GetStr(elem, "name", "title") ?? "";
        recipe.Description = GetStr(elem, "description", "notes");
        recipe.Author = GetStr(elem, "author");
        recipe.Yield = GetStr(elem, "servings", "yield");
        recipe.Category = GetStr(elem, "category");
        recipe.Source = GetStr(elem, "source");

        if (elem.TryGetProperty("ingredients", out var ings) && ings.ValueKind == JsonValueKind.Array)
        {
            foreach (var ing in ings.EnumerateArray())
            {
                if (ing.ValueKind == JsonValueKind.String)
                {
                    var line = ing.GetString() ?? "";
                    var (qty, unit, name) = TextParserHelper.ParseIngredientLine(line);
                    recipe.Ingredients.Add(new ParsedIngredient { Quantity = qty, Unit = unit, Name = name });
                }
                else
                {
                    recipe.Ingredients.Add(new ParsedIngredient
                    {
                        Quantity = GetStr(ing, "qty", "quantity"),
                        Unit = GetStr(ing, "unit"),
                        Name = GetStr(ing, "name", "ingredient") ?? ""
                    });
                }
            }
        }

        if (elem.TryGetProperty("directions", out var dirs) && dirs.ValueKind == JsonValueKind.Array)
        {
            int step = 1;
            foreach (var dir in dirs.EnumerateArray())
            {
                var t = dir.ValueKind == JsonValueKind.String ? dir.GetString() : GetStr(dir, "text", "step");
                if (!string.IsNullOrWhiteSpace(t))
                    recipe.Instructions.Add(new ParsedInstruction { Step = step++, Text = t! });
            }
        }
        return recipe;
    }

    private static string? GetStr(JsonElement elem, params string[] keys)
    {
        foreach (var key in keys)
            if (elem.TryGetProperty(key, out var val) && val.ValueKind == JsonValueKind.String)
                return val.GetString();
        return null;
    }
}
