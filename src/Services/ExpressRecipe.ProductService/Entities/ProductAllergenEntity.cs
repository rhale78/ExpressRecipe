using System;
using HighSpeedDAL.Core.Attributes;
using MessagePack;

namespace ExpressRecipe.ProductService.Entities;

[DalEntity]
[Table("ProductAllergen", PrimaryKeyType = PrimaryKeyType.Guid)]
[Cache(CacheStrategy.TwoLayer, MaxSize = 20000, ExpirationSeconds = 1800)]
[InMemoryTable(FlushIntervalSeconds = 30, MaxRowCount = 100000)]
[AutoAudit]
[SoftDelete]
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

    [Key(3)]
    public string? AllergenType { get; set; }

    [Key(4)]
    public string? Severity { get; set; }

    [Key(5)]
    public string? Notes { get; set; }

    [Key(6)]
    [Index]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Key(7)]
    public DateTime? UpdatedAt { get; set; }

    [Key(8)]
    public bool IsDeleted { get; set; } = false;
}
