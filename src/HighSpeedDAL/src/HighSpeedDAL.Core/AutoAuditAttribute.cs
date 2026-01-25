using System;

namespace HighSpeedDAL.Core.Attributes
{
    /// <summary>
    /// Enables automatic audit field population for an entity
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class AutoAuditAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets whether to track CreatedBy and CreatedDate
        /// </summary>
        public bool TrackCreated { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to track ModifiedBy and ModifiedDate
        /// </summary>
        public bool TrackModified { get; set; } = true;
    }
}
