using ExpressRecipe.Messaging.Core.Options;

namespace ExpressRecipe.Messaging.Core.Messages;

/// <summary>
/// Wraps a message with metadata for transport over a message broker.
/// </summary>
public sealed class MessageEnvelope
{
    /// <summary>Gets or sets the unique message identifier.</summary>
    public string MessageId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Gets or sets an optional correlation identifier for linking related messages.</summary>
    public string? CorrelationId { get; set; }

    /// <summary>Gets or sets the fully-qualified CLR type name of the message payload.</summary>
    public string MessageType { get; set; } = string.Empty;

    /// <summary>Gets or sets the simple class name of the message for easy lookup.</summary>
    public string MessageName { get; set; } = string.Empty;

    /// <summary>Gets or sets the JSON-serialized message payload.</summary>
    public byte[] Payload { get; set; } = Array.Empty<byte>();

    /// <summary>Gets or sets the UTC timestamp when the envelope was created.</summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Gets or sets the reply-to queue name for request/response patterns.</summary>
    public string? ReplyTo { get; set; }

    /// <summary>Gets or sets the message time-to-live duration.</summary>
    public TimeSpan? Ttl { get; set; }

    /// <summary>Gets or sets the routing mode for this message.</summary>
    public RoutingMode RoutingMode { get; set; } = RoutingMode.Broadcast;

    /// <summary>Gets or sets the destination service name for service-name routing.</summary>
    public string? DestinationServiceName { get; set; }

    /// <summary>Gets or sets custom headers associated with this message.</summary>
    public Dictionary<string, string> Headers { get; set; } = new();

    // --- W3C TraceContext distributed tracing fields ---

    /// <summary>Gets or sets the W3C trace ID for distributed tracing.</summary>
    public string? TraceId { get; set; }

    /// <summary>Gets or sets the W3C span ID for distributed tracing.</summary>
    public string? SpanId { get; set; }

    /// <summary>Gets or sets the W3C trace flags for distributed tracing.</summary>
    public string? TraceFlags { get; set; }

    /// <summary>Gets or sets the W3C trace state for distributed tracing.</summary>
    public string? TraceState { get; set; }
}
