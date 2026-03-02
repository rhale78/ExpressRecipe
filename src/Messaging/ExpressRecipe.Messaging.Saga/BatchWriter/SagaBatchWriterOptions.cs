namespace ExpressRecipe.Messaging.Saga.BatchWriter;

/// <summary>
/// Configuration options for <see cref="SagaBatchWriter{TState}"/>.
/// </summary>
public sealed class SagaBatchWriterOptions
{
    /// <summary>Maximum number of pending updates the channel can hold before back-pressure kicks in. Default: 10,000.</summary>
    public int ChannelCapacity { get; set; } = 10_000;

    /// <summary>Maximum number of items written per DB batch. Default: 500.</summary>
    public int MaxBatchSize { get; set; } = 500;

    /// <summary>
    /// Brief delay after reading the first item to allow more items to accumulate before flushing.
    /// Set to zero to flush immediately. Default: 5 ms.
    /// </summary>
    public TimeSpan CoalescingDelay { get; set; } = TimeSpan.FromMilliseconds(5);
}
