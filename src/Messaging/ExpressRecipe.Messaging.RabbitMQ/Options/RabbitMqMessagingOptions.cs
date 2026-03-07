namespace ExpressRecipe.Messaging.RabbitMQ.Options;

/// <summary>
/// Configuration options for the RabbitMQ messaging implementation.
/// </summary>
public sealed class RabbitMqMessagingOptions
{
    /// <summary>
    /// Gets or sets the prefix applied to all exchange and queue names.
    /// Defaults to <c>"expressrecipe"</c>.
    /// </summary>
    public string ExchangePrefix { get; set; } = "expressrecipe";

    /// <summary>
    /// Gets or sets the logical name of this service instance.
    /// Used for service-name routing and reply queue naming.
    /// </summary>
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the suffix appended to the exchange name to form the dead-letter exchange name.
    /// Defaults to <c>".dlx"</c>.
    /// </summary>
    public string DeadLetterExchangeSuffix { get; set; } = ".dlx";

    /// <summary>Gets or sets whether dead-letter queues are created and configured. Defaults to <c>true</c>.</summary>
    public bool EnableDeadLetter { get; set; } = true;

    /// <summary>Gets or sets the maximum number of delivery retries before a message is dead-lettered. Defaults to 3.</summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>Gets or sets the delay between retry attempts. Defaults to 5 seconds.</summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the number of channel objects to keep in the
    /// publish pool. Higher values improve publish throughput under heavy concurrency.
    /// Defaults to 64.
    /// </summary>
    public int ChannelPoolSize { get; set; } = 64;

    /// <summary>
    /// Gets or sets the default consumer dispatch concurrency for all consumer channels.
    /// A value greater than one enables concurrent message processing per consumer;
    /// handlers must be thread-safe when this is set above 1.
    /// Defaults to 1 (serial dispatch). Override per-subscription via <see cref="ExpressRecipe.Messaging.Core.Options.SubscribeOptions.ConsumerConcurrency"/>.
    /// </summary>
    public int ConsumerConcurrency { get; set; } = 1;
}
