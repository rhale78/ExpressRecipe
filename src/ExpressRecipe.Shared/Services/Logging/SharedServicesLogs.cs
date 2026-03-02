namespace ExpressRecipe.Shared.Services.Logging;

using Microsoft.Extensions.Logging;

/// <summary>
/// High-performance source-generated logging for HybridCacheService.
/// Called on every cache operation - critical hot path.
/// </summary>
public static partial class CacheLogs
{
    [LoggerMessage(
        EventId = 300,
        Level = LogLevel.Debug,
        Message = "[Cache] Hit: {CacheKey}")]
    public static partial void LogCacheHit(this ILogger logger, string cacheKey);

    [LoggerMessage(
        EventId = 301,
        Level = LogLevel.Debug,
        Message = "[Cache] Miss: {CacheKey}")]
    public static partial void LogCacheMiss(this ILogger logger, string cacheKey);

    [LoggerMessage(
        EventId = 302,
        Level = LogLevel.Debug,
        Message = "[Cache] Set: {CacheKey} (expires in {ExpirationSeconds}s)")]
    public static partial void LogCacheSet(this ILogger logger, string cacheKey, double expirationSeconds);

    [LoggerMessage(
        EventId = 303,
        Level = LogLevel.Debug,
        Message = "[Cache] Remove: {CacheKey}")]
    public static partial void LogCacheRemove(this ILogger logger, string cacheKey);

    [LoggerMessage(
        EventId = 304,
        Level = LogLevel.Debug,
        Message = "[Cache] Remove by tag: {Tag}")]
    public static partial void LogCacheRemoveByTag(this ILogger logger, string tag);

    [LoggerMessage(
        EventId = 305,
        Level = LogLevel.Warning,
        Message = "[Cache] Operation failed for key: {CacheKey}")]
    public static partial void LogCacheOperationFailed(this ILogger logger, string cacheKey, Exception exception);

    [LoggerMessage(
        EventId = 306,
        Level = LogLevel.Information,
        Message = "[Cache] GetOrCreate: {CacheKey} -> {FoundInCache} in {ElapsedMs}ms")]
    public static partial void LogGetOrCreate(this ILogger logger, string cacheKey, bool foundInCache, long elapsedMs);
}

/// <summary>
/// High-performance source-generated logging for legacy CacheService (Redis).
/// </summary>
public static partial class RedisCacheLogs
{
    [LoggerMessage(
        EventId = 310,
        Level = LogLevel.Debug,
        Message = "[Redis] Get: {CacheKey} -> {Found}")]
    public static partial void LogRedisGet(this ILogger logger, string cacheKey, bool found);

    [LoggerMessage(
        EventId = 311,
        Level = LogLevel.Debug,
        Message = "[Redis] Set: {CacheKey} (expires in {ExpirationSeconds}s)")]
    public static partial void LogRedisSet(this ILogger logger, string cacheKey, double expirationSeconds);

    [LoggerMessage(
        EventId = 312,
        Level = LogLevel.Error,
        Message = "[Redis] Operation failed for key: {CacheKey}")]
    public static partial void LogRedisError(this ILogger logger, string cacheKey, Exception exception);
}
