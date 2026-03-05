namespace ExpressRecipe.ProductService.Logging;

using Microsoft.Extensions.Logging;

/// <summary>
/// High-performance source-generated logging for ProductService.
/// Zero allocation when log level is disabled.
/// </summary>
public static partial class ProductServiceLogs
{
    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Information,
        Message = "[ProductStaging] Bulk insert: {InputCount} products -> {InsertedCount} rows in {ElapsedMs}ms ({RecordsPerSec:F1} rec/sec)")]
    public static partial void LogBulkInsert(this ILogger logger, int inputCount, int insertedCount, long elapsedMs, double recordsPerSec);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Information,
        Message = "[ProductStaging] Bulk augment: {InputCount} products -> {UpdatedCount} rows updated in {ElapsedMs}ms")]
    public static partial void LogBulkAugment(this ILogger logger, int inputCount, int updatedCount, long elapsedMs);

    [LoggerMessage(
        EventId = 2003,
        Level = LogLevel.Information,
        Message = "[ProductProcessing] Batch completed: {ProcessedCount} products in {ElapsedMs}ms ({RecordsPerSec:F1} rec/sec)")]
    public static partial void LogBatchProcessed(this ILogger logger, int processedCount, long elapsedMs, double recordsPerSec);

    [LoggerMessage(
        EventId = 2004,
        Level = LogLevel.Information,
        Message = "[ProductImport] CSV import: Loaded {RecordCount} records from {FilePath}")]
    public static partial void LogCsvLoaded(this ILogger logger, int recordCount, string filePath);

    [LoggerMessage(
        EventId = 2005,
        Level = LogLevel.Information,
        Message = "[ProductImport] JSON import: Loaded {RecordCount} records")]
    public static partial void LogJsonLoaded(this ILogger logger, int recordCount);

    [LoggerMessage(
        EventId = 2006,
        Level = LogLevel.Information,
        Message = "[ProductRepository] Created product: {ProductName} (ID: {ProductId})")]
    public static partial void LogProductCreated(this ILogger logger, string productName, Guid productId);

    [LoggerMessage(
        EventId = 2007,
        Level = LogLevel.Warning,
        Message = "[ProductProcessing] Failed to process product {ProductId}: {ErrorMessage}")]
    public static partial void LogProcessingFailed(this ILogger logger, Guid productId, string errorMessage);

    [LoggerMessage(
        EventId = 2008,
        Level = LogLevel.Debug,
        Message = "[ProductRepository] Cache hit for product: {ProductId}")]
    public static partial void LogCacheHit(this ILogger logger, Guid productId);

    [LoggerMessage(
        EventId = 2009,
        Level = LogLevel.Error,
        Message = "[ProductService] Database operation failed")]
    public static partial void LogDatabaseError(this ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 2010,
        Level = LogLevel.Information,
        Message = "[ProductProcessing→IngredientSvc] Batch: {BatchSize} products, {TextCount} unique ingredient texts → bulk parse")]
    public static partial void LogIngredientParseBatch(this ILogger logger, int batchSize, int textCount);

    [LoggerMessage(
        EventId = 2011,
        Level = LogLevel.Information,
        Message = "[ProductProcessing→IngredientSvc] Flush: {ProductCount} products, {IngredientCount} new ingredient names → bulk create+lookup")]
    public static partial void LogIngredientFlush(this ILogger logger, int productCount, int ingredientCount);

    [LoggerMessage(
        EventId = 2012,
        Level = LogLevel.Information,
        Message = "[ProductSaga] Started saga for product {ExternalId} (CorrelationId: {CorrelationId})")]
    public static partial void LogSagaStarted(this ILogger logger, string externalId, string correlationId);

    [LoggerMessage(
        EventId = 2013,
        Level = LogLevel.Information,
        Message = "[ProductSaga] Step '{StepName}' completed for {CorrelationId} (Mask: {Mask:X8})")]
    public static partial void LogSagaStepCompleted(this ILogger logger, string stepName, string correlationId, long mask);

    [LoggerMessage(
        EventId = 2014,
        Level = LogLevel.Warning,
        Message = "[ProductSaga] Saga failed for {CorrelationId}: {ErrorMessage}")]
    public static partial void LogSagaFailed(this ILogger logger, string correlationId, string errorMessage);

    [LoggerMessage(
        EventId = 2015,
        Level = LogLevel.Information,
        Message = "[ProductAIVerification] Verified {BatchSize} products in {ElapsedMs}ms (Valid: {ValidCount}, Invalid: {InvalidCount})")]
    public static partial void LogAIVerificationBatch(this ILogger logger, int batchSize, long elapsedMs, int validCount, int invalidCount);

    [LoggerMessage(
        EventId = 2016,
        Level = LogLevel.Information,
        Message = "[ImportChannel] Buffered {Count} products into channel (Total queued: {QueuedTotal})")]
    public static partial void LogChannelBuffered(this ILogger logger, int count, int queuedTotal);

    [LoggerMessage(
        EventId = 2017,
        Level = LogLevel.Information,
        Message = "[ImportChannel] Channel consumer processed batch: {BatchSize} records at {RecordsPerSec:F1} rec/sec")]
    public static partial void LogChannelBatchProcessed(this ILogger logger, int batchSize, double recordsPerSec);

    [LoggerMessage(
        EventId = 2018,
        Level = LogLevel.Information,
        Message = "[ImportSession] Session {SessionId} progress: {Processed}/{Total} ({PercentComplete:F1}%) at {RecordsPerSec:F1} rec/sec, ETA: {Eta}")]
    public static partial void LogImportProgress(this ILogger logger, string sessionId, int processed, int total, double percentComplete, double recordsPerSec, string eta);
}
