using ExpressRecipe.Messaging.Core.Abstractions;
using ExpressRecipe.Messaging.Core.Messages;
using ExpressRecipe.Messaging.Core.Options;
using ExpressRecipe.Messaging.Core.Serialization;
using ExpressRecipe.Messaging.RabbitMQ;
using ExpressRecipe.Messaging.RabbitMQ.Internal;
using ExpressRecipe.Messaging.RabbitMQ.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RabbitMQ.Client;
using MsOptions = Microsoft.Extensions.Options;

namespace ExpressRecipe.Messaging.Tests.RabbitMq;

/// <summary>
/// Unit tests for <see cref="RabbitMqMessageBus"/> using mocked RabbitMQ objects.
/// </summary>
public class RabbitMqMessageBusTests
{
    private static RabbitMqMessageBus CreateBus(
        IConnection? connection = null,
        IMessageSerializer? serializer = null,
        RabbitMqMessagingOptions? options = null,
        SubscriptionRegistry? registry = null)
    {
        var mockConnection = connection ?? CreateMockConnection();
        var ser = serializer ?? new JsonMessageSerializer();
        var opts = MsOptions.Options.Create(options ?? new RabbitMqMessagingOptions { ServiceName = "test-service" });
        var reg = registry ?? new SubscriptionRegistry();
        var logger = NullLogger<RabbitMqMessageBus>.Instance;
        return new RabbitMqMessageBus(mockConnection, ser, opts, reg, logger);
    }

    private static IConnection CreateMockConnection()
    {
        var mockChannel = new Mock<IChannel>();
        mockChannel.Setup(c => c.IsOpen).Returns(true);
        mockChannel.Setup(c => c.ExchangeDeclareAsync(
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<bool>(), It.IsAny<bool>(),
            It.IsAny<IDictionary<string, object?>>(),
            It.IsAny<bool>(), It.IsAny<bool>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockChannel.Setup(c => c.BasicPublishAsync(
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<bool>(), It.IsAny<BasicProperties>(),
            It.IsAny<ReadOnlyMemory<byte>>(),
            It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        mockChannel.Setup(c => c.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var mockConnection = new Mock<IConnection>();
        mockConnection.Setup(c => c.CreateChannelAsync(
            It.IsAny<CreateChannelOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockChannel.Object);

        return mockConnection.Object;
    }

    [Fact]
    public async Task SubscribeAsync_Delegate_AddsRegistration()
    {
        var registry = new SubscriptionRegistry();
        await using var bus = CreateBus(registry: registry);

        await bus.SubscribeAsync<TestMsg>(
            (msg, ctx, ct) => Task.CompletedTask,
            new SubscribeOptions { RoutingMode = RoutingMode.CompetingConsumer });

        var registrations = registry.GetAll();
        Assert.Single(registrations);
        Assert.Equal(typeof(TestMsg), registrations[0].MessageType);
    }

    [Fact]
    public async Task SubscribeRequestAsync_Delegate_AddsRegistration()
    {
        var registry = new SubscriptionRegistry();
        await using var bus = CreateBus(registry: registry);

        await bus.SubscribeRequestAsync<TestMsg, TestResponseMsg>(
            (msg, ctx, ct) => Task.FromResult(new TestResponseMsg("ok")));

        var registrations = registry.GetAll();
        Assert.Single(registrations);
        Assert.True(registrations[0].IsRequestHandler);
        Assert.Equal(typeof(TestResponseMsg), registrations[0].ResponseType);
    }

    [Fact]
    public async Task PublishAsync_InvokesMockChannel_WithCorrectExchange()
    {
        var mockChannel = new Mock<IChannel>();
        mockChannel.Setup(c => c.IsOpen).Returns(true);
        mockChannel.Setup(c => c.ExchangeDeclareAsync(
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<bool>(), It.IsAny<bool>(),
            It.IsAny<IDictionary<string, object?>>(),
            It.IsAny<bool>(), It.IsAny<bool>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockChannel.Setup(c => c.BasicPublishAsync(
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<bool>(), It.IsAny<BasicProperties>(),
            It.IsAny<ReadOnlyMemory<byte>>(),
            It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        mockChannel.Setup(c => c.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var mockConn = new Mock<IConnection>();
        mockConn.Setup(c => c.CreateChannelAsync(
            It.IsAny<CreateChannelOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockChannel.Object);

        await using var bus = CreateBus(connection: mockConn.Object);

        await bus.PublishAsync(new TestMsg("hello"),
            new PublishOptions { RoutingMode = RoutingMode.Broadcast });

        mockChannel.Verify(c => c.BasicPublishAsync(
            It.Is<string>(e => e.Contains("broadcast")),
            It.IsAny<string>(),
            It.IsAny<bool>(),
            It.IsAny<BasicProperties>(),
            It.IsAny<ReadOnlyMemory<byte>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendAsync_DirectsToNamedQueue()
    {
        var mockChannel = new Mock<IChannel>();
        mockChannel.Setup(c => c.IsOpen).Returns(true);
        mockChannel.Setup(c => c.BasicPublishAsync(
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<bool>(), It.IsAny<BasicProperties>(),
            It.IsAny<ReadOnlyMemory<byte>>(),
            It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        mockChannel.Setup(c => c.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var mockConn = new Mock<IConnection>();
        mockConn.Setup(c => c.CreateChannelAsync(
            It.IsAny<CreateChannelOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockChannel.Object);

        await using var bus = CreateBus(connection: mockConn.Object);
        await bus.SendAsync(new TestMsg("direct"), "my-specific-queue");

        mockChannel.Verify(c => c.BasicPublishAsync(
            string.Empty,
            "my-specific-queue",
            It.IsAny<bool>(),
            It.IsAny<BasicProperties>(),
            It.IsAny<ReadOnlyMemory<byte>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private sealed record TestMsg(string Text) : IMessage;
    private sealed record TestResponseMsg(string Result) : IMessage;
}
