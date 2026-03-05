using ExpressRecipe.Messaging.Core.Abstractions;
using ExpressRecipe.Shared.Messages;

namespace ExpressRecipe.PriceService.Services;

/// <summary>
/// Publishes price domain events to the message bus.
/// Methods are fire-and-forget best-effort: bus failures are logged but not re-thrown.
/// Both the synchronous REST path and the asynchronous channel path share this publisher.
/// </summary>
public interface IPriceEventPublisher
{
    /// <summary>Publish after a single price observation is recorded (sync REST call or channel worker).</summary>
    Task PublishPriceRecordedAsync(
        Guid priceObservationId, Guid productId, Guid storeId,
        decimal price, Guid recordedBy, CancellationToken ct = default);

    /// <summary>Publish once when a batch of price items is queued for async processing.</summary>
    Task PublishPriceBatchSubmittedAsync(
        string sessionId, int itemCount, Guid submittedBy, CancellationToken ct = default);

    /// <summary>Publish after a promotional deal is created.</summary>
    Task PublishDealCreatedAsync(
        Guid dealId, Guid productId, Guid storeId, string dealType,
        decimal originalPrice, decimal salePrice,
        DateTime startDate, DateTime endDate, CancellationToken ct = default);

    /// <summary>Publish after a store is added to the system.</summary>
    Task PublishStoreAddedAsync(
        Guid storeId, string name, string? city, string? state,
        string? chain, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class PriceEventPublisher : IPriceEventPublisher
{
    private readonly IMessageBus _bus;
    private readonly ILogger<PriceEventPublisher> _logger;

    public PriceEventPublisher(IMessageBus bus, ILogger<PriceEventPublisher> logger)
    {
        _bus    = bus;
        _logger = logger;
    }

    public Task PublishPriceRecordedAsync(
        Guid priceObservationId, Guid productId, Guid storeId,
        decimal price, Guid recordedBy, CancellationToken ct = default) =>
        SafePublishAsync(
            new PriceRecordedEvent(
                priceObservationId, productId, storeId,
                price, recordedBy, DateTimeOffset.UtcNow),
            PriceEventKeys.Recorded, ct);

    public Task PublishPriceBatchSubmittedAsync(
        string sessionId, int itemCount, Guid submittedBy, CancellationToken ct = default) =>
        SafePublishAsync(
            new PriceBatchSubmittedEvent(
                sessionId, itemCount, submittedBy, DateTimeOffset.UtcNow),
            PriceEventKeys.BatchSubmitted, ct);

    public Task PublishDealCreatedAsync(
        Guid dealId, Guid productId, Guid storeId, string dealType,
        decimal originalPrice, decimal salePrice,
        DateTime startDate, DateTime endDate, CancellationToken ct = default) =>
        SafePublishAsync(
            new DealCreatedEvent(
                dealId, productId, storeId, dealType,
                originalPrice, salePrice,
                new DateTimeOffset(startDate, TimeSpan.Zero),
                new DateTimeOffset(endDate, TimeSpan.Zero),
                DateTimeOffset.UtcNow),
            PriceEventKeys.DealCreated, ct);

    public Task PublishStoreAddedAsync(
        Guid storeId, string name, string? city, string? state,
        string? chain, CancellationToken ct = default) =>
        SafePublishAsync(
            new StoreAddedEvent(storeId, name, city, state, chain, DateTimeOffset.UtcNow),
            PriceEventKeys.StoreAdded, ct);

    // -------------------------------------------------------------------------

    private async Task SafePublishAsync<TMsg>(TMsg msg, string eventKey, CancellationToken ct)
        where TMsg : IMessage
    {
        try
        {
            await _bus.PublishAsync(msg, cancellationToken: ct).ConfigureAwait(false);
            _logger.LogInformation(
                "[PriceEventPublisher] Published {EventType} ({Key})", typeof(TMsg).Name, eventKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[PriceEventPublisher] Failed to publish {EventType} ({Key}): {Error}",
                typeof(TMsg).Name, eventKey, ex.Message);
        }
    }
}

/// <summary>
/// No-op publisher used when messaging is disabled (RabbitMQ not configured).
/// Keeps service registrations and controller logic identical regardless of environment.
/// When messaging is disabled, events are logged so the event flow is still observable.
/// </summary>
public sealed class NullPriceEventPublisher : IPriceEventPublisher
{
    private readonly ILogger<NullPriceEventPublisher>? _logger;

    public NullPriceEventPublisher(ILogger<NullPriceEventPublisher>? logger = null)
        => _logger = logger;

    public Task PublishPriceRecordedAsync(
        Guid priceObservationId, Guid productId, Guid storeId,
        decimal price, Guid recordedBy, CancellationToken ct = default)
    {
        _logger?.LogDebug(
            "[NullPriceEventPublisher] PriceRecorded skipped (messaging disabled): obs={ObsId} product={ProductId} store={StoreId} price={Price}",
            priceObservationId, productId, storeId, price);
        return Task.CompletedTask;
    }

    public Task PublishPriceBatchSubmittedAsync(
        string sessionId, int itemCount, Guid submittedBy, CancellationToken ct = default)
    {
        _logger?.LogDebug(
            "[NullPriceEventPublisher] PriceBatchSubmitted skipped (messaging disabled): session={Session} count={Count}",
            sessionId, itemCount);
        return Task.CompletedTask;
    }

    public Task PublishDealCreatedAsync(
        Guid dealId, Guid productId, Guid storeId, string dealType,
        decimal originalPrice, decimal salePrice,
        DateTime startDate, DateTime endDate, CancellationToken ct = default)
    {
        _logger?.LogDebug(
            "[NullPriceEventPublisher] DealCreated skipped (messaging disabled): deal={DealId} product={ProductId}",
            dealId, productId);
        return Task.CompletedTask;
    }

    public Task PublishStoreAddedAsync(
        Guid storeId, string name, string? city, string? state,
        string? chain, CancellationToken ct = default)
    {
        _logger?.LogDebug(
            "[NullPriceEventPublisher] StoreAdded skipped (messaging disabled): store={StoreId} name={Name}",
            storeId, name);
        return Task.CompletedTask;
    }
}
