using ExpressRecipe.RecipeParser.Helpers;
using ExpressRecipe.RecipeParser.Models;
using YamlDotNet.RepresentationModel;

namespace ExpressRecipe.RecipeParser.Parsers;

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
        recipe.Description = GetScalar(mapping, "description");
        recipe.Source = GetScalar(mapping, "source_url", "source");
        recipe.Author = GetScalar(mapping, "author");
        recipe.Url = GetScalar(mapping, "source_url", "url");
        recipe.PrepTime = GetScalar(mapping, "prep_time");
        recipe.CookTime = GetScalar(mapping, "cook_time");
        recipe.TotalTime = GetScalar(mapping, "total_time");
        recipe.Cuisine = GetScalar(mapping, "cuisine");

        // notes_from_file + notes
        var notesFromFile = GetScalar(mapping, "notes_from_file");
        var notes = GetScalar(mapping, "notes");
        if (notesFromFile != null && notes != null)
            recipe.Description = (recipe.Description != null ? recipe.Description + "\n" : "") + notes + "\n" + notesFromFile;
        else if (notesFromFile != null)
            recipe.Description = (recipe.Description != null ? recipe.Description + "\n" : "") + notesFromFile;
        else if (notes != null && recipe.Description == null)
            recipe.Description = notes;

        // yields: scalar or list [{amount, unit}]
        if (mapping.Children.TryGetValue(new YamlScalarNode("yields"), out var yieldsNode))
        {
            if (yieldsNode is YamlScalarNode ys)
                recipe.Yield = ys.Value;
            else if (yieldsNode is YamlSequenceNode ySeq)
            {
                var parts = new List<string>();
                foreach (var y in ySeq)
                {
                    if (y is YamlMappingNode ym)
                    {
                        var amount = GetScalar(ym, "amount");
                        var unit = GetScalar(ym, "unit");
                        if (amount != null) parts.Add(unit != null ? $"{amount} {unit}" : amount);
                    }
                    else if (y is YamlScalarNode ys2) parts.Add(ys2.Value ?? "");
                }
                recipe.Yield = string.Join(", ", parts.Where(p => p != ""));
            }
        }

        if (recipe.Yield == null)
            recipe.Yield = GetScalar(mapping, "servings", "yield");

        // oven_temp: scalar or {amount, unit}
        if (mapping.Children.TryGetValue(new YamlScalarNode("oven_temp"), out var ovenNode))
        {
            string? ovenStr = null;
            if (ovenNode is YamlScalarNode os) ovenStr = os.Value;
            else if (ovenNode is YamlMappingNode om)
            {
                var amount = GetScalar(om, "amount");
                var unit = GetScalar(om, "unit");
                ovenStr = amount != null ? (unit != null ? $"{amount} {unit}" : amount) : null;
            }
            if (ovenStr != null)
            {
                recipe.Tags.Add($"oven_temp:{ovenStr}");
            }
        }

        // categories: scalar or list
        if (mapping.Children.TryGetValue(new YamlScalarNode("categories"), out var catsNode))
        {
            if (catsNode is YamlScalarNode cs) recipe.Category = cs.Value;
            else if (catsNode is YamlSequenceNode cSeq)
                recipe.Category = string.Join(", ", cSeq.OfType<YamlScalarNode>().Select(n => n.Value ?? "").Where(v => v != ""));
        }
        else
            recipe.Category = GetScalar(mapping, "category");

        // tags
        if (mapping.Children.TryGetValue(new YamlScalarNode("tags"), out var tagsNode) && tagsNode is YamlSequenceNode tagSeq)
            recipe.Tags.AddRange(tagSeq.OfType<YamlScalarNode>().Select(n => n.Value ?? "").Where(v => v != ""));

        // Ingredients: list of strings, maps, OR sections [{name, ingredients}]
        if (mapping.Children.TryGetValue(new YamlScalarNode("ingredients"), out var ingsNode))
        {
            ParseIngredients(ingsNode, recipe);
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

    private static void ParseIngredients(YamlNode ingsNode, ParsedRecipe recipe)
    {
        if (ingsNode is not YamlSequenceNode ingsSeq) return;

        foreach (var ingNode in ingsSeq)
        {
            if (ingNode is YamlScalarNode scalar)
            {
                var line = scalar.Value ?? "";
                var (qty, unit, name) = TextParserHelper.ParseIngredientLine(line);
                string n = name;
                string p = TextParserHelper.ExtractPreparation(ref n);
                recipe.Ingredients.Add(new ParsedIngredient { Quantity = qty, Unit = unit, Name = n, Preparation = string.IsNullOrEmpty(p) ? null : p });
            }
            else if (ingNode is YamlMappingNode ingMap)
            {
                // Check if this is a section: {name, ingredients}
                if (ingMap.Children.ContainsKey(new YamlScalarNode("ingredients")))
                {
                    var sectionName = GetScalar(ingMap, "name") ?? "";
                    if (ingMap.Children.TryGetValue(new YamlScalarNode("ingredients"), out var sectionIngs))
                        ParseIngredientsWithGroup(sectionIngs, recipe, sectionName);
                }
                else
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
    }

    private static void ParseIngredientsWithGroup(YamlNode node, ParsedRecipe recipe, string groupName)
    {
        if (node is not YamlSequenceNode seq) return;
        foreach (var ingNode in seq)
        {
            ParsedIngredient ing;
            if (ingNode is YamlScalarNode scalar)
            {
                var line = scalar.Value ?? "";
                var (qty, unit, name) = TextParserHelper.ParseIngredientLine(line);
                string n = name;
                string p = TextParserHelper.ExtractPreparation(ref n);
                ing = new ParsedIngredient { Quantity = qty, Unit = unit, Name = n, Preparation = string.IsNullOrEmpty(p) ? null : p };
            }
            else if (ingNode is YamlMappingNode ingMap)
            {
                ing = new ParsedIngredient
                {
                    Quantity = GetScalar(ingMap, "amount", "quantity", "qty"),
                    Unit = GetScalar(ingMap, "unit", "units"),
                    Name = GetScalar(ingMap, "name", "ingredient") ?? "",
                    Preparation = GetScalar(ingMap, "preparation", "notes")
                };
            }
            else continue;
            ing.GroupHeading = groupName;
            recipe.Ingredients.Add(ing);
        }
    }

    private static string? GetScalar(YamlMappingNode mapping, params string[] keys)
    {
        foreach (var key in keys)
            if (mapping.Children.TryGetValue(new YamlScalarNode(key), out var val) && val is YamlScalarNode s)
                return s.Value;
        return null;
    }
}
