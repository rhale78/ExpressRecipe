using ExpressRecipe.Messaging.Core.Abstractions;
using ExpressRecipe.Shared.Messages;

namespace ExpressRecipe.RecipeService.Services;

/// <summary>
/// Publishes recipe domain events to the message bus.
/// All methods are fire-and-forget on a best-effort basis: if the bus is
/// unavailable the warning is logged and the caller is not disrupted.
/// </summary>
public interface IRecipeEventPublisher
{
    Task PublishCreatedAsync(Guid recipeId, string name, string? category,
        string? cuisine, Guid createdBy, CancellationToken ct = default);

    Task PublishUpdatedAsync(Guid recipeId, string name, string? category,
        string? cuisine, Guid updatedBy, IReadOnlyList<string> changedFields,
        CancellationToken ct = default);

    Task PublishDeletedAsync(Guid recipeId, Guid deletedBy, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class RecipeEventPublisher : IRecipeEventPublisher
{
    private readonly IMessageBus _bus;
    private readonly ILogger<RecipeEventPublisher> _logger;

    public RecipeEventPublisher(IMessageBus bus, ILogger<RecipeEventPublisher> logger)
    {
        _bus    = bus;
        _logger = logger;
    }

    public Task PublishCreatedAsync(Guid recipeId, string name, string? category,
        string? cuisine, Guid createdBy, CancellationToken ct = default) =>
        SafePublishAsync(
            new RecipeCreatedEvent(recipeId, name, category, cuisine, createdBy, DateTimeOffset.UtcNow),
            RecipeEventKeys.Created, ct);

    public Task PublishUpdatedAsync(Guid recipeId, string name, string? category,
        string? cuisine, Guid updatedBy, IReadOnlyList<string> changedFields,
        CancellationToken ct = default) =>
        SafePublishAsync(
            new RecipeUpdatedEvent(recipeId, name, category, cuisine, updatedBy, changedFields, DateTimeOffset.UtcNow),
            RecipeEventKeys.Updated, ct);

    public Task PublishDeletedAsync(Guid recipeId, Guid deletedBy, CancellationToken ct = default) =>
        SafePublishAsync(
            new RecipeDeletedEvent(recipeId, deletedBy, DateTimeOffset.UtcNow),
            RecipeEventKeys.Deleted, ct);

    private async Task SafePublishAsync<TMsg>(TMsg msg, string eventKey, CancellationToken ct)
        where TMsg : IMessage
    {
        try
        {
            await _bus.PublishAsync(msg, cancellationToken: ct).ConfigureAwait(false);
            _logger.LogInformation("[RecipeEventPublisher] Published {EventType} ({Key})", typeof(TMsg).Name, eventKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[RecipeEventPublisher] Failed to publish {EventType} ({Key}): {Error}",
                typeof(TMsg).Name, eventKey, ex.Message);
        }
    }
}

/// <summary>
/// No-op publisher used when messaging is disabled (RabbitMQ not configured).
/// Keeps service registrations identical regardless of environment.
/// </summary>
public sealed class NullRecipeEventPublisher : IRecipeEventPublisher
{
    public Task PublishCreatedAsync(Guid recipeId, string name, string? category,
        string? cuisine, Guid createdBy, CancellationToken ct = default) => Task.CompletedTask;

    public Task PublishUpdatedAsync(Guid recipeId, string name, string? category,
        string? cuisine, Guid updatedBy, IReadOnlyList<string> changedFields,
        CancellationToken ct = default) => Task.CompletedTask;

    public Task PublishDeletedAsync(Guid recipeId, Guid deletedBy, CancellationToken ct = default)
        => Task.CompletedTask;
}
