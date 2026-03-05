using ExpressRecipe.Messaging.Core.Abstractions;
using ExpressRecipe.Shared.Messages;

namespace ExpressRecipe.IngredientService.Services;

/// <summary>
/// Publishes ingredient domain events to the message bus.
/// All methods are fire-and-forget on a best-effort basis: if the bus is
/// unavailable the warning is logged and the caller is not disrupted.
/// </summary>
public interface IIngredientEventPublisher
{
    Task PublishCreatedAsync(Guid ingredientId, string name, CancellationToken ct = default);

    Task PublishUpdatedAsync(Guid ingredientId, string name, string? oldName, CancellationToken ct = default);

    Task PublishDeletedAsync(Guid ingredientId, string? name, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class IngredientEventPublisher : IIngredientEventPublisher
{
    private readonly IMessageBus _bus;
    private readonly ILogger<IngredientEventPublisher> _logger;

    public IngredientEventPublisher(IMessageBus bus, ILogger<IngredientEventPublisher> logger)
    {
        _bus    = bus;
        _logger = logger;
    }

    public Task PublishCreatedAsync(Guid ingredientId, string name, CancellationToken ct = default) =>
        SafePublishAsync(
            new IngredientCreatedEvent(ingredientId, name, DateTimeOffset.UtcNow),
            IngredientEventKeys.Created, ct);

    public Task PublishUpdatedAsync(Guid ingredientId, string name, string? oldName, CancellationToken ct = default) =>
        SafePublishAsync(
            new IngredientUpdatedEvent(ingredientId, name, oldName, DateTimeOffset.UtcNow),
            IngredientEventKeys.Updated, ct);

    public Task PublishDeletedAsync(Guid ingredientId, string? name, CancellationToken ct = default) =>
        SafePublishAsync(
            new IngredientDeletedEvent(ingredientId, name, DateTimeOffset.UtcNow),
            IngredientEventKeys.Deleted, ct);

    private async Task SafePublishAsync<TMsg>(TMsg msg, string eventKey, CancellationToken ct)
        where TMsg : IMessage
    {
        try
        {
            await _bus.PublishAsync(msg, cancellationToken: ct).ConfigureAwait(false);
            _logger.LogInformation("[IngredientEventPublisher] Published {EventType} ({Key})", typeof(TMsg).Name, eventKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[IngredientEventPublisher] Failed to publish {EventType} ({Key}): {Error}",
                typeof(TMsg).Name, eventKey, ex.Message);
        }
    }
}

/// <summary>
/// No-op publisher used when messaging is disabled (RabbitMQ not configured).
/// Keeps service registrations identical regardless of environment.
/// </summary>
public sealed class NullIngredientEventPublisher : IIngredientEventPublisher
{
    public Task PublishCreatedAsync(Guid ingredientId, string name, CancellationToken ct = default) => Task.CompletedTask;

    public Task PublishUpdatedAsync(Guid ingredientId, string name, string? oldName, CancellationToken ct = default) => Task.CompletedTask;

    public Task PublishDeletedAsync(Guid ingredientId, string? name, CancellationToken ct = default) => Task.CompletedTask;
}
