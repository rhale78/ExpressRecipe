using System.Threading.Channels;
using ExpressRecipe.Shared.DTOs.Product;

namespace ExpressRecipe.ProductService.Services;

/// <summary>
/// A single item in a batch product submission routed through the channel.
/// </summary>
public sealed class ProductBatchItem
{
    public CreateProductRequest Request { get; init; } = null!;
    public Guid SubmittedBy            { get; init; }
    public string? SessionId           { get; init; }
}

/// <summary>
/// Abstraction over the <see cref="Channel{T}"/> used for async batch product ingestion.
/// Single product creates go directly through the controller (sync REST path + event publish).
/// Bulk product submissions use this channel; <see cref="ProductBatchChannelWorker"/> processes them.
/// </summary>
public interface IProductBatchChannel
{
    bool TryWrite(ProductBatchItem item);
    ValueTask WriteAsync(ProductBatchItem item, CancellationToken ct = default);
    IAsyncEnumerable<ProductBatchItem> ReadAllAsync(CancellationToken ct = default);
    int Count { get; }
}

/// <inheritdoc />
public sealed class ProductBatchChannel : IProductBatchChannel
{
    private readonly Channel<ProductBatchItem> _channel;

    public ProductBatchChannel(IConfiguration configuration)
    {
        var capacity = configuration.GetValue("ProductService:BatchChannel:Capacity", 5_000);
        _channel = Channel.CreateBounded<ProductBatchItem>(
            new BoundedChannelOptions(capacity)
            {
                FullMode     = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });
    }

    public bool TryWrite(ProductBatchItem item) => _channel.Writer.TryWrite(item);
    public ValueTask WriteAsync(ProductBatchItem item, CancellationToken ct = default)
        => _channel.Writer.WriteAsync(item, ct);
    public IAsyncEnumerable<ProductBatchItem> ReadAllAsync(CancellationToken ct = default)
        => _channel.Reader.ReadAllAsync(ct);
    public int Count => _channel.Reader.Count;
}
