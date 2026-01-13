using System;
using System.Collections.Generic;

namespace HighSpeedDAL.DataManagement.SoftDelete
{
    /// <summary>
    /// Result of a soft delete operation.
    /// </summary>
    public class SoftDeleteResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether the operation was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the number of entities that were soft deleted.
        /// </summary>
        public int EntitiesDeleted { get; set; }

        /// <summary>
        /// Gets or sets the number of related entities that were cascade deleted.
        /// </summary>
        public int RelatedEntitiesDeleted { get; set; }

        /// <summary>
        /// Gets or sets the total number of entities affected (including cascades).
        /// </summary>
        public int TotalAffected => EntitiesDeleted + RelatedEntitiesDeleted;

        /// <summary>
        /// Gets or sets when the soft delete operation occurred.
        /// </summary>
        public DateTime DeletedAt { get; set; }

        /// <summary>
        /// Gets or sets who performed the soft delete.
        /// </summary>
        public string? DeletedBy { get; set; }

        /// <summary>
        /// Gets or sets any error message if the operation failed.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets the list of entity IDs that were deleted.
        /// </summary>
        public List<object> DeletedEntityIds { get; set; } = new List<object>();

        /// <summary>
        /// Gets or sets the list of related entity IDs that were cascade deleted.
        /// </summary>
        public List<object> CascadeDeletedEntityIds { get; set; } = new List<object>();

        /// <summary>
        /// Creates a successful result.
        /// </summary>
        /// <param name="entitiesDeleted">Number of entities deleted.</param>
        /// <param name="relatedDeleted">Number of related entities deleted.</param>
        /// <param name="deletedBy">Who performed the delete.</param>
        /// <returns>A successful soft delete result.</returns>
        public static SoftDeleteResult CreateSuccess(
            int entitiesDeleted,
            int relatedDeleted = 0,
            string? deletedBy = null)
        {
            return new SoftDeleteResult
            {
                Success = true,
                EntitiesDeleted = entitiesDeleted,
                RelatedEntitiesDeleted = relatedDeleted,
                DeletedAt = DateTime.UtcNow,
                DeletedBy = deletedBy
            };
        }

        /// <summary>
        /// Creates a failed result.
        /// </summary>
        /// <param name="errorMessage">The error message.</param>
        /// <returns>A failed soft delete result.</returns>
        public static SoftDeleteResult CreateFailure(string errorMessage)
        {
            return new SoftDeleteResult
            {
                Success = false,
                ErrorMessage = errorMessage,
                DeletedAt = DateTime.UtcNow
            };
        }
    }
}
