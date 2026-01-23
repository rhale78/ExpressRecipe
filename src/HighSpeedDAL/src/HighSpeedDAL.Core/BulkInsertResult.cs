using System;
using System.Collections.Generic;

namespace HighSpeedDAL.Core;

/// <summary>
/// Result of a bulk insert operation, including any entities that failed due to duplicate keys
/// </summary>
/// <typeparam name="TEntity">The entity type</typeparam>
public class BulkInsertResult<TEntity> where TEntity : class
{
    /// <summary>
    /// Number of entities successfully inserted
    /// </summary>
    public int InsertedCount { get; set; }

    /// <summary>
    /// Entities that failed to insert due to duplicate key violations.
    /// These can be processed for updates by the caller.
    /// </summary>
    public List<TEntity> DuplicateEntities { get; set; } = new();

    /// <summary>
    /// Total entities that were attempted to be inserted
    /// </summary>
    public int TotalAttempted { get; set; }

    /// <summary>
    /// Whether the operation completed successfully (with or without duplicates)
    /// </summary>
    public bool Success { get; set; } = true;

    /// <summary>
    /// Any error message if the operation failed for reasons other than duplicates
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Whether there were any duplicate entities found
    /// </summary>
    public bool HasDuplicates => DuplicateEntities.Count > 0;
}
