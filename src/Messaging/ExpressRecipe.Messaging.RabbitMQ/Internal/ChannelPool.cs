using RabbitMQ.Client;

namespace ExpressRecipe.Messaging.RabbitMQ.Internal;

/// <summary>
/// A thread-safe pool of <see cref="IChannel"/> instances used for publishing.
/// Channels are cheap to open but carry thread-affinity concerns; pooling avoids
/// creating a new channel per message while still allowing concurrent publishers.
/// </summary>
internal sealed class ChannelPool : IAsyncDisposable
{
    private readonly IConnection _connection;
    private readonly int _poolSize;
    private readonly SemaphoreSlim _semaphore;
    private readonly Stack<IChannel> _channels;
    private bool _disposed;

    /// <summary>
    /// Initializes a new <see cref="ChannelPool"/> backed by the supplied connection.
    /// </summary>
    public ChannelPool(IConnection connection, int poolSize)
    {
        _connection = connection;
        _poolSize = poolSize;
        _semaphore = new SemaphoreSlim(poolSize, poolSize);
        _channels = new Stack<IChannel>(poolSize);
    }

    /// <summary>
    /// Acquires a channel from the pool, creating one if the pool is empty.
    /// Always call <see cref="ReturnAsync"/> (or dispose the lease) when finished.
    /// </summary>
    public async ValueTask<IChannel> AcquireAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

        lock (_channels)
        {
            if (_channels.TryPop(out var existing) && existing.IsOpen)
                return existing;
        }

        return await _connection.CreateChannelAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Returns a channel to the pool. If the channel is closed it is discarded.
    /// </summary>
    public ValueTask ReturnAsync(IChannel channel)
    {
        if (channel.IsOpen && !_disposed)
        {
            lock (_channels)
            {
                if (_channels.Count < _poolSize)
                {
                    _channels.Push(channel);
                    _semaphore.Release();
                    return ValueTask.CompletedTask;
                }
            }
        }

        _semaphore.Release();
        return channel.DisposeAsync();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        List<IChannel> toDispose;
        lock (_channels)
        {
            toDispose = new List<IChannel>(_channels);
            _channels.Clear();
        }

        foreach (var ch in toDispose)
        {
            try { await ch.DisposeAsync().ConfigureAwait(false); }
            catch { /* best-effort */ }
        }

        _semaphore.Dispose();
    }
}
