using System;

namespace HighSpeedDAL.DataManagement.Archival
{
    /// <summary>
    /// Defines the strategy used for determining which records to archive.
    /// </summary>
    public enum ArchivalStrategy
    {
        /// <summary>
        /// Archive records based on age (e.g., older than 90 days).
        /// </summary>
        ByAge = 0,

        /// <summary>
        /// Archive records based on total count (keep only most recent N records).
        /// </summary>
        ByCount = 1,

        /// <summary>
        /// Archive records based on custom condition/predicate.
        /// </summary>
        ByCondition = 2,

        /// <summary>
        /// Archive records based on file size thresholds.
        /// </summary>
        BySize = 3
    }
}
