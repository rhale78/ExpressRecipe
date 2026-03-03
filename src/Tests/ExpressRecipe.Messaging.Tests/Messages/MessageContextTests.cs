using System.Diagnostics;
using ExpressRecipe.Messaging.Core.Messages;

namespace ExpressRecipe.Messaging.Tests.Messages;

public class MessageContextTests
{
    [Fact]
    public void MessageContext_RequiredProperties_AreSet()
    {
        var ctx = new MessageContext
        {
            MessageId = "msg-1",
            MessageType = "TestMessage"
        };

        Assert.Equal("msg-1", ctx.MessageId);
        Assert.Equal("TestMessage", ctx.MessageType);
        Assert.Null(ctx.CorrelationId);
        Assert.Null(ctx.Activity);
        Assert.Null(ctx.ReplyTo);
        Assert.NotNull(ctx.Headers);
        Assert.Empty(ctx.Headers);
    }

    [Fact]
    public void MessageContext_AllProperties_CanBeSet()
    {
        var now = DateTimeOffset.UtcNow;
        var headers = new Dictionary<string, string> { ["x-source"] = "test" };
        using var activity = new Activity("test-op").Start();

        var ctx = new MessageContext
        {
            MessageId = "msg-abc",
            CorrelationId = "corr-xyz",
            MessageType = "MyMessage",
            Timestamp = now,
            Headers = headers,
            Activity = activity,
            ReplyTo = "reply-queue-1"
        };

        Assert.Equal("msg-abc", ctx.MessageId);
        Assert.Equal("corr-xyz", ctx.CorrelationId);
        Assert.Equal("MyMessage", ctx.MessageType);
        Assert.Equal(now, ctx.Timestamp);
        Assert.Equal("test", ctx.Headers["x-source"]);
        Assert.Same(activity, ctx.Activity);
        Assert.Equal("reply-queue-1", ctx.ReplyTo);
    }
}
