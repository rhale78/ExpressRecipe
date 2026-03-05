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
        new JsonRecipeParser(),
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
        new OpenRecipeFormatParser(),
        new PaprikaParser(),
    ];

    public static ParseResult ParseFile(string filePath, RecipeParseOptions? options = null)
    {
        var errors = new List<ParseError>();
        try
        {
            string text = File.ReadAllText(filePath);
            string ext = Path.GetExtension(filePath).TrimStart('.');
            return ParseText(text, ext, options);
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
            string text = await File.ReadAllTextAsync(filePath, ct);
            string ext = Path.GetExtension(filePath).TrimStart('.');
            return ParseText(text, ext, options);
        }
        catch (Exception ex)
        {
            errors.Add(new ParseError { Level = "batch", Message = $"Failed to read file: {filePath}", Exception = ex });
            return new ParseResult { Success = false, Errors = errors };
        }
    }

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
            if (forced != null) return forced.Parse(text, options);
        }

        string? detectedFormat = FormatDetector.Detect(text, hint);

        if (detectedFormat != null)
        {
            var parser = Parsers.FirstOrDefault(p => p.FormatName.Equals(detectedFormat, StringComparison.OrdinalIgnoreCase));
            if (parser != null)
            {
                var r = parser.Parse(text, options);
                if (r.Success && r.Recipes.Count > 0) return r;
            }
        }

        foreach (var parser in Parsers)
        {
            try
            {
                if (parser.CanParse(text, hint))
                {
                    var r = parser.Parse(text, options);
                    if (r.Success && r.Recipes.Count > 0) return r;
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
}
