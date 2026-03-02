using ExpressRecipe.IngredientService.Data;
using ExpressRecipe.IngredientService.Services.Parsing;
using ExpressRecipe.Shared.DTOs.Product;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace ExpressRecipe.IngredientService.Tests.Services.Parsing;

public class IngredientParserTests
{
    private readonly Mock<IIngredientRepository> _mockRepo;
    private readonly Mock<ILogger<IngredientParser>> _mockLogger;
    private readonly IngredientParser _parser;

    public IngredientParserTests()
    {
        _mockRepo = new Mock<IIngredientRepository>();
        _mockLogger = new Mock<ILogger<IngredientParser>>();
        _parser = new IngredientParser(_mockRepo.Object, _mockLogger.Object);
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
        _mockRepo.Setup(r => r.GetIngredientByNameAsync(It.IsAny<string>()))
            .ReturnsAsync((IngredientDto?)null);

        var result = await _parser.ParseIngredientStringAsync("flour");

        result.Components.Should().HaveCount(1);
        result.Components[0].Name.Should().Be("flour");
        result.Components[0].Quantity.Should().BeNull();
        result.Components[0].Unit.Should().BeNull();
    }

    [Fact]
    public async Task ParseIngredientStringAsync_WithQuantityAndUnit_ParsesCorrectly()
    {
        _mockRepo.Setup(r => r.GetIngredientByNameAsync(It.IsAny<string>()))
            .ReturnsAsync((IngredientDto?)null);

        var result = await _parser.ParseIngredientStringAsync("2 cups flour");

        result.Components.Should().HaveCount(1);
        result.Components[0].Quantity.Should().Be(2m);
        result.Components[0].Unit.Should().Be("cup");
        result.Components[0].Name.Should().Be("flour");
    }

    [Fact]
    public async Task ParseIngredientStringAsync_WithFraction_ParsesQuantity()
    {
        _mockRepo.Setup(r => r.GetIngredientByNameAsync(It.IsAny<string>()))
            .ReturnsAsync((IngredientDto?)null);

        var result = await _parser.ParseIngredientStringAsync("1/2 tsp salt");

        result.Components.Should().HaveCount(1);
        result.Components[0].Quantity.Should().Be(0.5m);
        result.Components[0].Unit.Should().Be("tsp");
        result.Components[0].Name.Should().Be("salt");
    }

    [Fact]
    public async Task ParseIngredientStringAsync_WithMatchingIngredient_SetsDatabaseId()
    {
        var ingredientId = Guid.NewGuid();
        var matchedIngredient = new IngredientDto { Id = ingredientId, Name = "flour" };
        _mockRepo.Setup(r => r.GetIngredientByNameAsync("flour"))
            .ReturnsAsync(matchedIngredient);

        var result = await _parser.ParseIngredientStringAsync("flour");

        result.Components.Should().HaveCount(1);
        result.Components[0].BaseIngredientId.Should().Be(ingredientId);
        result.Components[0].MatchedName.Should().Be("flour");
    }

    [Fact]
    public async Task ParseIngredientStringAsync_WithNoMatch_IngredientIdIsNull()
    {
        _mockRepo.Setup(r => r.GetIngredientByNameAsync(It.IsAny<string>()))
            .ReturnsAsync((IngredientDto?)null);

        var result = await _parser.ParseIngredientStringAsync("flour");

        result.Components.Should().HaveCount(1);
        result.Components[0].BaseIngredientId.Should().BeNull();
        result.Components[0].MatchedName.Should().BeNull();
    }

    [Fact]
    public async Task ParseIngredientStringAsync_WithTablespoon_NormalizesToTbsp()
    {
        _mockRepo.Setup(r => r.GetIngredientByNameAsync(It.IsAny<string>()))
            .ReturnsAsync((IngredientDto?)null);

        var result = await _parser.ParseIngredientStringAsync("2 tablespoons butter");

        result.Components.Should().HaveCount(1);
        result.Components[0].Unit.Should().Be("tbsp");
        result.Components[0].Name.Should().Be("butter");
    }

    [Fact]
    public async Task ParseIngredientStringAsync_WithPounds_NormalizesToLb()
    {
        _mockRepo.Setup(r => r.GetIngredientByNameAsync(It.IsAny<string>()))
            .ReturnsAsync((IngredientDto?)null);

        var result = await _parser.ParseIngredientStringAsync("1 lb chicken");

        result.Components.Should().HaveCount(1);
        result.Components[0].Unit.Should().Be("lb");
        result.Components[0].Quantity.Should().Be(1m);
        result.Components[0].Name.Should().Be("chicken");
    }

    [Fact]
    public async Task ParseIngredientStringAsync_CommaDelimited_MultipleComponents()
    {
        _mockRepo.Setup(r => r.GetIngredientByNameAsync(It.IsAny<string>()))
            .ReturnsAsync((IngredientDto?)null);

        var result = await _parser.ParseIngredientStringAsync("flour, salt");

        result.Components.Should().HaveCount(2);
        result.Components[0].Name.Should().Be("flour");
        result.Components[1].Name.Should().Be("salt");
    }

    [Fact]
    public async Task ParseIngredientStringAsync_OriginalStringPreserved()
    {
        _mockRepo.Setup(r => r.GetIngredientByNameAsync(It.IsAny<string>()))
            .ReturnsAsync((IngredientDto?)null);

        const string input = "2 cups flour";
        var result = await _parser.ParseIngredientStringAsync(input);

        result.OriginalString.Should().Be(input);
    }

    [Fact]
    public async Task ParseIngredientStringAsync_OrderIndexAssignedSequentially()
    {
        _mockRepo.Setup(r => r.GetIngredientByNameAsync(It.IsAny<string>()))
            .ReturnsAsync((IngredientDto?)null);

        var result = await _parser.ParseIngredientStringAsync("flour, salt, sugar");

        result.Components[0].OrderIndex.Should().Be(0);
        result.Components[1].OrderIndex.Should().Be(1);
        result.Components[2].OrderIndex.Should().Be(2);
    }

    [Fact]
    public async Task BulkParseIngredientStringsAsync_MultipleStrings_ParsesAll()
    {
        _mockRepo.Setup(r => r.GetIngredientByNameAsync(It.IsAny<string>()))
            .ReturnsAsync((IngredientDto?)null);

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
        _mockRepo.Setup(r => r.GetIngredientByNameAsync(It.IsAny<string>()))
            .ReturnsAsync((IngredientDto?)null);

        var result = await _parser.BulkParseIngredientStringsAsync(new[] { "flour", "flour" });

        // Distinct() deduplicated the input → only one key returned
        result.Should().HaveCount(1);
        result.Should().ContainKey("flour");

        // Repository should have been called exactly once for the single unique ingredient
        _mockRepo.Verify(r => r.GetIngredientByNameAsync("flour"), Times.Once());
    }
}
