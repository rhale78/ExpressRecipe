using System;
using HighSpeedDAL.Core.Attributes;
using MessagePack;

namespace ExpressRecipe.ProductService.Entities;

/// <summary>
/// Ingredient entity for HighSpeedDAL with auto-generated CRUD operations
/// </summary>
[Table("Ingredient")]
[Cache(CacheStrategy.TwoLayer, MaxSize = 20000, ExpirationSeconds = 1800)] // 30 min memory, longer distributed
[DalEntity] // Triggers source generator to create IngredientEntityDal
[MessagePackObject]
public partial class IngredientEntity
{
    [Key(0)]
    public int Id { get; set; }

    [Key(1)]
    [Index(IsUnique = true)]
    public string Name { get; set; } = string.Empty;

    [Key(2)]
    public string? Description { get; set; }

    [Key(3)]
    [Index]
    public string? Category { get; set; }

    [Key(4)]
    public bool IsAllergen { get; set; } = false;

    [Key(5)]
    public string? AllergenType { get; set; }

    [Key(6)]
    [Index]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Key(7)]
    public DateTime? UpdatedAt { get; set; }

    [Key(8)]
    public bool IsDeleted { get; set; } = false;
}
