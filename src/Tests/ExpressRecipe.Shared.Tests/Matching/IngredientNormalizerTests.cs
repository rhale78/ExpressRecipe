using ExpressRecipe.Shared.Matching;
using FluentAssertions;

namespace ExpressRecipe.Shared.Tests.Matching;

public class IngredientNormalizerTests
{
    // ── Normalize ──────────────────────────────────────────────────────────────

    [Fact]
    public void Normalize_EmptyString_ReturnsEmpty()
        => IngredientNormalizer.Normalize(string.Empty).Should().Be(string.Empty);

    [Fact]
    public void Normalize_WhitespaceOnly_ReturnsEmpty()
        => IngredientNormalizer.Normalize("   ").Should().Be(string.Empty);

    [Fact]
    public void Normalize_PlainIngredient_ReturnsLowerCase()
        => IngredientNormalizer.Normalize("Butter").Should().Be("butter");

    [Fact]
    public void Normalize_StripsSizeModifier()
        => IngredientNormalizer.Normalize("large onion").Should().Be("onion");

    [Fact]
    public void Normalize_StripsStateModifier()
        => IngredientNormalizer.Normalize("frozen peas").Should().Be("peas");

    [Fact]
    public void Normalize_StripsQualityModifier()
        => IngredientNormalizer.Normalize("organic carrots").Should().Be("carrot");

    [Fact]
    public void Normalize_AppliesNaiveSingular_Independently()
        // "carrots" → 7 chars (> 4), ends in 's' not 'ss' → "carrot"
        => IngredientNormalizer.Normalize("carrots").Should().Be("carrot");

    [Fact]
    public void Normalize_StripsPrepWord()
        => IngredientNormalizer.Normalize("chopped onions").Should().Be("onion");

    [Fact]
    public void Normalize_StripsMeasurementPrefix()
        => IngredientNormalizer.Normalize("2 cups flour").Should().Be("flour");

    [Fact]
    public void Normalize_StripsMeasurementPrefixWithFraction()
        => IngredientNormalizer.Normalize("1/2 tsp salt").Should().Be("salt");

    [Fact]
    public void Normalize_StripsParenthesisContent()
        => IngredientNormalizer.Normalize("butter (softened)").Should().Be("butter");

    [Fact]
    public void Normalize_TruncatesAtComma()
        => IngredientNormalizer.Normalize("chicken, boneless").Should().Be("chicken");

    [Fact]
    public void Normalize_AppliesNaiveSingular_ForPluralWord()
        // "apples" → 6 chars (> 4), ends in 's' not 'ss' → "apple"
        => IngredientNormalizer.Normalize("apples").Should().Be("apple");

    [Fact]
    public void Normalize_DoesNotStripSingular_ShortWord()
        // "peas" → 4 chars, naive singular requires > 4 chars
        => IngredientNormalizer.Normalize("peas").Should().Be("peas");

    [Fact]
    public void Normalize_DoesNotStripDoubleS()
        => IngredientNormalizer.Normalize("class").Should().Be("class");

    [Fact]
    public void Normalize_MultipleModifiers_KeepsCoreIngredient()
        => IngredientNormalizer.Normalize("fresh organic chopped garlic").Should().Be("garlic");

    // ── Tokenize ───────────────────────────────────────────────────────────────

    [Fact]
    public void Tokenize_SingleWord_ReturnsSingleToken()
    {
        HashSet<string> tokens = IngredientNormalizer.Tokenize("flour");
        tokens.Should().ContainSingle().Which.Should().Be("flour");
    }

    [Fact]
    public void Tokenize_MultipleWords_ReturnsAllTokens()
    {
        HashSet<string> tokens = IngredientNormalizer.Tokenize("olive oil");
        tokens.Should().BeEquivalentTo(new[] { "olive", "oil" });
    }

    [Fact]
    public void Tokenize_IsCaseInsensitive()
    {
        HashSet<string> tokens = IngredientNormalizer.Tokenize("Olive Oil");
        tokens.Contains("olive").Should().BeTrue();
        tokens.Contains("OLIVE").Should().BeTrue();
    }

    [Fact]
    public void Tokenize_EmptyString_ReturnsEmptySet()
        => IngredientNormalizer.Tokenize(string.Empty).Should().BeEmpty();

    // ── SimpleLower ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Flour", "flour")]
    [InlineData("  Butter  ", "butter")]
    [InlineData("ALL CAPS", "all caps")]
    [InlineData("", "")]
    public void SimpleLower_ReturnsLowercaseTrimmed(string input, string expected)
        => IngredientNormalizer.SimpleLower(input).Should().Be(expected);

    // ── JaccardSimilarity ──────────────────────────────────────────────────────

    [Fact]
    public void JaccardSimilarity_IdenticalSets_Returns1()
    {
        HashSet<string> a = new() { "olive", "oil" };
        HashSet<string> b = new() { "olive", "oil" };
        IngredientNormalizer.JaccardSimilarity(a, b).Should().Be(1.0m);
    }

    [Fact]
    public void JaccardSimilarity_DisjointSets_Returns0()
    {
        HashSet<string> a = new() { "flour" };
        HashSet<string> b = new() { "sugar" };
        IngredientNormalizer.JaccardSimilarity(a, b).Should().Be(0.0m);
    }

    [Fact]
    public void JaccardSimilarity_PartialOverlap_ReturnsExpected()
    {
        HashSet<string> a = new() { "olive", "oil" };
        HashSet<string> b = new() { "oil", "vinegar" };
        // intersection=1, union=3 → 1/3
        IngredientNormalizer.JaccardSimilarity(a, b).Should().BeApproximately(0.333m, 0.001m);
    }

    [Fact]
    public void JaccardSimilarity_BothEmpty_Returns1()
    {
        HashSet<string> empty1 = new();
        HashSet<string> empty2 = new();
        IngredientNormalizer.JaccardSimilarity(empty1, empty2).Should().Be(1.0m);
    }

    [Fact]
    public void JaccardSimilarity_OneEmpty_Returns0()
    {
        HashSet<string> a = new() { "flour" };
        HashSet<string> b = new();
        IngredientNormalizer.JaccardSimilarity(a, b).Should().Be(0.0m);
    }

    // ── EditDistance ───────────────────────────────────────────────────────────

    [Fact]
    public void EditDistance_EqualStrings_Returns0()
        => IngredientNormalizer.EditDistance("flour", "flour").Should().Be(0);

    [Fact]
    public void EditDistance_SingleDeletion_Returns1()
        => IngredientNormalizer.EditDistance("flour", "flou").Should().Be(1);

    [Fact]
    public void EditDistance_SingleInsertion_Returns1()
        => IngredientNormalizer.EditDistance("flou", "flour").Should().Be(1);

    [Fact]
    public void EditDistance_SingleSubstitution_Returns1()
        => IngredientNormalizer.EditDistance("flour", "floor").Should().Be(1);

    [Fact]
    public void EditDistance_EmptyToNonEmpty_ReturnsLength()
        => IngredientNormalizer.EditDistance(string.Empty, "abc").Should().Be(3);

    [Fact]
    public void EditDistance_NonEmptyToEmpty_ReturnsLength()
        => IngredientNormalizer.EditDistance("abc", string.Empty).Should().Be(3);

    [Fact]
    public void EditDistance_BothEmpty_Returns0()
        => IngredientNormalizer.EditDistance(string.Empty, string.Empty).Should().Be(0);
}
