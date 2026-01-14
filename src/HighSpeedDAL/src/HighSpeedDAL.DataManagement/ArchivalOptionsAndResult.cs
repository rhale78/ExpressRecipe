using System;
using System.Collections.Generic;

namespace HighSpeedDAL.DataManagement.Archival
{
    /// <summary>
    /// Configuration options for archival operations.
    /// </summary>
    public class ArchivalOptions
    {
        /// <summary>
        /// Gets or sets the archival strategy to use.
        /// </summary>
        public ArchivalStrategy Strategy { get; set; } = ArchivalStrategy.ByAge;

        /// <summary>
        /// Gets or sets the age threshold in days for ByAge strategy.
        /// Records older than this will be archived.
        /// </summary>
        public int AgeDays { get; set; } = 90;

        /// <summary>
        /// Gets or sets the property name to use for age comparison.
        /// Default is "CreatedAt".
        /// </summary>
        public string AgeDatePropertyName { get; set; } = "CreatedAt";

        /// <summary>
        /// Gets or sets the maximum number of records to keep for ByCount strategy.
        /// </summary>
        public int MaxRecordsToKeep { get; set; } = 10000;

        /// <summary>
        /// Gets or sets the archive table name suffix.
        /// Archive table will be {TableName}{ArchiveTableSuffix}.
        /// Default is "Archive".
        /// </summary>
        public string ArchiveTableSuffix { get; set; } = "Archive";

        /// <summary>
        /// Gets or sets a value indicating whether to automatically create the archive table.
        /// </summary>
        public bool AutoCreateArchiveTable { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to delete records from main table after archiving.
        /// When false, records are copied to archive but remain in main table.
        /// </summary>
        public bool DeleteAfterArchive { get; set; } = true;

        /// <summary>
        /// Gets or sets the batch size for archival operations.
        /// Large batches process faster but use more memory.
        /// </summary>
        public int BatchSize { get; set; } = 1000;

        /// <summary>
        /// Gets or sets a value indicating whether to log archived record IDs.
        /// </summary>
        public bool LogArchivedIds { get; set; } = false;
    }

    /// <summary>
    /// Result of an archival operation.
    /// </summary>
    public class ArchivalResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether the operation was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the number of records archived.
        /// </summary>
        public int RecordsArchived { get; set; }

        /// <summary>
        /// Gets or sets the number of records deleted from the main table.
        /// </summary>
        public int RecordsDeleted { get; set; }

        /// <summary>
        /// Gets or sets when the archival operation occurred.
        /// </summary>
        public DateTime ArchivedAt { get; set; }

        /// <summary>
        /// Gets or sets the archival strategy used.
        /// </summary>
        public ArchivalStrategy Strategy { get; set; }

        /// <summary>
        /// Gets or sets any error message if the operation failed.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets the list of archived record IDs.
        /// </summary>
        public List<object> ArchivedRecordIds { get; set; } = new List<object>();

        /// <summary>
        /// Gets or sets the archive table name.
        /// </summary>
        public string? ArchiveTableName { get; set; }

        /// <summary>
        /// Creates a successful result.
        /// </summary>
        public static ArchivalResult CreateSuccess(
            int archived,
            int deleted,
            ArchivalStrategy strategy,
            string archiveTableName)
        {
            return new ArchivalResult
            {
                Success = true,
                RecordsArchived = archived,
                RecordsDeleted = deleted,
                ArchivedAt = DateTime.UtcNow,
                Strategy = strategy,
                ArchiveTableName = archiveTableName
            };
        }

        /// <summary>
        /// Creates a failed result.
        /// </summary>
        public static ArchivalResult CreateFailure(string errorMessage, ArchivalStrategy strategy)
        {
            return new ArchivalResult
            {
                Success = false,
                ErrorMessage = errorMessage,
                ArchivedAt = DateTime.UtcNow,
                Strategy = strategy
            };
        }
    }
}
