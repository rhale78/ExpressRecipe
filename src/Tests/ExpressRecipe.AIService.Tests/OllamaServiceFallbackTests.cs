using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ExpressRecipe.AIService.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;

namespace ExpressRecipe.AIService.Tests;

/// <summary>
/// Tests that <see cref="OllamaService.ExtractRecipeFromTextAsync"/> always returns a result —
/// even when Ollama returns 404 (model not installed) or the request times out.
/// </summary>
public class OllamaServiceFallbackTests
{
    private static OllamaService BuildService(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:11434"),
            Timeout     = TimeSpan.FromSeconds(5)
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AI:DefaultModel"] = "llama3.2",
                ["AI:QuickModel"]   = "llama3.2",
                ["AI:DeepModel"]    = "llama3.2",
                // Prevent the constructor from overriding BaseAddress with a second Uri()
                ["AI:OllamaEndpoint"] = "http://localhost:11434"
            })
            .Build();

        return new OllamaService(httpClient, config, NullLogger<OllamaService>.Instance);
    }

    [Fact]
    public async Task ExtractRecipeFromText_OllamaReturns404_FallsBackToRegexResult()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.NotFound));

        var service = BuildService(handlerMock.Object);

        const string recipeText = """
            Chocolate Chip Cookies
            Serves 24
            Prep time: 15 min
            Cook time: 12 min
            Ingredients:
            2 cups flour
            1 tsp vanilla
            Instructions:
            1. Mix all ingredients.
            2. Bake at 350F for 12 minutes.
            """;

        var result = await service.ExtractRecipeFromTextAsync(recipeText, "quick");

        result.Should().NotBeNull("fallback should always return a DTO");
        result.Title.Should().Be("Chocolate Chip Cookies");
        result.Servings.Should().Be(24);
        result.Ingredients.Should().NotBeEmpty();
        result.Instructions.Should().NotBeEmpty();
        result.ConfidenceScore.Should().BeLessThanOrEqualTo(0.85,
            "regex fallback is capped at 0.85 to signal it is not AI quality");
    }

    [Fact]
    public async Task ExtractRecipeFromText_OllamaTimesOut_FallsBackToRegexResult()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("Simulated timeout"));

        var service = BuildService(handlerMock.Object);

        const string recipeText = """
            Simple Salad
            Serves 2
            Ingredients:
            1 cup lettuce
            Instructions:
            1. Toss and serve.
            """;

        var result = await service.ExtractRecipeFromTextAsync(recipeText, "quick");

        result.Should().NotBeNull("timeout fallback should always return a DTO");
        result.Title.Should().Be("Simple Salad");
        result.ConfidenceScore.Should().BeLessThanOrEqualTo(0.85);
    }

    [Fact]
    public async Task ExtractRecipeFromText_OllamaReturnsGoodJson_ReturnsAiResult()
    {
        var aiJson = JsonSerializer.Serialize(new
        {
            title            = "Pasta Carbonara",
            servings         = 4,
            prepTimeMinutes  = 10,
            cookTimeMinutes  = 20,
            difficulty       = "Medium",
            ingredients      = Array.Empty<object>(),
            instructions     = Array.Empty<string>(),
            confidenceScore  = 0.92
        });

        var ollamaResponse = JsonSerializer.Serialize(new { response = aiJson });

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ollamaResponse, System.Text.Encoding.UTF8, "application/json")
            });

        var service = BuildService(handlerMock.Object);

        var result = await service.ExtractRecipeFromTextAsync("Pasta recipe text", "deep");

        result.Should().NotBeNull();
        result.Title.Should().Be("Pasta Carbonara");
        result.ConfidenceScore.Should().BeGreaterThanOrEqualTo(0.85,
            "AI deep result is boosted to minimum 0.85");
    }
}
