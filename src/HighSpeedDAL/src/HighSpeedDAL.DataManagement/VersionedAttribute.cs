using System;

namespace HighSpeedDAL.DataManagement.Versioning
{
    /// <summary>
    /// Marks an entity as versioned for optimistic concurrency control and temporal queries.
    /// When applied, the entity will automatically track versions and detect concurrent modifications.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public sealed class VersionedAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the versioning strategy to use.
        /// Default is RowVersion for SQL Server (best performance).
        /// </summary>
        public VersionStrategy Strategy { get; set; } = VersionStrategy.RowVersion;

        /// <summary>
        /// Gets or sets the name of the property that holds the version value.
        /// If not specified, defaults based on strategy:
        /// - RowVersion: "RowVersion"
        /// - Timestamp: "LastModified" or "ModifiedAt"
        /// - Integer: "Version"
        /// - Guid: "VersionGuid"
        /// </summary>
        public string? PropertyName { get; set; }

        /// <summary>
        /// Gets or sets the name of the database column that holds the version value.
        /// If not specified, uses the PropertyName or defaults based on strategy.
        /// </summary>
        public string? ColumnName { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to track version history in a separate table.
        /// When true, each version of the entity is stored in a history table for temporal queries.
        /// Default is false for better performance.
        /// </summary>
        public bool TrackHistory { get; set; } = false;

        /// <summary>
        /// Gets or sets the name of the history table.
        /// If not specified, defaults to "{EntityName}History".
        /// Only used when TrackHistory is true.
        /// </summary>
        public string? HistoryTableName { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to automatically create the history table.
        /// Only used when TrackHistory is true.
        /// Default is true.
        /// </summary>
        public bool AutoCreateHistoryTable { get; set; } = true;

        /// <summary>
        /// Gets or sets the number of days to retain version history.
        /// After this period, old versions can be purged.
        /// Only used when TrackHistory is true.
        /// Default is 365 days (1 year).
        /// Set to 0 for unlimited retention.
        /// </summary>
        public int HistoryRetentionDays { get; set; } = 365;

        /// <summary>
        /// Gets or sets a value indicating whether to throw an exception on version conflicts.
        /// When true (default), UpdateAsync will throw VersionConflictException if versions don't match.
        /// When false, the update will be silently ignored and return false.
        /// </summary>
        public bool ThrowOnConflict { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to track who modified the entity.
        /// When true, a "ModifiedBy" property is expected on the entity.
        /// Default is false.
        /// </summary>
        public bool TrackModifiedBy { get; set; } = false;

        /// <summary>
        /// Gets or sets the name of the property that holds the user who modified the entity.
        /// Only used when TrackModifiedBy is true.
        /// Default is "ModifiedBy".
        /// </summary>
        public string ModifiedByPropertyName { get; set; } = "ModifiedBy";

        /// <summary>
        /// Initializes a new instance of the <see cref="VersionedAttribute"/> class
        /// with the default RowVersion strategy.
        /// </summary>
        public VersionedAttribute()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="VersionedAttribute"/> class
        /// with the specified versioning strategy.
        /// </summary>
        /// <param name="strategy">The versioning strategy to use.</param>
        public VersionedAttribute(VersionStrategy strategy)
        {
            Strategy = strategy;
        }

        /// <summary>
        /// Gets the default property name for the specified strategy.
        /// </summary>
        /// <param name="strategy">The versioning strategy.</param>
        /// <returns>The default property name.</returns>
        public static string GetDefaultPropertyName(VersionStrategy strategy)
        {
            return strategy switch
            {
                VersionStrategy.RowVersion => "RowVersion",
                VersionStrategy.Timestamp => "LastModified",
                VersionStrategy.Integer => "Version",
                VersionStrategy.Guid => "VersionGuid",
                _ => "RowVersion"
            };
        }

        /// <summary>
        /// Gets the default column name for the specified strategy.
        /// </summary>
        /// <param name="strategy">The versioning strategy.</param>
        /// <returns>The default column name.</returns>
        public static string GetDefaultColumnName(VersionStrategy strategy)
        {
            return strategy switch
            {
                VersionStrategy.RowVersion => "RowVersion",
                VersionStrategy.Timestamp => "LastModified",
                VersionStrategy.Integer => "Version",
                VersionStrategy.Guid => "VersionGuid",
                _ => "RowVersion"
            };
        }
    }
}
