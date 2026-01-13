using System;

namespace HighSpeedDAL.DataManagement.SoftDelete
{
    /// <summary>
    /// Configuration options for soft delete operations.
    /// </summary>
    public class SoftDeleteOptions
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
        /// Default is false.
        /// </summary>
        public bool CascadeDelete { get; set; } = false;

        /// <summary>
        /// Gets or sets the number of days to retain soft deleted records before auto-purge.
        /// Set to 0 for unlimited retention.
        /// Default is 30 days.
        /// </summary>
        public int RetentionDays { get; set; } = 30;

        /// <summary>
        /// Gets or sets a value indicating whether to automatically purge records that exceed retention period.
        /// Default is false.
        /// </summary>
        public bool AutoPurge { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether to automatically filter out soft deleted records in queries.
        /// Default is true.
        /// </summary>
        public bool AutoFilter { get; set; } = true;

        /// <summary>
        /// Gets or sets the current user identifier for tracking who performed the delete.
        /// This should be set by the application context.
        /// </summary>
        public string? CurrentUserId { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to validate that required properties exist on the entity.
        /// When true, throws exception if IsDeleted, DeletedAt, or DeletedBy properties are missing.
        /// Default is true.
        /// </summary>
        public bool ValidateProperties { get; set; } = true;

        /// <summary>
        /// Creates a copy of these options.
        /// </summary>
        /// <returns>A new instance with the same values.</returns>
        public SoftDeleteOptions Clone()
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
                CurrentUserId = CurrentUserId,
                ValidateProperties = ValidateProperties
            };
        }
    }
}
