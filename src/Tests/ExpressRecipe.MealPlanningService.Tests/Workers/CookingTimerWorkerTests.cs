using FluentAssertions;
using Moq;
using Microsoft.Extensions.Logging;
using System.Net;
using ExpressRecipe.MealPlanningService.Data;
using ExpressRecipe.MealPlanningService.Services;
using ExpressRecipe.MealPlanningService.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace ExpressRecipe.MealPlanningService.Tests.Workers;

/// <summary>Minimal fake HTTP message handler for testing.</summary>
internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

    public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        => _responder = responder;

    public FakeHttpMessageHandler(HttpStatusCode status = HttpStatusCode.OK)
        : this(_ => new HttpResponseMessage(status)) { }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(_responder(request));
}

public class CookingTimerWorkerTests
{
    private readonly Mock<ICookingTimerRepository> _mockRepository;
    private readonly Mock<ILogger<CookingTimerWorker>> _mockLogger;
    private readonly Guid _testUserId;

    public CookingTimerWorkerTests()
    {
        _mockRepository = new Mock<ICookingTimerRepository>();
        _mockLogger = new Mock<ILogger<CookingTimerWorker>>();
        _testUserId = Guid.NewGuid();
    }

    private CookingTimerWorker CreateWorker(HttpMessageHandler? handler = null)
    {
        HttpClient client = new(handler ?? new FakeHttpMessageHandler())
        {
            BaseAddress = new Uri("http://notification-service")
        };

        Mock<IHttpClientFactory> httpFactory = new();
        httpFactory.Setup(f => f.CreateClient("NotificationService")).Returns(client);

        Mock<IServiceScopeFactory> scopeFactory = new();
        Mock<IServiceScope> scope = new();
        Mock<IServiceProvider> scopedProvider = new();

        scopedProvider
            .Setup(p => p.GetService(typeof(ICookingTimerRepository)))
            .Returns(_mockRepository.Object);
        scope.Setup(s => s.ServiceProvider).Returns(scopedProvider.Object);
        scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);

        return new CookingTimerWorker(scopeFactory.Object, httpFactory.Object, _mockLogger.Object);
    }

    private static Task InvokeProcessExpiredAsync(CookingTimerWorker worker, CancellationToken ct = default)
    {
        System.Reflection.MethodInfo method = typeof(CookingTimerWorker)
            .GetMethod("ProcessExpiredTimersAsync",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        return (Task)method.Invoke(worker, new object[] { ct })!;
    }

    [Fact]
    public async Task ProcessExpiredTimers_WhenNoExpiredTimers_DoesNotCallNotificationService()
    {
        _mockRepository
            .Setup(r => r.GetExpiredUnnotifiedTimersAsync(default))
            .ReturnsAsync(new List<CookingTimerDto>());

        int httpCallCount = 0;
        CookingTimerWorker worker = CreateWorker(new FakeHttpMessageHandler(_ =>
        {
            httpCallCount++;
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));

        await InvokeProcessExpiredAsync(worker);

        httpCallCount.Should().Be(0);
        _mockRepository.Verify(r => r.MarkNotificationSentAsync(It.IsAny<Guid>(), default), Times.Never);
    }

    [Fact]
    public async Task ProcessExpiredTimers_WithExpiredTimer_SendsNotificationAndMarksAsSent()
    {
        CookingTimerDto expiredTimer = TestDataFactory.CreateExpiredUnnotifiedTimer(_testUserId);

        _mockRepository
            .Setup(r => r.GetExpiredUnnotifiedTimersAsync(default))
            .ReturnsAsync(new List<CookingTimerDto> { expiredTimer });
        _mockRepository
            .Setup(r => r.MarkNotificationSentAsync(expiredTimer.Id, default))
            .Returns(Task.CompletedTask);

        CookingTimerWorker worker = CreateWorker(new FakeHttpMessageHandler(HttpStatusCode.OK));

        await InvokeProcessExpiredAsync(worker);

        _mockRepository.Verify(r => r.MarkNotificationSentAsync(expiredTimer.Id, default), Times.Once);
    }

    [Fact]
    public async Task ProcessExpiredTimers_WithMultipleTimers_ProcessesAll()
    {
        CookingTimerDto timer1 = TestDataFactory.CreateExpiredUnnotifiedTimer(_testUserId, "Timer 1");
        CookingTimerDto timer2 = TestDataFactory.CreateExpiredUnnotifiedTimer(_testUserId, "Timer 2");

        _mockRepository
            .Setup(r => r.GetExpiredUnnotifiedTimersAsync(default))
            .ReturnsAsync(new List<CookingTimerDto> { timer1, timer2 });
        _mockRepository
            .Setup(r => r.MarkNotificationSentAsync(It.IsAny<Guid>(), default))
            .Returns(Task.CompletedTask);

        CookingTimerWorker worker = CreateWorker(new FakeHttpMessageHandler(HttpStatusCode.OK));

        await InvokeProcessExpiredAsync(worker);

        _mockRepository.Verify(r => r.MarkNotificationSentAsync(timer1.Id, default), Times.Once);
        _mockRepository.Verify(r => r.MarkNotificationSentAsync(timer2.Id, default), Times.Once);
    }

    [Fact]
    public async Task ProcessExpiredTimers_WhenNotificationFails_ContinuesWithRemainingTimers()
    {
        CookingTimerDto timer1 = TestDataFactory.CreateExpiredUnnotifiedTimer(_testUserId, "Timer 1");
        CookingTimerDto timer2 = TestDataFactory.CreateExpiredUnnotifiedTimer(_testUserId, "Timer 2");

        _mockRepository
            .Setup(r => r.GetExpiredUnnotifiedTimersAsync(default))
            .ReturnsAsync(new List<CookingTimerDto> { timer1, timer2 });
        _mockRepository
            .Setup(r => r.MarkNotificationSentAsync(timer2.Id, default))
            .Returns(Task.CompletedTask);

        int callCount = 0;
        CookingTimerWorker worker = CreateWorker(new FakeHttpMessageHandler(_ =>
        {
            callCount++;
            if (callCount == 1) throw new HttpRequestException("Connection refused");
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));

        await InvokeProcessExpiredAsync(worker);

        // timer1 failed so MarkNotificationSent never called for it; timer2 succeeded
        _mockRepository.Verify(r => r.MarkNotificationSentAsync(timer1.Id, default), Times.Never);
        _mockRepository.Verify(r => r.MarkNotificationSentAsync(timer2.Id, default), Times.Once);
    }

    [Fact]
    public async Task ProcessExpiredTimers_AlreadyNotified_NotProcessedAgain()
    {
        // A timer that was already processed (NotificationSent=true) should not appear in results
        // because GetExpiredUnnotifiedTimersAsync filters NotificationSent=0
        _mockRepository
            .Setup(r => r.GetExpiredUnnotifiedTimersAsync(default))
            .ReturnsAsync(new List<CookingTimerDto>());

        CookingTimerWorker worker = CreateWorker();

        await InvokeProcessExpiredAsync(worker);

        // Verify MarkNotificationSentAsync is never called (timer already handled)
        _mockRepository.Verify(r => r.MarkNotificationSentAsync(It.IsAny<Guid>(), default), Times.Never);
    }
}
