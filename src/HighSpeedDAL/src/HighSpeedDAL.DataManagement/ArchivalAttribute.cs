using System;

namespace HighSpeedDAL.DataManagement.Archival
{
    /// <summary>
    /// Marks an entity for automatic archival of old data to archive tables.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public sealed class ArchivalAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the archival strategy to use.
        /// Default is ByAge (archive records older than specified days).
        /// </summary>
        public ArchivalStrategy Strategy { get; set; } = ArchivalStrategy.ByAge;

        /// <summary>
        /// Gets or sets the age threshold in days for ByAge strategy.
        /// Records older than this will be archived.
        /// Default is 90 days.
        /// </summary>
        public int AgeDays { get; set; } = 90;

        /// <summary>
        /// Gets or sets the property name to use for age comparison.
        /// This should be a DateTime property on the entity.
        /// Default is "CreatedAt".
        /// </summary>
        public string AgeDatePropertyName { get; set; } = "CreatedAt";

        /// <summary>
        /// Gets or sets the maximum number of records to keep for ByCount strategy.
        /// When record count exceeds this, oldest records are archived.
        /// Default is 10,000.
        /// </summary>
        public int MaxRecordsToKeep { get; set; } = 10000;

        /// <summary>
        /// Gets or sets the archive table name.
        /// If not specified, defaults to "{TableName}Archive".
        /// </summary>
        public string? ArchiveTableName { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to automatically create the archive table.
        /// When true, the archive table is created if it doesn't exist.
        /// Default is true.
        /// </summary>
        public bool AutoCreateArchiveTable { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to delete records from main table after archiving.
        /// When true (default), records are moved to archive table.
        /// When false, records are copied to archive table but remain in main table.
        /// </summary>
        public bool DeleteAfterArchive { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to enable scheduled automatic archival.
        /// When true, a background job should periodically call ArchiveAsync.
        /// Default is false.
        /// </summary>
        public bool EnableScheduledArchival { get; set; } = false;

        /// <summary>
        /// Gets or sets the batch size for archival operations.
        /// Larger batches are faster but use more memory.
        /// Default is 1,000.
        /// </summary>
        public int BatchSize { get; set; } = 1000;

        /// <summary>
        /// Gets or sets a value indicating whether to log individual archived record IDs.
        /// Useful for debugging but can be verbose for large operations.
        /// Default is false.
        /// </summary>
        public bool LogArchivedIds { get; set; } = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="ArchivalAttribute"/> class
        /// with default settings.
        /// </summary>
        public ArchivalAttribute()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ArchivalAttribute"/> class
        /// with the specified strategy.
        /// </summary>
        /// <param name="strategy">The archival strategy to use.</param>
        public ArchivalAttribute(ArchivalStrategy strategy)
        {
            Strategy = strategy;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ArchivalAttribute"/> class
        /// with ByAge strategy and specified age threshold.
        /// </summary>
        /// <param name="ageDays">Age threshold in days.</param>
        public ArchivalAttribute(int ageDays)
        {
            Strategy = ArchivalStrategy.ByAge;
            AgeDays = ageDays;
        }

        /// <summary>
        /// Creates ArchivalOptions from this attribute's configuration.
        /// </summary>
        /// <returns>Configured archival options.</returns>
        public ArchivalOptions ToOptions()
        {
            return new ArchivalOptions
            {
                Strategy = Strategy,
                AgeDays = AgeDays,
                AgeDatePropertyName = AgeDatePropertyName,
                MaxRecordsToKeep = MaxRecordsToKeep,
                ArchiveTableSuffix = ArchiveTableName ?? "Archive",
                AutoCreateArchiveTable = AutoCreateArchiveTable,
                DeleteAfterArchive = DeleteAfterArchive,
                BatchSize = BatchSize,
                LogArchivedIds = LogArchivedIds
            };
        }
    }
}
