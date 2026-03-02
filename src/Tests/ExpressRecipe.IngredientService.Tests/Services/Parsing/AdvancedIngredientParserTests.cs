using ExpressRecipe.IngredientService.Services.Parsing;
using FluentAssertions;

namespace ExpressRecipe.IngredientService.Tests.Services.Parsing;

public class AdvancedIngredientParserTests
{
    private readonly AdvancedIngredientParser _parser = new();

    [Fact]
    public void ParseIngredients_SimpleCommaList_ExtractsAllIngredients()
    {
        var result = _parser.ParseIngredients("Water, Sugar, Salt");

        result.Should().HaveCount(3);
        result.Should().Contain("Water");
        result.Should().Contain("Sugar");
        result.Should().Contain("Salt");
    }

    [Fact]
    public void ParseIngredients_EmptyString_ReturnsEmptyList()
    {
        var result = _parser.ParseIngredients(string.Empty);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseIngredients_WhitespaceOnly_ReturnsEmptyList()
    {
        var result = _parser.ParseIngredients("   ");

        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseIngredients_NullInput_ReturnsEmptyList()
    {
        var result = _parser.ParseIngredients(null!);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseIngredients_WithContainsPhrase_ExtractsSubIngredients()
    {
        // "Contains 2% or less of: ..." pattern separates main vs minor ingredients
        var result = _parser.ParseIngredients("Water, Contains 2% or less of: Salt, Sugar");

        result.Should().Contain("Water");
        result.Should().Contain("Salt");
        result.Should().Contain("Sugar");
    }

    [Fact]
    public void ParseIngredients_ContainsLessThan2Percent_HandledCorrectly()
    {
        var result = _parser.ParseIngredients("Enriched Flour, Contains 2% or less of: Salt, Sugar, Yeast");

        result.Should().Contain("Salt");
        result.Should().Contain("Sugar");
        result.Should().Contain("Yeast");
    }

    [Fact]
    public void ParseIngredients_WithParenthetical_ExtractsIngredientsFromParens()
    {
        // Main ingredient + sub-ingredients in parens should both be extracted
        var result = _parser.ParseIngredients("Flour (Wheat Flour, Niacin, Iron), Water");

        result.Should().Contain("Flour");
        result.Should().Contain("Water");
        // CleanIngredientName lowercases after first character: "Wheat Flour" → "Wheat flour"
        result.Should().Contain("Wheat flour");
    }

    [Fact]
    public void ParseIngredients_NestedParentheses_HandledCorrectly()
    {
        var result = _parser.ParseIngredients("Cheese (Milk, Salt), Water, Sugar");

        result.Should().Contain("Cheese");
        result.Should().Contain("Water");
        result.Should().Contain("Sugar");
        result.Should().Contain("Milk");
        result.Should().Contain("Salt");
    }

    [Fact]
    public void ParseIngredients_DuplicateIngredients_DeduplicatedByDefault()
    {
        // "Water" appears twice - should appear once in output
        var result = _parser.ParseIngredients("Water, Water, Sugar");

        result.Count(i => i.Equals("Water", StringComparison.OrdinalIgnoreCase)).Should().Be(1);
        result.Should().Contain("Sugar");
        result.Should().HaveCount(2);
    }

    [Fact]
    public void ParseIngredients_WithPercentagePrefix_StripsPercentage()
    {
        // "2% Milk" → percentage prefix stripped → "Milk"
        var result = _parser.ParseIngredients("2% Milk, Sugar");

        result.Should().Contain("Milk");
        result.Should().Contain("Sugar");
        result.Should().NotContain("2% Milk");
    }

    [Fact]
    public void ParseIngredients_WithAsterisk_StripsAsterisk()
    {
        // Asterisks are annotation markers and should be stripped during normalization
        var result = _parser.ParseIngredients("Water*, Sugar*");

        result.Should().Contain("Water");
        result.Should().Contain("Sugar");
    }

    [Fact]
    public void ParseIngredients_LongCommaList_AllExtracted()
    {
        const string ingredients = "Water, Wheat Flour, Sugar, Salt, Yeast, Butter, Eggs, Milk, Soy Lecithin, Vanilla";

        var result = _parser.ParseIngredients(ingredients);

        result.Should().HaveCountGreaterThanOrEqualTo(9);
        result.Should().Contain("Water");
        result.Should().Contain("Sugar");
        result.Should().Contain("Salt");
        result.Should().Contain("Yeast");
    }

    [Fact]
    public void ParseIngredients_ResultIsSortedAlphabetically()
    {
        var result = _parser.ParseIngredients("Zucchini, Apple, Mango");

        result.Should().BeInAscendingOrder(StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void BulkParseIngredients_MultipleLists_ReturnsResultsForEach()
    {
        var inputs = new[] { "Water, Sugar", "Salt, Pepper" };

        var result = _parser.BulkParseIngredients(inputs);

        result.Should().HaveCount(2);
        result["Water, Sugar"].Should().Contain("Water").And.Contain("Sugar");
        result["Salt, Pepper"].Should().Contain("Salt").And.Contain("Pepper");
    }

    [Fact]
    public void BulkParseIngredients_DuplicateInputs_DeduplicatedKeys()
    {
        // Identical texts should only produce one key in the result
        var inputs = new[] { "Water, Sugar", "Water, Sugar" };

        var result = _parser.BulkParseIngredients(inputs);

        result.Should().HaveCount(1);
    }

    // --- ValidateIngredient tests ---

    [Fact]
    public void ValidateIngredient_ValidIngredient_ReturnsValid()
    {
        var result = _parser.ValidateIngredient("flour");

        result.IsValid.Should().BeTrue();
        result.NeedsFurtherProcessing.Should().BeFalse();
    }

    [Fact]
    public void ValidateIngredient_EmptyString_ReturnsInvalid()
    {
        var result = _parser.ValidateIngredient(string.Empty);

        result.IsValid.Should().BeFalse();
        result.NeedsFurtherProcessing.Should().BeFalse();
        result.Reason.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ValidateIngredient_WithComma_ReturnsInvalidNeedsProcessing()
    {
        var result = _parser.ValidateIngredient("flour, salt");

        result.IsValid.Should().BeFalse();
        result.NeedsFurtherProcessing.Should().BeTrue();
    }

    [Fact]
    public void ValidateIngredient_TooLong_ReturnsInvalidNeedsProcessing()
    {
        // 41+ chars that is not a recognized long ingredient pattern
        var result = _parser.ValidateIngredient("some random ingredient that is definitely too long for this check");

        result.IsValid.Should().BeFalse();
        result.NeedsFurtherProcessing.Should().BeTrue();
    }

    [Fact]
    public void ValidateIngredient_WithUnrecognizedAndOr_ReturnsInvalidNeedsProcessing()
    {
        // "and" in middle that isn't a recognized compound
        var result = _parser.ValidateIngredient("peanut and almond");

        result.IsValid.Should().BeFalse();
        result.NeedsFurtherProcessing.Should().BeTrue();
    }

    [Fact]
    public void ValidateIngredient_MonoAndDiglycerides_IsRecognizedCompound()
    {
        // "mono and diglycerides" is a recognized compound ingredient
        var result = _parser.ValidateIngredient("mono and diglycerides");

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateIngredient_StartsWithClosingParen_ReturnsInvalidNeedsProcessing()
    {
        var result = _parser.ValidateIngredient(") Salt");

        result.IsValid.Should().BeFalse();
        result.NeedsFurtherProcessing.Should().BeTrue();
    }

    [Fact]
    public void ParseIngredients_AllDigitsEntry_Excluded()
    {
        // Bare numbers are not valid ingredients and should be filtered out
        var result = _parser.ParseIngredients("Water, 12345, Sugar");

        result.Should().Contain("Water");
        result.Should().Contain("Sugar");
        result.Should().NotContainMatch("*12345*");
    }
}
