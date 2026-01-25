using System;

namespace HighSpeedDAL.Core.Attributes
{
    /// <summary>
    /// Marks an entity to use the staging table pattern for high-write scenarios.
    /// 
    /// Pattern:
    /// - All INSERT/UPDATE/DELETE operations go to {TableName}_Staging
    /// - SELECT operations read from {TableName} (main table, lock-free)
    /// - Background sync merges staging → main table at configured intervals
    /// - Provides eventual consistency with configurable lag (default: 30 seconds)
    /// 
    /// Benefits:
    /// - Lock-free reads from main table (ultra-fast SELECT)
    /// - Batched writes reduce contention
    /// - Better performance for high-write scenarios
    /// - Automatic conflict resolution
    /// 
    /// Example:
    /// [StagingTable(SyncIntervalSeconds = 60, ConflictResolution = ConflictResolution.LastWriteWins)]
    /// public class HighVolumeEvent : SqlServerConnectionBase
    /// {
    ///     public int Id { get; set; }
    ///     public string EventType { get; set; }
    ///     public DateTime OccurredAt { get; set; }
    /// }
    /// 
    /// Creates:
    /// - HighVolumeEvent (main table, read-only for application)
    /// - HighVolumeEvent_Staging (all writes go here)
    /// 
    /// HighSpeedDAL Framework v0.1 - Phase 3
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class StagingTableAttribute : Attribute
    {
        /// <summary>
        /// Sync interval in seconds. How often staging data is merged to main table.
        /// Default: 30 seconds
        /// Minimum: 5 seconds
        /// Maximum: 3600 seconds (1 hour)
        /// </summary>
        public int SyncIntervalSeconds { get; set; } = 30;

        /// <summary>
        /// Alias for SyncIntervalSeconds (for test compatibility)
        /// </summary>
        public int MergeIntervalSeconds
        {
            get => SyncIntervalSeconds;
            set => SyncIntervalSeconds = value;
        }

        /// <summary>
        /// Strategy for resolving conflicts when same record modified in both tables.
        /// Default: LastWriteWins
        /// </summary>
        public ConflictResolution ConflictResolution { get; set; } = ConflictResolution.LastWriteWins;

        /// <summary>
        /// If true, automatically create indexes on staging table matching main table.
        /// Default: true
        /// </summary>
        public bool MirrorIndexes { get; set; } = true;

        /// <summary>
        /// If true, keep staging records after sync for audit trail.
        /// Default: false (staging cleared after sync)
        /// </summary>
        public bool RetainStagingHistory { get; set; } = false;

        /// <summary>
        /// Maximum number of records to sync in single batch.
        /// Default: 10000
        /// Higher = fewer sync cycles but more memory
        /// Lower = more sync cycles but less memory
        /// </summary>
        public int BatchSize { get; set; } = 10000;

        /// <summary>
        /// If true, sync operation runs in transaction for atomicity.
        /// Default: true
        /// Set false for better performance if atomicity not required
        /// </summary>
        public bool UseTransaction { get; set; } = true;

        /// <summary>
        /// Priority level for sync operations when multiple staging tables exist.
        /// Higher priority tables sync first.
        /// Default: 100
        /// Range: 1-1000
        /// </summary>
        public int SyncPriority { get; set; } = 100;

        /// <summary>
        /// If true, staging table auto-creates on application startup.
        /// Default: true
        /// </summary>
        public bool AutoCreateStagingTable { get; set; } = true;

        public StagingTableAttribute()
        {
        }

        public StagingTableAttribute(int syncIntervalSeconds)
        {
            if (syncIntervalSeconds < 5)
            {
                throw new ArgumentException("SyncIntervalSeconds must be >= 5", nameof(syncIntervalSeconds));
            }
            if (syncIntervalSeconds > 3600)
            {
                throw new ArgumentException("SyncIntervalSeconds must be <= 3600", nameof(syncIntervalSeconds));
            }

            SyncIntervalSeconds = syncIntervalSeconds;
        }
    }

    /// <summary>
    /// Strategy for resolving conflicts during staging→main sync.
    /// </summary>
    public enum ConflictResolution
    {
        /// <summary>
        /// Last write wins - most recent ModifiedDate takes precedence.
        /// Requires audit columns on entity.
        /// </summary>
        LastWriteWins = 0,

        /// <summary>
        /// Main table wins - staging changes ignored if main table has newer data.
        /// Conservative approach, prevents overwriting recent changes.
        /// </summary>
        MainTableWins = 1,

        /// <summary>
        /// Staging wins - always overwrite main table with staging data.
        /// Aggressive approach, staging is source of truth.
        /// </summary>
        StagingWins = 2,

        /// <summary>
        /// Throw exception on conflict - requires manual resolution.
        /// Safest approach but requires monitoring and intervention.
        /// </summary>
        ThrowOnConflict = 3
    }
}
