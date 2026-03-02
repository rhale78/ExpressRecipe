using ExpressRecipe.Client.Shared.Models.AI;
using System.Text.Json;
using System.Text.RegularExpressions;

// Allow the test project to access internal members
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("ExpressRecipe.AIService.Tests")]

namespace ExpressRecipe.AIService.Services;

/// <summary>
/// Pure-logic helper that extracts recipe data from raw text using regex.
/// Used as the AI fallback and is exposed as <c>internal</c> so the test project can exercise it directly.
/// </summary>
internal static class RecipeTextParser
{
    // Matches "2 cups flour", "1 1/2 tbsp sugar", "0.5 oz salt", etc.
    internal static readonly Regex IngredientLinePattern =
        new(@"^(\d+\s+\d+/\d+|\d+[./]\d+|\d+)\s+([a-zA-Z]+\.?)\s+(.+)$", RegexOptions.Compiled);

    private static readonly HashSet<string> KnownUnits = new(StringComparer.OrdinalIgnoreCase)
    {
        "cup","cups","tsp","tbsp","oz","lb","lbs","g","kg","ml","l",
        "teaspoon","teaspoons","tablespoon","tablespoons","ounce","ounces",
        "pound","pounds","gram","grams","clove","cloves","large","medium",
        "small","pinch","dash","can","cans","package","packages",
        "stick","sticks","bunch","bunches","slice","slices","piece","pieces",
        "sprig","sprigs","head","heads","sheet","sheets"
    };

    // ──────────────────────────────────────────────────────────────────────────
    // Public-to-internal entry point
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Extracts recipe data from raw text using regex heuristics.
    /// Confidence is capped at 0.85 (lower than AI) to signal it is a fallback.
    /// </summary>
    internal static ExtractedRecipeDto ExtractRecipeLocally(string text)
    {
        var result = new ExtractedRecipeDto();
        if (string.IsNullOrWhiteSpace(text)) return result;

        var lines = text.Split('\n').Select(l => l.Trim()).ToArray();
        var nonEmpty = lines.Where(l => l.Length > 0).ToArray();
        if (nonEmpty.Length == 0) return result;

        // Title: first non-empty line
        result.Title = nonEmpty[0];

        // Servings
        var servMatch = Regex.Match(text, @"(?:serves|makes|yields|servings?)\s*:?\s*(\d+)", RegexOptions.IgnoreCase);
        if (servMatch.Success && int.TryParse(servMatch.Groups[1].Value, out var serv))
            result.Servings = serv;
        if (result.Servings == 0) result.Servings = 4;

        // Prep time
        var prepMatch = Regex.Match(text, @"prep(?:\s*time)?\s*:?\s*(\d+)\s*(?:min(?:utes?)?|hr?s?)", RegexOptions.IgnoreCase);
        if (prepMatch.Success && int.TryParse(prepMatch.Groups[1].Value, out var prepVal))
            result.PrepTimeMinutes = prepVal;
        else
        {
            var altPrep = Regex.Match(text, @"(\d+)\s*min(?:utes?)?\s+prep", RegexOptions.IgnoreCase);
            if (altPrep.Success && int.TryParse(altPrep.Groups[1].Value, out var ap))
                result.PrepTimeMinutes = ap;
        }

        // Cook time
        var cookMatch = Regex.Match(text, @"(?:cook|bake)(?:\s*time)?\s*:?\s*(\d+)\s*(?:min(?:utes?)?|hr?s?)", RegexOptions.IgnoreCase);
        if (cookMatch.Success && int.TryParse(cookMatch.Groups[1].Value, out var cookVal))
            result.CookTimeMinutes = cookVal;
        else
        {
            var altCook = Regex.Match(text, @"(\d+)\s*min(?:utes?)?\s+cook", RegexOptions.IgnoreCase);
            if (altCook.Success && int.TryParse(altCook.Groups[1].Value, out var ac))
                result.CookTimeMinutes = ac;
        }

        // Difficulty
        if (Regex.IsMatch(text, @"\b(?:easy|simple|beginner)\b", RegexOptions.IgnoreCase))
            result.Difficulty = "Easy";
        else if (Regex.IsMatch(text, @"\b(?:advanced|difficult|hard|complex|challenging)\b", RegexOptions.IgnoreCase))
            result.Difficulty = "Hard";
        else
            result.Difficulty = "Medium";

        result.Cuisine = DetectCuisine(text);
        result.Category = DetectCategory(text);

        // Dietary
        var dietPatterns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Vegetarian"] = @"\bvegetarian\b",
            ["Vegan"]      = @"\bvegan\b",
            ["Gluten-Free"]= @"\bgluten[- ]free\b",
            ["Dairy-Free"] = @"\bdairy[- ]free\b",
            ["Keto"]       = @"\bketo\b",
            ["Paleo"]      = @"\bpaleo\b",
        };
        foreach (var (diet, pattern) in dietPatterns)
            if (Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase))
                result.DietaryInfo.Add(diet);

        if (!string.IsNullOrEmpty(result.Cuisine)) result.Tags.Add(result.Cuisine.ToLower());
        foreach (var d in result.DietaryInfo) result.Tags.Add(d.ToLower());

        // Parse sections
        var ingHeader    = new Regex(@"^ingredients?:?\s*$", RegexOptions.IgnoreCase);
        var stepHeader   = new Regex(@"^(?:instructions?|directions?|steps?|method):?\s*$", RegexOptions.IgnoreCase);
        var numberedLine = new Regex(@"^\d+[\.)\-]\s+(.+)$");
        var section = "none";

        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (ingHeader.IsMatch(line))  { section = "ingredients"; continue; }
            if (stepHeader.IsMatch(line)) { section = "steps";       continue; }

            if (section == "ingredients")
            {
                var ing = TryParseIngredientLine(line);
                if (ing != null) result.Ingredients.Add(ing);
            }
            else if (section == "steps")
            {
                var nm = numberedLine.Match(line);
                result.Instructions.Add(nm.Success ? nm.Groups[1].Value.Trim() : line);
            }
            else
            {
                var ing = TryParseIngredientLine(line);
                if (ing != null)
                    result.Ingredients.Add(ing);
                else
                {
                    var nm = numberedLine.Match(line);
                    if (nm.Success) result.Instructions.Add(nm.Groups[1].Value.Trim());
                }
            }
        }

        // Confidence — proportional to extracted data, capped at 0.85
        double confidence = 0.1;
        if (!string.IsNullOrWhiteSpace(result.Title)) confidence += 0.1;
        if (result.Ingredients.Count > 0)  confidence += 0.3;
        if (result.Instructions.Count > 0) confidence += 0.3;
        if (result.PrepTimeMinutes > 0 || result.CookTimeMinutes > 0) confidence += 0.1;
        if (result.Servings > 1) confidence += 0.1;
        result.ConfidenceScore = Math.Min(confidence, 0.85);

        return result;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // JSON extraction
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Extracts the first complete JSON object from a string by tracking brace depth.
    /// More reliable than a greedy regex when the AI includes extra text around the JSON.
    /// </summary>
    internal static string? ExtractFirstJsonObject(string text)
    {
        int start = text.IndexOf('{');
        if (start < 0) return null;

        int depth = 0;
        bool inString = false;
        bool escape = false;
        for (int i = start; i < text.Length; i++)
        {
            char c = text[i];
            if (escape)          { escape = false; continue; }
            if (c == '\\')       { escape = true;  continue; }
            if (c == '"')        { inString = !inString; continue; }
            if (inString)        continue;
            if (c == '{')        depth++;
            else if (c == '}') { depth--; if (depth == 0) return text[start..(i + 1)]; }
        }
        return null; // unterminated
    }

    /// <summary>
    /// Attempts to deserialise the first JSON object in <paramref name="response"/> into
    /// an <see cref="ExtractedRecipeDto"/>. Returns <c>null</c> on any failure.
    /// </summary>
    internal static ExtractedRecipeDto? TryParseAiExtractionResponse(string response)
    {
        try
        {
            var jsonText = ExtractFirstJsonObject(response);
            if (jsonText == null) return null;

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            };

            return JsonSerializer.Deserialize<ExtractedRecipeDto>(jsonText, options);
        }
        catch
        {
            return null;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Ingredient line parsing
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Tries to parse a single text line into a structured ingredient.
    /// Returns <c>null</c> when the line does not look like an ingredient.
    /// </summary>
    internal static ExtractedIngredientDto? TryParseIngredientLine(string line)
    {
        var match = IngredientLinePattern.Match(line);
        if (!match.Success) return null;
        var unitRaw = match.Groups[2].Value.TrimEnd('.');
        if (!KnownUnits.Contains(unitRaw)) return null;

        var name = match.Groups[3].Value.Trim();
        string? notes = null;
        var commaIdx = name.IndexOf(',');
        if (commaIdx > 0) { notes = name[(commaIdx + 1)..].Trim(); name = name[..commaIdx].Trim(); }

        return new ExtractedIngredientDto
        {
            Name     = name,
            Quantity = match.Groups[1].Value.Trim(),
            Unit     = unitRaw,
            Notes    = notes
        };
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Cuisine / category detection
    // ──────────────────────────────────────────────────────────────────────────

    internal static string? DetectCuisine(string text)
    {
        var cuisines = new[] { "Italian", "Mexican", "Chinese", "Indian", "French", "American", "Japanese", "Mediterranean", "Thai", "Greek" };
        foreach (var c in cuisines)
            if (Regex.IsMatch(text, $@"\b{c}\b", RegexOptions.IgnoreCase))
                return c;
        return null;
    }

    internal static string? DetectCategory(string text)
    {
        var cats = new (string Name, string[] Keywords)[]
        {
            ("Dessert",   new[] { "dessert", "cake", "cookie", "brownie", "pie", "tart", "pudding" }),
            ("Soup",      new[] { "soup", "stew", "chowder", "bisque" }),
            ("Salad",     new[] { "salad" }),
            ("Bread",     new[] { "bread", "roll", "bun", "loaf", "muffin" }),
            ("Breakfast", new[] { "breakfast", "brunch", "pancake", "waffle" }),
            ("Beverage",  new[] { "smoothie", "juice", "cocktail", "drink", "beverage" }),
            ("Appetizer", new[] { "appetizer", "starter", "dip" }),
            ("Snack",     new[] { "snack", "chips" }),
        };
        foreach (var (name, kws) in cats)
            if (kws.Any(k => Regex.IsMatch(text, $@"\b{Regex.Escape(k)}\b", RegexOptions.IgnoreCase)))
                return name;
        return null;
    }
}
