using ExpressRecipe.Messaging.Core.Abstractions;

namespace ExpressRecipe.Shared.Messages;

/// <summary>
/// Request sent to RecipeService to fetch per-serving nutrition for a recipe.
/// The messaging infrastructure correlates request/response via the envelope;
/// no payload CorrelationId is needed.
/// </summary>
public sealed record RequestRecipeNutrition : IMessage
{
    public Guid RecipeId { get; init; }
    public decimal RequestedServings { get; init; } = 1m;
}

/// <summary>
/// Response to <see cref="RequestRecipeNutrition"/> containing per-serving macro data.
/// <see cref="HasData"/> is <c>false</c> when no nutrition row exists for the recipe.
/// All macro fields are expressed per single serving of the recipe.
/// </summary>
public sealed record RecipeNutritionResponse : IMessage
{
    public Guid RecipeId { get; init; }
    public bool HasData { get; init; }
    public decimal CaloriesPerServing { get; init; }
    public decimal ProteinPerServing { get; init; }
    public decimal CarbsPerServing { get; init; }
    public decimal FatPerServing { get; init; }
    public decimal FiberPerServing { get; init; }
    public decimal SodiumPerServing { get; init; }
}
