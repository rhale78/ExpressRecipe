using ExpressRecipe.Messaging.Core.Options;
using ExpressRecipe.Messaging.RabbitMQ.Internal;

namespace ExpressRecipe.Messaging.Tests.Internal;

public class NamingConventionsTests
{
    private const string Prefix = "expressrecipe";

    [Theory]
    [InlineData(RoutingMode.Broadcast, "expressrecipe.broadcast.simpletestmessage")]
    [InlineData(RoutingMode.CompetingConsumer, "expressrecipe.work.simpletestmessage")]
    [InlineData(RoutingMode.Direct, "expressrecipe.direct.simpletestmessage")]
    public void GetExchangeName_ReturnsExpectedName(RoutingMode mode, string expected)
    {
        var result = NamingConventions.GetExchangeName(Prefix, typeof(SimpleTestMessage), mode);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetExchangeName_ServiceName_UsesServiceName()
    {
        var result = NamingConventions.GetExchangeName(Prefix, typeof(SimpleTestMessage), RoutingMode.ServiceName, "my-service");
        Assert.Equal("expressrecipe.service.my-service", result);
    }

    [Theory]
    [InlineData(RoutingMode.Broadcast, "expressrecipe.broadcast.simpletestmessage.queue")]
    [InlineData(RoutingMode.CompetingConsumer, "expressrecipe.work.simpletestmessage.queue")]
    [InlineData(RoutingMode.Direct, "expressrecipe.direct.simpletestmessage.queue")]
    public void GetQueueName_NoServiceName_ReturnsExpectedName(RoutingMode mode, string expected)
    {
        var result = NamingConventions.GetQueueName(Prefix, typeof(SimpleTestMessage), mode);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetQueueName_Broadcast_WithServiceName_IncludesServiceName()
    {
        var result = NamingConventions.GetQueueName(Prefix, typeof(SimpleTestMessage), RoutingMode.Broadcast, "my-svc");
        Assert.Equal("expressrecipe.broadcast.simpletestmessage.my-svc.queue", result);
    }

    [Fact]
    public void GetDeadLetterExchangeName_AppendsSuffix()
    {
        var result = NamingConventions.GetDeadLetterExchangeName("expressrecipe.work.order", ".dlx");
        Assert.Equal("expressrecipe.work.order.dlx", result);
    }

    [Fact]
    public void GetReplyQueueName_ReturnsExpectedFormat()
    {
        var instanceId = "abc123";
        var result = NamingConventions.GetReplyQueueName(Prefix, instanceId);
        Assert.Equal("expressrecipe.reply.abc123", result);
    }

    [Theory]
    [InlineData(RoutingMode.Broadcast, "fanout")]
    [InlineData(RoutingMode.CompetingConsumer, "direct")]
    [InlineData(RoutingMode.Direct, "direct")]
    [InlineData(RoutingMode.ServiceName, "topic")]
    public void GetExchangeType_ReturnsExpectedType(RoutingMode mode, string expected)
    {
        var result = NamingConventions.GetExchangeType(mode);
        Assert.Equal(expected, result);
    }

    private sealed class SimpleTestMessage : Core.Abstractions.IMessage { }
}
