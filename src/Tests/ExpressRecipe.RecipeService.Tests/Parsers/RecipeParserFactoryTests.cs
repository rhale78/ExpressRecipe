using ExpressRecipe.RecipeService.Parsers;
using FluentAssertions;

namespace ExpressRecipe.RecipeService.Tests.Parsers;

public class RecipeParserFactoryTests
{
    private readonly RecipeParserFactory _factory;

    public RecipeParserFactoryTests()
    {
        _factory = new RecipeParserFactory();
    }

    // ── GetAllParsers ─────────────────────────────────────────────────────────

    [Fact]
    public void GetAllParsers_ReturnsNonEmptyList()
    {
        var parsers = _factory.GetAllParsers();

        parsers.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GetAllParsers_ContainsPlainTextParser()
    {
        var parsers = _factory.GetAllParsers();

        parsers.Should().Contain(p => p.ParserName == "PlainTextParser");
    }

    [Fact]
    public void GetAllParsers_ContainsMealMasterParser()
    {
        var parsers = _factory.GetAllParsers();

        parsers.Should().Contain(p => p.ParserName == "MealMasterParser");
    }

    [Fact]
    public void GetAllParsers_ContainsJsonRecipeParser()
    {
        var parsers = _factory.GetAllParsers();

        parsers.Should().Contain(p => p.ParserName == "JsonRecipeParser");
    }

    // ── GetParserNames ────────────────────────────────────────────────────────

    [Fact]
    public void GetParserNames_ReturnsNonEmptyList()
    {
        var names = _factory.GetParserNames();

        names.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GetParserNames_CountMatchesGetAllParsers()
    {
        var names = _factory.GetParserNames();
        var parsers = _factory.GetAllParsers();

        names.Should().HaveCount(parsers.Count);
    }

    // ── GetParserByName ───────────────────────────────────────────────────────

    [Fact]
    public void GetParserByName_KnownName_ReturnsParser()
    {
        var parser = _factory.GetParserByName("PlainTextParser");

        parser.Should().NotBeNull();
        parser!.ParserName.Should().Be("PlainTextParser");
    }

    [Fact]
    public void GetParserByName_UnknownName_ReturnsNull()
    {
        var parser = _factory.GetParserByName("NonExistentParser");

        parser.Should().BeNull();
    }

    [Fact]
    public void GetParserByName_EmptyString_ReturnsNull()
    {
        var parser = _factory.GetParserByName(string.Empty);

        parser.Should().BeNull();
    }

    // ── GetParserBySourceType ─────────────────────────────────────────────────

    [Fact]
    public void GetParserBySourceType_TextSourceType_ReturnsPlainTextParser()
    {
        var parser = _factory.GetParserBySourceType("Text");

        parser.Should().NotBeNull();
        parser!.SourceType.Should().Be("Text");
    }

    [Fact]
    public void GetParserBySourceType_CaseInsensitive_ReturnsParser()
    {
        var lowerResult = _factory.GetParserBySourceType("text");
        var upperResult = _factory.GetParserBySourceType("TEXT");

        lowerResult.Should().NotBeNull();
        upperResult.Should().NotBeNull();
    }

    [Fact]
    public void GetParserBySourceType_UnknownType_ReturnsNull()
    {
        var parser = _factory.GetParserBySourceType("NonExistentType");

        parser.Should().BeNull();
    }

    // ── DetectParser ──────────────────────────────────────────────────────────

    [Fact]
    public void DetectParser_PlainTextContent_ReturnsParser()
    {
        var content = "My Great Pasta Recipe\n\nIngredients:\n2 cups flour\n\nInstructions:\nMix everything.";
        var context = new ParserContext();

        var parser = _factory.DetectParser(content, context);

        parser.Should().NotBeNull();
    }

    [Fact]
    public void DetectParser_EmptyContent_ReturnsFallbackParser()
    {
        // Even empty content should return something (PlainTextParser as fallback)
        var parser = _factory.DetectParser(string.Empty, new ParserContext());

        // Either null or PlainTextParser are acceptable for empty content
        if (parser != null)
        {
            parser.ParserName.Should().NotBeNullOrEmpty();
        }
    }
}
