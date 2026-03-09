using ExpressRecipe.Messaging.Core.Abstractions;
using ExpressRecipe.Shared.Messages;

namespace ExpressRecipe.SafeForkService.Services;

public interface ISafeForkEventPublisher
{
    Task PublishAllergenProfileUpdatedAsync(Guid memberId, Guid? householdId, CancellationToken ct = default);
    Task PublishAirborneSensitivityDetectedAsync(Guid memberId, Guid? householdId, Guid allergenProfileId, string allergenName, CancellationToken ct = default);
    Task PublishFreeformResolvedAsync(Guid memberId, Guid allergenProfileId, string freeFormName, int linksFound, CancellationToken ct = default);
}

public sealed class SafeForkEventPublisher : ISafeForkEventPublisher
{
    private readonly IMessageBus _bus;
    private readonly ILogger<SafeForkEventPublisher> _logger;

    public SafeForkEventPublisher(IMessageBus bus, ILogger<SafeForkEventPublisher> logger)
    {
        _bus = bus;
        _logger = logger;
    }

    public Task PublishAllergenProfileUpdatedAsync(Guid memberId, Guid? householdId, CancellationToken ct = default) =>
        SafePublishAsync(
            new AllergenProfileUpdatedEvent(memberId, householdId, DateTimeOffset.UtcNow),
            SafeForkEventKeys.AllergenProfileUpdated, ct);

    public Task PublishAirborneSensitivityDetectedAsync(Guid memberId, Guid? householdId, Guid allergenProfileId, string allergenName, CancellationToken ct = default) =>
        SafePublishAsync(
            new AirborneSensitivityDetectedEvent(memberId, householdId, allergenProfileId, allergenName, DateTimeOffset.UtcNow),
            SafeForkEventKeys.AirborneSensitivityDetected, ct);

    public Task PublishFreeformResolvedAsync(Guid memberId, Guid allergenProfileId, string freeFormName, int linksFound, CancellationToken ct = default) =>
        SafePublishAsync(
            new AllergenProfileFreeformResolvedEvent(memberId, allergenProfileId, freeFormName, linksFound, DateTimeOffset.UtcNow),
            SafeForkEventKeys.AllergenProfileFreeformResolved, ct);

    private async Task SafePublishAsync<TMsg>(TMsg msg, string eventKey, CancellationToken ct) where TMsg : IMessage
    {
        try
        {
            await _bus.PublishAsync(msg, cancellationToken: ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish SafeFork event {EventKey}", eventKey);
        }
    }
}

public sealed class NullSafeForkEventPublisher : ISafeForkEventPublisher
{
    public Task PublishAllergenProfileUpdatedAsync(Guid memberId, Guid? householdId, CancellationToken ct = default) => Task.CompletedTask;
    public Task PublishAirborneSensitivityDetectedAsync(Guid memberId, Guid? householdId, Guid allergenProfileId, string allergenName, CancellationToken ct = default) => Task.CompletedTask;
    public Task PublishFreeformResolvedAsync(Guid memberId, Guid allergenProfileId, string freeFormName, int linksFound, CancellationToken ct = default) => Task.CompletedTask;
}
