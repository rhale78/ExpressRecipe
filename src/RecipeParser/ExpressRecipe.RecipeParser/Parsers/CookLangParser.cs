using System.Text.RegularExpressions;
using ExpressRecipe.RecipeParser.Helpers;
using ExpressRecipe.RecipeParser.Models;

namespace ExpressRecipe.RecipeParser.Parsers;

public sealed class CookLangParser : IRecipeParser
{
    public string FormatName => "CookLang";

    private static readonly Regex IngredientRegex = new(
        @"@(?<name>[^@#~{}\n]+?)\{(?<qty>[^%}]*)(?:%(?<unit>[^}]*))?\}|@(?<name2>[^\s@#~{}\n]+)",
        RegexOptions.Compiled);

    private static readonly Regex CookwareRegex = new(
        @"#(?<name>[^@#~{}\n]+?)\{\}|#(?<name2>[^\s@#~{}\n]+)",
        RegexOptions.Compiled);

    private static readonly Regex TimerRegex = new(
        @"~\{(?<dur>[^%}]*)%(?<unit>[^}]*)\}|~\{(?<dur2>[^}]*)\}",
        RegexOptions.Compiled);

    public bool CanParse(string text, string? fileExtension = null)
    {
        if (fileExtension?.EndsWith("cook", StringComparison.OrdinalIgnoreCase) == true) return true;
        return text.Contains("@") && !text.TrimStart().StartsWith("<");
    }

    public ParseResult Parse(string text, RecipeParseOptions? options = null)
    {
        var result = new ParseResult { Format = FormatName };
        var errors = new List<ParseError>();

        try
        {
            var recipe = new ParsedRecipe { Format = FormatName };
            if (options?.IncludeRawText == true) recipe.RawText = text;

            text = Regex.Replace(text, @"\[-.*?-\]", "", RegexOptions.Singleline);

            var lines = text.Split('\n');
            var instructionSteps = new List<ParsedInstruction>();
            int stepNum = 1;

            foreach (var rawLine in lines)
            {
                var line = rawLine.TrimEnd('\r').Trim();

                if (string.IsNullOrWhiteSpace(line)) continue;
                if (line.StartsWith("--")) continue;

                if (line.StartsWith(">>"))
                {
                    var metaLine = line[2..].Trim();
                    int colonIdx = metaLine.IndexOf(':');
                    if (colonIdx > 0)
                    {
                        var key = metaLine[..colonIdx].Trim().ToLowerInvariant();
                        var value = metaLine[(colonIdx + 1)..].Trim();
                        ApplyMetadata(recipe, key, value);
                    }
                    continue;
                }

                var instruction = new ParsedInstruction { Step = stepNum++ };
                var cleanLine = line;

                int commentIdx = cleanLine.IndexOf("--");
                if (commentIdx > 0) cleanLine = cleanLine[..commentIdx].Trim();

                foreach (Match m in CookwareRegex.Matches(cleanLine))
                {
                    string cwName = (m.Groups["name"].Success ? m.Groups["name"].Value : m.Groups["name2"].Value).Trim();
                    if (!string.IsNullOrEmpty(cwName))
                        instruction.Cookware.Add(cwName);
                }

                foreach (Match m in TimerRegex.Matches(cleanLine))
                {
                    string dur = (m.Groups["dur"].Success ? m.Groups["dur"].Value : m.Groups["dur2"].Value).Trim();
                    string unit = m.Groups["unit"].Success ? m.Groups["unit"].Value.Trim() : "";
                    instruction.TimerText = string.IsNullOrEmpty(unit) ? dur : $"{dur} {unit}";
                }

                foreach (Match m in IngredientRegex.Matches(cleanLine))
                {
                    string ingName = (m.Groups["name"].Success ? m.Groups["name"].Value : m.Groups["name2"].Value).Trim();
                    string qty = m.Groups["qty"].Success ? m.Groups["qty"].Value.Trim() : "";
                    string unit = m.Groups["unit"].Success ? m.Groups["unit"].Value.Trim() : "";
                    if (!string.IsNullOrEmpty(ingName))
                    {
                        recipe.Ingredients.Add(new ParsedIngredient
                        {
                            Name = ingName,
                            Quantity = string.IsNullOrEmpty(qty) ? null : qty,
                            Unit = string.IsNullOrEmpty(unit) ? null : unit
                        });
                    }
                }

                string displayText = IngredientRegex.Replace(cleanLine, m =>
                {
                    string name = m.Groups["name"].Success ? m.Groups["name"].Value : m.Groups["name2"].Value;
                    return name.Trim();
                });
                displayText = CookwareRegex.Replace(displayText, m =>
                {
                    string name = m.Groups["name"].Success ? m.Groups["name"].Value : m.Groups["name2"].Value;
                    return name.Trim();
                });
                displayText = TimerRegex.Replace(displayText, m =>
                {
                    string dur = m.Groups["dur"].Success ? m.Groups["dur"].Value : m.Groups["dur2"].Value;
                    string unit2 = m.Groups["unit"].Success ? m.Groups["unit"].Value : "";
                    return string.IsNullOrEmpty(unit2) ? dur : $"{dur} {unit2}";
                });
                instruction.Text = displayText.Trim();
                if (!string.IsNullOrWhiteSpace(instruction.Text))
                    instructionSteps.Add(instruction);
            }

            recipe.Instructions = instructionSteps;
            if (string.IsNullOrEmpty(recipe.Title)) recipe.Title = "Untitled Recipe";

            result.Recipes.Add(recipe);
            result.Success = true;
        }
        catch (Exception ex)
        {
            LoggingHelper.LogBatchError(null, errors, "Failed to parse CookLang recipe", ex);
        }

        result.Errors = errors;
        return result;
    }

    private static void ApplyMetadata(ParsedRecipe recipe, string key, string value)
    {
        switch (key)
        {
            case "title": recipe.Title = value; break;
            case "description": recipe.Description = value; break;
            case "source": recipe.Source = value; break;
            case "author": recipe.Author = value; break;
            case "url": case "link": recipe.Url = value; break;
            case "yield": case "servings": recipe.Yield = value; break;
            case "prep time": case "preptime": recipe.PrepTime = value; break;
            case "cook time": case "cooktime": recipe.CookTime = value; break;
            case "total time": case "totaltime": recipe.TotalTime = value; break;
            case "category": case "categories": recipe.Category = value; break;
            case "cuisine": recipe.Cuisine = value; break;
            case "tags": recipe.Tags = value.Split(',').Select(t => t.Trim()).ToList(); break;
        }
    }
}
