using ExpressRecipe.Messaging.Core.Abstractions;

namespace ExpressRecipe.Shared.Messages;

/// <summary>
/// Routing key constants for GDPR events.
/// </summary>
public static class GdprEventKeys
{
    public const string DeleteUser   = "gdpr.user.delete";
    public const string ForgetUser   = "gdpr.user.forget";
    public const string DeleteMember = "gdpr.member.delete";
}

/// <summary>
/// Published by UserService when a user initiates account deletion.
/// Each microservice subscribes and hard-deletes all data owned by <see cref="UserId"/>.
/// UserService itself deletes last (after a 24 h confirmation window tracked via GdprRequest.Status).
/// </summary>
public record GdprDeleteEvent(
    Guid UserId,
    Guid RequestId,
    DateTimeOffset OccurredAt) : IMessage;

/// <summary>
/// Published by UserService when a user requests the "Right to be Forgotten" (anonymise).
/// Services that hold cross-service records (e.g. CommunityService) should anonymise
/// any reference to <see cref="UserId"/> rather than hard-deleting.
/// </summary>
public record GdprForgetEvent(
    Guid UserId,
    Guid RequestId,
    DateTimeOffset OccurredAt) : IMessage;

/// <summary>
/// Published by ProfileService after hard-deleting a <see cref="HouseholdMember"/> row as part of a
/// GDPR account-deletion request.  Downstream services that track data by <see cref="MemberId"/>
/// (e.g. PreferencesService, SafeForkService) subscribe and hard-delete their member-scoped rows.
/// </summary>
public record MemberGdprDeleteEvent(
    Guid MemberId,
    Guid UserId,
    Guid RequestId,
    DateTimeOffset OccurredAt) : IMessage;
