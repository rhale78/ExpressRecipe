namespace ExpressRecipe.MealPlanningService.Services;

public interface IMealSuggestionService
{
    Task<List<MealSuggestion>> SuggestAsync(SuggestionRequest request, CancellationToken ct = default);
    Task<List<MealSuggestion>> SuggestForWeekAsync(Guid userId, Guid? householdId, SuggestionRequest baseRequest, CancellationToken ct = default);
}
