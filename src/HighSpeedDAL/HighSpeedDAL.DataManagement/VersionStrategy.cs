using System;

namespace HighSpeedDAL.DataManagement.Versioning
{
    /// <summary>
    /// Defines the strategy used for entity versioning.
    /// </summary>
    public enum VersionStrategy
    {
        /// <summary>
        /// Uses SQL Server ROWVERSION (timestamp) for automatic versioning.
        /// Recommended for SQL Server for best performance and concurrency detection.
        /// </summary>
        RowVersion = 0,

        /// <summary>
        /// Uses a DateTime column to track when the entity was last modified.
        /// Suitable for both SQL Server and SQLite.
        /// </summary>
        Timestamp = 1,

        /// <summary>
        /// Uses an integer column that increments on each update.
        /// Suitable for both SQL Server and SQLite.
        /// </summary>
        Integer = 2,

        /// <summary>
        /// Uses a GUID column that generates a new value on each update.
        /// Suitable for both SQL Server and SQLite, useful for distributed systems.
        /// </summary>
        Guid = 3
    }
}
