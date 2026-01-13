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
    /// If true, drops and recreates the table on application startup (dev/test only)
    /// </summary>
    public bool DropOnStartup { get; set; }

    /// <summary>
    /// If true, creates the table if it doesn't exist
    /// </summary>
    public bool AutoCreate { get; set; } = true;

    /// <summary>
    /// If true, automatically updates table schema when entity definition changes
    /// </summary>
    public bool AutoMigrate { get; set; } = true;
}

/// <summary>
/// Marks a class as a reference table with pre-loaded data
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ReferenceTableAttribute : Attribute
{
    /// <summary>
    /// Path to CSV file containing reference data to load
    /// </summary>
    public string? CsvDataPath { get; set; }

    /// <summary>
    /// If true, loads data from static properties/methods in the class
    /// </summary>
    public bool LoadFromCode { get; set; }
}

/// <summary>
/// Marks a property as the primary key. If not specified, auto-creates an Id property.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class PrimaryKeyAttribute : Attribute
{
    /// <summary>
    /// If true, the key is auto-incrementing
    /// </summary>
    public bool AutoIncrement { get; set; } = true;
}

/// <summary>
/// Specifies custom SQL type for a property
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class SqlTypeAttribute : Attribute
{
    public string SqlType { get; }

    public SqlTypeAttribute(string sqlType)
    {
        SqlType = sqlType;
    }
}

/// <summary>
/// Marks a property to be indexed
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class IndexedAttribute : Attribute
{
    /// <summary>
    /// If true, creates a unique index
    /// </summary>
    public bool Unique { get; set; }

    /// <summary>
    /// Optional index name
    /// </summary>
    public string? IndexName { get; set; }
}

/// <summary>
/// Enables caching for this entity
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class CachedAttribute : Attribute
{
    /// <summary>
    /// Maximum number of items to cache. 0 = unlimited.
    /// </summary>
    public int MaxSize { get; set; }

    /// <summary>
    /// Cache expiration in seconds. 0 = no expiration.
    /// </summary>
    public int ExpirationSeconds { get; set; }

    /// <summary>
    /// Cache strategy: LocalMemory, TwoLayer, Distributed, Hybrid
    /// </summary>
    public CacheStrategy Strategy { get; set; } = CacheStrategy.LocalMemory;

    /// <summary>
    /// If true, preloads all data into cache on startup
    /// </summary>
    public bool PreloadOnStartup { get; set; }
}

/// <summary>
/// Cache strategy options
/// </summary>
public enum CacheStrategy
{
    /// <summary>
    /// Simple in-memory cache using ConcurrentDictionary
    /// </summary>
    LocalMemory,

    /// <summary>
    /// Two-layer cache: lock-free dictionary + ConcurrentDictionary
    /// </summary>
    TwoLayer,

    /// <summary>
    /// Distributed cache (Redis, etc.)
    /// </summary>
    Distributed,

    /// <summary>
    /// .NET 9 HybridCache (in-memory + distributed)
    /// </summary>
    Hybrid
}

/// <summary>
/// Enables audit columns (CreatedBy, CreatedDate, ModifiedBy, ModifiedDate)
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class AuditableAttribute : Attribute
{
}

/// <summary>
/// Enables row versioning for optimistic concurrency
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class RowVersionAttribute : Attribute
{
    /// <summary>
    /// If true, maintains version history in a separate table
    /// </summary>
    public bool TrackHistory { get; set; }
}

/// <summary>
/// Enables soft delete (IsDeleted flag instead of actual deletion)
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class SoftDeleteAttribute : Attribute
{
}

/// <summary>
/// Enables staging table for high-write scenarios
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class StagingTableAttribute : Attribute
{
    /// <summary>
    /// Merge interval in seconds
    /// </summary>
    public int MergeIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Minimum batch size before triggering merge
    /// </summary>
    public int MinBatchSize { get; set; } = 100;
}

/// <summary>
/// Enables automatic purging of old records
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class AutoPurgeAttribute : Attribute
{
    /// <summary>
    /// Age in days after which records are purged
    /// </summary>
    public int RetentionDays { get; set; }

    /// <summary>
    /// Property name to use for date comparison (defaults to CreatedDate if auditable)
    /// </summary>
    public string? DatePropertyName { get; set; }
}

/// <summary>
/// Excludes a property from database mapping
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class NotMappedAttribute : Attribute
{
}

/// <summary>
/// Specifies maximum length for string properties
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class MaxLengthAttribute : Attribute
{
    public int Length { get; }

    public MaxLengthAttribute(int length)
    {
        Length = length;
    }
}

/// <summary>
/// Marks a navigation property for one-to-one relationship
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class OneToOneAttribute : Attribute
{
    /// <summary>
    /// Foreign key property name
    /// </summary>
    public string? ForeignKeyProperty { get; set; }
}

/// <summary>
/// Marks a navigation property for one-to-many relationship
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class OneToManyAttribute : Attribute
{
    /// <summary>
    /// Foreign key property name in the related entity
    /// </summary>
    public string? ForeignKeyProperty { get; set; }
}

/// <summary>
/// Marks a navigation property for many-to-many relationship
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class ManyToManyAttribute : Attribute
{
    /// <summary>
    /// Junction table name. If not specified, auto-generates from entity names.
    /// </summary>
    public string? JunctionTableName { get; set; }
}
