using ExpressRecipe.Messaging.Core.Abstractions;

namespace ExpressRecipe.Shared.Messages;

/// <summary>
/// Routing key constants for meal-plan lifecycle events.
/// </summary>
public static class MealPlanEventKeys
{
    public const string Updated = "mealplan.updated";
}

/// <summary>
/// Published by MealPlanningService when a meal is rescheduled or removed.
/// Consumed by InventoryService (MealDelayStorageSubscriber).
/// </summary>
public record MealPlanUpdatedEvent(
    Guid HouseholdId,
    Guid? RecipeId,
    DateTime? OldPlannedDate,
    DateTime? NewPlannedDate,
    string ChangeType,        // "Rescheduled" | "Removed" | "Added"
    DateTimeOffset OccurredAt) : IMessage;
