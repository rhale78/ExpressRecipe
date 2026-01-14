using System;
using HighSpeedDAL.Core.Attributes;
using MessagePack;

namespace ExpressRecipe.ProductService.Entities;

/// <summary>
/// Product entity for HighSpeedDAL with auto-generated CRUD operations
/// Framework will auto-generate: public Guid Id { get; set; }
/// </summary>
[Table("Product", PrimaryKeyType = PrimaryKeyType.Guid)]
[Cache(CacheStrategy.TwoLayer, MaxSize = 10000, ExpirationSeconds = 900)] // 15 min memory, longer distributed
[DalEntity] // Triggers source generator to create ProductEntityDal
[MessagePackObject]
public partial class ProductEntity
{
    [Key(0)]
    [Index]
    public string Name { get; set; } = string.Empty;

    [Key(1)]
    [Index]
    public string? Brand { get; set; }

    [Key(2)]
    [Index(IsUnique = true)]
    public string? Barcode { get; set; }

    [Key(3)]
    public string? BarcodeType { get; set; }

    [Key(4)]
    public string? Description { get; set; }

    [Key(5)]
    [Index]
    public string? Category { get; set; }

    [Key(6)]
    public string? ServingSize { get; set; }

    [Key(7)]
    public string? ServingUnit { get; set; }

    [Key(8)]
    public string? ImageUrl { get; set; }

    [Key(9)]
    [Index]
    public string ApprovalStatus { get; set; } = "Pending";

    [Key(10)]
    public Guid? ApprovedBy { get; set; }

    [Key(11)]
    public DateTime? ApprovedAt { get; set; }

    [Key(12)]
    public string? RejectionReason { get; set; }

    [Key(13)]
    [Index]
    public Guid? SubmittedBy { get; set; }

    [Key(14)]
    [Index]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Key(15)]
    public DateTime? UpdatedAt { get; set; }

    [Key(16)]
    public bool IsDeleted { get; set; } = false;
}
