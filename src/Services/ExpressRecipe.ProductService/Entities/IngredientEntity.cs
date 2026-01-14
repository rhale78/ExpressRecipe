using System;
using HighSpeedDAL.Core.Attributes;
using MessagePack;

namespace ExpressRecipe.ProductService.Entities;

/// <summary>
/// Ingredient entity for HighSpeedDAL with auto-generated CRUD operations
/// Framework will auto-generate: public Guid Id { get; set; }
/// </summary>
[Table("Ingredient", PrimaryKeyType = PrimaryKeyType.Guid)]
[Cache(CacheStrategy.TwoLayer, MaxSize = 20000, ExpirationSeconds = 1800)] // 30 min memory, longer distributed
[DalEntity] // Triggers source generator to create IngredientEntityDal
[MessagePackObject]
public partial class IngredientEntity
{
    [Key(0)]
    [Index(IsUnique = true)]
    public string Name { get; set; } = string.Empty;

    [Key(1)]
    public string? Description { get; set; }

    [Key(2)]
    [Index]
    public string? Category { get; set; }

    [Key(3)]
    public bool IsAllergen { get; set; } = false;

    [Key(4)]
    public string? AllergenType { get; set; }

    [Key(5)]
    [Index]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Key(6)]
    public DateTime? UpdatedAt { get; set; }

    [Key(7)]
    public bool IsDeleted { get; set; } = false;
}
