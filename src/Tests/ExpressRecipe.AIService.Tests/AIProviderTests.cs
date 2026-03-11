using System.Net;
using System.Text.Json;
using ExpressRecipe.AIService.Configuration;
using ExpressRecipe.AIService.Data;
using ExpressRecipe.AIService.Providers;
using ExpressRecipe.AIService.Services;
using FluentAssertions;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;

namespace ExpressRecipe.AIService.Tests;

/// <summary>
/// Tests for the AI provider infrastructure: OllamaProvider, cloud stubs,
/// AIProviderFactory, and ApprovalQueueService.
/// </summary>
public class AIProviderTests
{
    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static OllamaProvider BuildOllamaProvider(HttpMessageHandler handler,
        string? baseUrl = "http://localhost:11434")
    {
        Mock<IHttpClientFactory> factoryMock = new();
        HttpClient client = new(handler) { Timeout = TimeSpan.FromSeconds(5) };
        factoryMock.Setup(f => f.CreateClient("Ollama")).Returns(client);

        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AI:Ollama:BaseUrl"]       = baseUrl,
                ["AI:Ollama:DefaultModel"]  = "llama3.2"
            })
            .Build();

        return new OllamaProvider(factoryMock.Object, config,
            NullLogger<OllamaProvider>.Instance);
    }

    private static Mock<HttpMessageHandler> BuildHandler(
        HttpStatusCode statusCode, string? responseJson = null)
    {
        Mock<HttpMessageHandler> mock = new();
        mock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(statusCode)
            {
                Content = responseJson is null
                    ? new StringContent(string.Empty)
                    : new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
            });
        return mock;
    }

    private static HybridCache BuildHybridCache()
    {
        ServiceCollection services = new();
        services.AddMemoryCache();
        services.AddDistributedMemoryCache();
#pragma warning disable EXTEXP0018
        services.AddHybridCache();
#pragma warning restore EXTEXP0018
        return services.BuildServiceProvider().GetRequiredService<HybridCache>();
    }

    private static ILocalModeConfig LocalMode(bool isLocal)
    {
        Mock<ILocalModeConfig> mock = new();
        mock.Setup(m => m.IsLocalMode).Returns(isLocal);
        return mock.Object;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // OllamaProvider — GenerateAsync
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task OllamaProvider_GenerateAsync_OllamaDown_ReturnsFailure()
    {
        Mock<HttpMessageHandler> handler = BuildHandler(HttpStatusCode.ServiceUnavailable);
        OllamaProvider provider = BuildOllamaProvider(handler.Object);

        AITextResult result = await provider.GenerateAsync("test prompt");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
        result.ProviderName.Should().Be("Ollama");
    }

    [Fact]
    public async Task OllamaProvider_GenerateAsync_Timeout_ReturnsFailure()
    {
        Mock<HttpMessageHandler> handler = new();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException("Simulated timeout"));

        OllamaProvider provider = BuildOllamaProvider(handler.Object);

        AITextResult result = await provider.GenerateAsync("prompt",
            new AIRequestOptions { Timeout = TimeSpan.FromMilliseconds(100) });

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
        result.ProviderName.Should().Be("Ollama");
    }

    [Fact]
    public async Task OllamaProvider_GenerateAsync_HttpException_ReturnsFailure()
    {
        Mock<HttpMessageHandler> handler = new();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        OllamaProvider provider = BuildOllamaProvider(handler.Object);

        AITextResult result = await provider.GenerateAsync("prompt");

        result.Success.Should().BeFalse();
        result.ProviderName.Should().Be("Ollama");
    }

    [Fact]
    public async Task OllamaProvider_GenerateAsync_Success_ReturnsText()
    {
        string ollamaJson = JsonSerializer.Serialize(new { response = "Hello world", eval_count = 42 });
        Mock<HttpMessageHandler> handler = BuildHandler(HttpStatusCode.OK, ollamaJson);
        OllamaProvider provider = BuildOllamaProvider(handler.Object);

        AITextResult result = await provider.GenerateAsync("test prompt");

        result.Success.Should().BeTrue();
        result.Text.Should().Be("Hello world");
        result.TokensUsed.Should().Be(42);
        result.ProviderName.Should().Be("Ollama");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // OllamaProvider — ScoreForApprovalAsync
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task OllamaProvider_ScoreForApproval_GoodRecipe_HighScore()
    {
        string approvalJson = JsonSerializer.Serialize(
            new { score = 0.95, reasoning = "Excellent recipe", kick_to_human = false });
        string ollamaJson = JsonSerializer.Serialize(new { response = approvalJson, eval_count = 50 });
        Mock<HttpMessageHandler> handler = BuildHandler(HttpStatusCode.OK, ollamaJson);
        OllamaProvider provider = BuildOllamaProvider(handler.Object);

        AIApprovalResult result = await provider.ScoreForApprovalAsync(
            "Chocolate Chip Cookies: mix flour, sugar, butter...", "Recipe");

        result.Success.Should().BeTrue();
        result.Score.Should().BeGreaterThanOrEqualTo(0.75m);
        result.KickToHuman.Should().BeFalse();
    }

    [Fact]
    public async Task OllamaProvider_ScoreForApproval_NotJson_KicksToHuman()
    {
        string ollamaJson = JsonSerializer.Serialize(
            new { response = "This is not valid JSON output", eval_count = 10 });
        Mock<HttpMessageHandler> handler = BuildHandler(HttpStatusCode.OK, ollamaJson);
        OllamaProvider provider = BuildOllamaProvider(handler.Object);

        AIApprovalResult result = await provider.ScoreForApprovalAsync("some content", "Recipe");

        result.Success.Should().BeFalse();
        result.KickToHuman.Should().BeTrue();
        result.ErrorMessage.Should().Be("Could not parse AI response");
    }

    [Fact]
    public async Task OllamaProvider_ScoreForApproval_OllamaDown_KicksToHuman()
    {
        Mock<HttpMessageHandler> handler = BuildHandler(HttpStatusCode.ServiceUnavailable);
        OllamaProvider provider = BuildOllamaProvider(handler.Object);

        AIApprovalResult result = await provider.ScoreForApprovalAsync("content", "Product");

        result.Success.Should().BeFalse();
        result.KickToHuman.Should().BeTrue();
    }

    [Fact]
    public async Task OllamaProvider_ScoreForApproval_BorderlineScore_KicksToHuman()
    {
        string approvalJson = JsonSerializer.Serialize(
            new { score = 0.50, reasoning = "Borderline", kick_to_human = true });
        string ollamaJson = JsonSerializer.Serialize(new { response = approvalJson, eval_count = 10 });
        Mock<HttpMessageHandler> handler = BuildHandler(HttpStatusCode.OK, ollamaJson);
        OllamaProvider provider = BuildOllamaProvider(handler.Object);

        AIApprovalResult result = await provider.ScoreForApprovalAsync("borderline content", "Recipe");

        result.Success.Should().BeTrue();
        result.KickToHuman.Should().BeTrue();
        result.Score.Should().Be(0.50m);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // OllamaProvider — ClassifyAsync
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task OllamaProvider_ClassifyAsync_MatchingClass_ReturnsMatch()
    {
        string ollamaJson = JsonSerializer.Serialize(new { response = "Dessert", eval_count = 5 });
        Mock<HttpMessageHandler> handler = BuildHandler(HttpStatusCode.OK, ollamaJson);
        OllamaProvider provider = BuildOllamaProvider(handler.Object);

        AIClassifyResult result = await provider.ClassifyAsync(
            "What type of recipe is this?",
            ["Breakfast", "Lunch", "Dinner", "Dessert"]);

        result.Success.Should().BeTrue();
        result.ChosenClass.Should().Be("Dessert");
        result.Confidence.Should().BeGreaterThan(0m);
    }

    [Fact]
    public async Task OllamaProvider_ClassifyAsync_NoMatch_FallsBackToFirst()
    {
        string ollamaJson = JsonSerializer.Serialize(new { response = "Unknown category", eval_count = 5 });
        Mock<HttpMessageHandler> handler = BuildHandler(HttpStatusCode.OK, ollamaJson);
        OllamaProvider provider = BuildOllamaProvider(handler.Object);

        AIClassifyResult result = await provider.ClassifyAsync(
            "Classify this",
            ["Breakfast", "Lunch", "Dinner"]);

        result.Success.Should().BeTrue();
        result.ChosenClass.Should().Be("Breakfast", "first class is the fallback");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ClaudeProvider — local mode vs cloud
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ClaudeProvider_LocalMode_ReturnsMockWithoutHttpCall()
    {
        ClaudeProvider provider = new(LocalMode(true));

        AITextResult result = await provider.GenerateAsync("prompt");

        result.Success.Should().BeTrue();
        result.Text.Should().Contain("mock");
        result.ProviderName.Should().Be("Claude");
    }

    [Fact]
    public async Task ClaudeProvider_CloudMode_ThrowsNotImplementedException()
    {
        ClaudeProvider provider = new(LocalMode(false));

        Func<Task> act = () => provider.GenerateAsync("prompt");

        await act.Should().ThrowAsync<NotImplementedException>();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // AIProviderFactory — use-case routing
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AIProviderFactory_RecipeApprovalUseCase_ReturnsOllama()
    {
        Mock<IAIProviderConfigRepository> configRepo = new();
        configRepo.Setup(r => r.GetConfigAsync("recipe-approval", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIProviderConfigDto { UseCase = "recipe-approval", Provider = "Ollama" });

        Mock<IHttpClientFactory> httpMock = new();
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
                { ["AI:Ollama:BaseUrl"] = "http://localhost:11434" })
            .Build();

        OllamaProvider ollamaProvider = new(httpMock.Object, config,
            NullLogger<OllamaProvider>.Instance);

        AIProviderFactory factory = new(
            [ollamaProvider],
            BuildHybridCache(),
            configRepo.Object);

        IAIProvider provider = await factory.GetProviderForUseCaseAsync("recipe-approval");

        provider.ProviderName.Should().Be("Ollama");
    }

    [Fact]
    public async Task AIProviderFactory_UnknownUseCase_FallsBackToGlobalThenOllama()
    {
        Mock<IAIProviderConfigRepository> configRepo = new();
        // Unknown use case returns null
        configRepo.Setup(r => r.GetConfigAsync("unknown-use-case", It.IsAny<CancellationToken>()))
            .ReturnsAsync((AIProviderConfigDto?)null);
        // global config also returns null → ultimate fallback to Ollama
        configRepo.Setup(r => r.GetConfigAsync("global", It.IsAny<CancellationToken>()))
            .ReturnsAsync((AIProviderConfigDto?)null);

        Mock<IHttpClientFactory> httpMock = new();
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
                { ["AI:Ollama:BaseUrl"] = "http://localhost:11434" })
            .Build();

        OllamaProvider ollamaProvider = new(httpMock.Object, config,
            NullLogger<OllamaProvider>.Instance);

        AIProviderFactory factory = new(
            [ollamaProvider],
            BuildHybridCache(),
            configRepo.Object);

        IAIProvider provider = await factory.GetProviderForUseCaseAsync("unknown-use-case");

        provider.ProviderName.Should().Be("Ollama");
    }

    [Fact]
    public async Task AIProviderFactory_GlobalConfigReturnsOllama_UsesOllama()
    {
        Mock<IAIProviderConfigRepository> configRepo = new();
        configRepo.Setup(r => r.GetConfigAsync("product-review", It.IsAny<CancellationToken>()))
            .ReturnsAsync((AIProviderConfigDto?)null);
        configRepo.Setup(r => r.GetConfigAsync("global", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIProviderConfigDto { UseCase = "global", Provider = "Ollama" });

        Mock<IHttpClientFactory> httpMock = new();
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
                { ["AI:Ollama:BaseUrl"] = "http://localhost:11434" })
            .Build();

        OllamaProvider ollamaProvider = new(httpMock.Object, config,
            NullLogger<OllamaProvider>.Instance);

        AIProviderFactory factory = new(
            [ollamaProvider],
            BuildHybridCache(),
            configRepo.Object);

        IAIProvider provider = await factory.GetProviderForUseCaseAsync("product-review");

        provider.ProviderName.Should().Be("Ollama");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ApprovalQueueService — mode routing
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ApprovalQueueService_AIFirst_HighScore_AutoApproves()
    {
        // Arrange
        Guid entityId = Guid.NewGuid();
        ApprovalConfigDto config = new()
        {
            EntityType = "Recipe", Mode = "AIFirst",
            AIConfidenceMin = 0.75m, HumanTimeoutMins = 120
        };

        Mock<IApprovalQueueRepository> queueMock = new();
        queueMock.Setup(q => q.GetApprovalConfigAsync("Recipe", It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        Mock<IAIProvider> aiMock = new();
        aiMock.Setup(a => a.ScoreForApprovalAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<AIRequestOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIApprovalResult { Success = true, Score = 0.95m, KickToHuman = false });

        Mock<IAIProviderFactory> factoryMock = new();
        factoryMock.Setup(f => f.GetProviderForUseCaseAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(aiMock.Object);

        Mock<IHttpClientFactory> httpMock = new();
        ApprovalQueueService svc = new(factoryMock.Object, queueMock.Object,
            httpMock.Object, NullLogger<ApprovalQueueService>.Instance);

        // Act
        await svc.SubmitForApprovalAsync(entityId, "Recipe", "Great cookie recipe");

        // Assert
        queueMock.Verify(q => q.ApproveAsync(entityId, "Recipe", It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
        queueMock.Verify(q => q.RejectAsync(It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        queueMock.Verify(q => q.MoveToHumanQueueAsync(It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ApprovalQueueService_AIFirst_LowScore_KicksToHuman()
    {
        Guid entityId = Guid.NewGuid();
        ApprovalConfigDto config = new()
        {
            EntityType = "Recipe", Mode = "AIFirst",
            AIConfidenceMin = 0.75m, HumanTimeoutMins = 120
        };

        Mock<IApprovalQueueRepository> queueMock = new();
        queueMock.Setup(q => q.GetApprovalConfigAsync("Recipe", It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        Mock<IAIProvider> aiMock = new();
        aiMock.Setup(a => a.ScoreForApprovalAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<AIRequestOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIApprovalResult
            {
                Success = true, Score = 0.55m, KickToHuman = false,
                Reasoning = "Content quality is borderline"
            });

        Mock<IAIProviderFactory> factoryMock = new();
        factoryMock.Setup(f => f.GetProviderForUseCaseAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(aiMock.Object);

        Mock<IHttpClientFactory> httpMock = new();
        ApprovalQueueService svc = new(factoryMock.Object, queueMock.Object,
            httpMock.Object, NullLogger<ApprovalQueueService>.Instance);

        await svc.SubmitForApprovalAsync(entityId, "Recipe", "Mediocre recipe");

        queueMock.Verify(q => q.RejectAsync(entityId, "Recipe",
            It.Is<string>(r => r.Length > 0), It.IsAny<CancellationToken>()), Times.Once);
        queueMock.Verify(q => q.ApproveAsync(It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ApprovalQueueService_HumanFirst_NotifiesModeratorsNoAI()
    {
        Guid entityId = Guid.NewGuid();
        ApprovalConfigDto config = new()
        {
            EntityType = "Product", Mode = "HumanFirst",
            AIConfidenceMin = 0.75m, HumanTimeoutMins = 120
        };

        Mock<IApprovalQueueRepository> queueMock = new();
        queueMock.Setup(q => q.GetApprovalConfigAsync("Product", It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        Mock<IAIProviderFactory> factoryMock = new();
        Mock<IAIProvider> aiMock = new();

        // Build a fake HTTP message handler for the notification call
        Mock<HttpMessageHandler> notifHandler = new();
        notifHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        Mock<IHttpClientFactory> httpMock = new();
        httpMock.Setup(f => f.CreateClient("NotificationService"))
            .Returns(new HttpClient(notifHandler.Object)
            {
                BaseAddress = new Uri("http://notification-service")
            });

        ApprovalQueueService svc = new(factoryMock.Object, queueMock.Object,
            httpMock.Object, NullLogger<ApprovalQueueService>.Instance);

        await svc.SubmitForApprovalAsync(entityId, "Product", "New product submission");

        // AI should NOT be called for HumanFirst
        factoryMock.Verify(f => f.GetProviderForUseCaseAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

        // Notification should be sent
        notifHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task ApprovalQueueService_AIUncertain_KicksToHumanQueue()
    {
        Guid entityId = Guid.NewGuid();

        Mock<IApprovalQueueRepository> queueMock = new();
        queueMock.Setup(q => q.GetApprovalConfigAsync("Recipe", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApprovalConfigDto
            {
                EntityType = "Recipe", Mode = "AIFirst", AIConfidenceMin = 0.75m
            });

        Mock<IAIProvider> aiMock = new();
        aiMock.Setup(a => a.ScoreForApprovalAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<AIRequestOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIApprovalResult
            {
                Success = false, KickToHuman = true,
                ErrorMessage = "Could not parse AI response"
            });

        Mock<IAIProviderFactory> factoryMock = new();
        factoryMock.Setup(f => f.GetProviderForUseCaseAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(aiMock.Object);

        Mock<IHttpClientFactory> httpMock = new();
        ApprovalQueueService svc = new(factoryMock.Object, queueMock.Object,
            httpMock.Object, NullLogger<ApprovalQueueService>.Instance);

        await svc.SubmitForApprovalAsync(entityId, "Recipe", "Content that AI cannot parse");

        queueMock.Verify(q => q.MoveToHumanQueueAsync(entityId, "Recipe",
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApprovalQueueService_ProcessPendingAI_TriggersAIForTimedOut()
    {
        List<PendingApprovalDto> timedOutItems =
        [
            new() { EntityId = Guid.NewGuid(), EntityType = "Recipe", Content = "Recipe content" }
        ];

        Mock<IApprovalQueueRepository> queueMock = new();
        queueMock.Setup(q => q.GetHumanTimedOutItemsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(timedOutItems);
        queueMock.Setup(q => q.GetApprovalConfigAsync("Recipe", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApprovalConfigDto
            {
                EntityType = "Recipe", Mode = "HumanFirst", AIConfidenceMin = 0.75m
            });

        Mock<IAIProvider> aiMock = new();
        aiMock.Setup(a => a.ScoreForApprovalAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<AIRequestOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIApprovalResult { Success = true, Score = 0.90m, KickToHuman = false });

        Mock<IAIProviderFactory> factoryMock = new();
        factoryMock.Setup(f => f.GetProviderForUseCaseAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(aiMock.Object);

        Mock<IHttpClientFactory> httpMock = new();
        ApprovalQueueService svc = new(factoryMock.Object, queueMock.Object,
            httpMock.Object, NullLogger<ApprovalQueueService>.Instance);

        await svc.ProcessPendingAIAsync();

        // AI should be called for the timed-out item
        aiMock.Verify(a => a.ScoreForApprovalAsync(
            "Recipe content", "Recipe", null, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // LocalModeConfig
    // ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("true",  true)]
    [InlineData("True",  true)]
    [InlineData("TRUE",  true)]
    [InlineData("false", false)]
    [InlineData("",      false)]
    [InlineData(null,    false)]
    public void LocalModeConfig_ParsesAppLocalModeFlag(string? value, bool expected)
    {
        Dictionary<string, string?> settings = [];
        if (value is not null)
        {
            settings["APP_LOCAL_MODE"] = value;
        }

        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        LocalModeConfig lm = new(config);

        lm.IsLocalMode.Should().Be(expected);
    }
}
