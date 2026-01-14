using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HighSpeedDAL.DataManagement.SoftDelete
{
    /// <summary>
    /// Defines the contract for managing soft deletes with recovery, cascade handling,
    /// and auto-purge capabilities.
    /// </summary>
    public interface ISoftDeleteManager
    {
        /// <summary>
        /// Soft deletes an entity by setting IsDeleted = true.
        /// </summary>
        /// <typeparam name="T">The type of entity.</typeparam>
        /// <param name="entityId">The ID of the entity to soft delete.</param>
        /// <param name="cascadeToRelated">Whether to cascade the delete to related entities.</param>
        /// <param name="deletedBy">Who is performing the delete. If null, uses current user from options.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Result of the soft delete operation.</returns>
        Task<SoftDeleteResult> SoftDeleteAsync<T>(
            object entityId,
            bool cascadeToRelated = false,
            string? deletedBy = null,
            CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Soft deletes an entity instance.
        /// </summary>
        /// <typeparam name="T">The type of entity.</typeparam>
        /// <param name="entity">The entity instance to soft delete.</param>
        /// <param name="cascadeToRelated">Whether to cascade the delete to related entities.</param>
        /// <param name="deletedBy">Who is performing the delete.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Result of the soft delete operation.</returns>
        Task<SoftDeleteResult> SoftDeleteEntityAsync<T>(
            T entity,
            bool cascadeToRelated = false,
            string? deletedBy = null,
            CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Soft deletes multiple entities.
        /// </summary>
        /// <typeparam name="T">The type of entity.</typeparam>
        /// <param name="entityIds">The IDs of entities to soft delete.</param>
        /// <param name="cascadeToRelated">Whether to cascade the delete to related entities.</param>
        /// <param name="deletedBy">Who is performing the delete.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Result of the soft delete operation.</returns>
        Task<SoftDeleteResult> SoftDeleteManyAsync<T>(
            IEnumerable<object> entityIds,
            bool cascadeToRelated = false,
            string? deletedBy = null,
            CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Recovers (undeletes) a soft deleted entity.
        /// </summary>
        /// <typeparam name="T">The type of entity.</typeparam>
        /// <param name="entityId">The ID of the entity to recover.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if recovery succeeded, false otherwise.</returns>
        Task<bool> RecoverAsync<T>(
            object entityId,
            CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Recovers multiple soft deleted entities.
        /// </summary>
        /// <typeparam name="T">The type of entity.</typeparam>
        /// <param name="entityIds">The IDs of entities to recover.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Number of entities recovered.</returns>
        Task<int> RecoverManyAsync<T>(
            IEnumerable<object> entityIds,
            CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Permanently deletes (purges) a soft deleted entity.
        /// This is a hard delete that cannot be recovered.
        /// </summary>
        /// <typeparam name="T">The type of entity.</typeparam>
        /// <param name="entityId">The ID of the entity to purge.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if purge succeeded, false otherwise.</returns>
        Task<bool> PurgeAsync<T>(
            object entityId,
            CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Permanently deletes all soft deleted entities that exceed the retention period.
        /// </summary>
        /// <typeparam name="T">The type of entity.</typeparam>
        /// <param name="olderThanDays">Delete records older than this many days. Uses attribute default if not specified.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Number of entities permanently deleted.</returns>
        Task<int> PurgeExpiredAsync<T>(
            int? olderThanDays = null,
            CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Gets all soft deleted entities of a type.
        /// </summary>
        /// <typeparam name="T">The type of entity.</typeparam>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of soft deleted entities.</returns>
        Task<List<T>> GetSoftDeletedAsync<T>(
            CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Gets soft deleted entities within a date range.
        /// </summary>
        /// <typeparam name="T">The type of entity.</typeparam>
        /// <param name="startDate">Start date for filter.</param>
        /// <param name="endDate">End date for filter.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of soft deleted entities within the date range.</returns>
        Task<List<T>> GetSoftDeletedInRangeAsync<T>(
            DateTime startDate,
            DateTime endDate,
            CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Checks if an entity is soft deleted.
        /// </summary>
        /// <typeparam name="T">The type of entity.</typeparam>
        /// <param name="entityId">The ID of the entity to check.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if the entity is soft deleted, false otherwise.</returns>
        Task<bool> IsSoftDeletedAsync<T>(
            object entityId,
            CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Gets the count of soft deleted entities.
        /// </summary>
        /// <typeparam name="T">The type of entity.</typeparam>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Count of soft deleted entities.</returns>
        Task<int> GetSoftDeletedCountAsync<T>(
            CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Checks if an entity type is configured for soft delete.
        /// </summary>
        /// <typeparam name="T">The type of entity.</typeparam>
        /// <returns>True if the entity is soft delete enabled, false otherwise.</returns>
        bool IsSoftDeleteEnabled<T>() where T : class;

        /// <summary>
        /// Gets the soft delete configuration for an entity type.
        /// </summary>
        /// <typeparam name="T">The type of entity.</typeparam>
        /// <returns>The SoftDeleteAttribute configuration, or null if not configured.</returns>
        SoftDeleteAttribute? GetSoftDeleteAttribute<T>() where T : class;

        /// <summary>
        /// Validates that an entity has the required soft delete properties.
        /// </summary>
        /// <typeparam name="T">The type of entity.</typeparam>
        /// <exception cref="InvalidOperationException">
        /// Thrown if required properties are missing.
        /// </exception>
        void ValidateEntity<T>() where T : class;
    }
}
