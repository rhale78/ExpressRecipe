using System;
using HighSpeedDAL.Core.Attributes;
using MessagePack;

namespace ExpressRecipe.ProductService.Entities
{
    /// <summary>
    /// Represents a link to an external system for a given product (e.g., a link to the product on OpenFoodFacts).
    /// </summary>
    [DalEntity]
    [Table("ProductExternalLink", PrimaryKeyType = PrimaryKeyType.Guid)]
    [AutoAudit]
    [SoftDelete]
    [NamedQuery("ByProductId", nameof(ProductId))]
    [NamedQuery("BySourceAndExternalId", nameof(Source), nameof(ExternalId), IsSingle = true)]
    [MessagePackObject]
    public partial class ProductExternalLinkEntity
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
        public string Source { get; set; } = string.Empty;

        [Key(3)]
        [Index]
        [Column(TypeName = "NVARCHAR(450)")]
        public string ExternalId { get; set; } = string.Empty;
    }
}
