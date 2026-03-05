using System.IO.Compression;
using System.Text;
using System.Text.Json;
using ExpressRecipe.RecipeParser.Helpers;
using ExpressRecipe.RecipeParser.Models;

namespace ExpressRecipe.RecipeParser.Parsers;

/// <summary>
/// Parses Paprika recipe format.
/// - .paprikarecipe files are gzip-compressed JSON
/// - .paprikarecipes files are ZIP archives containing multiple .paprikarecipe files
/// - Plain JSON text with Paprika schema also supported
/// </summary>
public sealed class PaprikaParser : IRecipeParser
{
    public string FormatName => "Paprika";

    public bool CanParse(string text, string? fileExtension = null)
    {
        if (fileExtension == null) return false;
        var ext = fileExtension.TrimStart('.').ToLowerInvariant();
        return ext == "paprika" || ext == "paprikarecipe" || ext == "paprikarecipes";
    }

    public ParseResult Parse(string text, RecipeParseOptions? options = null)
    {
        var result = new ParseResult { Format = FormatName };
        var errors = new List<ParseError>();

        try
        {
            // Try to parse as plain JSON first (Paprika schema)
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;

            var recipes = root.ValueKind == JsonValueKind.Array ? root : default;
            if (root.ValueKind == JsonValueKind.Object)
            {
                // Single recipe
                result.Recipes.Add(ParsePaprikaElement(root));
                result.Success = true;
                result.Errors = errors;
                return result;
            }

            if (recipes.ValueKind == JsonValueKind.Array)
            {
                int i = 0;
                foreach (var elem in recipes.EnumerateArray())
                {
                    try { result.Recipes.Add(ParsePaprikaElement(elem)); }
                    catch (Exception ex) { LoggingHelper.LogRecipeError(null, errors, i, null, "Failed to parse Paprika element", ex); }
                    i++;
                }
                result.Success = true;
            }
        }
        catch (Exception ex)
        {
            LoggingHelper.LogBatchError(null, errors, "Failed to parse Paprika JSON", ex);
        }

        result.Errors = errors;
        return result;
    }

    /// <summary>Parse a Paprika recipe from gzip-compressed bytes.</summary>
    public static ParseResult ParseGzip(byte[] gzipBytes, RecipeParseOptions? options = null)
    {
        var result = new ParseResult { Format = "Paprika" };
        var errors = new List<ParseError>();
        try
        {
            using var ms = new MemoryStream(gzipBytes);
            using var gz = new GZipStream(ms, CompressionMode.Decompress);
            using var sr = new StreamReader(gz, Encoding.UTF8);
            var json = sr.ReadToEnd();
            return ParseFromJson(json, options);
        }
        catch (Exception ex)
        {
            LoggingHelper.LogBatchError(null, errors, "Failed to decompress Paprika gzip data", ex);
            result.Errors = errors;
            return result;
        }
    }

    /// <summary>Parse a .paprikarecipes ZIP archive from bytes.</summary>
    public static ParseResult ParseZip(byte[] zipBytes, RecipeParseOptions? options = null)
    {
        var result = new ParseResult { Format = "Paprika" };
        var errors = new List<ParseError>();
        try
        {
            using var ms = new MemoryStream(zipBytes);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            int i = 0;
            foreach (var entry in zip.Entries)
            {
                try
                {
                    using var entryStream = entry.Open();
                    using var gz = new GZipStream(entryStream, CompressionMode.Decompress);
                    using var sr = new StreamReader(gz, Encoding.UTF8);
                    var json = sr.ReadToEnd();
                    var r = ParseFromJson(json, options);
                    result.Recipes.AddRange(r.Recipes);
                    result.Errors.AddRange(r.Errors);
                }
                catch (Exception ex) { LoggingHelper.LogRecipeError(null, errors, i, entry.Name, "Failed to parse Paprika zip entry", ex); }
                i++;
            }
            result.Success = result.Recipes.Count > 0;
        }
        catch (Exception ex)
        {
            LoggingHelper.LogBatchError(null, errors, "Failed to read Paprika zip archive", ex);
        }
        result.Errors.AddRange(errors);
        return result;
    }

    private static ParseResult ParseFromJson(string json, RecipeParseOptions? options)
    {
        var result = new ParseResult { Format = "Paprika" };
        var errors = new List<ParseError>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            result.Recipes.Add(ParsePaprikaElement(doc.RootElement));
            result.Success = true;
        }
        catch (Exception ex) { LoggingHelper.LogBatchError(null, errors, "Failed to parse Paprika JSON", ex); }
        result.Errors = errors;
        return result;
    }

    private static ParsedRecipe ParsePaprikaElement(JsonElement elem)
    {
        var recipe = new ParsedRecipe { Format = "Paprika" };
        recipe.Title = GetStr(elem, "name", "title") ?? "";
        recipe.Description = GetStr(elem, "description", "notes");
        recipe.Source = GetStr(elem, "source", "source_url");
        recipe.Url = GetStr(elem, "source_url");
        recipe.Author = GetStr(elem, "author");
        recipe.Yield = GetStr(elem, "servings", "yield");
        recipe.PrepTime = GetStr(elem, "prep_time");
        recipe.CookTime = GetStr(elem, "cook_time");
        recipe.TotalTime = GetStr(elem, "total_time");
        recipe.Category = GetStr(elem, "categories", "category");
        recipe.Cuisine = GetStr(elem, "cuisine");

        // Ingredients in Paprika are a single string with newlines
        var ingredientText = GetStr(elem, "ingredients");
        if (!string.IsNullOrWhiteSpace(ingredientText))
        {
            foreach (var line in ingredientText.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed)) continue;
                var (qty, unit, name) = TextParserHelper.ParseIngredientLine(trimmed);
                string n = name;
                string p = TextParserHelper.ExtractPreparation(ref n);
                recipe.Ingredients.Add(new ParsedIngredient { Quantity = qty, Unit = unit, Name = n, Preparation = string.IsNullOrEmpty(p) ? null : p });
            }
        }

        // Directions are also a single string
        var directions = GetStr(elem, "directions", "instructions");
        if (!string.IsNullOrWhiteSpace(directions))
        {
            var parts = directions.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                var t = parts[i].Trim();
                if (!string.IsNullOrWhiteSpace(t))
                    recipe.Instructions.Add(new ParsedInstruction { Step = i + 1, Text = t });
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
