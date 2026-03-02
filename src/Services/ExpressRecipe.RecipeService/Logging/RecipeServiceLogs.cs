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
}
