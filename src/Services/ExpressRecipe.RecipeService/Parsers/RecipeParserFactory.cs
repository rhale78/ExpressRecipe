namespace ExpressRecipe.RecipeService.Parsers;

/// <summary>
/// Factory for creating recipe parsers based on source type or parser name
/// </summary>
public class RecipeParserFactory
{
    private readonly Dictionary<string, IRecipeParser> _parsersByName;
    private readonly List<IRecipeParser> _allParsers;

    public RecipeParserFactory()
    {
        // Initialize all available parsers
        _allParsers = new List<IRecipeParser>
        {
            new MealMasterParser(),
            new MasterCookParser(),
            new JsonRecipeParser(),
            new RecipeKeeperParser(),
            new PaprikaParser(),
            new PlainTextParser(),
            new WebScraperParser(),
            new AllRecipesParser(),
            new FoodNetworkParser(),
            new TastyParser(),
            new SeriousEatsParser(),
            new NYTCookingParser()
        };

        _parsersByName = _allParsers.ToDictionary(p => p.ParserName, p => p);
    }

    /// <summary>
    /// Get parser by class name
    /// </summary>
    public IRecipeParser? GetParserByName(string parserClassName)
    {
        return _parsersByName.GetValueOrDefault(parserClassName);
    }

    /// <summary>
    /// Get parser by source type
    /// </summary>
    public IRecipeParser? GetParserBySourceType(string sourceType)
    {
        return _allParsers.FirstOrDefault(p =>
            p.SourceType.Equals(sourceType, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Auto-detect the best parser for the given content and context
    /// </summary>
    public IRecipeParser? DetectParser(string content, ParserContext context)
    {
        // Try parsers in order of specificity
        foreach (var parser in _allParsers)
        {
            if (parser.CanParse(content, context))
            {
                return parser;
            }
        }

        // Default to plain text parser as fallback
        return new PlainTextParser();
    }

    /// <summary>
    /// Get all available parsers
    /// </summary>
    public List<IRecipeParser> GetAllParsers()
    {
        return new List<IRecipeParser>(_allParsers);
    }

    /// <summary>
    /// Get parser names (for listing import sources)
    /// </summary>
    public List<string> GetParserNames()
    {
        return _allParsers.Select(p => p.ParserName).ToList();
    }
}
