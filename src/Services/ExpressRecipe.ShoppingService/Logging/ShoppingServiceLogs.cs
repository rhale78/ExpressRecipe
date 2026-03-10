namespace ExpressRecipe.ShoppingService.Logging;

using Microsoft.Extensions.Logging;

/// <summary>
/// High-performance source-generated logging for ShoppingService.
/// Zero allocation when log level is disabled.
/// </summary>
public static partial class ShoppingServiceLogs
{
    // ── ShoppingController ─────────────────────────────────────────────────────

    [LoggerMessage(
        EventId = 5001,
        Level = LogLevel.Debug,
        Message = "[Shopping] Getting shopping lists for user {UserId}")]
    public static partial void LogGettingLists(this ILogger logger, Guid userId);

    [LoggerMessage(
        EventId = 5002,
        Level = LogLevel.Information,
        Message = "[Shopping] Shopping list {ListId} created for user {UserId}")]
    public static partial void LogListCreated(this ILogger logger, Guid userId, Guid listId);

    [LoggerMessage(
        EventId = 5003,
        Level = LogLevel.Debug,
        Message = "[Shopping] Getting shopping list {ListId} for user {UserId}")]
    public static partial void LogGettingList(this ILogger logger, Guid userId, Guid listId);

    [LoggerMessage(
        EventId = 5004,
        Level = LogLevel.Information,
        Message = "[Shopping] Shopping list {ListId} updated by user {UserId}")]
    public static partial void LogListUpdated(this ILogger logger, Guid userId, Guid listId);

    [LoggerMessage(
        EventId = 5005,
        Level = LogLevel.Information,
        Message = "[Shopping] Shopping list {ListId} deleted by user {UserId}")]
    public static partial void LogListDeleted(this ILogger logger, Guid userId, Guid listId);

    [LoggerMessage(
        EventId = 5006,
        Level = LogLevel.Debug,
        Message = "[Shopping] Getting items for list {ListId} (user {UserId})")]
    public static partial void LogGettingListItems(this ILogger logger, Guid userId, Guid listId);

    [LoggerMessage(
        EventId = 5007,
        Level = LogLevel.Information,
        Message = "[Shopping] Item added to list {ListId} for user {UserId}")]
    public static partial void LogItemAdded(this ILogger logger, Guid userId, Guid listId);

    [LoggerMessage(
        EventId = 5008,
        Level = LogLevel.Information,
        Message = "[Shopping] Item {ItemId} quantity updated by user {UserId}")]
    public static partial void LogItemQuantityUpdated(this ILogger logger, Guid userId, Guid itemId);

    [LoggerMessage(
        EventId = 5009,
        Level = LogLevel.Debug,
        Message = "[Shopping] Item {ItemId} toggled by user {UserId}")]
    public static partial void LogItemToggled(this ILogger logger, Guid userId, Guid itemId);

    [LoggerMessage(
        EventId = 5010,
        Level = LogLevel.Information,
        Message = "[Shopping] Item {ItemId} removed from list by user {UserId}")]
    public static partial void LogItemRemoved(this ILogger logger, Guid userId, Guid itemId);

    [LoggerMessage(
        EventId = 5011,
        Level = LogLevel.Information,
        Message = "[Shopping] List {ListId} shared by user {UserId}")]
    public static partial void LogListShared(this ILogger logger, Guid userId, Guid listId);

    [LoggerMessage(
        EventId = 5012,
        Level = LogLevel.Debug,
        Message = "[Shopping] Getting shared lists for user {UserId}")]
    public static partial void LogGettingSharedLists(this ILogger logger, Guid userId);

    [LoggerMessage(
        EventId = 5013,
        Level = LogLevel.Debug,
        Message = "[Shopping] Getting stores for user {UserId}")]
    public static partial void LogGettingStores(this ILogger logger, Guid userId);

    [LoggerMessage(
        EventId = 5014,
        Level = LogLevel.Information,
        Message = "[Shopping] Store created by user {UserId}")]
    public static partial void LogStoreCreated(this ILogger logger, Guid userId);

    // ── OptimizationController ─────────────────────────────────────────────────

    [LoggerMessage(
        EventId = 5015,
        Level = LogLevel.Information,
        Message = "[Shopping] Optimizing list {ListId} with strategy '{Strategy}' for user {UserId}")]
    public static partial void LogOptimizingList(this ILogger logger, Guid userId, Guid listId, string strategy);

    [LoggerMessage(
        EventId = 5016,
        Level = LogLevel.Information,
        Message = "[Shopping] List {ListId} optimized ({Strategy}): {StoreCount} stores for user {UserId}")]
    public static partial void LogListOptimized(this ILogger logger, Guid userId, Guid listId, string strategy, int storeCount);

    [LoggerMessage(
        EventId = 5017,
        Level = LogLevel.Debug,
        Message = "[Shopping] Getting optimization result for list {ListId} (user {UserId})")]
    public static partial void LogGettingOptimization(this ILogger logger, Guid userId, Guid listId);

    [LoggerMessage(
        EventId = 5018,
        Level = LogLevel.Information,
        Message = "[Shopping] Adding recipe {RecipeId} ({Servings} servings) to list {ListId} for user {UserId}")]
    public static partial void LogAddingFromRecipe(this ILogger logger, Guid userId, Guid listId, Guid recipeId, int servings);

    [LoggerMessage(
        EventId = 5019,
        Level = LogLevel.Debug,
        Message = "[Shopping] Getting items for list {ListId} sorted by '{Mode}' at store {StoreId} (user {UserId})")]
    public static partial void LogGettingSortedItems(this ILogger logger, Guid userId, Guid listId, Guid storeId, string mode);

    [LoggerMessage(
        EventId = 5020,
        Level = LogLevel.Debug,
        Message = "[Shopping] Getting category preferences for user {UserId}")]
    public static partial void LogGettingCategoryPrefs(this ILogger logger, Guid userId);

    [LoggerMessage(
        EventId = 5021,
        Level = LogLevel.Information,
        Message = "[Shopping] {Count} category preferences updated for user {UserId}")]
    public static partial void LogCategoryPrefsUpdated(this ILogger logger, Guid userId, int count);

    [LoggerMessage(
        EventId = 5022,
        Level = LogLevel.Information,
        Message = "[Shopping] Category preference {PreferenceId} deleted by user {UserId}")]
    public static partial void LogCategoryPrefDeleted(this ILogger logger, Guid userId, Guid preferenceId);

    [LoggerMessage(
        EventId = 5023,
        Level = LogLevel.Debug,
        Message = "[Shopping] Getting price search profile for user {UserId}")]
    public static partial void LogGettingPriceProfile(this ILogger logger, Guid userId);

    [LoggerMessage(
        EventId = 5024,
        Level = LogLevel.Information,
        Message = "[Shopping] Price search profile updated for user {UserId}")]
    public static partial void LogPriceProfileUpdated(this ILogger logger, Guid userId);

    // ── PrintController ────────────────────────────────────────────────────────

    [LoggerMessage(
        EventId = 5025,
        Level = LogLevel.Information,
        Message = "[Shopping] Print request for list {ListId} (format: {Format}, optimized: {HasOptimization}) by user {UserId}")]
    public static partial void LogPrintRequest(this ILogger logger, Guid userId, Guid listId, string format, bool hasOptimization);

    [LoggerMessage(
        EventId = 5026,
        Level = LogLevel.Debug,
        Message = "[Shopping] Print completed for list {ListId} ({StoreCount} store pages, format: {Format}) by user {UserId}")]
    public static partial void LogPrintComplete(this ILogger logger, Guid userId, Guid listId, string format, int storeCount);

    // ── ShoppingSessionService ─────────────────────────────────────────────────

    [LoggerMessage(
        EventId = 5027,
        Level = LogLevel.Information,
        Message = "[Shopping] Importing ingredients from recipe {RecipeId} ({Servings} servings) into list {ListId} for user {UserId}")]
    public static partial void LogImportingRecipeIngredients(this ILogger logger, Guid userId, Guid listId, Guid recipeId, int servings);

    [LoggerMessage(
        EventId = 5028,
        Level = LogLevel.Information,
        Message = "[Shopping] Imported {ItemCount} ingredients into list {ListId} for user {UserId}")]
    public static partial void LogRecipeImportComplete(this ILogger logger, Guid userId, Guid listId, int itemCount);

    [LoggerMessage(
        EventId = 5029,
        Level = LogLevel.Information,
        Message = "[Shopping] Completing shopping session for list {ListId} (user {UserId})")]
    public static partial void LogSessionCompleting(this ILogger logger, Guid userId, Guid listId);

    [LoggerMessage(
        EventId = 5030,
        Level = LogLevel.Information,
        Message = "[Shopping] Shopping session completed for list {ListId}: {ItemsBought} items bought by user {UserId}")]
    public static partial void LogSessionCompleted(this ILogger logger, Guid userId, Guid listId, int itemsBought);
}
