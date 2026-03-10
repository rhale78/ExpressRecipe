using System.Net;
using System.Text;
using ExpressRecipe.VisionService.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ExpressRecipe.VisionService.Tests;

/// <summary>
/// Tests for individual vision providers and the VisionService orchestrator.
/// Verifies fault-tolerance: disabled or unavailable providers always return
/// Success=false without throwing, and the orchestrator selects the best result.
/// </summary>
public class VisionProviderTests
{
    // -----------------------------------------------------------------------
    // OnnxVisionProvider
    // -----------------------------------------------------------------------

    [Fact]
    public async Task OnnxVisionProvider_ModelPathMissing_ReturnsFalseWithoutThrowing()
    {
        OnnxVisionOptions options = new OnnxVisionOptions { ModelPath = "", Enabled = true };
        OnnxVisionProvider provider = new OnnxVisionProvider(options, NullLogger<OnnxVisionProvider>.Instance);

        VisionResult result = await provider.AnalyzeAsync(new byte[] { 1, 2, 3 });

        result.Success.Should().BeFalse("model path is empty so the session is never loaded");
        result.ProviderUsed.Should().Be("ONNX");
        result.ErrorMessage.Should().Be("Model not loaded");
    }

    [Fact]
    public async Task OnnxVisionProvider_ModelPathMissing_IsEnabledReturnsFalse()
    {
        OnnxVisionOptions options = new OnnxVisionOptions { ModelPath = "/nonexistent/model.onnx", Enabled = true };
        OnnxVisionProvider provider = new OnnxVisionProvider(options, NullLogger<OnnxVisionProvider>.Instance);

        provider.IsEnabled.Should().BeFalse("no model file exists at the given path");
    }

    // -----------------------------------------------------------------------
    // PaddleOcrProvider
    // -----------------------------------------------------------------------

    [Fact]
    public async Task PaddleOcrProvider_EmptyTextBlocks_ReturnsFalseAndMatcherNotCalled()
    {
        PaddleOcrOptions options = new PaddleOcrOptions { Enabled = true };
        Mock<IProductNameMatcher> matcherMock = new Mock<IProductNameMatcher>();
        PaddleOcrProvider provider = new PaddleOcrProvider(
            options,
            matcherMock.Object,
            NullLogger<PaddleOcrProvider>.Instance);

        VisionResult result = await provider.AnalyzeAsync(new byte[] { 1 });

        result.Success.Should().BeFalse("ExtractTextBlocks is a stub that always returns empty");
        result.ProviderUsed.Should().Be("PaddleOCR");
        matcherMock.Verify(
            m => m.MatchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "matcher should not be called when there are no text blocks");
    }

    [Fact]
    public async Task PaddleOcrProvider_Disabled_ReturnsFalseImmediately()
    {
        PaddleOcrOptions options = new PaddleOcrOptions { Enabled = false };
        Mock<IProductNameMatcher> matcherMock = new Mock<IProductNameMatcher>();
        PaddleOcrProvider provider = new PaddleOcrProvider(
            options,
            matcherMock.Object,
            NullLogger<PaddleOcrProvider>.Instance);

        VisionResult result = await provider.AnalyzeAsync(new byte[] { 1 });

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("disabled");
        matcherMock.Verify(
            m => m.MatchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // -----------------------------------------------------------------------
    // IProductNameMatcher — mock behaviour test
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ProductNameMatcher_MockedWithCheerios_ReturnsCorrectProductMatch()
    {
        Mock<IProductNameMatcher> matcherMock = new Mock<IProductNameMatcher>();
        matcherMock
            .Setup(m => m.MatchAsync("Cheerios General Mills", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProductMatchResult
            {
                ProductId = Guid.NewGuid(),
                ProductName = "Cheerios",
                Brand = "General Mills",
                Score = 0.9,
            });

        ProductMatchResult? match = await matcherMock.Object.MatchAsync("Cheerios General Mills");

        match.Should().NotBeNull();
        match!.ProductName.Should().Be("Cheerios");
        match.Brand.Should().Be("General Mills");
        match.Score.Should().Be(0.9);
    }

    // -----------------------------------------------------------------------
    // OllamaVisionProvider
    // -----------------------------------------------------------------------

    [Fact]
    public async Task OllamaVisionProvider_GenerateTimesOut_ReturnsFalseWithoutThrowing()
    {
        // Tags check succeeds immediately; generate call delays longer than TimeoutMs.
        OllamaTimeoutTestHandler handler = new OllamaTimeoutTestHandler(generateDelayMs: 5000);
        using HttpClient httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:11434"),
        };
        OllamaVisionOptions options = new OllamaVisionOptions
        {
            Enabled = true,
            TimeoutMs = 1,   // extremely short — generate call will always exceed this
            Model = "llava",
        };
        OllamaVisionProvider provider = new OllamaVisionProvider(
            options,
            httpClient,
            NullLogger<OllamaVisionProvider>.Instance);

        VisionResult result = await provider.AnalyzeAsync(new byte[] { 1, 2, 3 });

        result.Success.Should().BeFalse("the generate call timed out");
    }

    [Fact]
    public async Task OllamaVisionProvider_Disabled_ReturnsFalseWithDisabledMessage()
    {
        using HttpClient httpClient = new HttpClient();
        OllamaVisionOptions options = new OllamaVisionOptions { Enabled = false };
        OllamaVisionProvider provider = new OllamaVisionProvider(
            options,
            httpClient,
            NullLogger<OllamaVisionProvider>.Instance);

        VisionResult result = await provider.AnalyzeAsync(new byte[] { 1 });

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("disabled");
    }

    // -----------------------------------------------------------------------
    // AzureVisionProvider
    // -----------------------------------------------------------------------

    [Fact]
    public async Task AzureVisionProvider_Disabled_ReturnsFalseWithDisabledInErrorMessage()
    {
        AzureVisionOptions options = new AzureVisionOptions { Enabled = false };
        using HttpClient httpClient = new HttpClient();
        AzureVisionProvider provider = new AzureVisionProvider(
            options,
            httpClient,
            NullLogger<AzureVisionProvider>.Instance);

        VisionResult result = await provider.AnalyzeAsync(new byte[] { 1 });

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("disabled",
            "IsEnabled is false when Enabled=false, so AnalyzeAsync returns the disabled message immediately");
    }

    [Fact]
    public async Task AzureVisionProvider_EnabledButNoEndpoint_IsEnabledReturnsFalse()
    {
        AzureVisionOptions options = new AzureVisionOptions
        {
            Enabled = true,
            Endpoint = "",
            ApiKey = "",
        };
        using HttpClient httpClient = new HttpClient();
        AzureVisionProvider provider = new AzureVisionProvider(
            options,
            httpClient,
            NullLogger<AzureVisionProvider>.Instance);

        provider.IsEnabled.Should().BeFalse("Endpoint and ApiKey are both empty");
    }

    // -----------------------------------------------------------------------
    // VisionService orchestrator
    // -----------------------------------------------------------------------

    [Fact]
    public async Task VisionService_AllProvidersDisabled_ReturnsSuccessFalse()
    {
        OnnxVisionProvider onnx = new OnnxVisionProvider(
            new OnnxVisionOptions { Enabled = false },
            NullLogger<OnnxVisionProvider>.Instance);

        Mock<IProductNameMatcher> matcherMock = new Mock<IProductNameMatcher>();
        PaddleOcrProvider paddle = new PaddleOcrProvider(
            new PaddleOcrOptions { Enabled = false },
            matcherMock.Object,
            NullLogger<PaddleOcrProvider>.Instance);

        using HttpClient ollamaHttpClient = new HttpClient();
        OllamaVisionProvider ollama = new OllamaVisionProvider(
            new OllamaVisionOptions { Enabled = false },
            ollamaHttpClient,
            NullLogger<OllamaVisionProvider>.Instance);

        using HttpClient azureHttpClient = new HttpClient();
        AzureVisionProvider azure = new AzureVisionProvider(
            new AzureVisionOptions { Enabled = false },
            azureHttpClient,
            NullLogger<AzureVisionProvider>.Instance);

        Services.VisionService service = new Services.VisionService(
            onnx,
            paddle,
            ollama,
            azure,
            new VisionServiceOptions(),
            NullLogger<Services.VisionService>.Instance);

        VisionOptions visionOptions = new VisionOptions
        {
            AllowOnnx = true,
            AllowPaddleOcr = true,
            AllowOllamaVision = true,
            AllowAzureVision = true,
            MinConfidence = 0.55,
        };

        VisionResult result = await service.AnalyzeAsync(new byte[] { 1, 2, 3 }, visionOptions);

        result.Success.Should().BeFalse("every provider is disabled");
    }

    [Fact]
    public async Task VisionService_OllamaReturnsHighConfidence_ReturnsOllamaResult()
    {
        OnnxVisionProvider onnx = new OnnxVisionProvider(
            new OnnxVisionOptions { Enabled = false },
            NullLogger<OnnxVisionProvider>.Instance);

        Mock<IProductNameMatcher> matcherMock = new Mock<IProductNameMatcher>();
        PaddleOcrProvider paddle = new PaddleOcrProvider(
            new PaddleOcrOptions { Enabled = false },
            matcherMock.Object,
            NullLogger<PaddleOcrProvider>.Instance);

        OllamaSuccessHandler ollamaHandler = new OllamaSuccessHandler("Cheerios", "General Mills", 0.9);
        using HttpClient ollamaClient = new HttpClient(ollamaHandler)
        {
            BaseAddress = new Uri("http://localhost:11434"),
        };
        OllamaVisionProvider ollama = new OllamaVisionProvider(
            new OllamaVisionOptions { Enabled = true, TimeoutMs = 5000, Model = "llava" },
            ollamaClient,
            NullLogger<OllamaVisionProvider>.Instance);

        using HttpClient azureClient = new HttpClient();
        AzureVisionProvider azure = new AzureVisionProvider(
            new AzureVisionOptions { Enabled = false },
            azureClient,
            NullLogger<AzureVisionProvider>.Instance);

        Services.VisionService service = new Services.VisionService(
            onnx,
            paddle,
            ollama,
            azure,
            new VisionServiceOptions(),
            NullLogger<Services.VisionService>.Instance);

        VisionOptions visionOptions = new VisionOptions
        {
            AllowOnnx = true,
            AllowPaddleOcr = true,
            AllowOllamaVision = true,
            AllowAzureVision = false,
            MinConfidence = 0.55,
        };

        VisionResult result = await service.AnalyzeAsync(new byte[] { 1, 2, 3 }, visionOptions);

        result.Success.Should().BeTrue();
        result.ProviderUsed.Should().Be("OllamaVision");
        result.ProductName.Should().Be("Cheerios");
        result.Confidence.Should().BeGreaterThan(0.55);
    }
}

// ---------------------------------------------------------------------------
// HTTP handler helpers
// ---------------------------------------------------------------------------

/// <summary>
/// Returns HTTP 200 for the Ollama /api/tags availability check, then delays
/// on the /api/generate call so that the provider's internal timeout fires.
/// </summary>
internal sealed class OllamaTimeoutTestHandler : HttpMessageHandler
{
    private readonly int _generateDelayMs;

    internal OllamaTimeoutTestHandler(int generateDelayMs)
    {
        _generateDelayMs = generateDelayMs;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        bool isTagsCheck = request.RequestUri != null
            && request.RequestUri.AbsolutePath.Contains("tags", StringComparison.Ordinal);

        if (isTagsCheck)
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"models\":[]}", Encoding.UTF8, "application/json"),
            };
        }

        await Task.Delay(_generateDelayMs, cancellationToken);
        return new HttpResponseMessage(HttpStatusCode.OK);
    }
}

/// <summary>
/// Returns a valid Ollama /api/tags response and a successful /api/generate
/// response containing the supplied product details.
/// </summary>
internal sealed class OllamaSuccessHandler : HttpMessageHandler
{
    private readonly string _productName;
    private readonly string _brand;
    private readonly double _confidence;

    internal OllamaSuccessHandler(string productName, string brand, double confidence)
    {
        _productName = productName;
        _brand = brand;
        _confidence = confidence;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        bool isTagsCheck = request.RequestUri != null
            && request.RequestUri.AbsolutePath.Contains("tags", StringComparison.Ordinal);

        if (isTagsCheck)
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"models\":[{\"name\":\"llava\"}]}",
                    Encoding.UTF8,
                    "application/json"),
            };
        }

        // Build the nested JSON that OllamaVisionProvider.ParseOllamaResponse expects.
        // The outer object has a single "response" key whose value is a JSON string.
        string innerJson = System.Text.Json.JsonSerializer.Serialize(
            new System.Collections.Generic.Dictionary<string, object>
            {
                ["productName"] = _productName,
                ["brand"] = _brand,
                ["detectedText"] = Array.Empty<string>(),
                ["labels"] = Array.Empty<string>(),
                ["confidence"] = _confidence,
            });

        string outerJson = System.Text.Json.JsonSerializer.Serialize(
            new System.Collections.Generic.Dictionary<string, object>
            {
                ["response"] = innerJson,
            });

        await Task.Yield();
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(outerJson, Encoding.UTF8, "application/json"),
        };
    }
}
