using System.Threading.Channels;
using ExpressRecipe.Shared.DTOs.Recipe;

namespace ExpressRecipe.RecipeService.Services;

/// <summary>A single item in a batch recipe import routed through the channel.</summary>
public sealed class RecipeBatchItem
{
    public CreateRecipeRequest Request { get; init; } = null!;
    public Guid SubmittedBy            { get; init; }
    public string? SessionId           { get; init; }
}

/// <summary>
/// Abstraction over the <see cref="Channel{T}"/> used for async batch recipe ingestion.
/// Single recipe creates go directly through the controller (sync REST path + event publish).
/// Bulk recipe submissions use this channel; <see cref="RecipeBatchChannelWorker"/> processes them.
/// </summary>
public interface IRecipeBatchChannel
{
    bool TryWrite(RecipeBatchItem item);
    ValueTask WriteAsync(RecipeBatchItem item, CancellationToken ct = default);
    IAsyncEnumerable<RecipeBatchItem> ReadAllAsync(CancellationToken ct = default);
    int Count { get; }
}

/// <inheritdoc />
public sealed class RecipeBatchChannel : IRecipeBatchChannel
{
    private readonly Channel<RecipeBatchItem> _channel;

    public RecipeBatchChannel(IConfiguration configuration)
    {
        var capacity = configuration.GetValue("RecipeService:BatchChannel:Capacity", 2_000);
        _channel = Channel.CreateBounded<RecipeBatchItem>(
            new BoundedChannelOptions(capacity)
            {
                FullMode     = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });
    }

    public bool TryWrite(RecipeBatchItem item) => _channel.Writer.TryWrite(item);
    public ValueTask WriteAsync(RecipeBatchItem item, CancellationToken ct = default)
        => _channel.Writer.WriteAsync(item, ct);
    public IAsyncEnumerable<RecipeBatchItem> ReadAllAsync(CancellationToken ct = default)
        => _channel.Reader.ReadAllAsync(ct);
    public int Count => _channel.Reader.Count;
}
