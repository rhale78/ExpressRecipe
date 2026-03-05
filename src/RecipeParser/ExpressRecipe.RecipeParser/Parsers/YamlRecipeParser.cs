using ExpressRecipe.RecipeParser.Helpers;
using ExpressRecipe.RecipeParser.Models;
using YamlDotNet.RepresentationModel;

namespace ExpressRecipe.RecipeParser.Parsers;

public sealed class YamlRecipeParser : IRecipeParser
{
    public string FormatName => "Yaml";

    public bool CanParse(string text, string? fileExtension = null)
    {
        if (fileExtension?.EndsWith("yaml", StringComparison.OrdinalIgnoreCase) == true ||
            fileExtension?.EndsWith("yml", StringComparison.OrdinalIgnoreCase) == true) return true;
        return text.Contains("ingredients:") || text.Contains("title:") || text.Contains("name:");
    }

    public ParseResult Parse(string text, RecipeParseOptions? options = null)
    {
        var result = new ParseResult { Format = FormatName };
        var errors = new List<ParseError>();

        try
        {
            var yaml = new YamlStream();
            yaml.Load(new StringReader(text));

            if (yaml.Documents.Count == 0)
            {
                result.Success = true;
                return result;
            }

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
                    else if (root is YamlSequenceNode seq)
                    {
                        int i = 0;
                        foreach (var node in seq)
                        {
                            try
                            {
                                if (node is YamlMappingNode m)
                                    result.Recipes.Add(ParseMapping(m, options));
                            }
                            catch (Exception ex)
                            {
                                LoggingHelper.LogRecipeError(null, errors, i, null, "Failed to parse YAML recipe node", ex);
                            }
                            i++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    LoggingHelper.LogRecipeError(null, errors, docIdx, null, "Failed to parse YAML document", ex);
                }
            }

            result.Success = true;
        }
        catch (Exception ex)
        {
            LoggingHelper.LogBatchError(null, errors, "Failed to parse YAML", ex);
        }

        result.Errors = errors;
        return result;
    }

    private static ParsedRecipe ParseMapping(YamlMappingNode mapping, RecipeParseOptions? options)
    {
        var recipe = new ParsedRecipe { Format = "Yaml" };

        recipe.Title = GetScalar(mapping, "title", "name") ?? "";
        recipe.Description = GetScalar(mapping, "description", "notes");
        recipe.Source = GetScalar(mapping, "source");
        recipe.Author = GetScalar(mapping, "author");
        recipe.Url = GetScalar(mapping, "url", "link");
        recipe.Yield = GetScalar(mapping, "yield", "servings", "serves");
        recipe.PrepTime = GetScalar(mapping, "prep_time", "prepTime", "prep time");
        recipe.CookTime = GetScalar(mapping, "cook_time", "cookTime", "cook time");
        recipe.TotalTime = GetScalar(mapping, "total_time", "totalTime");
        recipe.Category = GetScalar(mapping, "category", "categories");
        recipe.Cuisine = GetScalar(mapping, "cuisine");

        if (mapping.Children.TryGetValue(new YamlScalarNode("tags"), out var tagsNode) && tagsNode is YamlSequenceNode tagSeq)
            recipe.Tags = tagSeq.OfType<YamlScalarNode>().Select(n => n.Value ?? "").Where(v => v != "").ToList();

        if (mapping.Children.TryGetValue(new YamlScalarNode("ingredients"), out var ingsNode))
        {
            if (ingsNode is YamlSequenceNode ingsSeq)
            {
                foreach (var ingNode in ingsSeq)
                {
                    if (ingNode is YamlScalarNode scalar)
                    {
                        var line = scalar.Value ?? "";
                        var (qty, unit, name) = TextParserHelper.ParseIngredientLine(line);
                        string n = name;
                        string p = TextParserHelper.ExtractPreparation(ref n);
                        recipe.Ingredients.Add(new ParsedIngredient
                        {
                            Quantity = qty,
                            Unit = unit,
                            Name = n,
                            Preparation = string.IsNullOrEmpty(p) ? null : p
                        });
                    }
                    else if (ingNode is YamlMappingNode ingMap)
                    {
                        recipe.Ingredients.Add(new ParsedIngredient
                        {
                            Quantity = GetScalar(ingMap, "quantity", "amount", "qty"),
                            Unit = GetScalar(ingMap, "unit", "measure"),
                            Name = GetScalar(ingMap, "name", "ingredient") ?? "",
                            Preparation = GetScalar(ingMap, "preparation", "prep", "notes")
                        });
                    }
                }
            }
        }

        YamlNode? instNode = null;
        mapping.Children.TryGetValue(new YamlScalarNode("instructions"), out instNode);
        if (instNode == null) mapping.Children.TryGetValue(new YamlScalarNode("directions"), out instNode);
        if (instNode == null) mapping.Children.TryGetValue(new YamlScalarNode("steps"), out instNode);

        if (instNode is YamlSequenceNode instSeq)
        {
            int step = 1;
            foreach (var inst in instSeq)
            {
                string? stepText = null;
                if (inst is YamlScalarNode s) stepText = s.Value;
                else if (inst is YamlMappingNode m) stepText = GetScalar(m, "text", "description", "step");
                if (!string.IsNullOrWhiteSpace(stepText))
                    recipe.Instructions.Add(new ParsedInstruction { Step = step++, Text = stepText });
            }
        }
        else if (instNode is YamlScalarNode instScalar && !string.IsNullOrWhiteSpace(instScalar.Value))
        {
            recipe.Instructions.Add(new ParsedInstruction { Step = 1, Text = instScalar.Value! });
        }

        return recipe;
    }

    private static string? GetScalar(YamlMappingNode mapping, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (mapping.Children.TryGetValue(new YamlScalarNode(key), out var val) && val is YamlScalarNode scalar)
                return scalar.Value;
        }
        return null;
    }
}
