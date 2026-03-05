using Microsoft.Extensions.Logging;

namespace ExpressRecipe.PriceService.Logging;

/// <summary>
/// High-performance source-generated logging for PriceService.
/// Zero allocation when log level is disabled.
/// </summary>
public static partial class PriceServiceLogs
{
    [LoggerMessage(
        EventId = 4001,
        Level = LogLevel.Information,
        Message = "[PriceImport] Batch: {BatchSize} prices processed in {ElapsedMs}ms ({RecordsPerSec:F1} rec/sec)")]
    public static partial void LogBatchProcessed(this ILogger logger, int batchSize, long elapsedMs, double recordsPerSec);

    [LoggerMessage(
        EventId = 4002,
        Level = LogLevel.Information,
        Message = "[PriceImport] Session {SessionId} progress: {Processed}/{Total} at {RecordsPerSec:F1} rec/sec")]
    public static partial void LogImportProgress(this ILogger logger, string sessionId, int processed, int total, double recordsPerSec);

    [LoggerMessage(
        EventId = 4003,
        Level = LogLevel.Information,
        Message = "[PriceSaga] Price {PriceStagingId} linked to product {ProductId} (exact: {IsExact})")]
    public static partial void LogPriceLinked(this ILogger logger, Guid priceStagingId, Guid productId, bool isExact);

    [LoggerMessage(
        EventId = 4004,
        Level = LogLevel.Warning,
        Message = "[PriceSaga] Price {PriceStagingId} could not be linked: {Reason}")]
    public static partial void LogPriceLinkFailed(this ILogger logger, Guid priceStagingId, string reason);

    [LoggerMessage(
        EventId = 4005,
        Level = LogLevel.Information,
        Message = "[PriceSaga] Started saga for price staging {PriceStagingId} (CorrelationId: {CorrelationId})")]
    public static partial void LogSagaStarted(this ILogger logger, Guid priceStagingId, string correlationId);

    [LoggerMessage(
        EventId = 4006,
        Level = LogLevel.Warning,
        Message = "[PriceSaga] Saga failed for {CorrelationId}: {ErrorMessage}")]
    public static partial void LogSagaFailed(this ILogger logger, string correlationId, string errorMessage);
}
