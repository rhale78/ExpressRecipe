using ExpressRecipe.Messaging.Core.Abstractions;

namespace ExpressRecipe.Shared.Messages;

/// <summary>
/// Routing key constants for recipe cooking events.
/// </summary>
public static class RecipeCookedEventKeys
{
    public const string Cooked = "recipe.cooked";
}

/// <summary>
/// Published when a recipe is cooked (for inventory deduction).
/// </summary>
public record RecipeCookedEvent(
    Guid RecipeId,
    Guid UserId,
    Guid? HouseholdId,
    int Servings,
    DateTimeOffset CookedAt,
    Guid CookingHistoryId) : IMessage;
