using System;
using HighSpeedDAL.Core.Attributes;
using MessagePack;

namespace ExpressRecipe.ProductService.Entities;

/// <summary>
/// Represents an allergen associated with a product.
/// Note: This is distinct from the main Allergen reference table. This links a product to an allergen by name.
/// </summary>
[DalEntity]
[Table("ProductAllergen", PrimaryKeyType = PrimaryKeyType.Guid)]
[AutoAudit]
[MessagePackObject]
public partial class ProductAllergenEntity
{
    [Key(0)]
    [PrimaryKey]
    public Guid Id { get; set; }

    [Key(1)]
    [Index]
    public Guid ProductId { get; set; }

    [Key(2)]
    [Index]
    public string AllergenName { get; set; } = string.Empty;
}