namespace ExpressRecipe.Messaging.Core.Options;

/// <summary>
/// Options that control how a message is published to subscribers.
/// </summary>
public sealed class PublishOptions
{
    /// <summary>Gets or sets the optional time-to-live for the message. Null means no expiry.</summary>
    public TimeSpan? Ttl { get; set; }

    /// <summary>Gets or sets the routing mode. Defaults to <see cref="RoutingMode.Broadcast"/>.</summary>
    public RoutingMode RoutingMode { get; set; } = RoutingMode.Broadcast;

    /// <summary>Gets or sets an optional correlation identifier.</summary>
    public string? CorrelationId { get; set; }

    /// <summary>Gets or sets optional custom headers to include with the message.</summary>
    public Dictionary<string, string>? Headers { get; set; }

    /// <summary>Gets or sets whether the message is persisted to disk. Defaults to <c>true</c>.</summary>
    public bool Persistent { get; set; } = true;

    /// <summary>Gets or sets the message priority (0–9). Null means default priority.</summary>
    public byte? Priority { get; set; }

    /// <summary>Gets or sets an explicit message ID. If null, one is generated automatically.</summary>
    public string? MessageId { get; set; }
}
