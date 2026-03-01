using ExpressRecipe.Messaging.Core.Messages;
using ExpressRecipe.Messaging.Core.Options;

namespace ExpressRecipe.Messaging.Tests.Messages;

public class MessageEnvelopeTests
{
    [Fact]
    public void Envelope_DefaultConstruction_HasValidDefaults()
    {
        var envelope = new MessageEnvelope();

        Assert.NotNull(envelope.MessageId);
        Assert.NotEmpty(envelope.MessageId);
        Assert.Empty(envelope.Payload);
        Assert.Empty(envelope.Headers);
        Assert.Null(envelope.CorrelationId);
        Assert.Null(envelope.ReplyTo);
        Assert.Null(envelope.Ttl);
        Assert.Equal(RoutingMode.Broadcast, envelope.RoutingMode);
        Assert.True(envelope.Timestamp <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public void Envelope_WithAllFields_PropertiesSetCorrectly()
    {
        var id = Guid.NewGuid().ToString();
        var correlationId = Guid.NewGuid().ToString();
        var payload = "test"u8.ToArray();
        var headers = new Dictionary<string, string> { ["x-custom"] = "value" };
        var ttl = TimeSpan.FromSeconds(60);

        var envelope = new MessageEnvelope
        {
            MessageId = id,
            CorrelationId = correlationId,
            MessageType = "MyApp.MyMessage",
            MessageName = "MyMessage",
            Payload = payload,
            Headers = headers,
            Ttl = ttl,
            RoutingMode = RoutingMode.CompetingConsumer,
            ReplyTo = "reply-queue",
            DestinationServiceName = "svc",
            TraceId = "abc123",
            SpanId = "def456"
        };

        Assert.Equal(id, envelope.MessageId);
        Assert.Equal(correlationId, envelope.CorrelationId);
        Assert.Equal("MyApp.MyMessage", envelope.MessageType);
        Assert.Equal(payload, envelope.Payload);
        Assert.Equal("value", envelope.Headers["x-custom"]);
        Assert.Equal(ttl, envelope.Ttl);
        Assert.Equal(RoutingMode.CompetingConsumer, envelope.RoutingMode);
        Assert.Equal("reply-queue", envelope.ReplyTo);
        Assert.Equal("abc123", envelope.TraceId);
    }

    [Fact]
    public void Envelope_MessageId_IsUniquePerInstance()
    {
        var a = new MessageEnvelope();
        var b = new MessageEnvelope();
        Assert.NotEqual(a.MessageId, b.MessageId);
    }

    [Fact]
    public void Envelope_Timestamp_IsUtc()
    {
        var before = DateTimeOffset.UtcNow;
        var envelope = new MessageEnvelope();
        var after = DateTimeOffset.UtcNow;

        Assert.True(envelope.Timestamp >= before);
        Assert.True(envelope.Timestamp <= after);
    }
}
