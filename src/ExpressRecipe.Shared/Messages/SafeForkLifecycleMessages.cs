using ExpressRecipe.Messaging.Core.Abstractions;

namespace ExpressRecipe.Shared.Messages;

/// <summary>
/// Routing key constants for SafeFork allergen-profile events.
/// </summary>
public static class SafeForkEventKeys
{
    public const string AllergenProfileUpdated         = "safefork.allergenprofile.updated";
    public const string AirborneSensitivityDetected    = "safefork.allergenprofile.airborne-detected";
    public const string AllergenProfileFreeformResolved = "safefork.allergenprofile.freeform-resolved";
}

/// <summary>
/// Emitted whenever an allergen-profile entry is added, updated, or removed.
/// Consumers: HybridCache invalidation for allergen:member:{id}.
/// </summary>
public record AllergenProfileUpdatedEvent(
    Guid MemberId,
    Guid? HouseholdId,
    DateTimeOffset OccurredAt) : IMessage;

/// <summary>
/// Emitted when a member's allergen profile entry is upgraded to AirborneSensitive.
/// Consumers: NotificationService (alert user), ShoppingService (remove product from suggestions).
/// </summary>
public record AirborneSensitivityDetectedEvent(
    Guid MemberId,
    Guid? HouseholdId,
    Guid AllergenProfileId,
    string AllergenName,
    DateTimeOffset OccurredAt) : IMessage;

/// <summary>
/// Emitted by AllergenResolutionWorkflow when a previously-unresolved freeform entry is matched.
/// Consumers: NotificationService (inform user of successful resolution).
/// </summary>
public record AllergenProfileFreeformResolvedEvent(
    Guid MemberId,
    Guid AllergenProfileId,
    string FreeFormName,
    int LinksFound,
    DateTimeOffset OccurredAt) : IMessage;
