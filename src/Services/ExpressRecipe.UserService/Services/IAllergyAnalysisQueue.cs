using System.Threading.Channels;

namespace ExpressRecipe.UserService.Services;

/// <summary>
/// Thin wrapper so controllers don't depend on <see cref="System.Threading.Channels"/> directly.
/// </summary>
public interface IAllergyAnalysisQueue
{
    void Enqueue(Guid incidentId);
    IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken ct);
}

public sealed class AllergyAnalysisQueue : IAllergyAnalysisQueue
{
    private readonly Channel<Guid> _channel =
        Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions { SingleReader = true });

    public void Enqueue(Guid incidentId)
        => _channel.Writer.TryWrite(incidentId);

    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken ct)
        => _channel.Reader.ReadAllAsync(ct);
}
