using System.Threading.Channels;

namespace ExpressRecipe.IngredientService.Services;

/// <summary>A single item in a batch ingredient creation routed through the channel.</summary>
public sealed class IngredientBatchItem
{
    public string Name       { get; init; } = string.Empty;
    public Guid SubmittedBy  { get; init; }
    public string? SessionId { get; init; }
}

/// <summary>
/// Abstraction over the <see cref="Channel{T}"/> used for async batch ingredient creation.
/// Single ingredient creates go directly through the controller (sync REST path + event publish).
/// Bulk ingredient submissions use this channel; <see cref="IngredientBatchChannelWorker"/> processes them.
/// </summary>
public interface IIngredientBatchChannel
{
    bool TryWrite(IngredientBatchItem item);
    ValueTask WriteAsync(IngredientBatchItem item, CancellationToken ct = default);
    IAsyncEnumerable<IngredientBatchItem> ReadAllAsync(CancellationToken ct = default);
    int Count { get; }
}

/// <inheritdoc />
public sealed class IngredientBatchChannel : IIngredientBatchChannel
{
    private readonly Channel<IngredientBatchItem> _channel;

    public IngredientBatchChannel(IConfiguration configuration)
    {
        var capacity = configuration.GetValue("IngredientService:BatchChannel:Capacity", 5_000);
        _channel = Channel.CreateBounded<IngredientBatchItem>(
            new BoundedChannelOptions(capacity)
            {
                FullMode     = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });
    }

    public bool TryWrite(IngredientBatchItem item) => _channel.Writer.TryWrite(item);
    public ValueTask WriteAsync(IngredientBatchItem item, CancellationToken ct = default)
        => _channel.Writer.WriteAsync(item, ct);
    public IAsyncEnumerable<IngredientBatchItem> ReadAllAsync(CancellationToken ct = default)
        => _channel.Reader.ReadAllAsync(ct);
    public int Count => _channel.Reader.Count;
}
