namespace ExpressRecipe.RecipeService.Logging;

using Microsoft.Extensions.Logging;

/// <summary>
/// High-performance source-generated logging for RecipeService.
/// Zero allocation when log level is disabled.
/// </summary>
public static partial class RecipeServiceLogs
{
    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Information,
        Message = "[RecipeImport] Producer: Read {RecordCount} recipes from file")]
    public static partial void LogRecipesRead(this ILogger logger, int recordCount);

    [LoggerMessage(
        EventId = 3002,
        Level = LogLevel.Information,
        Message = "[RecipeProcessing] Writer: Processed {TotalProcessed} | Speed: {RecordsPerSec:F1} rec/sec | Lag: {LagCount} records")]
    public static partial void LogProcessingProgress(this ILogger logger, int totalProcessed, double recordsPerSec, int lagCount);

    [LoggerMessage(
        EventId = 3003,
        Level = LogLevel.Information,
        Message = "[RecipeStaging] Bulk insert: {InputCount} recipes -> {InsertedCount} rows in {ElapsedMs}ms ({RecordsPerSec:F1} rec/sec)")]
    public static partial void LogBulkInsert(this ILogger logger, int inputCount, int insertedCount, long elapsedMs, double recordsPerSec);

    [LoggerMessage(
        EventId = 3004,
        Level = LogLevel.Information,
        Message = "[RecipeProcessing] Batch completed: {ProcessedCount} recipes in {ElapsedMs}ms")]
    public static partial void LogBatchCompleted(this ILogger logger, int processedCount, long elapsedMs);

    [LoggerMessage(
        EventId = 3005,
        Level = LogLevel.Information,
        Message = "[RecipeImport] Import pipeline completed: {TotalRecords} recipes in {TotalMinutes:F1} minutes")]
    public static partial void LogImportCompleted(this ILogger logger, int totalRecords, double totalMinutes);

    [LoggerMessage(
        EventId = 3006,
        Level = LogLevel.Information,
        Message = "[RecipeRepository] Created recipe: {RecipeName} (ID: {RecipeId})")]
    public static partial void LogRecipeCreated(this ILogger logger, string recipeName, Guid recipeId);

    [LoggerMessage(
        EventId = 3007,
        Level = LogLevel.Information,
        Message = "[RecipeSearch] Search for '{SearchTerm}' returned {ResultCount} recipes in {ElapsedMs}ms")]
    public static partial void LogSearchCompleted(this ILogger logger, string searchTerm, int resultCount, long elapsedMs);

    [LoggerMessage(
        EventId = 3008,
        Level = LogLevel.Warning,
        Message = "[RecipeProcessing] Failed to process recipe {RecipeId}: {ErrorMessage}")]
    public static partial void LogProcessingFailed(this ILogger logger, Guid recipeId, string errorMessage);

    [LoggerMessage(
        EventId = 3009,
        Level = LogLevel.Debug,
        Message = "[RecipeCache] Cache hit for recipe: {RecipeId}")]
    public static partial void LogCacheHit(this ILogger logger, Guid recipeId);

    [LoggerMessage(
        EventId = 3010,
        Level = LogLevel.Error,
        Message = "[RecipeService] Database operation failed")]
    public static partial void LogDatabaseError(this ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 3011,
        Level = LogLevel.Information,
        Message = "[RecipeProcessing→IngredientSvc] Batch: {BatchSize} recipes, {UniqueStrings} unique ingredient strings → bulk parse")]
    public static partial void LogIngredientParseBatch(this ILogger logger, int batchSize, int uniqueStrings);

    [LoggerMessage(
        EventId = 3012,
        Level = LogLevel.Information,
        Message = "[RecipeProcessing→IngredientSvc] Flush: {RecipeCount} recipes, {IngredientCount} unique ingredients → bulk create+lookup")]
    public static partial void LogIngredientFlush(this ILogger logger, int recipeCount, int ingredientCount);

    // ------------------------------------------------------------------
    // Share token events (3013–3020)
    // ------------------------------------------------------------------

    [LoggerMessage(
        EventId = 3013,
        Level = LogLevel.Information,
        Message = "[RecipeShare] Generated share token for recipe {RecipeId} by user {UserId} (expires in {ExpiryDays} days)")]
    public static partial void LogShareTokenGenerated(this ILogger logger, Guid recipeId, Guid userId, int expiryDays);

    [LoggerMessage(
        EventId = 3014,
        Level = LogLevel.Information,
        Message = "[RecipeShare] Token '{Token}' accessed – ViewCount now {ViewCount}")]
    public static partial void LogShareTokenAccessed(this ILogger logger, string token, int viewCount);

    [LoggerMessage(
        EventId = 3015,
        Level = LogLevel.Information,
        Message = "[RecipeShare] Token '{Token}' revoked by user {UserId}")]
    public static partial void LogShareTokenRevoked(this ILogger logger, string token, Guid userId);

    [LoggerMessage(
        EventId = 3016,
        Level = LogLevel.Information,
        Message = "[RecipePrint] Generated {Format} for recipe {RecipeId}")]
    public static partial void LogRecipePrinted(this ILogger logger, string format, Guid recipeId);

    [LoggerMessage(
        EventId = 3017,
        Level = LogLevel.Information,
        Message = "[HouseholdFavorite] Favorite {FavoriteId} household share set to {Shared} for household {HouseholdId}")]
    public static partial void LogHouseholdShareUpdated(this ILogger logger, Guid favoriteId, bool shared, Guid? householdId);
}
