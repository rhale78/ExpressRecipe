using System.Diagnostics;
using ExpressRecipe.Messaging.Core.Messages;

namespace ExpressRecipe.Messaging.Core.Tracing;

/// <summary>
/// Provides OpenTelemetry <see cref="ActivitySource"/> support for the ExpressRecipe messaging system.
/// Implements W3C TraceContext propagation for distributed tracing across message boundaries.
/// </summary>
public static class MessagingActivitySource
{
    /// <summary>The name of the activity source used for messaging telemetry.</summary>
    public const string ActivitySourceName = "ExpressRecipe.Messaging";

    /// <summary>The shared <see cref="ActivitySource"/> instance.</summary>
    public static readonly ActivitySource Source = new(ActivitySourceName, "1.0.0");

    /// <summary>
    /// Starts a publish activity for the given message type and destination.
    /// </summary>
    /// <param name="messageType">The simple message type name.</param>
    /// <param name="destination">The exchange or queue being published to.</param>
    /// <returns>A new <see cref="Activity"/>, or <c>null</c> if no listeners are attached.</returns>
    public static Activity? StartPublishActivity(string messageType, string destination)
    {
        return Source.StartActivity(
            $"publish {messageType}",
            ActivityKind.Producer,
            default(ActivityContext),
            tags: new ActivityTagsCollection
            {
                { "messaging.system", "rabbitmq" },
                { "messaging.operation", "publish" },
                { "messaging.destination", destination },
                { "messaging.message_type", messageType }
            });
    }

    /// <summary>
    /// Starts a receive activity for the given message type and source, optionally linking to the upstream trace.
    /// </summary>
    /// <param name="messageType">The simple message type name.</param>
    /// <param name="source">The exchange or queue being consumed from.</param>
    /// <param name="traceId">The upstream W3C trace ID extracted from the message envelope.</param>
    /// <param name="spanId">The upstream W3C span ID extracted from the message envelope.</param>
    /// <returns>A new <see cref="Activity"/>, or <c>null</c> if no listeners are attached.</returns>
    public static Activity? StartReceiveActivity(string messageType, string source, string? traceId = null, string? spanId = null)
    {
        ActivityContext parentContext = default;

        if (traceId is not null && spanId is not null)
        {
            try
            {
                var parsedTraceId = ActivityTraceId.CreateFromString(traceId.AsSpan());
                var parsedSpanId = ActivitySpanId.CreateFromString(spanId.AsSpan());
                parentContext = new ActivityContext(parsedTraceId, parsedSpanId, ActivityTraceFlags.Recorded, isRemote: true);
            }
            catch (ArgumentOutOfRangeException) { /* ignore invalid trace context */ }
        }

        return Source.StartActivity(
            $"receive {messageType}",
            ActivityKind.Consumer,
            parentContext,
            tags: new ActivityTagsCollection
            {
                { "messaging.system", "rabbitmq" },
                { "messaging.operation", "receive" },
                { "messaging.source", source },
                { "messaging.message_type", messageType }
            });
    }

    /// <summary>
    /// Injects the current W3C trace context from the given <see cref="Activity"/> into the <see cref="MessageEnvelope"/> headers.
    /// </summary>
    /// <param name="envelope">The envelope to enrich with tracing data.</param>
    /// <param name="activity">The current activity. When null, no data is injected.</param>
    public static void InjectTraceContext(MessageEnvelope envelope, Activity? activity)
    {
        if (activity is null)
            return;

        envelope.TraceId = activity.TraceId.ToString();
        envelope.SpanId = activity.SpanId.ToString();
        envelope.TraceFlags = ((int)activity.ActivityTraceFlags).ToString();
        envelope.TraceState = activity.TraceStateString;
    }

    /// <summary>
    /// Extracts the W3C trace context from a <see cref="MessageEnvelope"/> and returns an <see cref="ActivityContext"/>.
    /// </summary>
    /// <param name="envelope">The envelope containing tracing data.</param>
    /// <returns>The extracted <see cref="ActivityContext"/>, or <c>null</c> if no valid trace data is present.</returns>
    public static ActivityContext? ExtractTraceContext(MessageEnvelope envelope)
    {
        if (envelope.TraceId is null || envelope.SpanId is null)
            return null;

        try
        {
            var traceId = ActivityTraceId.CreateFromString(envelope.TraceId.AsSpan());
            var spanId = ActivitySpanId.CreateFromString(envelope.SpanId.AsSpan());

            var flags = ActivityTraceFlags.None;
            if (int.TryParse(envelope.TraceFlags, out var flagsInt))
                flags = (ActivityTraceFlags)flagsInt;

            return new ActivityContext(traceId, spanId, flags, envelope.TraceState, isRemote: true);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }
}
