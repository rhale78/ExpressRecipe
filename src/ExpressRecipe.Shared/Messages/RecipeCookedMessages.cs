using ExpressRecipe.Messaging.Core.Abstractions;

namespace ExpressRecipe.Shared.Messages;

/// <summary>
/// Routing key constants for recipe-cooked events.
/// </summary>
public static class RecipeCookedEventKeys
{
    public const string Cooked = "recipe.cooked";
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
    Guid CookingHistoryId) : IMessage;
