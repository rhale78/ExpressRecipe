using ExpressRecipe.Messaging.Core.Abstractions;

namespace ExpressRecipe.Shared.Messages;

// ── Routing keys ────────────────────────────────────────────────────────────

/// <summary>Routing key constants for the MemberOnboarding saga.</summary>
public static class MemberOnboardingKeys
{
    public const string CreateMember          = "profile.createmember";
    public const string InitAllergenProfile   = "safefork.initallergen";
    public const string InitCookProfile       = "preferences.initcook";
    public const string SendWelcome           = "notification.welcome";
}

// ── Requests ─────────────────────────────────────────────────────────────────

public record RequestCreateMember(
    string CorrelationId,
    Guid HouseholdId,
    string MemberType,
    string DisplayName) : IMessage;

public record RequestInitAllergenProfile(
    string CorrelationId,
    Guid MemberId) : IMessage;

public record RequestInitCookProfile(
    string CorrelationId,
    Guid MemberId) : IMessage;

public record RequestSendWelcome(
    string CorrelationId,
    Guid MemberId) : IMessage;

// ── Results ──────────────────────────────────────────────────────────────────

public record MemberRecordCreated(
    string CorrelationId,
    Guid MemberId) : IMessage;

public record AllergenProfileInitialized(
    string CorrelationId,
    Guid MemberId) : IMessage;

public record CookProfileInitialized(
    string CorrelationId,
    Guid MemberId) : IMessage;

public record WelcomeNotificationSent(
    string CorrelationId,
    Guid MemberId) : IMessage;
