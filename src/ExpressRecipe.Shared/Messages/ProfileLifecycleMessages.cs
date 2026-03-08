using ExpressRecipe.Messaging.Core.Abstractions;

namespace ExpressRecipe.Shared.Messages;

/// <summary>
/// Routing key constants for household-member / profile lifecycle events.
/// </summary>
public static class ProfileEventKeys
{
    public const string MemberAdded   = "profile.member.added";
    public const string MemberRemoved = "profile.member.removed";
    public const string FamilyAdminNotification = "profile.admin.notification";
}

/// <summary>
/// Emitted after a new household member record is created.
/// Consumers: SafeForkService (init AllergenProfile), PreferencesService (init CookProfile).
/// </summary>
public record HouseholdMemberAddedEvent(
    Guid MemberId,
    Guid HouseholdId,
    string MemberType,
    string DisplayName,
    bool HasUserAccount,
    DateTimeOffset OccurredAt) : IMessage;

/// <summary>
/// Emitted after a household member is soft-deleted.
/// Consumers: SafeForkService (soft-delete allergen profile), PreferencesService (soft-delete cook profile).
/// </summary>
public record HouseholdMemberRemovedEvent(
    Guid MemberId,
    Guid HouseholdId,
    string MemberType,
    DateTimeOffset OccurredAt) : IMessage;

/// <summary>
/// Emitted when a Teen member activates their account.
/// Consumers: NotificationService (notify FamilyAdmin).
/// </summary>
public record FamilyAdminNotificationEvent(
    Guid HouseholdId,
    Guid MemberId,
    string NotificationType,
    string Message,
    DateTimeOffset OccurredAt) : IMessage;
