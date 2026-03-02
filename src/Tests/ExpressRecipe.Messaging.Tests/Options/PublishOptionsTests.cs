using ExpressRecipe.Messaging.Core.Options;

namespace ExpressRecipe.Messaging.Tests.Options;

public class PublishOptionsTests
{
    [Fact]
    public void Default_Values_AreCorrect()
    {
        var opts = new PublishOptions();
        Assert.Equal(RoutingMode.Broadcast, opts.RoutingMode);
        Assert.True(opts.Persistent);
        Assert.Null(opts.Ttl);
        Assert.Null(opts.CorrelationId);
        Assert.Null(opts.Headers);
        Assert.Null(opts.Priority);
        Assert.Null(opts.MessageId);
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        var opts = new PublishOptions
        {
            RoutingMode = RoutingMode.CompetingConsumer,
            Persistent = false,
            Ttl = TimeSpan.FromMinutes(1),
            CorrelationId = "corr-1",
            Priority = 5,
            MessageId = "msg-123",
            Headers = new Dictionary<string, string> { ["k"] = "v" }
        };

        Assert.Equal(RoutingMode.CompetingConsumer, opts.RoutingMode);
        Assert.False(opts.Persistent);
        Assert.Equal(TimeSpan.FromMinutes(1), opts.Ttl);
        Assert.Equal("corr-1", opts.CorrelationId);
        Assert.Equal((byte)5, opts.Priority);
        Assert.Equal("msg-123", opts.MessageId);
        Assert.Equal("v", opts.Headers["k"]);
    }
}

public class SubscribeOptionsTests
{
    [Fact]
    public void Default_Values_AreCorrect()
    {
        var opts = new SubscribeOptions();
        Assert.True(opts.Durable);
        Assert.False(opts.AutoAck);
        Assert.False(opts.Exclusive);
        Assert.Equal(10, opts.PrefetchCount);
        Assert.Equal(RoutingMode.CompetingConsumer, opts.RoutingMode);
        Assert.Null(opts.QueueName);
        Assert.False(opts.AutoDelete);
        Assert.True(opts.DeadLetterEnabled);
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        var opts = new SubscribeOptions
        {
            QueueName = "my-queue",
            Durable = false,
            AutoAck = true,
            PrefetchCount = 50,
            RoutingMode = RoutingMode.Broadcast,
            ServiceName = "svc",
            AutoDelete = true,
            DeadLetterEnabled = false
        };

        Assert.Equal("my-queue", opts.QueueName);
        Assert.False(opts.Durable);
        Assert.True(opts.AutoAck);
        Assert.Equal(50, opts.PrefetchCount);
        Assert.Equal(RoutingMode.Broadcast, opts.RoutingMode);
        Assert.Equal("svc", opts.ServiceName);
        Assert.True(opts.AutoDelete);
        Assert.False(opts.DeadLetterEnabled);
    }
}

public class RequestOptionsTests
{
    [Fact]
    public void Default_Timeout_Is30Seconds()
    {
        var opts = new RequestOptions();
        Assert.Equal(TimeSpan.FromSeconds(30), opts.Timeout);
        Assert.False(opts.Persistent);
        Assert.Null(opts.CorrelationId);
    }
}
