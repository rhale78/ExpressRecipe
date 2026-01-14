using System;

namespace HighSpeedDAL.Core.Attributes;

/// <summary>
/// Specifies the database table name for an entity and primary key configuration.
/// 
/// OPTIONAL: If not specified, table name defaults to pluralized class name (Product → Products)
/// 
/// Primary Key Behavior:
/// - By default, framework auto-generates "public int Id { get; set; }" if no Id property exists
/// - Set PrimaryKeyType = PrimaryKeyType.Guid to auto-generate Guid instead
/// - If entity has Id property or [PrimaryKey] attribute, PrimaryKeyType is ignored
/// 
/// Examples:
/// - [Table] on class Product → creates "Products" table with int Id
/// - [Table(PrimaryKeyType = PrimaryKeyType.Guid)] → creates table with Guid Id
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
    /// Specifies the primary key data type when framework auto-generates the Id property.
    /// Only applies if entity does not have an existing Id property or [PrimaryKey] attribute.
    /// Default: PrimaryKeyType.Int (auto-increment integer)
    /// Set to PrimaryKeyType.Guid for distributed systems or offline-first scenarios.
    /// </summary>
    public PrimaryKeyType PrimaryKeyType { get; set; } = PrimaryKeyType.Int;

    /// <summary>
    /// Default constructor - table name will be pluralized class name, int Id auto-generated
    /// </summary>
    public TableAttribute()
    {
        Name = null;
        Schema = null;
        PrimaryKeyType = PrimaryKeyType.Int;
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
        PrimaryKeyType = PrimaryKeyType.Int;
    }
}
