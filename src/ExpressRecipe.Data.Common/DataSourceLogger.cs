using System;
using Microsoft.Extensions.Logging;

namespace ExpressRecipe.Data.Common
{
    /// <summary>
    /// Centralized logging for data source operations (Database vs Memory vs Cache).
    /// Provides consistent, detailed logging about where data is being read from and written to.
    /// </summary>
    public static class DataSourceLogger
    {
        /// <summary>
        /// Logs when data is read from database.
        /// </summary>
        public static void LogDatabaseRead(
            ILogger logger,
            string tableName,
            string operation,
            object? identifier = null,
            int? rowCount = null)
        {
            return;
            var identifierStr = identifier != null ? $" | ID: {identifier}" : "";
            var rowCountStr = rowCount.HasValue ? $" | Rows: {rowCount}" : "";

            logger.LogInformation(
                "DATA_SOURCE=DATABASE | Operation=READ | Table={Table} | Method={Operation}{IdentifierStr}{RowCountStr}",
                tableName,
                operation,
                identifierStr,
                rowCountStr);
        }

        /// <summary>
        /// Logs when data is written to database.
        /// </summary>
        public static void LogDatabaseWrite(
            ILogger logger,
            string tableName,
            string operation,
            object? identifier = null,
            int rowsAffected = 1)
        {
            var identifierStr = identifier != null ? $" | ID: {identifier}" : "";

            logger.LogInformation(
                "DATA_SOURCE=DATABASE | Operation=WRITE | Table={Table} | Method={Operation} | RowsAffected={Rows}{IdentifierStr}",
                tableName,
                operation,
                rowsAffected,
                identifierStr);
        }

        /// <summary>
        /// Logs when data is retrieved from cache (L1/L2).
        /// </summary>
        public static void LogCacheHit(
            ILogger logger,
            string tableName,
            string cacheLevel,
            object? identifier = null)
        {
            var identifierStr = identifier != null ? $" | ID: {identifier}" : "";

            logger.LogInformation(
                "DATA_SOURCE=CACHE | CacheLevel={CacheLevel} | Table={Table}{IdentifierStr}",
                cacheLevel,
                tableName,
                identifierStr);
        }

        /// <summary>
        /// Logs when cache is missed and fallback to database is needed.
        /// </summary>
        public static void LogCacheMiss(
            ILogger logger,
            string tableName,
            object? identifier = null)
        {
            var identifierStr = identifier != null ? $" | ID: {identifier}" : "";

            logger.LogDebug(
                "DATA_SOURCE=CACHE | Status=MISS | Table={Table} | Fallback=DATABASE{IdentifierStr}",
                tableName,
                identifierStr);
        }

        /// <summary>
        /// Logs when data is read from in-memory table (if implemented).
        /// </summary>
        public static void LogMemoryRead(
            ILogger logger,
            string tableName,
            string operation,
            object? identifier = null,
            int? rowCount = null)
        {
            return;
            var identifierStr = identifier != null ? $" | ID: {identifier}" : "";
            var rowCountStr = rowCount.HasValue ? $" | Rows: {rowCount}" : "";

            logger.LogInformation(
                "DATA_SOURCE=MEMORY | Operation=READ | Table={Table} | Method={Operation}{IdentifierStr}{RowCountStr}",
                tableName,
                operation,
                identifierStr,
                rowCountStr);
        }

        /// <summary>
        /// Logs when data is written to in-memory table (if implemented).
        /// </summary>
        public static void LogMemoryWrite(
            ILogger logger,
            string tableName,
            string operation,
            object? identifier = null,
            int rowsAffected = 1)
        {
            return;
            var identifierStr = identifier != null ? $" | ID: {identifier}" : "";

            logger.LogInformation(
                "DATA_SOURCE=MEMORY | Operation=WRITE | Table={Table} | Method={Operation} | RowsAffected={Rows}{IdentifierStr}",
                tableName,
                operation,
                rowsAffected,
                identifierStr);
        }

        /// <summary>
        /// Logs the data source configuration for a table at initialization time.
        /// </summary>
        public static void LogTableConfiguration(
            ILogger logger,
            string tableName,
            bool hasMemory,
            bool hasCache,
            string cacheStrategy = "None")
        {
            List<string> sources = [];

            if (hasCache)
            {
                sources.Add($"Cache[{cacheStrategy}]");
            }

            if (hasMemory)
            {
                sources.Add("InMemory");
            }

            sources.Add("Database[SqlServer]");

            logger.LogInformation(
                "TABLE_CONFIG | Table={Table} | DataSources={Sources} | Fallback=>{Fallback}",
                tableName,
                string.Join(" => ", sources),
                "Database");
        }

        /// <summary>
        /// Logs a diagnostic summary of data source usage.
        /// </summary>
        public static void LogDataSourceSummary(
            ILogger logger,
            string tableName,
            long cacheHits,
            long cacheMisses,
            long databaseReads,
            long databaseWrites)
        {
            double cacheHitRate = cacheHits + cacheMisses > 0
                ? (cacheHits * 100.0) / (cacheHits + cacheMisses)
                : 0;

            logger.LogInformation(
                "DATA_SOURCE_SUMMARY | Table={Table} | CacheHits={CacheHits} | CacheMisses={CacheMisses} | CacheHitRate={HitRate:F1}% | DbReads={DbReads} | DbWrites={DbWrites}",
                tableName,
                cacheHits,
                cacheMisses,
                cacheHitRate,
                databaseReads,
                databaseWrites);
        }
    }
}
