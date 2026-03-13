using ExpressRecipe.Messaging.Core.Abstractions;
using ExpressRecipe.Messaging.Core.Messages;
using ExpressRecipe.Messaging.Core.Options;
using ExpressRecipe.ProfileService.Data;
using ExpressRecipe.ProfileService.Services;
using ExpressRecipe.Shared.Messages;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ExpressRecipe.ProfileService.Tests.Services;

public class GdprEventSubscriberTests
{
    private readonly Mock<IMessageBus> _bus;
    private readonly Mock<IHouseholdMemberRepository> _repo;
    private readonly GdprEventSubscriber _subscriber;
    private readonly IServiceProvider _serviceProvider;

    public GdprEventSubscriberTests()
    {
        _bus  = new Mock<IMessageBus>();
        _repo = new Mock<IHouseholdMemberRepository>();

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

        _subscriber = new GdprEventSubscriber(
            _bus.Object,
            scopeFactory.Object,
            new Mock<ILogger<GdprEventSubscriber>>().Object);
    }

    // ──────────────────────── StartAsync ────────────────────────────────────

    [Fact]
    public async Task StartAsync_RegistersSubscriptionForGdprDeleteEvent()
    {
        await _subscriber.StartAsync(CancellationToken.None);

        _bus.Verify(b => b.SubscribeAsync(
            It.IsAny<Func<GdprDeleteEvent, MessageContext, CancellationToken, Task>>(),
            It.IsAny<SubscribeOptions?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ──────────────────────── HandleDeleteAsync ──────────────────────────────

    [Fact]
    public async Task HandleDelete_WhenMembersExist_UnlinksAndPublishesMemberGdprDeletePerMember()
    {
        // Arrange
        Guid userId    = Guid.NewGuid();
        Guid requestId = Guid.NewGuid();
        Guid memberId1 = Guid.NewGuid();
        Guid memberId2 = Guid.NewGuid();

        _repo.Setup(r => r.DeleteUserDataAsync(userId, It.IsAny<CancellationToken>()))
             .ReturnsAsync(new List<Guid> { memberId1, memberId2 });

        _bus.Setup(b => b.PublishAsync(It.IsAny<MemberGdprDeleteEvent>(),
                                       It.IsAny<PublishOptions?>(),
                                       It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _subscriber.StartAsync(CancellationToken.None);

        // Capture the registered handler
        Func<GdprDeleteEvent, MessageContext, CancellationToken, Task>? handler = null;
        _bus.Setup(b => b.SubscribeAsync(
            It.IsAny<Func<GdprDeleteEvent, MessageContext, CancellationToken, Task>>(),
            It.IsAny<SubscribeOptions?>(),
            It.IsAny<CancellationToken>()))
            .Callback<Func<GdprDeleteEvent, MessageContext, CancellationToken, Task>,
                      SubscribeOptions?, CancellationToken>(
                (h, _, _) => handler = h);

        await _subscriber.StartAsync(CancellationToken.None);

        GdprDeleteEvent evt = new GdprDeleteEvent(userId, requestId, DateTimeOffset.UtcNow);
        await handler!.Invoke(evt, new MessageContext { MessageId = Guid.NewGuid().ToString(), MessageType = "GdprEvent" }, CancellationToken.None);

        // Assert: one publish per affected member
        _bus.Verify(b => b.PublishAsync(
            It.Is<MemberGdprDeleteEvent>(e => e.MemberId == memberId1 && e.UserId == userId),
            It.IsAny<PublishOptions?>(), It.IsAny<CancellationToken>()), Times.Once);
        _bus.Verify(b => b.PublishAsync(
            It.Is<MemberGdprDeleteEvent>(e => e.MemberId == memberId2 && e.UserId == userId),
            It.IsAny<PublishOptions?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleDelete_WhenNoMembersLinked_PublishesNoMemberEvents()
    {
        // Arrange
        Guid userId    = Guid.NewGuid();
        Guid requestId = Guid.NewGuid();

        _repo.Setup(r => r.DeleteUserDataAsync(userId, It.IsAny<CancellationToken>()))
             .ReturnsAsync(new List<Guid>());

        Func<GdprDeleteEvent, MessageContext, CancellationToken, Task>? handler = null;
        _bus.Setup(b => b.SubscribeAsync(
            It.IsAny<Func<GdprDeleteEvent, MessageContext, CancellationToken, Task>>(),
            It.IsAny<SubscribeOptions?>(),
            It.IsAny<CancellationToken>()))
            .Callback<Func<GdprDeleteEvent, MessageContext, CancellationToken, Task>,
                      SubscribeOptions?, CancellationToken>(
                (h, _, _) => handler = h);

        await _subscriber.StartAsync(CancellationToken.None);

        GdprDeleteEvent evt = new GdprDeleteEvent(userId, requestId, DateTimeOffset.UtcNow);
        await handler!.Invoke(evt, new MessageContext { MessageId = Guid.NewGuid().ToString(), MessageType = "GdprEvent" }, CancellationToken.None);

        // Assert: no member-scoped events published
        _bus.Verify(b => b.PublishAsync(
            It.IsAny<MemberGdprDeleteEvent>(),
            It.IsAny<PublishOptions?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleDelete_WhenRepositoryThrows_DoesNotRethrow()
    {
        // Arrange
        Guid userId    = Guid.NewGuid();
        Guid requestId = Guid.NewGuid();

        _repo.Setup(r => r.DeleteUserDataAsync(userId, It.IsAny<CancellationToken>()))
             .ThrowsAsync(new InvalidOperationException("db error"));

        Func<GdprDeleteEvent, MessageContext, CancellationToken, Task>? handler = null;
        _bus.Setup(b => b.SubscribeAsync(
            It.IsAny<Func<GdprDeleteEvent, MessageContext, CancellationToken, Task>>(),
            It.IsAny<SubscribeOptions?>(),
            It.IsAny<CancellationToken>()))
            .Callback<Func<GdprDeleteEvent, MessageContext, CancellationToken, Task>,
                      SubscribeOptions?, CancellationToken>(
                (h, _, _) => handler = h);

        await _subscriber.StartAsync(CancellationToken.None);

        GdprDeleteEvent evt = new GdprDeleteEvent(userId, requestId, DateTimeOffset.UtcNow);

        // Act – should not throw
        Func<Task> act = () => handler!.Invoke(evt, new MessageContext { MessageId = Guid.NewGuid().ToString(), MessageType = "GdprEvent" }, CancellationToken.None);
        await act.Should().NotThrowAsync();
    }
}
