using System;
using HighSpeedDAL.Core.Attributes;
using MessagePack;

namespace ExpressRecipe.ProductService.Entities;

/// <summary>
/// Represents a key-value metadata pair associated with a product.
/// </summary>
[DalEntity]
[Table("ProductMetadata", PrimaryKeyType = PrimaryKeyType.Guid)]
[AutoAudit]
[SoftDelete]
[NamedQuery("ByProductId", nameof(ProductId))]
[NamedQuery("ByProductIdAndKey", nameof(ProductId), nameof(MetaKey), IsSingle = true)]
[MessagePackObject]
public partial class ProductMetadataEntity
{
    [Key(0)]
    [PrimaryKey]
    public Guid Id { get; set; }

    [Key(1)]
    [Index]
    public Guid ProductId { get; set; }

    [Key(2)]
    [Index]
    [Column(TypeName = "NVARCHAR(450)")]
    public string MetaKey { get; set; } = string.Empty;

    [Key(3)]
    public string MetaValue { get; set; } = string.Empty;
}
