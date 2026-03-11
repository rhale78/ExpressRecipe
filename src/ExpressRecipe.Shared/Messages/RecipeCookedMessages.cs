using ExpressRecipe.Messaging.Core.Abstractions;

namespace ExpressRecipe.Shared.Messages;

/// <summary>
/// Routing key constants for recipe-cooked events.
/// </summary>
public static class RecipeCookedEventKeys
{
    public const string Cooked  = "recipe.cooked";
    public const string Session = "recipe.cooked.session";
}

/// <summary>
/// Published when a user cooks a recipe, allowing inventory to be decremented
/// and cooking history to be correlated.
/// </summary>
public record RecipeCookedEvent(
    Guid RecipeId,
    Guid UserId,
    Guid? HouseholdId,
    int Servings,
    DateTimeOffset CookedAt,
    Guid CookingHistoryId) : IMessage
{
    /// <summary>True when the cooking history row already has a user rating at publish time.</summary>
    public bool HasRating { get; init; } = false;
}

/// <summary>
/// Published when a cook session is logged via CookSessionController,
/// allowing WorkQueue generators (e.g. MealPlanningService) to subscribe.
/// </summary>
public sealed record RecipeCookedSessionEvent : IMessage
{
    public Guid SessionId { get; init; }
    public Guid UserId { get; init; }
    public Guid HouseholdId { get; init; }
    public Guid RecipeId { get; init; }
    public DateTimeOffset CookedAt { get; init; }
    public bool HasRating { get; init; }
}
