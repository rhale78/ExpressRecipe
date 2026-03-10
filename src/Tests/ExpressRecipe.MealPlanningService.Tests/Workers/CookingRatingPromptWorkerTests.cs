using System.Net;
using ExpressRecipe.MealPlanningService.Data;
using ExpressRecipe.MealPlanningService.Workers;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ExpressRecipe.MealPlanningService.Tests.Workers;

public class CookingRatingPromptWorkerTests
{
    private readonly Mock<IMealPlanningRepository> _repoMock = new();

    private CookingRatingPromptWorker CreateWorker(HttpMessageHandler handler)
    {
        Mock<IServiceProvider> providerMock    = new();
        Mock<IServiceScope>    scopeMock       = new();
        Mock<IHttpClientFactory> httpFactoryMock = new();

        providerMock.Setup(p => p.GetService(typeof(IMealPlanningRepository))).Returns(_repoMock.Object);
        providerMock.Setup(p => p.GetService(typeof(IHttpClientFactory))).Returns(httpFactoryMock.Object);

        // IServiceProvider itself must return IServiceScopeFactory so CreateScope can work via extension methods
        Mock<IServiceScopeFactory> scopeFactoryMock = new();
        scopeMock.Setup(s => s.ServiceProvider).Returns(providerMock.Object);
        scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);
        providerMock.Setup(p => p.GetService(typeof(IServiceScopeFactory))).Returns(scopeFactoryMock.Object);

        HttpClient httpClient = new(handler) { BaseAddress = new Uri("http://notification-service") };
        httpFactoryMock.Setup(f => f.CreateClient("NotificationService")).Returns(httpClient);

        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Services:NotificationService"] = "http://notification-service"
            })
            .Build();

        return new CookingRatingPromptWorker(
            providerMock.Object,
            config,
            NullLogger<CookingRatingPromptWorker>.Instance);
    }

    private static Task InvokeProcessAsync(CookingRatingPromptWorker worker, CancellationToken ct = default)
    {
        System.Reflection.MethodInfo method = typeof(CookingRatingPromptWorker)
            .GetMethod("ProcessRatingPromptsAsync",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        return (Task)method.Invoke(worker, new object[] { ct })!;
    }

    [Fact]
    public async Task ProcessRatingPrompts_WhenNoUnratedHistory_SendsNoNotifications()
    {
        _repoMock.Setup(r => r.GetUnratedCookingHistoryAsync(It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<RatingPromptRow>());

        int httpCallCount = 0;
        CookingRatingPromptWorker worker = CreateWorker(new FakeHttpMessageHandler(_ =>
        {
            httpCallCount++;
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));

        await InvokeProcessAsync(worker);

        httpCallCount.Should().Be(0);
        _repoMock.Verify(r => r.MarkRatingPromptSentAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessRatingPrompts_WithRows_SendsNotificationForEachAndMarksAsSent()
    {
        List<RatingPromptRow> rows = new()
        {
            new RatingPromptRow { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), RecipeName = "Pasta" },
            new RatingPromptRow { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), RecipeName = "Steak" }
        };

        _repoMock.Setup(r => r.GetUnratedCookingHistoryAsync(It.IsAny<CancellationToken>()))
                 .ReturnsAsync(rows);
        _repoMock.Setup(r => r.MarkRatingPromptSentAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);

        CookingRatingPromptWorker worker = CreateWorker(new FakeHttpMessageHandler(HttpStatusCode.OK));

        await InvokeProcessAsync(worker);

        foreach (RatingPromptRow row in rows)
        {
            _repoMock.Verify(r => r.MarkRatingPromptSentAsync(row.Id, It.IsAny<CancellationToken>()), Times.Once);
        }
    }

    [Fact]
    public async Task ProcessRatingPrompts_WhenNotificationServiceReturnsError_DoesNotMarkSent()
    {
        RatingPromptRow row = new() { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), RecipeName = "Fish" };

        _repoMock.Setup(r => r.GetUnratedCookingHistoryAsync(It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<RatingPromptRow> { row });

        CookingRatingPromptWorker worker = CreateWorker(
            new FakeHttpMessageHandler(HttpStatusCode.InternalServerError));

        await InvokeProcessAsync(worker);

        _repoMock.Verify(r => r.MarkRatingPromptSentAsync(row.Id, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessRatingPrompts_WhenNotificationServiceThrows_ContinuesToNextRow()
    {
        RatingPromptRow row1 = new() { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), RecipeName = "Soup" };
        RatingPromptRow row2 = new() { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), RecipeName = "Pizza" };

        _repoMock.Setup(r => r.GetUnratedCookingHistoryAsync(It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<RatingPromptRow> { row1, row2 });
        _repoMock.Setup(r => r.MarkRatingPromptSentAsync(row2.Id, It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);

        int calls = 0;
        CookingRatingPromptWorker worker = CreateWorker(new FakeHttpMessageHandler(_ =>
        {
            calls++;
            if (calls == 1) throw new HttpRequestException("Network error");
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));

        await InvokeProcessAsync(worker);

        // row1 threw, should not be marked
        _repoMock.Verify(r => r.MarkRatingPromptSentAsync(row1.Id, It.IsAny<CancellationToken>()), Times.Never);
        // row2 succeeded
        _repoMock.Verify(r => r.MarkRatingPromptSentAsync(row2.Id, It.IsAny<CancellationToken>()), Times.Once);
    }
}
