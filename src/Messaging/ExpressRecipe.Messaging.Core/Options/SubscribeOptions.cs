namespace ExpressRecipe.Messaging.Core.Options;

/// <summary>
/// Options that control how a consumer subscribes to messages.
/// </summary>
public sealed class SubscribeOptions
{
    /// <summary>
    /// Gets or sets a custom queue name. If null, the queue name is derived automatically
    /// from the message type name and <see cref="RoutingMode"/>.
    /// </summary>
    public string? QueueName { get; set; }

    /// <summary>Gets or sets whether the queue survives broker restarts. Defaults to <c>true</c>.</summary>
    public bool Durable { get; set; } = true;

    /// <summary>
    /// Gets or sets whether messages are automatically acknowledged on receipt.
    /// Defaults to <c>false</c> (manual acknowledgement for reliability).
    /// </summary>
    public bool AutoAck { get; set; } = false;

    /// <summary>Gets or sets whether the queue is exclusive to this connection. Defaults to <c>false</c>.</summary>
    public bool Exclusive { get; set; } = false;

    /// <summary>Gets or sets the maximum number of unacknowledged messages per consumer (backpressure). Defaults to 10.</summary>
    public int PrefetchCount { get; set; } = 10;

    /// <summary>Gets or sets the routing mode for this subscription. Defaults to <see cref="RoutingMode.CompetingConsumer"/>.</summary>
    public RoutingMode RoutingMode { get; set; } = RoutingMode.CompetingConsumer;

    /// <summary>Gets or sets the service name used when <see cref="RoutingMode"/> is <see cref="RoutingMode.ServiceName"/>.</summary>
    public string? ServiceName { get; set; }

    /// <summary>Gets or sets whether the queue is automatically deleted when the last consumer disconnects. Defaults to <c>false</c>.</summary>
    public bool AutoDelete { get; set; } = false;

    /// <summary>Gets or sets whether dead-lettering is enabled for this queue. Defaults to <c>true</c>.</summary>
    public bool DeadLetterEnabled { get; set; } = true;

    /// <summary>Gets or sets the dead-letter exchange name. If null, the default DLX name is used.</summary>
    public string? DeadLetterExchange { get; set; }
}
