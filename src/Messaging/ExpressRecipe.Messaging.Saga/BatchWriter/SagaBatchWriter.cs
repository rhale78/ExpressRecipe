using System.Threading.Channels;
using ExpressRecipe.Messaging.Saga.Abstractions;
using Microsoft.Extensions.Logging;

namespace ExpressRecipe.Messaging.Saga.BatchWriter;

/// <summary>
/// Write-behind buffer for saga state updates.
/// Enqueues updates into a bounded <see cref="Channel{T}"/> and flushes them
/// to the database in configurable batches, dramatically reducing DB round-trips
/// during high-throughput bulk processing.
/// </summary>
/// <typeparam name="TState">The saga state type.</typeparam>
public sealed class SagaBatchWriter<TState> : IAsyncDisposable
    where TState : class, ISagaState
{
    private readonly ISagaBatchRepository<TState> _repository;
    private readonly SagaBatchWriterOptions _options;
    private readonly ILogger<SagaBatchWriter<TState>> _logger;

    private readonly Channel<SagaBatchWriteItem> _channel;
    private readonly Task _flushTask;
    private readonly CancellationTokenSource _cts = new();

    /// <summary>
    /// Initializes a new instance of <see cref="SagaBatchWriter{TState}"/>.
    /// </summary>
    public SagaBatchWriter(
        ISagaBatchRepository<TState> repository,
        SagaBatchWriterOptions? options = null,
        ILogger<SagaBatchWriter<TState>>? logger = null)
    {
        _repository = repository;
        _options = options ?? new SagaBatchWriterOptions();
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<SagaBatchWriter<TState>>.Instance;

        _channel = Channel.CreateBounded<SagaBatchWriteItem>(new BoundedChannelOptions(_options.ChannelCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,  // single consumer for ordered batching
            SingleWriter = false  // multiple producers allowed
        });

        _flushTask = Task.Run(() => FlushLoopAsync(_cts.Token));
    }

    /// <summary>
    /// Enqueues a mask update for the given correlation ID.
    /// Returns quickly; the actual DB write happens asynchronously.
    /// </summary>
    public async ValueTask EnqueueMaskUpdateAsync(string correlationId, long maskToAdd, CancellationToken cancellationToken = default)
    {
        var item = new SagaBatchWriteItem(correlationId, maskToAdd, null, null);
        await _channel.Writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Enqueues a status update (e.g., Completed or Failed) for the given correlation ID.
    /// </summary>
    public async ValueTask EnqueueStatusUpdateAsync(
        string correlationId, SagaStatus status, DateTimeOffset? completedAt = null,
        CancellationToken cancellationToken = default)
    {
        var item = new SagaBatchWriteItem(correlationId, 0, status, completedAt);
        await _channel.Writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Enqueues both a mask update and a status update atomically (single channel write).
    /// </summary>
    public async ValueTask EnqueueAsync(
        string correlationId, long maskToAdd, SagaStatus status, DateTimeOffset? completedAt = null,
        CancellationToken cancellationToken = default)
    {
        var item = new SagaBatchWriteItem(correlationId, maskToAdd, status, completedAt);
        await _channel.Writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Signals no more writes and waits for all pending updates to flush to the DB.
    /// </summary>
    public async ValueTask CompleteAsync(CancellationToken cancellationToken = default)
    {
        _channel.Writer.TryComplete();
        await _flushTask.ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _channel.Writer.TryComplete();
        _cts.Cancel();
        try { await _flushTask.ConfigureAwait(false); } catch (OperationCanceledException) { }
        _cts.Dispose();
    }

    // ── Background flush loop ─────────────────────────────────────────────────

    private async Task FlushLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new List<SagaBatchWriteItem>(_options.MaxBatchSize);

        while (!cancellationToken.IsCancellationRequested)
        {
            buffer.Clear();

            // Wait for the first item
            try
            {
                if (!await _channel.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
                    break; // channel completed
            }
            catch (OperationCanceledException) { break; }

            // Drain up to MaxBatchSize items without blocking
            while (buffer.Count < _options.MaxBatchSize && _channel.Reader.TryRead(out var item))
                buffer.Add(item);

            if (buffer.Count == 0) continue;

            // Apply a short coalescing delay to allow more items to accumulate
            if (_options.CoalescingDelay > TimeSpan.Zero && buffer.Count < _options.MaxBatchSize)
            {
                try { await Task.Delay(_options.CoalescingDelay, cancellationToken).ConfigureAwait(false); }
                catch (OperationCanceledException) { }

                while (buffer.Count < _options.MaxBatchSize && _channel.Reader.TryRead(out var extra))
                    buffer.Add(extra);
            }

            await FlushBufferAsync(buffer, cancellationToken).ConfigureAwait(false);
        }

        // Drain remaining items on graceful shutdown
        while (_channel.Reader.TryRead(out var remaining))
            buffer.Add(remaining);

        if (buffer.Count > 0)
            await FlushBufferAsync(buffer, CancellationToken.None).ConfigureAwait(false);
    }

    private async Task FlushBufferAsync(List<SagaBatchWriteItem> buffer, CancellationToken cancellationToken)
    {
        // Coalesce multiple updates for the same correlation ID
        var maskUpdates = new Dictionary<string, long>();
        var statusUpdates = new Dictionary<string, (SagaStatus Status, DateTimeOffset? CompletedAt)>();

        foreach (var item in buffer)
        {
            if (item.MaskToAdd != 0)
            {
                maskUpdates.TryGetValue(item.CorrelationId, out var existing);
                maskUpdates[item.CorrelationId] = existing | item.MaskToAdd;
            }

            if (item.NewStatus.HasValue)
                statusUpdates[item.CorrelationId] = (item.NewStatus.Value, item.CompletedAt);
        }

        try
        {
            if (maskUpdates.Count > 0)
            {
                var updates = maskUpdates.Select(kv => (kv.Key, kv.Value));
                await _repository.BatchUpdateMaskAsync(updates, cancellationToken).ConfigureAwait(false);
                _logger.LogDebug("SagaBatchWriter: flushed {Count} mask update(s)", maskUpdates.Count);
            }

            if (statusUpdates.Count > 0)
            {
                var updates = statusUpdates.Select(kv => (kv.Key, kv.Value.Status, kv.Value.CompletedAt));
                await _repository.BatchUpdateStatusAsync(updates, cancellationToken).ConfigureAwait(false);
                _logger.LogDebug("SagaBatchWriter: flushed {Count} status update(s)", statusUpdates.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SagaBatchWriter: error flushing {Count} update(s)", buffer.Count);
        }
    }
}
