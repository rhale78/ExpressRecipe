using System;

namespace HighSpeedDAL.Core.Attributes;

/// <summary>
/// Specifies column-level configuration for an entity property
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class ColumnAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the database column name
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Alias for Name property (for test compatibility)
    /// </summary>
    public string? ColumnName => Name;

    /// <summary>
    /// Gets or sets the SQL data type
    /// </summary>
    public string? TypeName { get; set; }

    /// <summary>
    /// Gets or sets the maximum length for string columns
    /// </summary>
    public int MaxLength { get; set; }

    /// <summary>
    /// Gets or sets whether the column is indexed
    /// </summary>
    public bool IsIndexed { get; set; }

    /// <summary>
    /// Gets or sets whether the column has a unique constraint
    /// </summary>
    public bool IsUnique { get; set; }

    public ColumnAttribute()
    {
    }

    public ColumnAttribute(string name)
    {
        Name = name;
    }
}
