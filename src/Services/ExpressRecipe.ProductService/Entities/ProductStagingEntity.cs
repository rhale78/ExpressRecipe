using System;
using HighSpeedDAL.Core.Attributes;
using MessagePack;

namespace ExpressRecipe.ProductService.Entities;

[DalEntity]
[Table("ProductStaging", PrimaryKeyType = PrimaryKeyType.Guid)]
[Cache(CacheStrategy.Memory, MaxSize = 10000, ExpirationSeconds = 300)]
[InMemoryTable(
    FlushIntervalSeconds = 30,
    MaxRowCount = 50000,
    EnforceConstraints = false,    // Staging table is temporary
    ValidateOnWrite = false,       // Data already validated before staging
    TrackOperations = false)]      // Volatile processing buffer
[AutoAudit]
[SoftDelete]
// Named queries for efficient lookups
[NamedQuery("ByProcessingStatus", nameof(ProcessingStatus))]
[NamedQuery("ByBarcode", nameof(Barcode), IsSingle = true)]
[NamedQuery("ByExternalId", nameof(ExternalId), IsSingle = true)]
[MessagePackObject]
public partial class ProductStagingEntity
{
    [Key(0)]
    [PrimaryKey]
    public Guid Id { get; set; }

    [Key(1)]
    [Index(IsUnique = true)]
    [Column(TypeName = "NVARCHAR(450)")]
    public string ExternalId { get; set; } = string.Empty;

    [Key(2)]
    [Index]
    [Column(TypeName = "NVARCHAR(450)")]
    public string? Barcode { get; set; }

    [Key(3)]
    public string? ProductName { get; set; }

    [Key(4)]
    public string? GenericName { get; set; }

    [Key(5)]
    public string? Brands { get; set; }

    [Key(6)]
    public string? IngredientsText { get; set; }

    [Key(7)]
    public string? IngredientsTextEn { get; set; }

    [Key(8)]
    public string? Allergens { get; set; }

    [Key(9)]
    public string? AllergensHierarchy { get; set; }

    [Key(10)]
    public string? Categories { get; set; }

    [Key(11)]
    public string? CategoriesHierarchy { get; set; }

    [Key(12)]
    public string? NutritionData { get; set; }

    [Key(13)]
    public string? ImageUrl { get; set; }

    [Key(14)]
    public string? ImageSmallUrl { get; set; }

    [Key(15)]
    public string? Lang { get; set; }

    [Key(16)]
    public string? Countries { get; set; }

    [Key(17)]
    public string? NutriScore { get; set; }

    [Key(18)]
    public int? NovaGroup { get; set; }

    [Key(19)]
    public string? EcoScore { get; set; }

    [Key(20)]
    public string? RawJson { get; set; }

    [Key(21)]
    [Index]
    [Column(TypeName = "NVARCHAR(450)")]
    public string ProcessingStatus { get; set; } = "Pending"; // Pending, Processing, Completed, Failed

    [Key(22)]
    public DateTime? ProcessedAt { get; set; }

    [Key(23)]
    public string? ProcessingError { get; set; }

    [Key(24)]
    public int ProcessingAttempts { get; set; } = 0;

    [Key(27)]
    public bool IsDeleted { get; set; } = false;
}
