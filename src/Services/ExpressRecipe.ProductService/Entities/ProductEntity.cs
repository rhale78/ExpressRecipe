using System;
using HighSpeedDAL.Core.Attributes;
using MessagePack;

namespace ExpressRecipe.ProductService.Entities;

[DalEntity]
[Table("Product", PrimaryKeyType = PrimaryKeyType.Guid)]
[Cache(CacheStrategy.TwoLayer, MaxSize = 10000, ExpirationSeconds = 900)]
[InMemoryTable(FlushIntervalSeconds = 30, MaxRowCount = 100000)]
[AutoAudit]
[SoftDelete]
[MessagePackObject]
public partial class ProductEntity
{
    [Key(0)]
    [PrimaryKey]
    public Guid Id { get; set; }

    [Key(1)]
    [Index]
    public string Name { get; set; } = string.Empty;

    [Key(2)]
    [Index]
    public string? Brand { get; set; }

    [Key(3)]
    [Index(IsUnique = true)]
    public string? Barcode { get; set; }

    [Key(4)]
    public string? BarcodeType { get; set; }

    [Key(5)]
    public string? Description { get; set; }

    [Key(6)]
    [Index]
    public string? Category { get; set; }

    [Key(7)]
    public string? ServingSize { get; set; }

    [Key(8)]
    public string? ServingUnit { get; set; }

    [Key(9)]
    public string? ImageUrl { get; set; }

    [Key(10)]
    [Index]
    public string ApprovalStatus { get; set; } = "Pending";

    [Key(11)]
    public Guid? ApprovedBy { get; set; }

    [Key(12)]
    public DateTime? ApprovedAt { get; set; }

    [Key(13)]
    public string? RejectionReason { get; set; }

    [Key(14)]
    [Index]
    public Guid? SubmittedBy { get; set; }

    [Key(15)]
    [Index]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Key(16)]
    public DateTime? UpdatedAt { get; set; }

    [Key(17)]
    public bool IsDeleted { get; set; } = false;
}
