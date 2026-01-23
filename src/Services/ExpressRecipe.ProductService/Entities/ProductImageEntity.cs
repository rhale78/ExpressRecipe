using System;
using HighSpeedDAL.Core.Attributes;
using MessagePack;

namespace ExpressRecipe.ProductService.Entities;

[DalEntity]
[Table("ProductImage", PrimaryKeyType = PrimaryKeyType.Guid)]
[Cache(CacheStrategy.TwoLayer, MaxSize = 50000, ExpirationSeconds = 900)]
[InMemoryTable(
    FlushIntervalSeconds = 30,
    MaxRowCount = 200000,
    EnforceConstraints = false,    // Reference data constraints at DB
    ValidateOnWrite = false,       // Read-heavy image metadata
    TrackOperations = false)]      // No operation history needed
[AutoAudit]
[SoftDelete]
// Named queries for efficient lookups
[NamedQuery("ByProductId", nameof(ProductId))]
[NamedQuery("ByProductIds", nameof(ProductId))]  // For batch loading - supports IEnumerable<Guid>
[NamedQuery("ByProductIdAndPrimary", nameof(ProductId), nameof(IsPrimary))]
[MessagePackObject]
public partial class ProductImageEntity
{
    [Key(0)]
    [PrimaryKey]
    public Guid Id { get; set; }

    [Key(1)]
    [Index]
    public Guid ProductId { get; set; }

    [Key(2)]
    public string ImageType { get; set; } = string.Empty; // 'Front', 'Back', 'Side', 'Nutrition', 'Ingredients', 'Other'

    [Key(3)]
    public string? ImageUrl { get; set; }  // Note: Add unique constraint via migration on (ProductId, ImageUrl)

    [Key(4)]
    public string? LocalFilePath { get; set; }

    [Key(5)]
    public string? FileName { get; set; }

    [Key(6)]
    public long? FileSize { get; set; }

    [Key(7)]
    public string? MimeType { get; set; }

    [Key(8)]
    public int? Width { get; set; }

    [Key(9)]
    public int? Height { get; set; }

    [Key(10)]
    public int DisplayOrder { get; set; } = 0;

    [Key(11)]
    [Index]
    public bool IsPrimary { get; set; } = false;

    [Key(12)]
    public bool IsUserUploaded { get; set; } = false;

    [Key(13)]
    public string? SourceSystem { get; set; }

    [Key(14)]
    public string? SourceId { get; set; }

}
