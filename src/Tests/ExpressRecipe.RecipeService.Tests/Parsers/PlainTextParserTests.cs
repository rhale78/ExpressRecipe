using ExpressRecipe.RecipeService.Parsers;
using FluentAssertions;

namespace ExpressRecipe.RecipeService.Tests.Parsers;

public class PlainTextParserTests
{
    private readonly PlainTextParser _parser;

    public PlainTextParserTests()
    {
        _parser = new PlainTextParser();
    }

    // ── Parser Metadata ───────────────────────────────────────────────────────

    [Fact]
    public void ParserName_IsPlainTextParser()
    {
        _parser.ParserName.Should().Be("PlainTextParser");
    }

    [Fact]
    public void SourceType_IsText()
    {
        _parser.SourceType.Should().Be("Text");
    }

    // ── CanParse ──────────────────────────────────────────────────────────────

    [Fact]
    public void CanParse_NonEmptyContent_ReturnsTrue()
    {
        var canParse = _parser.CanParse("Some recipe text", new ParserContext());

        canParse.Should().BeTrue();
    }

    [Fact]
    public void CanParse_EmptyContent_ReturnsFalse()
    {
        var canParse = _parser.CanParse(string.Empty, new ParserContext());

        canParse.Should().BeFalse();
    }

    [Fact]
    public void CanParse_WhitespaceOnly_ReturnsFalse()
    {
        var canParse = _parser.CanParse("   \n\t  ", new ParserContext());

        canParse.Should().BeFalse();
    }

    // ── ParseAsync – Basic Parsing ────────────────────────────────────────────

    [Fact]
    public async Task ParseAsync_SingleLine_ReturnsOneRecipeWithName()
    {
        var content = "Chocolate Cake";
        var context = new ParserContext();

        var results = await _parser.ParseAsync(content, context);

        results.Should().HaveCount(1);
        results[0].Name.Should().Be("Chocolate Cake");
    }

    [Fact]
    public async Task ParseAsync_EmptyContent_ReturnsEmptyList()
    {
        var content = string.Empty;
        var context = new ParserContext();

        var results = await _parser.ParseAsync(content, context);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task ParseAsync_WhitespaceOnly_ReturnsEmptyList()
    {
        var content = "   \n\n\t  ";
        var context = new ParserContext();

        var results = await _parser.ParseAsync(content, context);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task ParseAsync_FirstNonEmptyLineBecomesRecipeName()
    {
        var content = "Lemon Tart\n\nA delicious dessert.";
        var context = new ParserContext();

        var results = await _parser.ParseAsync(content, context);

        results.Should().HaveCount(1);
        results[0].Name.Should().Be("Lemon Tart");
    }

    [Fact]
    public async Task ParseAsync_WithFileNameContext_UsesFirstLineAsName()
    {
        var content = "Banana Bread\nMoist and delicious.";
        var context = new ParserContext { FileName = "myrecipe.txt" };

        var results = await _parser.ParseAsync(content, context);

        // First line should be the recipe name (overrides FileName default)
        results[0].Name.Should().Be("Banana Bread");
    }

    // ── ParseAsync – Ingredients ──────────────────────────────────────────────

    [Fact]
    public async Task ParseAsync_IngredientsSection_ParsesIngredients()
    {
        var content = """
            Pasta Carbonara
            Ingredients:
            2 cups pasta
            3 eggs
            1 cup parmesan cheese
            Instructions:
            Cook the pasta.
            """;
        var context = new ParserContext();

        var results = await _parser.ParseAsync(content, context);

        results.Should().HaveCount(1);
        results[0].Ingredients.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ParseAsync_IngredientWithQuantity_ParsesQuantity()
    {
        var content = """
            My Recipe
            Ingredients:
            2 cups flour
            """;
        var context = new ParserContext();

        var results = await _parser.ParseAsync(content, context);

        var ingredient = results[0].Ingredients.FirstOrDefault(i =>
            i.IngredientName.Contains("flour", StringComparison.OrdinalIgnoreCase));
        ingredient.Should().NotBeNull();
        ingredient!.Quantity.Should().Be(2m);
    }

    // ── ParseAsync – Instructions ─────────────────────────────────────────────

    [Fact]
    public async Task ParseAsync_InstructionsSection_ParsesInstructions()
    {
        var content = """
            Scrambled Eggs
            Ingredients:
            3 eggs
            Instructions:
            Crack the eggs into a bowl.
            Whisk until combined.
            Cook in a pan over medium heat.
            """;
        var context = new ParserContext();

        var results = await _parser.ParseAsync(content, context);

        results[0].Instructions.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ParseAsync_NumberedSteps_LinesStartingWithDigitAreParsed()
    {
        // Lines starting with a digit are treated as ingredients (starts with quantity).
        // This is PlainTextParser heuristic behavior.
        var content = """
            Simple Soup
            1. Chop the vegetables.
            2. Boil water in a pot.
            """;
        var context = new ParserContext();

        var results = await _parser.ParseAsync(content, context);

        // Parser should produce one recipe with at least some parsed content
        results.Should().HaveCount(1);
        (results[0].Ingredients.Count + results[0].Instructions.Count).Should().BeGreaterThan(0);
    }

    // ── ParseAsync – Servings / Time ──────────────────────────────────────────

    [Fact]
    public async Task ParseAsync_ServingsLine_ParsesServings()
    {
        // Regex requires number before keyword: (\d+)\s*(?:servings?|serves)
        // Line must not start with a digit (would be caught as ingredient first)
        var content = """
            Pancakes
            Makes 4 servings
            Ingredients:
            1 cup flour
            """;
        var context = new ParserContext();

        var results = await _parser.ParseAsync(content, context);

        results[0].Servings.Should().Be(4);
    }

    [Fact]
    public async Task ParseAsync_PrepTimeLine_ParsesPrepTime()
    {
        // ParseTime handles "min" but not "minutes" (plural with -es)
        var content = """
            Quick Omelette
            Preparation 5 min
            Ingredients:
            2 eggs
            """;
        var context = new ParserContext();

        var results = await _parser.ParseAsync(content, context);

        results[0].PrepTimeMinutes.Should().Be(5);
    }

    [Fact]
    public async Task ParseAsync_CookTimeLine_ParsesCookTime()
    {
        // Must not start with "cook" (would be caught as instruction first)
        var content = """
            Roast Chicken
            Total cook time 90 min
            Ingredients:
            1 whole chicken
            """;
        var context = new ParserContext();

        var results = await _parser.ParseAsync(content, context);

        results[0].CookTimeMinutes.Should().Be(90);
    }
}
