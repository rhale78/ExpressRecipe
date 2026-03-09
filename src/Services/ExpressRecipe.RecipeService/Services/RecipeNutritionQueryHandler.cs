using ExpressRecipe.Messaging.Core.Abstractions;
using ExpressRecipe.Messaging.Core.Messages;
using ExpressRecipe.RecipeService.Data;
using ExpressRecipe.Shared.Messages;

namespace ExpressRecipe.RecipeService.Services;

/// <summary>
/// Handles <see cref="RequestRecipeNutrition"/> messages from other services (e.g. MealPlanningService).
/// Returns per-serving macro data for the requested recipe via the request/response messaging pattern.
/// </summary>
public sealed class RecipeNutritionQueryHandler : IRequestHandler<RequestRecipeNutrition, RecipeNutritionResponse>
{
    private readonly IRecipeNutritionRepository _nutrition;
    private readonly ILogger<RecipeNutritionQueryHandler> _logger;

    public RecipeNutritionQueryHandler(
        IRecipeNutritionRepository nutrition,
        ILogger<RecipeNutritionQueryHandler> logger)
    {
        _nutrition = nutrition;
        _logger = logger;
    }

    public async Task<RecipeNutritionResponse> HandleAsync(
        RequestRecipeNutrition request,
        MessageContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("[RecipeNutritionQueryHandler] Nutrition lookup: RecipeId={RecipeId}", request.RecipeId);

        RecipeNutritionRow? row = await _nutrition.GetByRecipeIdAsync(request.RecipeId, cancellationToken);

        if (row is null)
        {
            return new RecipeNutritionResponse
            {
                CorrelationId = request.CorrelationId,
                RecipeId = request.RecipeId,
                HasData = false
            };
        }

        return new RecipeNutritionResponse
        {
            CorrelationId = request.CorrelationId,
            RecipeId = request.RecipeId,
            HasData = true,
            BaseServings = row.BaseServings,
            CaloriesPerServing = row.Calories,
            ProteinPerServing = row.Protein,
            CarbsPerServing = row.TotalCarbohydrates,
            FatPerServing = row.TotalFat,
            FiberPerServing = row.DietaryFiber,
            SodiumPerServing = row.Sodium
        };
    }
}
