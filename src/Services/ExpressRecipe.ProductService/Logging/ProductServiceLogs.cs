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
}
