using System;

namespace HighSpeedDAL.Core.Attributes;

/// <summary>
/// Marks a table for automatic audit tracking.
/// When applied, framework automatically adds these properties if not already present:
/// - DateTime CreatedDate { get; set; }
/// - string CreatedBy { get; set; }
/// - DateTime ModifiedDate { get; set; }
/// - string ModifiedBy { get; set; }
/// 
/// Framework automatically populates these fields on insert/update operations.
/// 
/// HighSpeedDAL Framework v0.1
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class AuditAttribute : Attribute
{
    /// <summary>
    /// If true, maintains history of all changes in a separate audit history table.
    /// Default: false
    /// </summary>
    public bool TrackHistory { get; set; }

    /// <summary>
    /// Name of the audit history table (if TrackHistory is true).
    /// If not specified, defaults to "{TableName}_AuditHistory"
    /// </summary>
    public string HistoryTableName { get; set; }

    public AuditAttribute()
    {
        TrackHistory = false;
        HistoryTableName = string.Empty;
    }
}
