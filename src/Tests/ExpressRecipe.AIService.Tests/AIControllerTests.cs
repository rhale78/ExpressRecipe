using ExpressRecipe.AIService.Controllers;
using ExpressRecipe.AIService.Services;
using ExpressRecipe.Client.Shared.Models.AI;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ExpressRecipe.AIService.Tests;

/// <summary>
/// Unit tests for <see cref="AIController"/> recipe extraction endpoint.
/// </summary>
public class AIControllerTests
{
    private readonly Mock<IOllamaService> _serviceMock = new();
    private readonly AIController _controller;

    public AIControllerTests()
    {
        _controller = new AIController(_serviceMock.Object, NullLogger<AIController>.Instance);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Input validation
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExtractRecipe_NullText_ReturnsBadRequest()
    {
        var request = new RecipeExtractionRequest { RecipeText = null };

        var result = await _controller.ExtractRecipe(request);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ExtractRecipe_EmptyText_ReturnsBadRequest()
    {
        var request = new RecipeExtractionRequest { RecipeText = "" };

        var result = await _controller.ExtractRecipe(request);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // ParseMode routing
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExtractRecipe_EmptyParseMode_DefaultsToQuick()
    {
        var dto = new ExtractedRecipeDto { Title = "Test", ConfidenceScore = 0.8 };
        _serviceMock.Setup(s => s.ExtractRecipeFromTextAsync(It.IsAny<string>(), "quick"))
                    .ReturnsAsync(dto);

        var request = new RecipeExtractionRequest { RecipeText = "Some text", ParseMode = "" };
        var result = await _controller.ExtractRecipe(request);

        result.Result.Should().BeOfType<OkObjectResult>();
        _serviceMock.Verify(s => s.ExtractRecipeFromTextAsync("Some text", "quick"), Times.Once);
    }

    [Fact]
    public async Task ExtractRecipe_DeepMode_ForwardsDeepToService()
    {
        var dto = new ExtractedRecipeDto { Title = "Deep Result", ConfidenceScore = 0.95 };
        _serviceMock.Setup(s => s.ExtractRecipeFromTextAsync(It.IsAny<string>(), "deep"))
                    .ReturnsAsync(dto);

        var request = new RecipeExtractionRequest { RecipeText = "Recipe text", ParseMode = "deep" };
        var result = await _controller.ExtractRecipe(request);

        result.Result.Should().BeOfType<OkObjectResult>();
        _serviceMock.Verify(s => s.ExtractRecipeFromTextAsync("Recipe text", "deep"), Times.Once);
    }

    [Fact]
    public async Task ExtractRecipe_QuickMode_ForwardsQuickToService()
    {
        var dto = new ExtractedRecipeDto { Title = "Quick Result", ConfidenceScore = 0.70 };
        _serviceMock.Setup(s => s.ExtractRecipeFromTextAsync(It.IsAny<string>(), "quick"))
                    .ReturnsAsync(dto);

        var request = new RecipeExtractionRequest { RecipeText = "Recipe text", ParseMode = "quick" };
        var result = await _controller.ExtractRecipe(request);

        result.Result.Should().BeOfType<OkObjectResult>();
        _serviceMock.Verify(s => s.ExtractRecipeFromTextAsync("Recipe text", "quick"), Times.Once);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Response content
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExtractRecipe_ServiceReturnsDto_OkContainsSameDto()
    {
        var expected = new ExtractedRecipeDto
        {
            Title           = "Chocolate Cake",
            Servings        = 8,
            ConfidenceScore = 0.92
        };
        _serviceMock.Setup(s => s.ExtractRecipeFromTextAsync(It.IsAny<string>(), It.IsAny<string>()))
                    .ReturnsAsync(expected);

        var request = new RecipeExtractionRequest { RecipeText = "Any recipe text" };
        var result  = await _controller.ExtractRecipe(request);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(expected);
    }
}
