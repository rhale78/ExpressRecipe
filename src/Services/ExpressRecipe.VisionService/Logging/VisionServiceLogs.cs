namespace ExpressRecipe.VisionService.Logging;

using Microsoft.Extensions.Logging;

/// <summary>
/// High-performance source-generated logging for VisionService.
/// Zero allocation when log level is disabled.
/// </summary>
public static partial class VisionServiceLogs
{
    [LoggerMessage(
        EventId = 6001,
        Level = LogLevel.Information,
        Message = "[Vision] Analyze request from user {UserId}: {ImageSizeBytes} bytes")]
    public static partial void LogAnalyzeRequest(this ILogger logger, string userId, int imageSizeBytes);

    [LoggerMessage(
        EventId = 6002,
        Level = LogLevel.Debug,
        Message = "[Vision] Analyze complete for user {UserId}: provider={ProviderUsed} confidence={Confidence:F3} in {ElapsedMs}ms")]
    public static partial void LogAnalyzeComplete(this ILogger logger, string userId, string providerUsed, double confidence, long elapsedMs);

    [LoggerMessage(
        EventId = 6003,
        Level = LogLevel.Information,
        Message = "[Vision] OCR request from user {UserId}: {ImageSizeBytes} bytes")]
    public static partial void LogOcrRequest(this ILogger logger, string userId, int imageSizeBytes);

    [LoggerMessage(
        EventId = 6004,
        Level = LogLevel.Debug,
        Message = "[Vision] OCR complete for user {UserId}: {TextBlockCount} text blocks in {ElapsedMs}ms")]
    public static partial void LogOcrComplete(this ILogger logger, string userId, int textBlockCount, long elapsedMs);

    [LoggerMessage(
        EventId = 6005,
        Level = LogLevel.Debug,
        Message = "[Vision] Health check requested")]
    public static partial void LogHealthCheck(this ILogger logger);

    [LoggerMessage(
        EventId = 6006,
        Level = LogLevel.Warning,
        Message = "[Vision] No image provided in request from user {UserId}")]
    public static partial void LogNoImageProvided(this ILogger logger, string userId);

    [LoggerMessage(
        EventId = 6007,
        Level = LogLevel.Debug,
        Message = "[Vision] Using provider '{ProviderName}' for user {UserId}")]
    public static partial void LogProviderSelected(this ILogger logger, string userId, string providerName);

    [LoggerMessage(
        EventId = 6008,
        Level = LogLevel.Warning,
        Message = "[Vision] All vision providers disabled or unavailable")]
    public static partial void LogAllProvidersDisabled(this ILogger logger);

    [LoggerMessage(
        EventId = 6009,
        Level = LogLevel.Debug,
        Message = "[Vision] Provider result confidence {Confidence:F3} below threshold {Threshold:F3}, trying next provider")]
    public static partial void LogConfidenceBelowThreshold(this ILogger logger, double confidence, double threshold);

    [LoggerMessage(
        EventId = 6010,
        Level = LogLevel.Information,
        Message = "[Vision] ONNX provider enabled (model loaded)")]
    public static partial void LogOnnxProviderEnabled(this ILogger logger);

    [LoggerMessage(
        EventId = 6011,
        Level = LogLevel.Debug,
        Message = "[Vision] Provider chain result: provider={ProviderUsed} confidence={Confidence:F3} product='{MatchedProduct}'")]
    public static partial void LogProviderChainResult(this ILogger logger, string providerUsed, double confidence, string matchedProduct);
}
