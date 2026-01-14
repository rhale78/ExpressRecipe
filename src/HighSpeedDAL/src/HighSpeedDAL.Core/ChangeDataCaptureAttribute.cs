using System;

namespace HighSpeedDAL.Core.Attributes;

/// <summary>
/// Marks an entity for Change Data Capture (CDC).
/// 
/// CDC tracks all changes (INSERT, UPDATE, DELETE) to entities for:
/// - Compliance auditing (SOX, HIPAA, GDPR)
/// - Event sourcing and CQRS patterns
/// - Data replication to other systems
/// - Point-in-time recovery
/// - Analytics and reporting
/// 
/// The CDC system captures before/after snapshots of data changes along with:
/// - Who made the change (user ID)
/// - When the change occurred (timestamp)
/// - What type of change (INSERT/UPDATE/DELETE)
/// - Transaction ID for grouping related changes
/// 
/// Example:
/// [ChangeDataCapture(RetentionDays = 90, CaptureFullSnapshot = true)]
/// [Audit]
/// public class CustomerOrder : SqlServerConnectionBase
/// {
///     public int Id { get; set; }
///     public decimal Amount { get; set; }
/// }
/// 
/// HighSpeedDAL Framework v0.1 - Phase 4
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class ChangeDataCaptureAttribute : Attribute
{
    /// <summary>
    /// Number of days to retain CDC records. Default is 90 days.
    /// Set to 0 for indefinite retention (not recommended for production).
    /// Set to 365+ for long-term compliance requirements.
    /// </summary>
    public int RetentionDays { get; set; } = 90;

    /// <summary>
    /// Whether to capture the full entity snapshot or only changed columns.
    /// Default is true (full snapshot) for simplicity and complete audit trail.
    /// Set to false to capture only changed columns (reduces storage).
    /// </summary>
    public bool CaptureFullSnapshot { get; set; } = true;

    /// <summary>
    /// Whether to capture schema changes to the table.
    /// Default is false. Set to true to track ADD COLUMN, DROP COLUMN, ALTER COLUMN.
    /// </summary>
    public bool CaptureSchemaChanges { get; set; } = false;

    /// <summary>
    /// Whether to enable CDC for this entity. Default is true.
    /// Set to false to temporarily disable CDC without removing the attribute.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Custom table name for storing CDC records.
    /// If not specified, defaults to "{EntityName}_CDC".
    /// </summary>
    public string? CustomCdcTableName { get; set; }

    /// <summary>
    /// Whether to partition the CDC table by date for better performance.
    /// Recommended for high-volume tables. Requires SQL Server Enterprise Edition.
    /// </summary>
    public bool UsePartitioning { get; set; } = false;

    /// <summary>
    /// Whether to compress CDC data (SQL Server Enterprise Edition only).
    /// Reduces storage by 50-80% for large CDC tables.
    /// </summary>
    public bool UseCompression { get; set; } = false;
}

/// <summary>
/// Represents the type of change captured by CDC.
/// </summary>
public enum CdcOperation
{
    /// <summary>
    /// A new record was inserted.
    /// </summary>
    Insert = 1,

    /// <summary>
    /// An existing record was updated.
    /// </summary>
    Update = 2,

    /// <summary>
    /// A record was deleted.
    /// </summary>
    Delete = 3,

    /// <summary>
    /// Schema of the table was changed.
    /// </summary>
    SchemaChange = 4
}

/// <summary>
/// Represents a CDC record capturing a single change.
/// </summary>
public sealed class CdcRecord
{
    /// <summary>
    /// Unique identifier for this CDC record.
    /// </summary>
    public long CdcId { get; set; }

    /// <summary>
    /// Type of operation (Insert, Update, Delete, SchemaChange).
    /// </summary>
    public CdcOperation Operation { get; set; }

    /// <summary>
    /// Primary key value of the changed record.
    /// Stored as string to support any PK type.
    /// </summary>
    public string PrimaryKeyValue { get; set; } = string.Empty;

    /// <summary>
    /// Table name where the change occurred.
    /// </summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>
    /// Data before the change (NULL for INSERT operations).
    /// Stored as JSON for flexibility.
    /// </summary>
    public string? DataBefore { get; set; }

    /// <summary>
    /// Data after the change (NULL for DELETE operations).
    /// Stored as JSON for flexibility.
    /// </summary>
    public string? DataAfter { get; set; }

    /// <summary>
    /// User who made the change.
    /// </summary>
    public string ChangedBy { get; set; } = string.Empty;

    /// <summary>
    /// When the change occurred (UTC).
    /// </summary>
    public DateTime ChangedAt { get; set; }

    /// <summary>
    /// Transaction ID for grouping related changes.
    /// Multiple changes in a single transaction share the same ID.
    /// </summary>
    public Guid TransactionId { get; set; }

    /// <summary>
    /// Application context or metadata.
    /// Can store request ID, session ID, IP address, etc.
    /// </summary>
    public string? Context { get; set; }
}
