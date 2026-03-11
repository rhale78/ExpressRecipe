using ExpressRecipe.ProductService.Services;
using FluentAssertions;
using Xunit;

namespace ExpressRecipe.ProductService.Tests.Services;

public class IngredientNormalizerTests
{
    [Theory]
    [InlineData("Enriched Flour (Wheat Flour, Niacin)", new[] { "enriched flour", "wheat flour", "niacin" })]
    [InlineData("Salt", new[] { "salt" })]
    [InlineData("Sugar; Water, Citric Acid", new[] { "sugar", "water", "citric acid" })]
    [InlineData(null, new string[0])]
    [InlineData("", new string[0])]
    [InlineData("   ", new string[0])]
    public void Normalize_VariousInputs_ReturnsExpectedTokens(
        string? raw, string[] expected)
    {
        List<string> result = IngredientNormalizer.Normalize(raw);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Normalize_FillerWords_Excluded()
    {
        // Fillers are removed when they appear as standalone comma-separated tokens
        List<string> result = IngredientNormalizer.Normalize("Wheat, and, Oats, or, Barley");

        result.Should().Contain("wheat");
        result.Should().Contain("oats");
        result.Should().Contain("barley");
        result.Should().NotContain("and");
        result.Should().NotContain("or");
    }

    [Fact]
    public void Normalize_SingleCharacterTokens_Excluded()
    {
        // "Vitamin E" should not produce standalone "e" token (1 char after splitting)
        List<string> result = IngredientNormalizer.Normalize("Vitamin E, Iron");

        result.Should().NotContain("e");
        result.Should().Contain("iron");
    }

    [Fact]
    public void Normalize_NestedParentheses_ExpandedCorrectly()
    {
        // "Corn Syrup (High Fructose (Refined))" should expand both levels
        List<string> result = IngredientNormalizer.Normalize(
            "Corn Syrup (High Fructose (Refined))");

        result.Should().Contain("corn syrup");
        result.Should().Contain("high fructose");
        result.Should().Contain("refined");
    }

    [Fact]
    public void Normalize_AllLowercase()
    {
        List<string> result = IngredientNormalizer.Normalize("WHEAT FLOUR");

        result.Should().OnlyContain(s => s == s.ToLowerInvariant());
    }

    [Fact]
    public void NormalizeAll_MultipleRawStrings_CombinedDeduped()
    {
        List<string> result = IngredientNormalizer.NormalizeAll(new[]
        {
            "Wheat, Milk",
            "Wheat, Eggs"  // wheat duplicated across strings
        });

        result.Should().Contain("wheat").And.Subject.Count(s => s == "wheat").Should().Be(1);
        result.Should().Contain("milk");
        result.Should().Contain("eggs");
    }

    [Fact]
    public void Normalize_ProblemStatement_ExactExpectation()
    {
        // From problem statement: "Enriched Flour (Wheat Flour, Niacin)"
        // → ["enriched flour", "wheat flour", "niacin"]
        List<string> result = IngredientNormalizer.Normalize("Enriched Flour (Wheat Flour, Niacin)");

        result.Should().Contain("enriched flour");
        result.Should().Contain("wheat flour");
        result.Should().Contain("niacin");
    }
}
