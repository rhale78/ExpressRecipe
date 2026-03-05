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

    // ------------------------------------------------------------------
    // Product lifecycle event subscriber (4007–4015)
    // ------------------------------------------------------------------

    [LoggerMessage(
        EventId = 4007,
        Level = LogLevel.Information,
        Message = "[ProductEvent→Price] Received {EventType} for product {ProductId}")]
    public static partial void LogProductEventReceived(this ILogger logger, string eventType, Guid productId);

    [LoggerMessage(
        EventId = 4008,
        Level = LogLevel.Information,
        Message = "[ProductEvent→Price] Product {ProductId} deleted – deactivated {Count} price rows")]
    public static partial void LogPricesDeactivated(this ILogger logger, Guid productId, int count);

    [LoggerMessage(
        EventId = 4009,
        Level = LogLevel.Information,
        Message = "[ProductEvent→Price] Product {ProductId} renamed – updated {Count} price rows with new name '{NewName}'")]
    public static partial void LogPriceProductNameUpdated(this ILogger logger, Guid productId, int count, string newName);

    [LoggerMessage(
        EventId = 4010,
        Level = LogLevel.Information,
        Message = "[ProductEvent→Price] Product {ProductId} barcode changed – updated {Count} price rows with new UPC '{NewUpc}'")]
    public static partial void LogPriceProductUpcUpdated(this ILogger logger, Guid productId, int count, string? newUpc);

    [LoggerMessage(
        EventId = 4011,
        Level = LogLevel.Information,
        Message = "[ProductEvent→Price] Product {ProductId} created/updated – refreshed lookup cache for barcode '{Barcode}'")]
    public static partial void LogProductCacheRefreshed(this ILogger logger, Guid productId, string? barcode);

    [LoggerMessage(
        EventId = 4012,
        Level = LogLevel.Warning,
        Message = "[ProductEvent→Price] Failed to handle {EventType} for product {ProductId}: {ErrorMessage}")]
    public static partial void LogProductEventHandlerFailed(this ILogger logger, string eventType, Guid productId, string errorMessage);

    [LoggerMessage(
        EventId = 4013,
        Level = LogLevel.Information,
        Message = "[ProductEvent→Price] Subscriber started – listening on routing key '{RoutingKey}'")]
    public static partial void LogSubscriberStarted(this ILogger logger, string routingKey);

    [LoggerMessage(
        EventId = 4014,
        Level = LogLevel.Warning,
        Message = "[ProductEvent→Price] Subscriber stopped unexpectedly: {ErrorMessage}")]
    public static partial void LogSubscriberStopped(this ILogger logger, string errorMessage);

    [LoggerMessage(
        EventId = 4015,
        Level = LogLevel.Information,
        Message = "[ProductEvent→Price] Product {ProductId} approved – price lookups now active for barcode '{Barcode}'")]
    public static partial void LogProductApprovedCacheReady(this ILogger logger, Guid productId, string? barcode);
}
