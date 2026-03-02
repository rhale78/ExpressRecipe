namespace ExpressRecipe.IngredientService.Logging;

using Microsoft.Extensions.Logging;

/// <summary>
/// High-performance source-generated logging for IngredientService.
/// Zero allocation when log level is disabled.
/// </summary>
public static partial class IngredientServiceLogs
{
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "[IngredientService] Bulk lookup: {RequestCount} names -> {FoundCount} matches in {ElapsedMs}ms")]
    public static partial void LogBulkLookup(this ILogger logger, int requestCount, int foundCount, long elapsedMs);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Information,
        Message = "[IngredientService] Bulk create: {RequestCount} names -> {CreatedCount} new ingredients in {ElapsedMs}ms")]
    public static partial void LogBulkCreate(this ILogger logger, int requestCount, int createdCount, long elapsedMs);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Information,
        Message = "[IngredientService] Created ingredient: {IngredientName} (ID: {IngredientId})")]
    public static partial void LogIngredientCreated(this ILogger logger, string ingredientName, Guid ingredientId);

    [LoggerMessage(
        EventId = 1004,
        Level = LogLevel.Information,
        Message = "[IngredientService] Updated ingredient: {IngredientId}")]
    public static partial void LogIngredientUpdated(this ILogger logger, Guid ingredientId);

    [LoggerMessage(
        EventId = 1005,
        Level = LogLevel.Information,
        Message = "[IngredientService] Deleted ingredient: {IngredientId}")]
    public static partial void LogIngredientDeleted(this ILogger logger, Guid ingredientId);

    [LoggerMessage(
        EventId = 1006,
        Level = LogLevel.Debug,
        Message = "[IngredientService] Cache hit for key: {CacheKey}")]
    public static partial void LogCacheHit(this ILogger logger, string cacheKey);

    [LoggerMessage(
        EventId = 1007,
        Level = LogLevel.Debug,
        Message = "[IngredientService] Cache miss for key: {CacheKey}")]
    public static partial void LogCacheMiss(this ILogger logger, string cacheKey);

    [LoggerMessage(
        EventId = 1008,
        Level = LogLevel.Warning,
        Message = "[IngredientService] Bulk operation received empty list")]
    public static partial void LogEmptyBulkRequest(this ILogger logger);

    [LoggerMessage(
        EventId = 1009,
        Level = LogLevel.Error,
        Message = "[IngredientService] Database operation failed")]
    public static partial void LogDatabaseError(this ILogger logger, Exception exception);
}
