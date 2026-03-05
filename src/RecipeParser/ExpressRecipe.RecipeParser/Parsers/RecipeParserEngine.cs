using ExpressRecipe.RecipeParser.Models;

namespace ExpressRecipe.RecipeParser.Parsers;

public static class RecipeParserEngine
{
    private static readonly IRecipeParser[] Parsers = CreateParsers();

    private static IRecipeParser[] CreateParsers() =>
    [
        new MealMasterParser(),
        new CookLangParser(),
        new NextcloudCookbookParser(),
        new TandoorParser(),
        new GoogleStructuredDataParser(),
        new JsonRecipeParser(),
        new OpenRecipeFormatParser(),
        new YamlRecipeParser(),
        new MasterCookParser(),
        new RecipeMLParser(),
        new LivingCookbookParser(),
        new MacGourmetParser(),
        new HomeCookinParser(),
        new RxolParser(),
        new RemlParser(),
        new RecipeBookXmlParser(),
        new EatDrinkFeedGoodParser(),
        new ConnoisseurParser(),
        new YumParser(),
        new ChickenPingParser(),
        new BigOvenXmlParser(),
        new CooknParser(),
        new PaprikaParser(),
    ];

    public static ParseResult ParseFile(string filePath, RecipeParseOptions? options = null)
    {
        var errors = new List<ParseError>();
        try
        {
            string ext = GetFileExtension(filePath);
            // Paprika files are binary (gzip or zip) — do not read as text
            if (ext == "paprikarecipe")
                return PaprikaParser.ParseGzip(File.ReadAllBytes(filePath), options);
            if (ext == "paprikarecipes")
                return PaprikaParser.ParseZip(File.ReadAllBytes(filePath), options);
            return ParseText(File.ReadAllText(filePath), ext, options);
        }
        catch (Exception ex)
        {
            errors.Add(new ParseError { Level = "batch", Message = $"Failed to read file: {filePath}", Exception = ex });
            return new ParseResult { Success = false, Errors = errors };
        }
    }

    public static async Task<ParseResult> ParseFileAsync(string filePath, RecipeParseOptions? options = null, CancellationToken ct = default)
    {
        var errors = new List<ParseError>();
        try
        {
            string ext = GetFileExtension(filePath);
            // Paprika files are binary (gzip or zip) — do not read as text
            if (ext == "paprikarecipe")
                return PaprikaParser.ParseGzip(await File.ReadAllBytesAsync(filePath, ct), options);
            if (ext == "paprikarecipes")
                return PaprikaParser.ParseZip(await File.ReadAllBytesAsync(filePath, ct), options);
            return ParseText(await File.ReadAllTextAsync(filePath, ct), ext, options);
        }
        catch (Exception ex)
        {
            errors.Add(new ParseError { Level = "batch", Message = $"Failed to read file: {filePath}", Exception = ex });
            return new ParseResult { Success = false, Errors = errors };
        }
    }

    private static string GetFileExtension(string filePath)
        => Path.GetExtension(filePath).TrimStart('.').ToLowerInvariant();

    public static ParseResult ParseStream(Stream stream, string? hint = null, RecipeParseOptions? options = null)
    {
        var errors = new List<ParseError>();
        try
        {
            using var reader = new StreamReader(stream, leaveOpen: true);
            string text = reader.ReadToEnd();
            return ParseText(text, hint, options);
        }
        catch (Exception ex)
        {
            errors.Add(new ParseError { Level = "batch", Message = "Failed to read stream", Exception = ex });
            return new ParseResult { Success = false, Errors = errors };
        }
    }

    public static async Task<ParseResult> ParseStreamAsync(Stream stream, string? hint = null, RecipeParseOptions? options = null, CancellationToken ct = default)
    {
        var errors = new List<ParseError>();
        try
        {
            using var reader = new StreamReader(stream, leaveOpen: true);
            string text = await reader.ReadToEndAsync(ct);
            return ParseText(text, hint, options);
        }
        catch (Exception ex)
        {
            errors.Add(new ParseError { Level = "batch", Message = "Failed to read stream", Exception = ex });
            return new ParseResult { Success = false, Errors = errors };
        }
    }

    public static ParseResult ParseText(string text, string? hint = null, RecipeParseOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new ParseResult { Success = false, Errors = [new ParseError { Level = "batch", Message = "Empty input" }] };

        if (options?.ForceFormat != null)
        {
            var forced = Parsers.FirstOrDefault(p => p.FormatName.Equals(options.ForceFormat, StringComparison.OrdinalIgnoreCase));
            if (forced != null) return ApplyOptions(forced.Parse(text, options), options);
        }

        string? detectedFormat = FormatDetector.Detect(text, hint);

        if (detectedFormat != null)
        {
            var parser = Parsers.FirstOrDefault(p => p.FormatName.Equals(detectedFormat, StringComparison.OrdinalIgnoreCase));
            if (parser != null)
            {
                var r = parser.Parse(text, options);
                if (r.Success && r.Recipes.Count > 0) return ApplyOptions(r, options);
            }
        }

        foreach (var parser in Parsers)
        {
            try
            {
                if (parser.CanParse(text, hint))
                {
                    var r = parser.Parse(text, options);
                    if (r.Success && r.Recipes.Count > 0) return ApplyOptions(r, options);
                }
            }
            catch { /* continue */ }
        }

        return new ParseResult
        {
            Success = false,
            Errors = [new ParseError { Level = "batch", Message = "No parser could handle the input" }]
        };
    }

    /// <summary>Applies MaxRecipes and StrictMode options to a parse result.</summary>
    private static ParseResult ApplyOptions(ParseResult result, RecipeParseOptions? options)
    {
        if (options == null) return result;

        // Enforce MaxRecipes limit
        int max = options.MaxRecipes;
        if (max > 0 && max < int.MaxValue && result.Recipes.Count > max)
            result.Recipes.RemoveRange(max, result.Recipes.Count - max);

        // StrictMode: treat any errors as a failure
        if (options.StrictMode && result.Errors.Count > 0)
            result.Success = false;

        return result;
    }
}
