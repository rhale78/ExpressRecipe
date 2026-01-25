using System;

namespace HighSpeedDAL.Core.Attributes
{
    /// <summary>
    /// Marks an entity to use memory-mapped file as L0 cache (lowest level, cross-process).
    /// Works in conjunction with database and optional L1/L2 caching layers.
    /// 
    /// Cache Hierarchy:
    /// L0: Memory-Mapped File (cross-process, disk-backed) - THIS ATTRIBUTE
    /// L1: Memory Cache (in-process, ConcurrentDictionary) - [Cached] attribute
    /// L2: Distributed Cache (Redis, etc.) - [Cached] attribute
    /// L3: Database - Always present
    /// 
    /// Use Cases:
    /// - Distributed queues across microservices
    /// - Reference data sharing (product catalogs, config)
    /// - Cross-process caching for read-heavy workloads
    /// - Session data sharing across service instances
    /// 
    /// Example:
    /// [MemoryMappedTable(FileName = "ProductCatalog", SizeMB = 500, SyncMode = MemoryMappedSyncMode.Batched)]
    /// [Cached(Strategy = CacheStrategy.TwoLayer, MaxSize = 10000)]
    /// [Table("Products")]
    /// [DalEntity]
    /// public class Product
    /// {
    ///     public int Id { get; set; }
    ///     public string Name { get; set; }
    ///     public decimal Price { get; set; }
    /// }
    /// 
    /// Generated DAL will:
    /// 1. Check memory-mapped file first (L0) - fastest, cross-process
    /// 2. Check memory cache if enabled (L1) - fast, in-process only
    /// 3. Check distributed cache if enabled (L2) - slower, cross-server
    /// 4. Query database (L3) - slowest, source of truth
    /// 5. Populate caches on database hit (working backwards: L3?L2?L1?L0)
    /// 
    /// HighSpeedDAL Framework v0.1 - Memory-Mapped Cache Extension
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class MemoryMappedTableAttribute : Attribute
    {
        /// <summary>
        /// Memory-mapped file name for cross-process sharing.
        /// File is created in %TEMP%\HighSpeedDAL\{FileName}.mmf
        /// REQUIRED - Must be unique per entity type
        /// </summary>
        public string FileName { get; set; } = null!;

        /// <summary>
        /// Maximum size of memory-mapped file in megabytes.
        /// Must be large enough to hold all cached rows plus header (16KB).
        /// Default: 100MB
        /// Minimum: 1MB
        /// Maximum: 2048MB (2GB - practical limit for cross-process)
        /// </summary>
        public int SizeMB { get; set; } = 100;

        /// <summary>
        /// Synchronization mode for memory-mapped file operations.
        /// Immediate: Flush to file after every insert/update (slowest, most durable)
        /// Batched: Flush at intervals based on FlushIntervalSeconds (balanced)
        /// Manual: Only flush when explicitly requested (fastest, least durable)
        /// Default: Batched
        /// </summary>
        public MemoryMappedSyncMode SyncMode { get; set; } = MemoryMappedSyncMode.Batched;

        /// <summary>
        /// Flush interval in seconds for Batched sync mode.
        /// Default: 30 seconds
        /// Minimum: 1 second
        /// Maximum: 3600 seconds (1 hour)
        /// Ignored for Immediate and Manual modes.
        /// </summary>
        public int FlushIntervalSeconds { get; set; } = 30;

        /// <summary>
        /// When true, automatically creates the memory-mapped file if it doesn't exist.
        /// When false, throws exception if file doesn't exist.
        /// Default: true
        /// </summary>
        public bool AutoCreateFile { get; set; } = true;

        /// <summary>
        /// When true, automatically loads data from memory-mapped file on DAL initialization.
        /// When false, starts with empty cache (file still created for writes).
        /// Default: true
        /// </summary>
        public bool AutoLoadOnStartup { get; set; } = true;

        /// <summary>
        /// When true, memory-mapped file is treated as read-only cache.
        /// Writes go to database only, reads check L0 cache first.
        /// When false, writes update both database and L0 cache.
        /// Default: false (read-write cache)
        /// </summary>
        public bool ReadOnlyCache { get; set; } = false;

        /// <summary>
        /// Maximum number of rows to keep in memory-mapped file.
        /// When exceeded, oldest rows are evicted (LRU policy).
        /// Default: 0 (unlimited - keep all rows from database queries)
        /// </summary>
        public int MaxCachedRows { get; set; } = 0;

        /// <summary>
        /// Time-to-live for cached rows in seconds.
        /// Rows older than this are not returned from cache.
        /// Default: 0 (no expiration - cache until schema change or explicit flush)
        /// </summary>
        public int TimeToLiveSeconds { get; set; } = 0;

        /// <summary>
        /// Validates the attribute configuration.
        /// </summary>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(FileName))
            {
                throw new ArgumentException("FileName must be specified for MemoryMappedTable", nameof(FileName));
            }

            if (SizeMB < 1 || SizeMB > 2048)
            {
                throw new ArgumentOutOfRangeException(nameof(SizeMB), 
                    "SizeMB must be between 1 and 2048");
            }

            if (FlushIntervalSeconds < 0 || FlushIntervalSeconds > 3600)
            {
                throw new ArgumentOutOfRangeException(nameof(FlushIntervalSeconds), 
                    "FlushIntervalSeconds must be between 0 and 3600");
            }

            if (MaxCachedRows < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(MaxCachedRows), 
                    "MaxCachedRows must be non-negative (0 for unlimited)");
            }

            if (TimeToLiveSeconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(TimeToLiveSeconds), 
                    "TimeToLiveSeconds must be non-negative (0 for no expiration)");
            }
        }
    }
}
