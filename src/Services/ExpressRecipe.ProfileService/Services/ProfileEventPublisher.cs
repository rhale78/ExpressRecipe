using ExpressRecipe.Messaging.Core.Abstractions;
using ExpressRecipe.Shared.Messages;

namespace ExpressRecipe.ProfileService.Services;

public interface IProfileEventPublisher
{
    Task PublishMemberAddedAsync(Guid memberId, Guid householdId, string memberType, string displayName, bool hasUserAccount, CancellationToken ct = default);
    Task PublishMemberRemovedAsync(Guid memberId, Guid householdId, string memberType, CancellationToken ct = default);
    Task PublishFamilyAdminNotificationAsync(Guid householdId, Guid memberId, string notificationType, string message, CancellationToken ct = default);
}

public sealed class ProfileEventPublisher : IProfileEventPublisher
{
    private readonly IMessageBus _bus;
    private readonly ILogger<ProfileEventPublisher> _logger;

    public ProfileEventPublisher(IMessageBus bus, ILogger<ProfileEventPublisher> logger)
    {
        _bus = bus;
        _logger = logger;
    }

    public Task PublishMemberAddedAsync(Guid memberId, Guid householdId, string memberType, string displayName, bool hasUserAccount, CancellationToken ct = default) =>
        SafePublishAsync(
            new HouseholdMemberAddedEvent(memberId, householdId, memberType, displayName, hasUserAccount, DateTimeOffset.UtcNow),
            ProfileEventKeys.MemberAdded, ct);

    public Task PublishMemberRemovedAsync(Guid memberId, Guid householdId, string memberType, CancellationToken ct = default) =>
        SafePublishAsync(
            new HouseholdMemberRemovedEvent(memberId, householdId, memberType, DateTimeOffset.UtcNow),
            ProfileEventKeys.MemberRemoved, ct);

    public Task PublishFamilyAdminNotificationAsync(Guid householdId, Guid memberId, string notificationType, string message, CancellationToken ct = default) =>
        SafePublishAsync(
            new FamilyAdminNotificationEvent(householdId, memberId, notificationType, message, DateTimeOffset.UtcNow),
            ProfileEventKeys.FamilyAdminNotification, ct);

    private async Task SafePublishAsync<TMsg>(TMsg msg, string eventKey, CancellationToken ct) where TMsg : IMessage
    {
        try
        {
            await _bus.PublishAsync(msg, cancellationToken: ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish profile event {EventKey}", eventKey);
        }
    }
}

/// <summary>
/// No-op publisher used when messaging is disabled (RabbitMQ not configured).
/// </summary>
public sealed class NullProfileEventPublisher : IProfileEventPublisher
{
    public Task PublishMemberAddedAsync(Guid memberId, Guid householdId, string memberType, string displayName, bool hasUserAccount, CancellationToken ct = default) => Task.CompletedTask;
    public Task PublishMemberRemovedAsync(Guid memberId, Guid householdId, string memberType, CancellationToken ct = default) => Task.CompletedTask;
    public Task PublishFamilyAdminNotificationAsync(Guid householdId, Guid memberId, string notificationType, string message, CancellationToken ct = default) => Task.CompletedTask;
}
