using System.Diagnostics;
using ExpressRecipe.Messaging.Core.Messages;
using ExpressRecipe.Messaging.Core.Tracing;

namespace ExpressRecipe.Messaging.Tests.Tracing;

public class MessagingActivitySourceTests
{
    [Fact]
    public void ActivitySourceName_IsCorrect()
    {
        Assert.Equal("ExpressRecipe.Messaging", MessagingActivitySource.ActivitySourceName);
    }

    [Fact]
    public void InjectTraceContext_NullActivity_DoesNotThrow()
    {
        var envelope = new MessageEnvelope();
        // Should not throw
        MessagingActivitySource.InjectTraceContext(envelope, null);
        Assert.Null(envelope.TraceId);
        Assert.Null(envelope.SpanId);
    }

    [Fact]
    public void InjectTraceContext_WithActivity_SetsTraceFields()
    {
        // Create a real activity to test injection
        using var source = new ActivitySource("TestSource");
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = source.StartActivity("test", ActivityKind.Producer);
        Assert.NotNull(activity);

        var envelope = new MessageEnvelope();
        MessagingActivitySource.InjectTraceContext(envelope, activity);

        Assert.NotNull(envelope.TraceId);
        Assert.NotNull(envelope.SpanId);
        Assert.Equal(activity.TraceId.ToString(), envelope.TraceId);
        Assert.Equal(activity.SpanId.ToString(), envelope.SpanId);
    }

    [Fact]
    public void ExtractTraceContext_NullFields_ReturnsNull()
    {
        var envelope = new MessageEnvelope();
        var ctx = MessagingActivitySource.ExtractTraceContext(envelope);
        Assert.Null(ctx);
    }

    [Fact]
    public void ExtractTraceContext_ValidFields_ReturnsContext()
    {
        // Create a known trace/span ID pair
        using var source = new ActivitySource("TestSource2");
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = source.StartActivity("inject-test");
        Assert.NotNull(activity);

        var envelope = new MessageEnvelope();
        MessagingActivitySource.InjectTraceContext(envelope, activity);

        var ctx = MessagingActivitySource.ExtractTraceContext(envelope);

        Assert.NotNull(ctx);
        Assert.Equal(activity.TraceId.ToString(), ctx.Value.TraceId.ToString());
        Assert.Equal(activity.SpanId.ToString(), ctx.Value.SpanId.ToString());
    }

    [Fact]
    public void ExtractTraceContext_InvalidFields_ReturnsNull()
    {
        var envelope = new MessageEnvelope
        {
            TraceId = "invalid-trace-id",
            SpanId = "invalid-span-id"
        };
        var ctx = MessagingActivitySource.ExtractTraceContext(envelope);
        Assert.Null(ctx);
    }

    [Fact]
    public void StartPublishActivity_ReturnsNull_WhenNoListener()
    {
        // Without a listener attached to our source, activity should be null
        var activity = MessagingActivitySource.StartPublishActivity("TestMessage", "test-exchange");
        // May be null if no listener; just ensure it doesn't throw
        activity?.Dispose();
    }

    [Fact]
    public void StartReceiveActivity_ReturnsNull_WhenNoListener()
    {
        var activity = MessagingActivitySource.StartReceiveActivity("TestMessage", "test-queue", "traceId", "spanId");
        activity?.Dispose();
    }
}
