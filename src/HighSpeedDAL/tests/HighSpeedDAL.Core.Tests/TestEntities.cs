using HighSpeedDAL.Core.Attributes;

namespace HighSpeedDAL.Core.Tests.Models;

/// <summary>
/// Simple test entity for basic CRUD operations
/// </summary>
[Table("TestEntities")]
[Cache(CacheStrategy.Memory, ExpirationSeconds = 300)]
public class TestEntity
{
    [PrimaryKey]
    [Identity]
    public int Id { get; set; }

    [Column("EntityName")]
    [Index]
    public string Name { get; set; } = string.Empty;

    [Column]
    public string? Description { get; set; }

    [Column]
    [Index]
    public DateTime CreatedDate { get; set; }

    [Column]
    public bool IsActive { get; set; }

    // [Audit] - Audit is class-level only
    public DateTime LastModified { get; set; }

    // [Audit] - Audit is class-level only
    public string? ModifiedBy { get; set; }
}

/// <summary>
/// Entity with composite key for testing
/// </summary>
[Table("CompositeKeyEntities")]
public class CompositeKeyEntity
{
    [PrimaryKey(Order = 1)]
    public int TenantId { get; set; }

    [PrimaryKey(Order = 2)]
    public int EntityId { get; set; }

    [Column]
    public string Data { get; set; } = string.Empty;
}

/// <summary>
/// Reference table entity for testing
/// </summary>
[Table("StatusTypes")]
[ReferenceTable]
[Cache(CacheStrategy.TwoLayer)]
public class StatusType
{
    [PrimaryKey]
    public int StatusId { get; set; }

    [Column]
    [Index(IsUnique = true)]
    public string StatusCode { get; set; } = string.Empty;

    [Column]
    public string StatusName { get; set; } = string.Empty;

    [Column]
    public int DisplayOrder { get; set; }
}

/// <summary>
/// Entity using staging table for high-write scenarios
/// </summary>
[Table("HighVolumeData")]
[StagingTable(60)]
[Cache(CacheStrategy.None)]
public class HighVolumeEntity
{
    [PrimaryKey]
    [Identity]
    public long Id { get; set; }

    [Column]
    [Index]
    public DateTime EventTime { get; set; }

    [Column]
    public string EventType { get; set; } = string.Empty;

    [Column]
    public string Payload { get; set; } = string.Empty;

    [Column]
    public int ProcessingStatus { get; set; }
}

/// <summary>
/// Entity with audit fields for testing audit functionality
/// </summary>
[Table("AuditedEntities")]
[AutoAudit]
public class AuditedEntity
{
    [PrimaryKey]
    [Identity]
    public int Id { get; set; }

    [Column]
    public string Name { get; set; } = string.Empty;

    // [Audit] - Audit is class-level only
    public DateTime CreatedDate { get; set; }

    // [Audit] - Audit is class-level only
    public string CreatedBy { get; set; } = string.Empty;

    // [Audit] - Audit is class-level only
    public DateTime? ModifiedDate { get; set; }

    // [Audit] - Audit is class-level only
    public string? ModifiedBy { get; set; }
}

/// <summary>
/// Entity with soft delete for testing
/// </summary>
[Table("SoftDeleteEntities")]
[SoftDelete]
public class SoftDeleteEntity
{
    [PrimaryKey]
    [Identity]
    public int Id { get; set; }

    [Column]
    public string Name { get; set; } = string.Empty;

    [Column]
    public bool IsDeleted { get; set; }

    [Column]
    public DateTime? DeletedDate { get; set; }
}

/// <summary>
/// Entity with custom schema for testing
/// </summary>
[Table("CustomSchemaEntity", Schema = "dbo")]
public class CustomSchemaEntity
{
    [PrimaryKey]
    public Guid Id { get; set; }

    [Column]
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Entity with various column types for testing type mapping
/// </summary>
[Table("TypeTestEntities")]
public class TypeTestEntity
{
    [PrimaryKey]
    [Identity]
    public int Id { get; set; }

    [Column]
    public string StringValue { get; set; } = string.Empty;

    [Column]
    public int IntValue { get; set; }

    [Column]
    public long LongValue { get; set; }

    [Column]
    public decimal DecimalValue { get; set; }

    [Column]
    public double DoubleValue { get; set; }

    [Column]
    public DateTime DateTimeValue { get; set; }

    [Column]
    public bool BoolValue { get; set; }

    [Column]
    public Guid GuidValue { get; set; }

    [Column]
    public byte[] ByteArrayValue { get; set; } = Array.Empty<byte>();

    [Column]
    public int? NullableIntValue { get; set; }

    [Column]
    public DateTime? NullableDateTimeValue { get; set; }
}
