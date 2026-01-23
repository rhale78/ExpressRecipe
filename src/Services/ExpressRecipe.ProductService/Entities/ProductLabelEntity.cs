using System;
using HighSpeedDAL.Core.Attributes;
using MessagePack;

namespace ExpressRecipe.ProductService.Entities;

/// <summary>
/// Represents a label associated with a product (e.g., "Organic", "Gluten-Free").
/// </summary>
[DalEntity]
[Table("ProductLabel", PrimaryKeyType = PrimaryKeyType.Guid)]
[AutoAudit]
[SoftDelete]
[NamedQuery("ByProductId", nameof(ProductId))]
[NamedQuery("ByLabelName", nameof(LabelName))]
[MessagePackObject]
public partial class ProductLabelEntity
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
    public string LabelName { get; set; } = string.Empty;
}
