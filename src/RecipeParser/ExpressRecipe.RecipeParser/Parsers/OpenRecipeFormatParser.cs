using ExpressRecipe.RecipeParser.Helpers;
using ExpressRecipe.RecipeParser.Models;
using YamlDotNet.RepresentationModel;

namespace ExpressRecipe.RecipeParser.Parsers;

/// <summary>
/// Parses OpenRecipeFormat YAML files - a community standard for recipe exchange.
/// See: https://openrecipeformat.readthedocs.io
/// </summary>
public sealed class OpenRecipeFormatParser : IRecipeParser
{
    public string FormatName => "OpenRecipeFormat";

    public bool CanParse(string text, string? fileExtension = null)
    {
        if (fileExtension?.EndsWith("yaml", StringComparison.OrdinalIgnoreCase) == true ||
            fileExtension?.EndsWith("yml", StringComparison.OrdinalIgnoreCase) == true)
        {
            return text.Contains("ingredients:") || text.Contains("steps:");
        }
        return false;
    }

    public ParseResult Parse(string text, RecipeParseOptions? options = null)
    {
        var result = new ParseResult { Format = FormatName };
        var errors = new List<ParseError>();
        try
        {
            var yaml = new YamlStream();
            yaml.Load(new StringReader(text));

            for (int docIdx = 0; docIdx < yaml.Documents.Count; docIdx++)
            {
                try
                {
                    var root = yaml.Documents[docIdx].RootNode;
                    if (root is YamlMappingNode mapping)
                    {
                        var recipe = ParseMapping(mapping, options);
                        result.Recipes.Add(recipe);
                    }
                }
                catch (Exception ex)
                {
                    LoggingHelper.LogRecipeError(null, errors, docIdx, null, "Failed to parse OpenRecipeFormat document", ex);
                }
            }
            result.Success = true;
        }
        catch (Exception ex) { LoggingHelper.LogBatchError(null, errors, "Failed to parse OpenRecipeFormat YAML", ex); }
        result.Errors = errors;
        return result;
    }

    private static ParsedRecipe ParseMapping(YamlMappingNode mapping, RecipeParseOptions? options)
    {
        var recipe = new ParsedRecipe { Format = "OpenRecipeFormat" };

        recipe.Title = GetScalar(mapping, "name", "title") ?? "";
        recipe.Description = GetScalar(mapping, "description", "notes");
        recipe.Source = GetScalar(mapping, "source_url", "source");
        recipe.Author = GetScalar(mapping, "author");
        recipe.Url = GetScalar(mapping, "source_url", "url");
        recipe.Yield = GetScalar(mapping, "servings", "yield");
        recipe.PrepTime = GetScalar(mapping, "prep_time");
        recipe.CookTime = GetScalar(mapping, "cook_time");
        recipe.TotalTime = GetScalar(mapping, "total_time");
        recipe.Category = GetScalar(mapping, "categories", "category");
        recipe.Cuisine = GetScalar(mapping, "cuisine");

        if (mapping.Children.TryGetValue(new YamlScalarNode("tags"), out var tagsNode) && tagsNode is YamlSequenceNode tagSeq)
            recipe.Tags = tagSeq.OfType<YamlScalarNode>().Select(n => n.Value ?? "").Where(v => v != "").ToList();

        // Ingredients: list of strings or maps with {name, amount, unit}
        YamlNode? ingsNode = null;
        mapping.Children.TryGetValue(new YamlScalarNode("ingredients"), out ingsNode);
        if (ingsNode is YamlSequenceNode ingsSeq)
        {
            foreach (var ingNode in ingsSeq)
            {
                if (ingNode is YamlScalarNode scalar)
                {
                    var line = scalar.Value ?? "";
                    var (qty, unit, name) = TextParserHelper.ParseIngredientLine(line);
                    string n = name; string p = TextParserHelper.ExtractPreparation(ref n);
                    recipe.Ingredients.Add(new ParsedIngredient { Quantity = qty, Unit = unit, Name = n, Preparation = string.IsNullOrEmpty(p) ? null : p });
                }
                else if (ingNode is YamlMappingNode ingMap)
                {
                    recipe.Ingredients.Add(new ParsedIngredient
                    {
                        Quantity = GetScalar(ingMap, "amount", "quantity", "qty"),
                        Unit = GetScalar(ingMap, "unit", "units"),
                        Name = GetScalar(ingMap, "name", "ingredient") ?? "",
                        Preparation = GetScalar(ingMap, "preparation", "notes")
                    });
                }
            }
        }

        // Steps/Instructions
        YamlNode? stepsNode = null;
        mapping.Children.TryGetValue(new YamlScalarNode("steps"), out stepsNode);
        if (stepsNode == null) mapping.Children.TryGetValue(new YamlScalarNode("instructions"), out stepsNode);
        if (stepsNode is YamlSequenceNode stepsSeq)
        {
            int step = 1;
            foreach (var s in stepsSeq)
            {
                string? t = null;
                if (s is YamlScalarNode ss) t = ss.Value;
                else if (s is YamlMappingNode sm) t = GetScalar(sm, "step", "text", "description");
                if (!string.IsNullOrWhiteSpace(t))
                    recipe.Instructions.Add(new ParsedInstruction { Step = step++, Text = t });
            }
        }

        return recipe;
    }

    private static string? GetScalar(YamlMappingNode mapping, params string[] keys)
    {
        foreach (var key in keys)
            if (mapping.Children.TryGetValue(new YamlScalarNode(key), out var val) && val is YamlScalarNode s)
                return s.Value;
        return null;
    }
}
