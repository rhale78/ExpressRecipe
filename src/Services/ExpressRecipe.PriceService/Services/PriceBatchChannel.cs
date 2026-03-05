using System.Threading.Channels;
using ExpressRecipe.PriceService.Data;

namespace ExpressRecipe.PriceService.Services;

/// <summary>
/// Request model used when a single price observation is submitted to the batch channel.
/// Matches the *BatchItem naming used by ProductBatchItem, RecipeBatchItem and IngredientBatchItem.
/// </summary>
public sealed class PriceBatchItem
{
    public Guid ProductId   { get; init; }
    public Guid StoreId     { get; init; }
    public decimal Price    { get; init; }
    public DateTime? ObservedAt { get; init; }
    public Guid SubmittedBy { get; init; }
    /// <summary>Optional session identifier for batch correlation.</summary>
    public string? SessionId { get; init; }
}

/// <summary>
/// Abstraction over the <see cref="Channel{T}"/> used for async price batch processing.
/// Both the controller (single write) and bulk import code (many writes) produce to this channel;
/// <see cref="PriceBatchChannelWorker"/> consumes and processes items.
/// Consistent with IProductBatchChannel, IRecipeBatchChannel, IIngredientBatchChannel.
/// </summary>
public interface IPriceBatchChannel
{
    /// <summary>
    /// Try to write a price item to the channel without blocking.
    /// Returns <c>false</c> if the channel is at capacity.
    /// </summary>
    bool TryWrite(PriceBatchItem item);

    /// <summary>
    /// Write a price item to the channel, waiting if necessary until space is available.
    /// </summary>
    ValueTask WriteAsync(PriceBatchItem item, CancellationToken ct = default);

    /// <summary>
    /// Read all pending items. Used by the background worker.
    /// </summary>
    IAsyncEnumerable<PriceBatchItem> ReadAllAsync(CancellationToken ct = default);

    /// <summary>Returns the approximate number of items waiting to be processed.</summary>
    int Count { get; }
}

/// <inheritdoc />
public sealed class PriceBatchChannel : IPriceBatchChannel
{
    private readonly Channel<PriceBatchItem> _channel;

    public PriceBatchChannel(IConfiguration configuration)
    {
        var capacity = configuration.GetValue("PriceService:BatchChannel:Capacity", 10_000);
        _channel = Channel.CreateBounded<PriceBatchItem>(
            new BoundedChannelOptions(capacity)
            {
                // Wait instead of drop – provides backpressure to callers and prevents silent data loss.
                // The batch endpoint uses TryWrite first; if full it falls back to WriteAsync (backpressure).
                FullMode        = BoundedChannelFullMode.Wait,
                SingleReader    = true,
                SingleWriter    = false,
                AllowSynchronousContinuations = false
            });
    }

    public bool TryWrite(PriceBatchItem item)
        => _channel.Writer.TryWrite(item);

    public ValueTask WriteAsync(PriceBatchItem item, CancellationToken ct = default)
        => _channel.Writer.WriteAsync(item, ct);

    public IAsyncEnumerable<PriceBatchItem> ReadAllAsync(CancellationToken ct = default)
        => _channel.Reader.ReadAllAsync(ct);

    public int Count => _channel.Reader.Count;
}
