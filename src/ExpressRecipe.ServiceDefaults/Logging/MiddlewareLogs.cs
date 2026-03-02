namespace ExpressRecipe.ServiceDefaults.Logging;

using Microsoft.Extensions.Logging;

/// <summary>
/// High-performance source-generated logging for ExceptionHandlingMiddleware.
/// Called on every HTTP request that throws an exception.
/// </summary>
public static partial class ExceptionMiddlewareLogs
{
    [LoggerMessage(
        EventId = 400,
        Level = LogLevel.Error,
        Message = "[Middleware] Unhandled exception in {Path}")]
    public static partial void LogUnhandledException(this ILogger logger, string path, Exception exception);

    [LoggerMessage(
        EventId = 401,
        Level = LogLevel.Warning,
        Message = "[Middleware] Validation error in {Path}: {ErrorMessage}")]
    public static partial void LogValidationError(this ILogger logger, string path, string errorMessage);

    [LoggerMessage(
        EventId = 402,
        Level = LogLevel.Information,
        Message = "[Middleware] Exception handled: {ExceptionType} -> {StatusCode}")]
    public static partial void LogExceptionHandled(this ILogger logger, string exceptionType, int statusCode);
}

/// <summary>
/// High-performance source-generated logging for RateLimitingMiddleware.
/// Called on every HTTP request for rate limit checks.
/// </summary>
public static partial class RateLimitLogs
{
    [LoggerMessage(
        EventId = 410,
        Level = LogLevel.Warning,
        Message = "[RateLimit] Rate limit exceeded for {IpAddress}: {RequestCount} requests in window")]
    public static partial void LogRateLimitExceeded(this ILogger logger, string ipAddress, int requestCount);

    [LoggerMessage(
        EventId = 411,
        Level = LogLevel.Debug,
        Message = "[RateLimit] Request allowed for {IpAddress}: {RequestCount}/{MaxRequests}")]
    public static partial void LogRequestAllowed(this ILogger logger, string ipAddress, int requestCount, int maxRequests);

    [LoggerMessage(
        EventId = 412,
        Level = LogLevel.Information,
        Message = "[RateLimit] Window reset for {IpAddress}")]
    public static partial void LogWindowReset(this ILogger logger, string ipAddress);

    [LoggerMessage(
        EventId = 413,
        Level = LogLevel.Warning,
        Message = "[RateLimit] Client blocked: {IpAddress} (retry after {RetryAfterSeconds}s)")]
    public static partial void LogClientBlocked(this ILogger logger, string ipAddress, int retryAfterSeconds);
}
