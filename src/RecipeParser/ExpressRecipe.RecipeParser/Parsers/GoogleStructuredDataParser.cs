using System.Text.Json;
using System.Text.RegularExpressions;
using ExpressRecipe.RecipeParser.Helpers;
using ExpressRecipe.RecipeParser.Models;

namespace ExpressRecipe.RecipeParser.Parsers;

public sealed class GoogleStructuredDataParser : IRecipeParser
{
    public string FormatName => "GoogleStructuredData";

    public bool CanParse(string text, string? fileExtension = null)
    {
        if (text.Contains("<script type=\"application/ld+json\">", StringComparison.OrdinalIgnoreCase))
            return true;
        if (text.Contains("\"@type\"") && (text.Contains("\"Recipe\"") || text.Contains("schema.org/Recipe")))
            return true;
        return false;
    }

    public ParseResult Parse(string text, RecipeParseOptions? options = null)
    {
        var result = new ParseResult { Format = FormatName };
        var errors = new List<ParseError>();

        try
        {
            var jsonBlocks = ExtractJsonBlocks(text);
            foreach (var json in jsonBlocks)
            {
                try
                {
                    using var doc = JsonDocument.Parse(json);
                    ExtractRecipes(doc.RootElement, result.Recipes, options);
                }
                catch (Exception ex)
                {
                    LoggingHelper.LogBatchError(null, errors, "Failed to parse JSON-LD block", ex);
                }
            }
            result.Success = result.Recipes.Count > 0;
        }
        catch (Exception ex)
        {
            LoggingHelper.LogBatchError(null, errors, "Failed to parse GoogleStructuredData", ex);
        }

        result.Errors = errors;
        return result;
    }

    private static List<string> ExtractJsonBlocks(string text)
    {
        var blocks = new List<string>();

        // Extract from HTML script tags
        var matches = Regex.Matches(text, @"<script\s+type=""application/ld\+json""[^>]*>([\s\S]*?)</script>", RegexOptions.IgnoreCase);
        if (matches.Count > 0)
        {
            foreach (Match m in matches)
                blocks.Add(m.Groups[1].Value.Trim());
            return blocks;
        }

        // Bare JSON
        blocks.Add(text.Trim());
        return blocks;
    }

    private static void ExtractRecipes(JsonElement root, List<ParsedRecipe> recipes, RecipeParseOptions? options)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var elem in root.EnumerateArray())
                ExtractRecipes(elem, recipes, options);
            return;
        }

        if (root.ValueKind != JsonValueKind.Object) return;

        // Handle @graph
        if (root.TryGetProperty("@graph", out var graph) && graph.ValueKind == JsonValueKind.Array)
        {
            foreach (var elem in graph.EnumerateArray())
                ExtractRecipes(elem, recipes, options);
            return;
        }

        // Check @type
        if (root.TryGetProperty("@type", out var typeElem))
        {
            bool isRecipe = false;
            if (typeElem.ValueKind == JsonValueKind.String)
                isRecipe = typeElem.GetString() == "Recipe";
            else if (typeElem.ValueKind == JsonValueKind.Array)
                isRecipe = typeElem.EnumerateArray().Any(t => t.GetString() == "Recipe");

            if (isRecipe)
            {
                recipes.Add(ParseRecipeElement(root, options));
                return;
            }
        }
    }

    private static ParsedRecipe ParseRecipeElement(JsonElement elem, RecipeParseOptions? options)
    {
        var recipe = new ParsedRecipe { Format = "GoogleStructuredData" };

        recipe.Title = GetStr(elem, "name") ?? "";
        recipe.Description = GetStr(elem, "description");
        recipe.Url = GetStr(elem, "url");

        // Author can be string or {"@type":"Person","name":"..."}
        if (elem.TryGetProperty("author", out var author))
        {
            if (author.ValueKind == JsonValueKind.String)
                recipe.Author = author.GetString();
            else if (author.ValueKind == JsonValueKind.Object)
                recipe.Author = GetStr(author, "name");
            else if (author.ValueKind == JsonValueKind.Array)
            {
                var authors = new List<string>();
                foreach (var a in author.EnumerateArray())
                {
                    if (a.ValueKind == JsonValueKind.String) authors.Add(a.GetString() ?? "");
                    else if (a.ValueKind == JsonValueKind.Object) { var n = GetStr(a, "name"); if (n != null) authors.Add(n); }
                }
                recipe.Author = string.Join(", ", authors);
            }
        }

        recipe.Yield = GetStr(elem, "recipeYield");
        recipe.PrepTime = ParseIsoDuration(GetStr(elem, "prepTime"));
        recipe.CookTime = ParseIsoDuration(GetStr(elem, "cookTime"));
        recipe.TotalTime = ParseIsoDuration(GetStr(elem, "totalTime"));
        recipe.Cuisine = GetStr(elem, "recipeCuisine");

        // Category: string or array
        if (elem.TryGetProperty("recipeCategory", out var cat))
        {
            if (cat.ValueKind == JsonValueKind.String) recipe.Category = cat.GetString();
            else if (cat.ValueKind == JsonValueKind.Array)
                recipe.Category = string.Join(", ", cat.EnumerateArray().Select(c => c.GetString() ?? "").Where(c => c != ""));
        }

        // Keywords → Tags
        if (elem.TryGetProperty("keywords", out var kw))
        {
            if (kw.ValueKind == JsonValueKind.String)
                recipe.Tags = (kw.GetString() ?? "").Split(',').Select(t => t.Trim()).Where(t => t != "").ToList();
            else if (kw.ValueKind == JsonValueKind.Array)
                recipe.Tags = kw.EnumerateArray().Select(t => t.GetString() ?? "").Where(t => t != "").ToList();
        }

        // recipeIngredient
        if (elem.TryGetProperty("recipeIngredient", out var ings) && ings.ValueKind == JsonValueKind.Array)
        {
            foreach (var ing in ings.EnumerateArray())
            {
                var line = ing.GetString() ?? "";
                var (qty, unit, name) = TextParserHelper.ParseIngredientLine(line);
                string n = name;
                string p = TextParserHelper.ExtractPreparation(ref n);
                recipe.Ingredients.Add(new ParsedIngredient { Quantity = qty, Unit = unit, Name = n, Preparation = string.IsNullOrEmpty(p) ? null : p });
            }
        }

        // recipeInstructions
        if (elem.TryGetProperty("recipeInstructions", out var insts))
        {
            int step = 1;
            if (insts.ValueKind == JsonValueKind.Array)
            {
                foreach (var inst in insts.EnumerateArray())
                {
                    if (inst.ValueKind == JsonValueKind.String)
                    {
                        var t = inst.GetString();
                        if (!string.IsNullOrWhiteSpace(t))
                            recipe.Instructions.Add(new ParsedInstruction { Step = step++, Text = t });
                    }
                    else if (inst.ValueKind == JsonValueKind.Object)
                    {
                        var instType = GetStr(inst, "@type");
                        if (instType == "HowToSection")
                        {
                            var sectionName = GetStr(inst, "name");
                            if (inst.TryGetProperty("itemListElement", out var items) && items.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var item in items.EnumerateArray())
                                {
                                    var t = GetStr(item, "text", "name");
                                    if (!string.IsNullOrWhiteSpace(t))
                                        recipe.Instructions.Add(new ParsedInstruction { Step = step++, Text = t!, TimerText = sectionName });
                                }
                            }
                        }
                        else
                        {
                            var t = GetStr(inst, "text", "name");
                            if (!string.IsNullOrWhiteSpace(t))
                                recipe.Instructions.Add(new ParsedInstruction { Step = step++, Text = t! });
                        }
                    }
                }
            }
            else if (insts.ValueKind == JsonValueKind.String)
            {
                var t = insts.GetString();
                if (!string.IsNullOrWhiteSpace(t))
                    recipe.Instructions.Add(new ParsedInstruction { Step = 1, Text = t });
            }
        }

        // Nutrition
        if (elem.TryGetProperty("nutrition", out var nut) && nut.ValueKind == JsonValueKind.Object)
        {
            recipe.Nutrition = new ParsedNutrition
            {
                Calories = GetStr(nut, "calories"),
                Fat = GetStr(nut, "fatContent"),
                Carbohydrates = GetStr(nut, "carbohydrateContent"),
                Protein = GetStr(nut, "proteinContent"),
                Fiber = GetStr(nut, "fiberContent"),
                Sugar = GetStr(nut, "sugarContent"),
                Sodium = GetStr(nut, "sodiumContent"),
                Cholesterol = GetStr(nut, "cholesterolContent")
            };
        }

        return recipe;
    }

    internal static string? ParseIsoDuration(string? iso)
    {
        if (string.IsNullOrEmpty(iso)) return null;
        // If already human-readable, pass through
        if (!iso.StartsWith("PT", StringComparison.OrdinalIgnoreCase) && !iso.StartsWith("P", StringComparison.OrdinalIgnoreCase))
            return iso;

        var match = Regex.Match(iso, @"P(?:(\d+)D)?T?(?:(\d+)H)?(?:(\d+)M)?(?:(\d+)S)?", RegexOptions.IgnoreCase);
        if (!match.Success) return iso;

        int days = match.Groups[1].Success ? int.Parse(match.Groups[1].Value) : 0;
        int hours = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 0;
        int minutes = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0;
        int seconds = match.Groups[4].Success ? int.Parse(match.Groups[4].Value) : 0;

        hours += days * 24;
        minutes += seconds / 60;

        if (hours > 0 && minutes > 0) return $"{hours} hr {minutes} min";
        if (hours > 0) return $"{hours} hr";
        if (minutes > 0) return $"{minutes} min";
        return iso;
    }

    private static string? GetStr(JsonElement elem, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (elem.TryGetProperty(key, out var val))
            {
                if (val.ValueKind == JsonValueKind.String) return val.GetString();
                if (val.ValueKind == JsonValueKind.Number) return val.GetRawText();
            }
        }
        return null;
    }
}
