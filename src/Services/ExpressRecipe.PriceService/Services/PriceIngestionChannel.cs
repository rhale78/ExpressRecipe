using System.Threading.Channels;
using ExpressRecipe.PriceService.Data;

namespace ExpressRecipe.PriceService.Services;

/// <summary>
/// Request model used when a single price observation is submitted to the ingestion channel.
/// </summary>
public sealed class PriceIngestionRequest
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
/// Abstraction over the <see cref="Channel{T}"/> used for async price ingestion.
/// Both the controller (single write) and bulk import code (many writes) produce to this channel;
/// <see cref="PriceIngestionChannelWorker"/> consumes and processes items.
/// </summary>
public interface IPriceIngestionChannel
{
    /// <summary>
    /// Try to write a price request to the channel without blocking.
    /// Returns <c>false</c> if the channel is at capacity and the request is dropped.
    /// </summary>
    bool TryWrite(PriceIngestionRequest request);

    /// <summary>
    /// Write a price request to the channel, waiting if necessary until space is available.
    /// </summary>
    ValueTask WriteAsync(PriceIngestionRequest request, CancellationToken ct = default);

    /// <summary>
    /// Read all pending items. Used by the background worker.
    /// </summary>
    IAsyncEnumerable<PriceIngestionRequest> ReadAllAsync(CancellationToken ct = default);

    /// <summary>Returns the approximate number of items waiting to be processed.</summary>
    int Count { get; }
}

/// <inheritdoc />
public sealed class PriceIngestionChannel : IPriceIngestionChannel
{
    private readonly Channel<PriceIngestionRequest> _channel;

    public PriceIngestionChannel(IConfiguration configuration)
    {
        var capacity = configuration.GetValue("PriceService:IngestionChannel:Capacity", 10_000);
        _channel = Channel.CreateBounded<PriceIngestionRequest>(
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

    public bool TryWrite(PriceIngestionRequest request)
        => _channel.Writer.TryWrite(request);

    public ValueTask WriteAsync(PriceIngestionRequest request, CancellationToken ct = default)
        => _channel.Writer.WriteAsync(request, ct);

    public IAsyncEnumerable<PriceIngestionRequest> ReadAllAsync(CancellationToken ct = default)
        => _channel.Reader.ReadAllAsync(ct);

    public int Count => _channel.Reader.Count;
}
