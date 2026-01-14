using System;

namespace HighSpeedDAL.DataManagement.SoftDelete
{
    /// <summary>
    /// Marks an entity as soft-deletable, enabling soft delete functionality with recovery,
    /// cascade handling, and auto-purge capabilities.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public sealed class SoftDeleteAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the name of the property that indicates if an entity is soft deleted.
        /// Default is "IsDeleted".
        /// </summary>
        public string IsDeletedPropertyName { get; set; } = "IsDeleted";

        /// <summary>
        /// Gets or sets the name of the property that tracks when an entity was soft deleted.
        /// Default is "DeletedAt".
        /// </summary>
        public string DeletedAtPropertyName { get; set; } = "DeletedAt";

        /// <summary>
        /// Gets or sets the name of the property that tracks who soft deleted an entity.
        /// Default is "DeletedBy".
        /// </summary>
        public string DeletedByPropertyName { get; set; } = "DeletedBy";

        /// <summary>
        /// Gets or sets a value indicating whether to cascade soft deletes to related entities.
        /// When true, soft deleting this entity will also soft delete related entities.
        /// Default is false.
        /// </summary>
        public bool CascadeDelete { get; set; } = false;

        /// <summary>
        /// Gets or sets the number of days to retain soft deleted records before auto-purge.
        /// After this period, soft deleted records can be permanently deleted.
        /// Set to 0 for unlimited retention.
        /// Default is 30 days.
        /// </summary>
        public int RetentionDays { get; set; } = 30;

        /// <summary>
        /// Gets or sets a value indicating whether to automatically purge records that exceed retention period.
        /// When true, a background job should periodically call PurgeExpiredAsync.
        /// Default is false.
        /// </summary>
        public bool AutoPurge { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether to automatically filter out soft deleted records in queries.
        /// When true (default), normal queries will exclude soft deleted records unless explicitly requested.
        /// Default is true.
        /// </summary>
        public bool AutoFilter { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to validate that required properties exist.
        /// When true, throws exception if IsDeleted, DeletedAt, or DeletedBy properties are missing.
        /// Default is true.
        /// </summary>
        public bool ValidateProperties { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum cascade depth when cascade deleting.
        /// Prevents infinite loops in circular relationships.
        /// Default is 10.
        /// </summary>
        public int MaxCascadeDepth { get; set; } = 10;

        /// <summary>
        /// Initializes a new instance of the <see cref="SoftDeleteAttribute"/> class
        /// with default settings.
        /// </summary>
        public SoftDeleteAttribute()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SoftDeleteAttribute"/> class
        /// with cascade delete enabled.
        /// </summary>
        /// <param name="cascadeDelete">Whether to cascade deletes to related entities.</param>
        public SoftDeleteAttribute(bool cascadeDelete)
        {
            CascadeDelete = cascadeDelete;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SoftDeleteAttribute"/> class
        /// with cascade delete and retention settings.
        /// </summary>
        /// <param name="cascadeDelete">Whether to cascade deletes to related entities.</param>
        /// <param name="retentionDays">Number of days to retain soft deleted records.</param>
        public SoftDeleteAttribute(bool cascadeDelete, int retentionDays)
        {
            CascadeDelete = cascadeDelete;
            RetentionDays = retentionDays;
        }

        /// <summary>
        /// Creates SoftDeleteOptions from this attribute's configuration.
        /// </summary>
        /// <returns>Configured soft delete options.</returns>
        public SoftDeleteOptions ToOptions()
        {
            return new SoftDeleteOptions
            {
                IsDeletedPropertyName = IsDeletedPropertyName,
                DeletedAtPropertyName = DeletedAtPropertyName,
                DeletedByPropertyName = DeletedByPropertyName,
                CascadeDelete = CascadeDelete,
                RetentionDays = RetentionDays,
                AutoPurge = AutoPurge,
                AutoFilter = AutoFilter,
                ValidateProperties = ValidateProperties
            };
        }
    }
}
