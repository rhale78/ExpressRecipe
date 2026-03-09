using ExpressRecipe.Messaging.Core.Abstractions;

namespace ExpressRecipe.Shared.Messages;

/// <summary>
/// Request sent to RecipeService to fetch per-serving nutrition for a recipe.
/// </summary>
public sealed record RequestRecipeNutrition : IMessage
{
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
    public Guid RecipeId { get; init; }
    public decimal RequestedServings { get; init; } = 1m;
}

/// <summary>
/// Response to <see cref="RequestRecipeNutrition"/> containing per-serving macro data.
/// <see cref="HasData"/> is <c>false</c> when no nutrition row exists for the recipe.
/// </summary>
public sealed record RecipeNutritionResponse : IMessage
{
    public Guid CorrelationId { get; init; }
    public Guid RecipeId { get; init; }
    public bool HasData { get; init; }
    public decimal BaseServings { get; init; }
    public decimal CaloriesPerServing { get; init; }
    public decimal ProteinPerServing { get; init; }
    public decimal CarbsPerServing { get; init; }
    public decimal FatPerServing { get; init; }
    public decimal FiberPerServing { get; init; }
    public decimal SodiumPerServing { get; init; }
}
