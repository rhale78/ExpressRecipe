using System;

namespace HighSpeedDAL.DataManagement.Versioning
{
    /// <summary>
    /// Exception thrown when a version conflict is detected during an update operation.
    /// This indicates that another user or process has modified the entity since it was retrieved.
    /// </summary>
    public class VersionConflictException : Exception
    {
        /// <summary>
        /// Gets the type of entity that had the version conflict.
        /// </summary>
        public Type EntityType { get; }

        /// <summary>
        /// Gets the ID of the entity that had the version conflict.
        /// </summary>
        public object EntityId { get; }

        /// <summary>
        /// Gets the expected version value.
        /// </summary>
        public object? ExpectedVersion { get; }

        /// <summary>
        /// Gets the actual version value found in the database.
        /// </summary>
        public object? ActualVersion { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="VersionConflictException"/> class.
        /// </summary>
        /// <param name="entityType">The type of entity.</param>
        /// <param name="entityId">The ID of the entity.</param>
        /// <param name="expectedVersion">The expected version.</param>
        /// <param name="actualVersion">The actual version.</param>
        public VersionConflictException(
            Type entityType,
            object entityId,
            object? expectedVersion,
            object? actualVersion)
            : base(BuildMessage(entityType, entityId, expectedVersion, actualVersion))
        {
            EntityType = entityType;
            EntityId = entityId;
            ExpectedVersion = expectedVersion;
            ActualVersion = actualVersion;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="VersionConflictException"/> class
        /// with a custom message.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="entityType">The type of entity.</param>
        /// <param name="entityId">The ID of the entity.</param>
        /// <param name="expectedVersion">The expected version.</param>
        /// <param name="actualVersion">The actual version.</param>
        public VersionConflictException(
            string message,
            Type entityType,
            object entityId,
            object? expectedVersion,
            object? actualVersion)
            : base(message)
        {
            EntityType = entityType;
            EntityId = entityId;
            ExpectedVersion = expectedVersion;
            ActualVersion = actualVersion;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="VersionConflictException"/> class
        /// with a custom message and inner exception.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="innerException">The inner exception.</param>
        /// <param name="entityType">The type of entity.</param>
        /// <param name="entityId">The ID of the entity.</param>
        /// <param name="expectedVersion">The expected version.</param>
        /// <param name="actualVersion">The actual version.</param>
        public VersionConflictException(
            string message,
            Exception innerException,
            Type entityType,
            object entityId,
            object? expectedVersion,
            object? actualVersion)
            : base(message, innerException)
        {
            EntityType = entityType;
            EntityId = entityId;
            ExpectedVersion = expectedVersion;
            ActualVersion = actualVersion;
        }

        /// <summary>
        /// Builds the error message for the exception.
        /// </summary>
        /// <param name="entityType">The type of entity.</param>
        /// <param name="entityId">The ID of the entity.</param>
        /// <param name="expectedVersion">The expected version.</param>
        /// <param name="actualVersion">The actual version.</param>
        /// <returns>The formatted error message.</returns>
        private static string BuildMessage(
            Type entityType,
            object entityId,
            object? expectedVersion,
            object? actualVersion)
        {
            string expectedStr = FormatVersion(expectedVersion);
            string actualStr = FormatVersion(actualVersion);

            return $"Version conflict detected for {entityType.Name} with ID {entityId}. " +
                   $"Expected version: {expectedStr}, Actual version: {actualStr}. " +
                   $"The entity has been modified by another user or process.";
        }

        /// <summary>
        /// Formats a version value for display in the error message.
        /// </summary>
        /// <param name="version">The version value.</param>
        /// <returns>The formatted version string.</returns>
        private static string FormatVersion(object? version)
        {
            if (version == null)
            {
                return "<null>";
            }

            if (version is byte[] bytes)
            {
                return BitConverter.ToString(bytes);
            }

            return version.ToString() ?? "<unknown>";
        }
    }
}
