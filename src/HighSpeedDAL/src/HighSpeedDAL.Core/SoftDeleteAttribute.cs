using System;

namespace HighSpeedDAL.Core.Attributes
{
    /// <summary>
    /// Marks a table for soft delete support.
    /// When applied, framework automatically adds these properties if not already present:
    /// - bool IsDeleted { get; set; }
    /// - DateTime? DeletedDate { get; set; }
    /// - string DeletedBy { get; set; }
    /// 
    /// Delete operations will set IsDeleted=true instead of removing rows.
    /// All queries automatically filter WHERE IsDeleted = 0 unless explicitly requested.
    /// 
    /// HighSpeedDAL Framework v0.1
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class SoftDeleteAttribute : Attribute
    {
        /// <summary>
        /// If true, permanently deleted rows older than RetentionDays will be purged.
        /// Default: false
        /// </summary>
        public bool AutoPurge { get; set; }

        /// <summary>
        /// Number of days to retain soft-deleted rows before permanent purge (if AutoPurge enabled).
        /// Default: 90 days
        /// </summary>
        public int RetentionDays { get; set; }

        /// <summary>
        /// Name of the boolean column that indicates if a row is deleted
        /// Default: "IsDeleted"
        /// </summary>
        public string DeletedColumn { get; set; } = "IsDeleted";

        /// <summary>
        /// Name of the datetime column that stores when a row was deleted
        /// Default: "DeletedDate"
        /// </summary>
        public string DeletedDateColumn { get; set; } = "DeletedDate";

        public SoftDeleteAttribute()
        {
            AutoPurge = false;
            RetentionDays = 90;
        }
    }
}
