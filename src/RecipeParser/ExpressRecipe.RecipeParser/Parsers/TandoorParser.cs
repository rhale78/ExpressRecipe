using System.Text.Json;
using ExpressRecipe.RecipeParser.Helpers;
using ExpressRecipe.RecipeParser.Models;

namespace ExpressRecipe.RecipeParser.Parsers;

/// <summary>
/// Parses Tandoor Recipes JSON export format.
/// Tandoor uses a specific schema with fields like recipe_ingredient, steps, etc.
/// </summary>
public sealed class TandoorParser : IRecipeParser
{
    public string FormatName => "Tandoor";

    public bool CanParse(string text, string? fileExtension = null)
    {
        if (!text.TrimStart().StartsWith("{") && !text.TrimStart().StartsWith("[")) return false;
        return text.Contains("recipe_ingredient") || text.Contains("\"steps\"") ||
               (text.Contains("\"name\"") && text.Contains("\"working_time\""));
    }

    public ParseResult Parse(string text, RecipeParseOptions? options = null)
    {
        var result = new ParseResult { Format = FormatName };
        var errors = new List<ParseError>();

        try
        {
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
            {
                int i = 0;
                foreach (var elem in root.EnumerateArray())
                {
                    try { result.Recipes.Add(ParseTandoorElement(elem, options)); }
                    catch (Exception ex) { LoggingHelper.LogRecipeError(null, errors, i, null, "Failed to parse Tandoor recipe", ex); }
                    i++;
                }
            }
            else if (root.ValueKind == JsonValueKind.Object)
            {
                result.Recipes.Add(ParseTandoorElement(root, options));
            }

            result.Success = true;
        }
        catch (Exception ex) { LoggingHelper.LogBatchError(null, errors, "Failed to parse Tandoor JSON", ex); }

        result.Errors = errors;
        return result;
    }

    private static ParsedRecipe ParseTandoorElement(JsonElement elem, RecipeParseOptions? options)
    {
        var recipe = new ParsedRecipe { Format = "Tandoor" };

        recipe.Title = GetStr(elem, "name") ?? "";
        recipe.Description = GetStr(elem, "description");

        // Source URL
        recipe.Url = GetStr(elem, "source_url");
        recipe.Source = recipe.Url;

        // Servings
        recipe.Yield = GetStr(elem, "servings", "serving_size") ??
                       GetNum(elem, "servings")?.ToString();

        // Times: Tandoor uses working_time (prep), waiting_time (cook) in minutes
        if (elem.TryGetProperty("working_time", out var wt) && wt.ValueKind == JsonValueKind.Number)
            recipe.PrepTime = $"{wt.GetInt32()} min";
        if (elem.TryGetProperty("waiting_time", out var wait) && wait.ValueKind == JsonValueKind.Number)
            recipe.CookTime = $"{wait.GetInt32()} min";

        // Categories/Keywords as tags
        if (elem.TryGetProperty("keywords", out var kws) && kws.ValueKind == JsonValueKind.Array)
        {
            recipe.Tags = kws.EnumerateArray()
                .Select(k => k.ValueKind == JsonValueKind.Object ? GetStr(k, "name", "label") : k.GetString())
                .Where(t => t != null && t != "")
                .Select(t => t!)
                .ToList();
        }

        // Steps contain instruction text and ingredients
        if (elem.TryGetProperty("steps", out var stepsArr) && stepsArr.ValueKind == JsonValueKind.Array)
        {
            int stepNum = 1;
            foreach (var step in stepsArr.EnumerateArray())
            {
                // Instructions from step text
                var instText = GetStr(step, "instruction", "text");
                if (!string.IsNullOrWhiteSpace(instText))
                    recipe.Instructions.Add(new ParsedInstruction { Step = stepNum++, Text = instText });

                // Ingredients from step
                if (step.TryGetProperty("ingredients", out var stepIngs) && stepIngs.ValueKind == JsonValueKind.Array)
                {
                    foreach (var ing in stepIngs.EnumerateArray())
                    {
                        var name = GetNestedStr(ing, "food", "name") ?? GetStr(ing, "name") ?? "";
                        var qty = GetNum(ing, "amount")?.ToString() ?? GetStr(ing, "amount");
                        var unit = GetNestedStr(ing, "unit", "name") ?? GetStr(ing, "unit");
                        var note = GetStr(ing, "note");
                        if (!string.IsNullOrWhiteSpace(name))
                            recipe.Ingredients.Add(new ParsedIngredient
                            {
                                Name = name,
                                Quantity = qty,
                                Unit = unit,
                                Preparation = note
                            });
                    }
                }
            }
        }

        // Flat ingredients (some export formats)
        if (elem.TryGetProperty("recipe_ingredient", out var recipeIngs) && recipeIngs.ValueKind == JsonValueKind.Array)
        {
            foreach (var ing in recipeIngs.EnumerateArray())
            {
                var name = GetNestedStr(ing, "food", "name") ?? GetStr(ing, "name") ?? "";
                var qty = GetNum(ing, "amount")?.ToString() ?? GetStr(ing, "amount");
                var unit = GetNestedStr(ing, "unit", "name") ?? GetStr(ing, "unit");
                var note = GetStr(ing, "note");
                if (!string.IsNullOrWhiteSpace(name))
                    recipe.Ingredients.Add(new ParsedIngredient
                    {
                        Name = name,
                        Quantity = qty,
                        Unit = unit,
                        Preparation = note
                    });
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

    private static string? GetNestedStr(JsonElement elem, string objectKey, string fieldKey)
    {
        if (elem.TryGetProperty(objectKey, out var obj) && obj.ValueKind == JsonValueKind.Object)
            return GetStr(obj, fieldKey);
        return null;
    }

    private static double? GetNum(JsonElement elem, string key)
    {
        if (elem.TryGetProperty(key, out var val) && val.ValueKind == JsonValueKind.Number)
            return val.GetDouble();
        return null;
    }
}
