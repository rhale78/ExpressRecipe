using System.Diagnostics;

namespace ExpressRecipe.Messaging.Core.Messages;

/// <summary>
/// Contextual information passed to message handlers when a message is received.
/// </summary>
public sealed class MessageContext
{
    /// <summary>Gets the unique identifier of the received message.</summary>
    public required string MessageId { get; init; }

    /// <summary>Gets the optional correlation identifier linking related messages.</summary>
    public string? CorrelationId { get; init; }

    /// <summary>Gets the fully-qualified CLR type name of the message.</summary>
    public required string MessageType { get; init; }

    /// <summary>Gets the UTC timestamp when the message was originally created.</summary>
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>Gets the custom headers associated with this message.</summary>
    public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>();

    /// <summary>Gets the OpenTelemetry <see cref="System.Diagnostics.Activity"/> associated with this message, if tracing is enabled.</summary>
    public Activity? Activity { get; init; }

    /// <summary>Gets the reply-to queue name for request/response handlers to know where to send responses.</summary>
    public string? ReplyTo { get; init; }
}
