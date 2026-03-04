namespace ExpressRecipe.Client.Shared.Logging;

using Microsoft.Extensions.Logging;

/// <summary>
/// High-performance source-generated logging for ApiClientBase.
/// Used by all HTTP client wrappers calling microservices.
/// </summary>
public static partial class ApiClientLogs
{
    [LoggerMessage(
        EventId = 200,
        Level = LogLevel.Trace,
        Message = "[ApiClient] {Method} {Endpoint} -> {StatusCode} in {ElapsedMs}ms")]
    public static partial void LogApiCall(this ILogger logger, string method, string endpoint, int statusCode, long elapsedMs);

    [LoggerMessage(
        EventId = 201,
        Level = LogLevel.Warning,
        Message = "[ApiClient] Retry {Attempt} for {Method} {Endpoint}")]
    public static partial void LogRetryAttempt(this ILogger logger, int attempt, string method, string endpoint);

    [LoggerMessage(
        EventId = 202,
        Level = LogLevel.Error,
        Message = "[ApiClient] Failed: {Method} {Endpoint} -> {StatusCode}")]
    public static partial void LogApiCallFailed(this ILogger logger, string method, string endpoint, int statusCode, Exception exception);

    [LoggerMessage(
        EventId = 203,
        Level = LogLevel.Warning,
        Message = "[ApiClient] Auth missing for {Method} {Endpoint}")]
    public static partial void LogAuthenticationMissing(this ILogger logger, string method, string endpoint);

    [LoggerMessage(
        EventId = 204,
        Level = LogLevel.Trace,
        Message = "[ApiClient] Auth added: {Method} {Endpoint}")]
    public static partial void LogAuthenticationAdded(this ILogger logger, string method, string endpoint);

    [LoggerMessage(
        EventId = 205,
        Level = LogLevel.Information,
        Message = "[ApiClient] Bulk: {Method} {Endpoint} | {ItemCount} items -> {StatusCode} in {ElapsedMs}ms")]
    public static partial void LogBulkApiCall(this ILogger logger, string method, string endpoint, int itemCount, int statusCode, long elapsedMs);
}

/// <summary>
/// High-performance source-generated logging for IngredientServiceClient.
/// Hot path for ingredient lookups and bulk creates.
/// </summary>
public static partial class IngredientClientLogs
{
    [LoggerMessage(
        EventId = 210,
        Level = LogLevel.Information,
        Message = "[Ingredient] Bulk lookup: {RequestCount} names -> {FoundCount} found in {ElapsedMs}ms")]
    public static partial void LogBulkLookup(this ILogger logger, int requestCount, int foundCount, long elapsedMs);

    [LoggerMessage(
        EventId = 211,
        Level = LogLevel.Information,
        Message = "[Ingredient] Bulk create: {RequestCount} names -> {CreatedCount} created in {ElapsedMs}ms")]
    public static partial void LogBulkCreate(this ILogger logger, int requestCount, int createdCount, long elapsedMs);

    [LoggerMessage(
        EventId = 212,
        Level = LogLevel.Warning,
        Message = "[Ingredient] Bulk operation: {RequestCount} names -> 0 results")]
    public static partial void LogNoResults(this ILogger logger, int requestCount);

    [LoggerMessage(
        EventId = 213,
        Level = LogLevel.Error,
        Message = "[Ingredient] Bulk operation failed: {RequestCount} items")]
    public static partial void LogBulkOperationFailed(this ILogger logger, int requestCount, Exception exception);
}
