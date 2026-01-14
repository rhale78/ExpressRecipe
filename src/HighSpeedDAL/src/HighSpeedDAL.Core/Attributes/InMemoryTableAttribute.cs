using System;

namespace HighSpeedDAL.Core.Attributes;

/// <summary>
/// Marks an entity to use high-speed in-memory table storage with optional flush to staging/main table.
/// 
/// Pattern:
/// - All CRUD operations occur in memory using optimized dictionary structures
/// - Data can be queried using SQL-like WHERE clauses
/// - Periodic flush to staging table (if configured) or direct to main table
/// - Respects column constraints: data types, max lengths, nullability, unique indexes
/// 
/// Benefits:
/// - Ultra-fast writes (no database round-trip)
/// - Ultra-fast reads (pure memory access)
/// - Full constraint validation in memory
/// - Queryable with WHERE clauses
/// - Configurable persistence strategy
/// 
/// Example:
/// [InMemoryTable(FlushIntervalSeconds = 30, MaxRowCount = 100000)]
/// [Table("HighVolumeEvents")]
/// [DalEntity]
/// public class HighVolumeEvent
/// {
///     public int Id { get; set; }
///     
///     [MaxLength(100)]
///     public string EventType { get; set; }
///     
///     public DateTime OccurredAt { get; set; }
/// }
/// 
/// HighSpeedDAL Framework v0.1 - Phase 4
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class InMemoryTableAttribute : Attribute
{
    /// <summary>
    /// Flush interval in seconds. How often in-memory data is persisted.
    /// Default: 30 seconds
    /// Minimum: 1 second
    /// Maximum: 3600 seconds (1 hour)
    /// Set to 0 to disable automatic flushing (manual flush only)
    /// </summary>
    public int FlushIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum number of rows to keep in memory before forcing a flush.
    /// Default: 100000 rows
    /// Minimum: 100 rows
    /// Set to 0 for unlimited (not recommended for production)
    /// </summary>
    public int MaxRowCount { get; set; } = 100000;

    /// <summary>
    /// When true, flushes to staging table first, then staging syncs to main table.
    /// When false, flushes directly to main table.
    /// Default: true (recommended for high-write scenarios)
    /// </summary>
    public bool FlushToStaging { get; set; } = true;

    /// <summary>
    /// When true, automatically generates sequential IDs for new rows.
    /// When false, caller must provide IDs.
    /// Default: true
    /// </summary>
    public bool AutoGenerateId { get; set; } = true;

    /// <summary>
    /// When true, enforces unique constraints and indexes in memory.
    /// When false, skips unique validation (faster but less safe).
    /// Default: true
    /// </summary>
    public bool EnforceConstraints { get; set; } = true;

    /// <summary>
    /// When true, validates data types and lengths on insert/update.
    /// When false, skips validation (faster but may cause flush failures).
    /// Default: true
    /// </summary>
    public bool ValidateOnWrite { get; set; } = true;

    /// <summary>
    /// Number of rows to process per batch during flush operations.
    /// Higher values = faster flush, more memory usage.
    /// Default: 1000
    /// </summary>
    public int FlushBatchSize { get; set; } = 1000;

    /// <summary>
    /// When true, preserves rows in memory after flush (for continued querying).
    /// When false, clears flushed rows from memory.
    /// Default: false
    /// </summary>
    public bool RetainAfterFlush { get; set; } = false;

    /// <summary>
    /// Flush priority for concurrent flush scheduling.
    /// Higher priority tables flush first.
    /// Default: 0
    /// </summary>
    public int FlushPriority { get; set; } = 0;

    /// <summary>
    /// When true, tracks insert/update/delete operations for replay during flush.
    /// When false, only flushes current state (faster, but loses operation history).
    /// Default: true
    /// </summary>
    public bool TrackOperations { get; set; } = true;

    /// <summary>
    /// Memory-mapped file name for cross-process sharing.
    /// When null/empty, memory-mapped file feature is disabled.
    /// File is created in %TEMP%\HighSpeedDAL\{FileName}.mmf
    /// Default: null (disabled)
    /// </summary>
    public string? MemoryMappedFileName { get; set; } = null;

    /// <summary>
    /// Maximum size of memory-mapped file in megabytes.
    /// Must be large enough to hold all rows plus header (16KB).
    /// Default: 100MB
    /// Minimum: 1MB
    /// Maximum: 2048MB (2GB - practical limit for cross-process)
    /// </summary>
    public int MemoryMappedFileSizeMB { get; set; } = 100;

    /// <summary>
    /// Synchronization mode for memory-mapped file operations.
    /// Immediate: Flush to file after every insert/update (slowest, most durable)
    /// Batched: Flush at intervals based on FlushIntervalSeconds (balanced)
    /// Manual: Only flush when explicitly requested (fastest, least durable)
    /// Default: Batched
    /// </summary>
    public MemoryMappedSyncMode SyncMode { get; set; } = MemoryMappedSyncMode.Batched;

    /// <summary>
    /// When true, automatically creates the memory-mapped file if it doesn't exist.
    /// When false, throws exception if file doesn't exist.
    /// Default: true
    /// </summary>
    public bool AutoCreateFile { get; set; } = true;

    /// <summary>
    /// When true, automatically loads data from memory-mapped file on startup.
    /// When false, starts with empty table (file still created for writes).
    /// Default: true
    /// </summary>
    public bool AutoLoadOnStartup { get; set; } = true;

    /// <summary>
    /// When true, deletes the memory-mapped file when the InMemoryTable is disposed.
    /// When false, file persists for cross-process scenarios or restarts.
    /// Default: false (preserve file for persistence)
    /// Recommended: true for tests/development, false for production.
    /// </summary>
    public bool DeleteFileOnDispose { get; set; } = false;

        /// <summary>
        /// Validates the attribute configuration.
        /// </summary>
        public void Validate()
        {
            if (FlushIntervalSeconds < 0 || FlushIntervalSeconds > 3600)
            {
                throw new ArgumentOutOfRangeException(nameof(FlushIntervalSeconds), 
                    "FlushIntervalSeconds must be between 0 and 3600");
            }

            if (MaxRowCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(MaxRowCount), 
                    "MaxRowCount must be non-negative (0 for unlimited)");
            }

            if (FlushBatchSize < 1 || FlushBatchSize > 100000)
            {
                throw new ArgumentOutOfRangeException(nameof(FlushBatchSize), 
                    "FlushBatchSize must be between 1 and 100000");
            }

            if (MemoryMappedFileSizeMB < 1 || MemoryMappedFileSizeMB > 2048)
            {
                throw new ArgumentOutOfRangeException(nameof(MemoryMappedFileSizeMB), 
                    "MemoryMappedFileSizeMB must be between 1 and 2048");
            }
        }
    }

    /// <summary>
    /// Synchronization mode for memory-mapped file operations.
    /// </summary>
    public enum MemoryMappedSyncMode
    {
        /// <summary>
        /// Flush to file after every insert/update operation.
        /// Slowest but most durable. Recommended for critical queue data.
        /// </summary>
        Immediate = 0,

        /// <summary>
        /// Flush at intervals based on FlushIntervalSeconds.
        /// Balanced approach for most scenarios.
        /// </summary>
        Batched = 1,

        /// <summary>
        /// Only flush when explicitly requested via FlushAsync().
        /// Fastest but least durable. Recommended for high-throughput bulk operations.
        /// </summary>
        Manual = 2
    }
