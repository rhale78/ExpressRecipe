using ExpressRecipe.Messaging.Core.Abstractions;
using ExpressRecipe.Messaging.Core.Messages;
using ExpressRecipe.Messaging.Core.Options;
using ExpressRecipe.PreferencesService.Data;
using ExpressRecipe.PreferencesService.Services;
using ExpressRecipe.Shared.Messages;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ExpressRecipe.PreferencesService.Tests.Services;

public class HouseholdMemberSubscriberGdprTests
{
    private readonly Mock<IMessageBus> _bus;
    private readonly Mock<ICookProfileRepository> _repo;
    private readonly HouseholdMemberSubscriber _subscriber;
    private readonly IServiceProvider _serviceProvider;

    public HouseholdMemberSubscriberGdprTests()
    {
        _bus  = new Mock<IMessageBus>();
        _repo = new Mock<ICookProfileRepository>();

        var services = new ServiceCollection();
        services.AddSingleton(_repo.Object);
        _serviceProvider = services.BuildServiceProvider();

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(f => f.CreateScope()).Returns(() =>
        {
            var scope = new Mock<IServiceScope>();
            scope.Setup(s => s.ServiceProvider).Returns(_serviceProvider);
            return scope.Object;
        });

        _subscriber = new HouseholdMemberSubscriber(
            _bus.Object,
            scopeFactory.Object,
            new Mock<ILogger<HouseholdMemberSubscriber>>().Object);
    }

    // ──────────────────────── StartAsync ────────────────────────────────────

    [Fact]
    public async Task StartAsync_RegistersSubscriptionForMemberGdprDeleteEvent()
    {
        await _subscriber.StartAsync(CancellationToken.None);

        _bus.Verify(b => b.SubscribeAsync(
            It.IsAny<Func<MemberGdprDeleteEvent, MessageContext, CancellationToken, Task>>(),
            It.IsAny<SubscribeOptions?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ──────────────────────── MemberGdprDeleteEvent handling ─────────────────

    [Fact]
    public async Task HandleMemberGdprDelete_CallsDeleteMemberDataAsync()
    {
        // Arrange
        Guid memberId  = Guid.NewGuid();
        Guid userId    = Guid.NewGuid();
        Guid requestId = Guid.NewGuid();

        _repo.Setup(r => r.DeleteMemberDataAsync(memberId, It.IsAny<CancellationToken>()))
             .Returns(Task.CompletedTask);

        Func<MemberGdprDeleteEvent, MessageContext, CancellationToken, Task>? handler = null;
        _bus.Setup(b => b.SubscribeAsync(
            It.IsAny<Func<MemberGdprDeleteEvent, MessageContext, CancellationToken, Task>>(),
            It.IsAny<SubscribeOptions?>(),
            It.IsAny<CancellationToken>()))
            .Callback<Func<MemberGdprDeleteEvent, MessageContext, CancellationToken, Task>,
                      SubscribeOptions?, CancellationToken>(
                (h, _, _) => handler = h);

        await _subscriber.StartAsync(CancellationToken.None);

        MemberGdprDeleteEvent evt = new MemberGdprDeleteEvent(memberId, userId, requestId, DateTimeOffset.UtcNow);
        await handler!.Invoke(evt, new MessageContext { MessageId = Guid.NewGuid().ToString(), MessageType = "GdprEvent" }, CancellationToken.None);

        // Assert
        _repo.Verify(r => r.DeleteMemberDataAsync(memberId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleMemberGdprDelete_WhenRepositoryThrows_DoesNotRethrow()
    {
        // Arrange
        Guid memberId  = Guid.NewGuid();
        Guid userId    = Guid.NewGuid();
        Guid requestId = Guid.NewGuid();

        _repo.Setup(r => r.DeleteMemberDataAsync(memberId, It.IsAny<CancellationToken>()))
             .ThrowsAsync(new InvalidOperationException("db error"));

        Func<MemberGdprDeleteEvent, MessageContext, CancellationToken, Task>? handler = null;
        _bus.Setup(b => b.SubscribeAsync(
            It.IsAny<Func<MemberGdprDeleteEvent, MessageContext, CancellationToken, Task>>(),
            It.IsAny<SubscribeOptions?>(),
            It.IsAny<CancellationToken>()))
            .Callback<Func<MemberGdprDeleteEvent, MessageContext, CancellationToken, Task>,
                      SubscribeOptions?, CancellationToken>(
                (h, _, _) => handler = h);

        await _subscriber.StartAsync(CancellationToken.None);

        MemberGdprDeleteEvent evt = new MemberGdprDeleteEvent(memberId, userId, requestId, DateTimeOffset.UtcNow);

        Func<Task> act = () => handler!.Invoke(evt, new MessageContext { MessageId = Guid.NewGuid().ToString(), MessageType = "GdprEvent" }, CancellationToken.None);
        await act.Should().NotThrowAsync();
    }
}
