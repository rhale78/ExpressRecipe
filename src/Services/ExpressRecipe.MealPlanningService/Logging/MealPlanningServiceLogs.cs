namespace ExpressRecipe.MealPlanningService.Logging;

using Microsoft.Extensions.Logging;

/// <summary>
/// High-performance source-generated logging for MealPlanningService.
/// Zero allocation when log level is disabled.
/// </summary>
public static partial class MealPlanningServiceLogs
{
    // ── Controller: Meal Plans ────────────────────────────────────────────────

    [LoggerMessage(
        EventId = 4001,
        Level = LogLevel.Information,
        Message = "[MealPlanning] Creating meal plan for user {UserId} ({StartDate:d} to {EndDate:d})")]
    public static partial void LogCreatingMealPlan(this ILogger logger, Guid userId, DateTime startDate, DateTime endDate);

    [LoggerMessage(
        EventId = 4002,
        Level = LogLevel.Information,
        Message = "[MealPlanning] Meal plan {PlanId} created for user {UserId}")]
    public static partial void LogMealPlanCreated(this ILogger logger, Guid userId, Guid planId);

    [LoggerMessage(
        EventId = 4003,
        Level = LogLevel.Information,
        Message = "[MealPlanning] Meal plan {PlanId} deleted by user {UserId}")]
    public static partial void LogMealPlanDeleted(this ILogger logger, Guid userId, Guid planId);

    [LoggerMessage(
        EventId = 4004,
        Level = LogLevel.Debug,
        Message = "[MealPlanning] Getting meal plans for user {UserId}")]
    public static partial void LogGettingMealPlans(this ILogger logger, Guid userId);

    [LoggerMessage(
        EventId = 4005,
        Level = LogLevel.Debug,
        Message = "[MealPlanning] Getting meal plan {PlanId} for user {UserId}")]
    public static partial void LogGettingMealPlan(this ILogger logger, Guid userId, Guid planId);

    // ── Controller: Planned Meals ─────────────────────────────────────────────

    [LoggerMessage(
        EventId = 4006,
        Level = LogLevel.Information,
        Message = "[MealPlanning] Adding planned meal (recipe {RecipeId}) to plan {PlanId} for user {UserId}")]
    public static partial void LogAddingPlannedMeal(this ILogger logger, Guid userId, Guid planId, Guid recipeId);

    [LoggerMessage(
        EventId = 4007,
        Level = LogLevel.Information,
        Message = "[MealPlanning] Planned meal {MealId} added to plan {PlanId} for user {UserId}")]
    public static partial void LogPlannedMealAdded(this ILogger logger, Guid userId, Guid planId, Guid mealId);

    [LoggerMessage(
        EventId = 4008,
        Level = LogLevel.Debug,
        Message = "[MealPlanning] Getting planned meals for plan {PlanId}")]
    public static partial void LogGettingPlannedMeals(this ILogger logger, Guid planId);

    [LoggerMessage(
        EventId = 4009,
        Level = LogLevel.Information,
        Message = "[MealPlanning] Completing planned meal {MealId} in plan {PlanId} for user {UserId}")]
    public static partial void LogCompletingPlannedMeal(this ILogger logger, Guid userId, Guid planId, Guid mealId);

    [LoggerMessage(
        EventId = 4010,
        Level = LogLevel.Information,
        Message = "[MealPlanning] Planned meal {MealId} completed \u2014 cooking history {HistoryId} recorded for user {UserId}")]
    public static partial void LogPlannedMealCompleted(this ILogger logger, Guid userId, Guid mealId, Guid historyId);

    // ── Controller: Goals ─────────────────────────────────────────────────────

    [LoggerMessage(
        EventId = 4011,
        Level = LogLevel.Information,
        Message = "[MealPlanning] Setting nutritional goal '{GoalType}' for user {UserId}")]
    public static partial void LogSettingGoal(this ILogger logger, Guid userId, string goalType);

    [LoggerMessage(
        EventId = 4012,
        Level = LogLevel.Debug,
        Message = "[MealPlanning] Getting nutritional goals for user {UserId}")]
    public static partial void LogGettingGoals(this ILogger logger, Guid userId);

    // ── Controller: Cooking History ───────────────────────────────────────────

    [LoggerMessage(
        EventId = 4013,
        Level = LogLevel.Information,
        Message = "[MealPlanning] Recording cooking history for user {UserId}, recipe {RecipeId}, household {HouseholdId}")]
    public static partial void LogRecordingCookingHistory(this ILogger logger, Guid userId, Guid recipeId, Guid? householdId);

    [LoggerMessage(
        EventId = 4014,
        Level = LogLevel.Information,
        Message = "[MealPlanning] Cooking history {HistoryId} recorded for user {UserId}")]
    public static partial void LogCookingHistoryRecorded(this ILogger logger, Guid userId, Guid historyId);

    [LoggerMessage(
        EventId = 4015,
        Level = LogLevel.Information,
        Message = "[MealPlanning] Updating cooking rating for history {HistoryId} to {Rating} by user {UserId}")]
    public static partial void LogUpdatingCookingRating(this ILogger logger, Guid userId, Guid historyId, byte rating);

    [LoggerMessage(
        EventId = 4016,
        Level = LogLevel.Debug,
        Message = "[MealPlanning] Getting last {DaysBack} days of cooking history for user {UserId}")]
    public static partial void LogGettingCookingHistory(this ILogger logger, Guid userId, int daysBack);

    // ── Controller: Suggestions ───────────────────────────────────────────────

    [LoggerMessage(
        EventId = 4017,
        Level = LogLevel.Information,
        Message = "[MealPlanning] Getting {Mode} suggestions for user {UserId} (mealType: {MealType})")]
    public static partial void LogGettingSuggestions(this ILogger logger, Guid userId, string mode, string mealType);

    [LoggerMessage(
        EventId = 4018,
        Level = LogLevel.Debug,
        Message = "[MealPlanning] Generated {Count} meal suggestions for user {UserId}")]
    public static partial void LogSuggestionsGenerated(this ILogger logger, Guid userId, int count);

    [LoggerMessage(
        EventId = 4029,
        Level = LogLevel.Information,
        Message = "[MealPlanning] Week suggestions requested for user {UserId} (mode: {Mode})")]
    public static partial void LogWeekSuggestionsRequested(this ILogger logger, Guid userId, string mode);

    // ── Controller: Shopping List ─────────────────────────────────────────────

    [LoggerMessage(
        EventId = 4019,
        Level = LogLevel.Information,
        Message = "[MealPlanning] Generating shopping list from plan {PlanId} ({MealCount} meals) for user {UserId}")]
    public static partial void LogGeneratingShoppingList(this ILogger logger, Guid userId, Guid planId, int mealCount);

    [LoggerMessage(
        EventId = 4020,
        Level = LogLevel.Information,
        Message = "[MealPlanning] Shopping list generated from plan {PlanId}: {ItemsAdded} items added for user {UserId}")]
    public static partial void LogShoppingListGenerated(this ILogger logger, Guid userId, Guid planId, int itemsAdded);

    // ── Controller: Nutrition / Most-cooked ──────────────────────────────────

    [LoggerMessage(
        EventId = 4021,
        Level = LogLevel.Debug,
        Message = "[MealPlanning] Getting nutrition summary for user {UserId} on {Date:d}")]
    public static partial void LogNutritionSummaryRequest(this ILogger logger, Guid userId, DateTime date);

    [LoggerMessage(
        EventId = 4022,
        Level = LogLevel.Debug,
        Message = "[MealPlanning] Getting most-cooked recipes for user {UserId} (last {DaysBack} days)")]
    public static partial void LogMostCookedRequest(this ILogger logger, Guid userId, int daysBack);

    // ── Service: Suggestion pipeline ─────────────────────────────────────────

    [LoggerMessage(
        EventId = 4023,
        Level = LogLevel.Debug,
        Message = "[MealPlanning] {Count} recipe candidates fetched for user {UserId} (mealType: {MealType})")]
    public static partial void LogSuggestionCandidates(this ILogger logger, Guid userId, int count, string mealType);

    [LoggerMessage(
        EventId = 4024,
        Level = LogLevel.Debug,
        Message = "[MealPlanning] After '{Reason}' filter: {Remaining} candidates remain for user {UserId}")]
    public static partial void LogSuggestionFiltered(this ILogger logger, Guid userId, int remaining, string reason);

    [LoggerMessage(
        EventId = 4025,
        Level = LogLevel.Debug,
        Message = "[MealPlanning] Suggestion cache hit for user {UserId} (key: {CacheKey})")]
    public static partial void LogSuggestionCacheHit(this ILogger logger, Guid userId, string cacheKey);

    [LoggerMessage(
        EventId = 4026,
        Level = LogLevel.Debug,
        Message = "[MealPlanning] Suggestion cache miss for user {UserId} (key: {CacheKey})")]
    public static partial void LogSuggestionCacheMiss(this ILogger logger, Guid userId, string cacheKey);

    // ── Workers ───────────────────────────────────────────────────────────────

    [LoggerMessage(
        EventId = 4027,
        Level = LogLevel.Debug,
        Message = "[MealPlanning] Publishing RecipeCookedEvent for user {UserId} household {HouseholdId} (history {HistoryId})")]
    public static partial void LogPublishingRecipeCookedEvent(this ILogger logger, Guid userId, Guid? householdId, Guid historyId);

    [LoggerMessage(
        EventId = 4028,
        Level = LogLevel.Debug,
        Message = "[MealPlanning] Sending rating prompt for user {UserId} \u2014 recipe '{RecipeName}' (history {HistoryId})")]
    public static partial void LogSendingRatingPrompt(this ILogger logger, Guid userId, Guid historyId, string recipeName);
}
