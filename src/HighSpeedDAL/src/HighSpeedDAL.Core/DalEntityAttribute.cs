using System;

namespace HighSpeedDAL.Core.Attributes;

/// <summary>
/// Marks a class as a DAL entity that should have CRUD operations generated
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class DalEntityAttribute : Attribute
{
    /// <summary>
    /// Optional custom table name. If not specified, uses class name.
    /// </summary>
    public string? TableName { get; set; }

    /// <summary>
    /// If true, creates the table if it doesn't exist
    /// </summary>
    public bool AutoCreate { get; set; } = true;

    /// <summary>
    /// If true, automatically updates table schema when entity definition changes
    /// </summary>
    public bool AutoMigrate { get; set; } = true;
}
