using System;

namespace HighSpeedDAL.Core.Attributes;

/// <summary>
/// Specifies the database table name for an entity.
/// 
/// OPTIONAL: If not specified, table name defaults to pluralized class name (Product → Products)
/// 
/// Examples:
/// - [Table] on class Product → creates "Products" table
/// - [Table("CustomName")] → creates "CustomName" table
/// - [Table("Products", Schema = "dbo")] → creates "dbo.Products" table
/// 
/// HighSpeedDAL Framework v0.1
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class TableAttribute : Attribute
{
    /// <summary>
    /// The database table name.
    /// If null or empty, will be auto-generated from class name using pluralization.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Alias for Name property (for test compatibility)
    /// </summary>
    public string? TableName => Name;

    /// <summary>
    /// Optional schema name (e.g., "dbo", "sales", "inventory")
    /// If not specified, uses database default schema
    /// </summary>
    public string? Schema { get; set; }

    /// <summary>
    /// Default constructor - table name will be pluralized class name
    /// </summary>
    public TableAttribute()
    {
        Name = null;
        Schema = null;
    }

    /// <summary>
    /// Constructor with explicit table name override
    /// </summary>
    /// <param name="name">Custom table name (overrides pluralization)</param>
    public TableAttribute(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Table name cannot be null or empty when explicitly specified", nameof(name));
        }

        Name = name;
        Schema = null;
    }
}
