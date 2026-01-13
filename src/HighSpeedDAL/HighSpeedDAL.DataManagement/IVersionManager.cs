using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HighSpeedDAL.DataManagement.Versioning
{
    /// <summary>
    /// Defines the contract for managing entity versions for optimistic concurrency
    /// and temporal queries.
    /// </summary>
    public interface IVersionManager
    {
        /// <summary>
        /// Gets the version information for an entity.
        /// </summary>
        /// <typeparam name="T">The type of entity.</typeparam>
        /// <param name="entity">The entity instance.</param>
        /// <returns>Version information for the entity, or null if not versioned.</returns>
        VersionInfo? GetVersionInfo<T>(T entity) where T : class;

        /// <summary>
        /// Gets the version information for an entity by its ID.
        /// </summary>
        /// <typeparam name="T">The type of entity.</typeparam>
        /// <param name="entityId">The entity ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Version information for the entity, or null if not found.</returns>
        Task<VersionInfo?> GetVersionInfoByIdAsync<T>(
            object entityId,
            CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Validates that an entity's version matches the current database version.
        /// </summary>
        /// <typeparam name="T">The type of entity.</typeparam>
        /// <param name="entity">The entity to validate.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if versions match, false otherwise.</returns>
        /// <exception cref="VersionConflictException">
        /// Thrown if versions don't match and ThrowOnConflict is true.
        /// </exception>
        Task<bool> ValidateVersionAsync<T>(
            T entity,
            CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Updates an entity with version checking for optimistic concurrency.
        /// </summary>
        /// <typeparam name="T">The type of entity.</typeparam>
        /// <param name="entity">The entity to update.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if update succeeded, false if version conflict occurred.</returns>
        /// <exception cref="VersionConflictException">
        /// Thrown if version conflict occurs and ThrowOnConflict is true.
        /// </exception>
        Task<bool> UpdateWithVersionCheckAsync<T>(
            T entity,
            CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Updates multiple entities with version checking.
        /// </summary>
        /// <typeparam name="T">The type of entity.</typeparam>
        /// <param name="entities">The entities to update.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A dictionary mapping entities to their update results.</returns>
        Task<Dictionary<T, bool>> UpdateManyWithVersionCheckAsync<T>(
            IEnumerable<T> entities,
            CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Increments the version of an entity.
        /// </summary>
        /// <typeparam name="T">The type of entity.</typeparam>
        /// <param name="entity">The entity to increment.</param>
        void IncrementVersion<T>(T entity) where T : class;

        /// <summary>
        /// Gets an entity as it existed at a specific point in time (temporal query).
        /// Requires TrackHistory to be enabled on the entity.
        /// </summary>
        /// <typeparam name="T">The type of entity.</typeparam>
        /// <param name="entityId">The entity ID.</param>
        /// <param name="asOfDate">The date to retrieve the entity as of.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The entity as it existed at the specified time, or null if not found.</returns>
        Task<T?> GetAsOfAsync<T>(
            object entityId,
            DateTime asOfDate,
            CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Gets all versions of an entity from the history table.
        /// Requires TrackHistory to be enabled on the entity.
        /// </summary>
        /// <typeparam name="T">The type of entity.</typeparam>
        /// <param name="entityId">The entity ID.</param>
        /// <param name="startDate">Optional start date filter.</param>
        /// <param name="endDate">Optional end date filter.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of historical versions ordered by date descending.</returns>
        Task<List<T>> GetVersionHistoryAsync<T>(
            object entityId,
            DateTime? startDate = null,
            DateTime? endDate = null,
            CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Saves the current entity version to the history table.
        /// Requires TrackHistory to be enabled on the entity.
        /// </summary>
        /// <typeparam name="T">The type of entity.</typeparam>
        /// <param name="entity">The entity to save to history.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Task representing the async operation.</returns>
        Task SaveToHistoryAsync<T>(
            T entity,
            CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Creates the version history table for an entity type.
        /// Only needed if AutoCreateHistoryTable is false.
        /// </summary>
        /// <typeparam name="T">The type of entity.</typeparam>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Task representing the async operation.</returns>
        Task CreateHistoryTableAsync<T>(
            CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Purges old version history records that exceed the retention period.
        /// </summary>
        /// <typeparam name="T">The type of entity.</typeparam>
        /// <param name="retentionDays">Number of days to retain. Uses attribute default if not specified.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The number of history records purged.</returns>
        Task<int> PurgeOldHistoryAsync<T>(
            int? retentionDays = null,
            CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Checks if an entity type is configured for versioning.
        /// </summary>
        /// <typeparam name="T">The type of entity.</typeparam>
        /// <returns>True if the entity is versioned, false otherwise.</returns>
        bool IsVersioned<T>() where T : class;

        /// <summary>
        /// Gets the versioning configuration for an entity type.
        /// </summary>
        /// <typeparam name="T">The type of entity.</typeparam>
        /// <returns>The VersionedAttribute configuration, or null if not versioned.</returns>
        VersionedAttribute? GetVersionedAttribute<T>() where T : class;

        /// <summary>
        /// Compares two version values to determine if they are equal.
        /// </summary>
        /// <param name="version1">First version.</param>
        /// <param name="version2">Second version.</param>
        /// <param name="strategy">The versioning strategy.</param>
        /// <returns>True if versions are equal, false otherwise.</returns>
        bool VersionsEqual(object? version1, object? version2, VersionStrategy strategy);
    }
}
