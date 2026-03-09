using ExpressRecipe.Messaging.Core.Abstractions;
using ExpressRecipe.MealPlanningService.Data;
using ExpressRecipe.Shared.Messages;

namespace ExpressRecipe.MealPlanningService.Services;

public interface INutritionLoggingService
{
    Task LogCookingEventAsync(Guid userId, Guid recipeId, string recipeName,
        string? mealType, decimal servingsEaten, Guid? cookingHistoryId, Guid? plannedMealId,
        CancellationToken ct = default);

    Task LogManualEntryAsync(Guid userId, string recipeName, string? mealType,
        decimal servingsEaten, decimal? calories, decimal? protein, decimal? carbs,
        decimal? fat, decimal? fiber, decimal? sodium, CancellationToken ct = default);
}

/// <summary>
/// Logs cooking events and manual nutrition entries to <see cref="DailyNutritionLog"/>.
/// When messaging is available, fetches per-serving macros from RecipeService via
/// <see cref="RequestRecipeNutrition"/>. On timeout or bus failure, the row is still
/// inserted with null nutrition columns so no log entry is silently lost.
/// </summary>
public sealed class NutritionLoggingService : INutritionLoggingService
{
    private static readonly TimeSpan NutritionRequestTimeout = TimeSpan.FromSeconds(2);

    private readonly INutritionLogRepository _logRepo;
    private readonly IMessageBus? _bus;
    private readonly ILogger<NutritionLoggingService> _logger;

    public NutritionLoggingService(
        INutritionLogRepository logRepo,
        IMessageBus? bus,
        ILogger<NutritionLoggingService> logger)
    {
        _logRepo = logRepo;
        _bus     = bus;
        _logger  = logger;
    }

    public async Task LogCookingEventAsync(Guid userId, Guid recipeId, string recipeName,
        string? mealType, decimal servingsEaten, Guid? cookingHistoryId, Guid? plannedMealId,
        CancellationToken ct = default)
    {
        RecipeNutritionResponse? nutrition = await FetchNutritionAsync(recipeId, ct);

        decimal? calories = null, protein = null, carbs = null, fat = null, fiber = null, sodium = null;
        if (nutrition is { HasData: true })
        {
            decimal ratio = nutrition.BaseServings > 0
                ? servingsEaten / nutrition.BaseServings
                : servingsEaten;

            calories = nutrition.CaloriesPerServing * ratio;
            protein  = nutrition.ProteinPerServing  * ratio;
            carbs    = nutrition.CarbsPerServing    * ratio;
            fat      = nutrition.FatPerServing      * ratio;
            fiber    = nutrition.FiberPerServing    * ratio;
            sodium   = nutrition.SodiumPerServing   * ratio;
        }

        await _logRepo.InsertLogAsync(new DailyNutritionLogRow
        {
            Id               = Guid.NewGuid(),
            UserId           = userId,
            LogDate          = DateOnly.FromDateTime(DateTime.UtcNow),
            MealType         = mealType,
            CookingHistoryId = cookingHistoryId,
            RecipeId         = recipeId,
            RecipeName       = recipeName,
            ServingsEaten    = servingsEaten,
            Calories         = calories,
            Protein          = protein,
            Carbohydrates    = carbs,
            TotalFat         = fat,
            DietaryFiber     = fiber,
            Sodium           = sodium,
            IsManualEntry    = false
        }, ct);

        if (cookingHistoryId.HasValue)
        {
            await _logRepo.MarkNutritionLoggedAsync(cookingHistoryId.Value, ct);
        }
    }

    public async Task LogManualEntryAsync(Guid userId, string recipeName, string? mealType,
        decimal servingsEaten, decimal? calories, decimal? protein, decimal? carbs,
        decimal? fat, decimal? fiber, decimal? sodium, CancellationToken ct = default)
    {
        await _logRepo.InsertLogAsync(new DailyNutritionLogRow
        {
            Id            = Guid.NewGuid(),
            UserId        = userId,
            LogDate       = DateOnly.FromDateTime(DateTime.UtcNow),
            MealType      = mealType,
            RecipeName    = recipeName,
            ServingsEaten = servingsEaten,
            Calories      = calories,
            Protein       = protein,
            Carbohydrates = carbs,
            TotalFat      = fat,
            DietaryFiber  = fiber,
            Sodium        = sodium,
            IsManualEntry = true
        }, ct);
    }

    private async Task<RecipeNutritionResponse?> FetchNutritionAsync(Guid recipeId, CancellationToken ct)
    {
        if (_bus is null)
        {
            return null;
        }

        try
        {
            using CancellationTokenSource timeout = new(NutritionRequestTimeout);
            using CancellationTokenSource linked  =
                CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);

            return await _bus.RequestAsync<RequestRecipeNutrition, RecipeNutritionResponse>(
                new RequestRecipeNutrition { RecipeId = recipeId }, null, linked.Token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Could not fetch nutrition for RecipeId={RecipeId}; logging without macros", recipeId);
            return null;
        }
    }
}
