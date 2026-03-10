using ExpressRecipe.IngredientService.Services.Parsing;
using ExpressRecipe.Shared.DTOs.Product;
using ExpressRecipe.Shared.Matching;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace ExpressRecipe.IngredientService.Tests.Services.Parsing;

public class IngredientParserTests
{
    private readonly Mock<IIngredientMatchingService> _mockMatching;
    private readonly Mock<ILogger<IngredientParser>> _mockLogger;
    private readonly IngredientParser _parser;

    public IngredientParserTests()
    {
        _mockMatching = new Mock<IIngredientMatchingService>();
        _mockLogger = new Mock<ILogger<IngredientParser>>();
        _parser = new IngredientParser(_mockMatching.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task ParseIngredientStringAsync_EmptyString_ReturnsEmptyResult()
    {
        var result = await _parser.ParseIngredientStringAsync(string.Empty);

        result.OriginalString.Should().BeEmpty();
        result.Components.Should().BeEmpty();
    }

    [Fact]
    public async Task ParseIngredientStringAsync_SimpleIngredient_ExtractsName()
    {
        _mockMatching.Setup(m => m.MatchAsync(It.IsAny<string>(), It.IsAny<string>(), null, default))
            .ReturnsAsync(MatchResult.Unresolved("flour", "flour"));

        var result = await _parser.ParseIngredientStringAsync("flour");

        result.Components.Should().HaveCount(1);
        result.Components[0].Name.Should().Be("flour");
        result.Components[0].Quantity.Should().BeNull();
        result.Components[0].Unit.Should().BeNull();
    }

    [Fact]
    public async Task ParseIngredientStringAsync_WithQuantityAndUnit_ParsesCorrectly()
    {
        _mockMatching.Setup(m => m.MatchAsync(It.IsAny<string>(), It.IsAny<string>(), null, default))
            .ReturnsAsync(MatchResult.Unresolved("flour", "flour"));

        var result = await _parser.ParseIngredientStringAsync("2 cups flour");

        result.Components.Should().HaveCount(1);
        result.Components[0].Quantity.Should().Be(2m);
        result.Components[0].Unit.Should().Be("cup");
        result.Components[0].Name.Should().Be("flour");
    }

    [Fact]
    public async Task ParseIngredientStringAsync_WithFraction_ParsesQuantity()
    {
        _mockMatching.Setup(m => m.MatchAsync(It.IsAny<string>(), It.IsAny<string>(), null, default))
            .ReturnsAsync(MatchResult.Unresolved("salt", "salt"));

        var result = await _parser.ParseIngredientStringAsync("1/2 tsp salt");

        result.Components.Should().HaveCount(1);
        result.Components[0].Quantity.Should().Be(0.5m);
        result.Components[0].Unit.Should().Be("tsp");
        result.Components[0].Name.Should().Be("salt");
    }

    [Fact]
    public async Task ParseIngredientStringAsync_WithMatchingIngredient_SetsDatabaseId()
    {
        Guid ingredientId = Guid.NewGuid();
        _mockMatching.Setup(m => m.MatchAsync("flour", It.IsAny<string>(), null, default))
            .ReturnsAsync(new MatchResult
            {
                IngredientId = ingredientId,
                IngredientName = "flour",
                Confidence = 1.0m,
                Strategy = MatchStrategy.Exact,
                RawInput = "flour",
                NormalizedInput = "flour"
            });

        var result = await _parser.ParseIngredientStringAsync("flour");

        result.Components.Should().HaveCount(1);
        result.Components[0].BaseIngredientId.Should().Be(ingredientId);
        result.Components[0].MatchedName.Should().Be("flour");
    }

    [Fact]
    public async Task ParseIngredientStringAsync_WithNoMatch_IngredientIdIsNull()
    {
        _mockMatching.Setup(m => m.MatchAsync(It.IsAny<string>(), It.IsAny<string>(), null, default))
            .ReturnsAsync(MatchResult.Unresolved("flour", "flour"));

        var result = await _parser.ParseIngredientStringAsync("flour");

        result.Components.Should().HaveCount(1);
        result.Components[0].BaseIngredientId.Should().BeNull();
        result.Components[0].MatchedName.Should().BeNull();
    }

    [Fact]
    public async Task ParseIngredientStringAsync_WithTablespoon_NormalizesToTbsp()
    {
        _mockMatching.Setup(m => m.MatchAsync(It.IsAny<string>(), It.IsAny<string>(), null, default))
            .ReturnsAsync(MatchResult.Unresolved("butter", "butter"));

        var result = await _parser.ParseIngredientStringAsync("2 tablespoons butter");

        result.Components.Should().HaveCount(1);
        result.Components[0].Unit.Should().Be("tbsp");
        result.Components[0].Name.Should().Be("butter");
    }

    [Fact]
    public async Task ParseIngredientStringAsync_WithPounds_NormalizesToLb()
    {
        _mockMatching.Setup(m => m.MatchAsync(It.IsAny<string>(), It.IsAny<string>(), null, default))
            .ReturnsAsync(MatchResult.Unresolved("chicken", "chicken"));

        var result = await _parser.ParseIngredientStringAsync("1 lb chicken");

        result.Components.Should().HaveCount(1);
        result.Components[0].Unit.Should().Be("lb");
        result.Components[0].Quantity.Should().Be(1m);
        result.Components[0].Name.Should().Be("chicken");
    }

    [Fact]
    public async Task ParseIngredientStringAsync_CommaDelimited_MultipleComponents()
    {
        _mockMatching.Setup(m => m.MatchAsync(It.IsAny<string>(), It.IsAny<string>(), null, default))
            .ReturnsAsync((string raw, string _, Guid? __, CancellationToken ___) =>
                MatchResult.Unresolved(raw, raw));

        var result = await _parser.ParseIngredientStringAsync("flour, salt");

        result.Components.Should().HaveCount(2);
        result.Components[0].Name.Should().Be("flour");
        result.Components[1].Name.Should().Be("salt");
    }

    [Fact]
    public async Task ParseIngredientStringAsync_OriginalStringPreserved()
    {
        _mockMatching.Setup(m => m.MatchAsync(It.IsAny<string>(), It.IsAny<string>(), null, default))
            .ReturnsAsync(MatchResult.Unresolved("flour", "flour"));

        const string input = "2 cups flour";
        var result = await _parser.ParseIngredientStringAsync(input);

        result.OriginalString.Should().Be(input);
    }

    [Fact]
    public async Task ParseIngredientStringAsync_OrderIndexAssignedSequentially()
    {
        _mockMatching.Setup(m => m.MatchAsync(It.IsAny<string>(), It.IsAny<string>(), null, default))
            .ReturnsAsync((string raw, string _, Guid? __, CancellationToken ___) =>
                MatchResult.Unresolved(raw, raw));

        var result = await _parser.ParseIngredientStringAsync("flour, salt, sugar");

        result.Components[0].OrderIndex.Should().Be(0);
        result.Components[1].OrderIndex.Should().Be(1);
        result.Components[2].OrderIndex.Should().Be(2);
    }

    [Fact]
    public async Task BulkParseIngredientStringsAsync_MultipleStrings_ParsesAll()
    {
        _mockMatching.Setup(m => m.MatchAsync(It.IsAny<string>(), It.IsAny<string>(), null, default))
            .ReturnsAsync((string raw, string _, Guid? __, CancellationToken ___) =>
                MatchResult.Unresolved(raw, raw));

        var result = await _parser.BulkParseIngredientStringsAsync(new[] { "flour", "salt" });

        result.Should().HaveCount(2);
        result.Should().ContainKey("flour");
        result.Should().ContainKey("salt");
        result["flour"].Components.Should().HaveCount(1);
        result["salt"].Components.Should().HaveCount(1);
    }

    [Fact]
    public async Task BulkParseIngredientStringsAsync_DuplicateStrings_ParsedOnce()
    {
        _mockMatching.Setup(m => m.MatchAsync(It.IsAny<string>(), It.IsAny<string>(), null, default))
            .ReturnsAsync(MatchResult.Unresolved("flour", "flour"));

        var result = await _parser.BulkParseIngredientStringsAsync(new[] { "flour", "flour" });

        // Distinct() deduplicated the input → only one key returned
        result.Should().HaveCount(1);
        result.Should().ContainKey("flour");

        // Matching service should have been called exactly once for the single unique ingredient
        _mockMatching.Verify(m => m.MatchAsync("flour", It.IsAny<string>(), null, default), Times.Once());
    }
}
