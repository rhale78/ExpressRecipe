using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HighSpeedDAL.DataManagement.Archival
{
    /// <summary>
    /// Defines the contract for managing data archival operations.
    /// </summary>
    public interface IArchivalManager
    {
        /// <summary>
        /// Archives old records according to the configured strategy.
        /// </summary>
        /// <typeparam name="T">The type of entity.</typeparam>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Result of the archival operation.</returns>
        Task<ArchivalResult> ArchiveAsync<T>(
            CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Archives records based on a custom age threshold.
        /// </summary>
        /// <typeparam name="T">The type of entity.</typeparam>
        /// <param name="olderThanDays">Archive records older than this many days.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Result of the archival operation.</returns>
        Task<ArchivalResult> ArchiveByAgeAsync<T>(
            int olderThanDays,
            CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Archives records to maintain a maximum count in the main table.
        /// </summary>
        /// <typeparam name="T">The type of entity.</typeparam>
        /// <param name="maxRecordsToKeep">Maximum number of records to keep in main table.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Result of the archival operation.</returns>
        Task<ArchivalResult> ArchiveByCountAsync<T>(
            int maxRecordsToKeep,
            CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Restores archived records back to the main table.
        /// </summary>
        /// <typeparam name="T">The type of entity.</typeparam>
        /// <param name="recordIds">IDs of records to restore.</param>
        /// <param name="deleteFromArchive">Whether to delete records from archive after restoring.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Number of records restored.</returns>
        Task<int> RestoreFromArchiveAsync<T>(
            IEnumerable<object> recordIds,
            bool deleteFromArchive = true,
            CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Gets all archived records.
        /// </summary>
        /// <typeparam name="T">The type of entity.</typeparam>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of archived records.</returns>
        Task<List<T>> GetArchivedRecordsAsync<T>(
            CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Gets archived records within a date range.
        /// </summary>
        /// <typeparam name="T">The type of entity.</typeparam>
        /// <param name="startDate">Start date for filter.</param>
        /// <param name="endDate">End date for filter.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of archived records within the date range.</returns>
        Task<List<T>> GetArchivedRecordsInRangeAsync<T>(
            DateTime startDate,
            DateTime endDate,
            CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Gets the count of archived records.
        /// </summary>
        /// <typeparam name="T">The type of entity.</typeparam>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Count of archived records.</returns>
        Task<int> GetArchivedCountAsync<T>(
            CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Creates the archive table for an entity type.
        /// Only needed if AutoCreateArchiveTable is false.
        /// </summary>
        /// <typeparam name="T">The type of entity.</typeparam>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Task representing the async operation.</returns>
        Task CreateArchiveTableAsync<T>(
            CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Permanently deletes records from the archive table.
        /// </summary>
        /// <typeparam name="T">The type of entity.</typeparam>
        /// <param name="recordIds">IDs of records to delete from archive.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Number of records deleted.</returns>
        Task<int> PurgeFromArchiveAsync<T>(
            IEnumerable<object> recordIds,
            CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Checks if an entity type is configured for archival.
        /// </summary>
        /// <typeparam name="T">The type of entity.</typeparam>
        /// <returns>True if the entity is archival enabled, false otherwise.</returns>
        bool IsArchivalEnabled<T>() where T : class;

        /// <summary>
        /// Gets the archival configuration for an entity type.
        /// </summary>
        /// <typeparam name="T">The type of entity.</typeparam>
        /// <returns>The ArchivalAttribute configuration, or null if not configured.</returns>
        ArchivalAttribute? GetArchivalAttribute<T>() where T : class;
    }
}
