using System;
using HighSpeedDAL.Core.Attributes;
using MessagePack;

namespace ExpressRecipe.ProductService.Entities
{
    [DalEntity]
    [Table("Ingredient", PrimaryKeyType = PrimaryKeyType.Guid)]
    [Cache(CacheStrategy.TwoLayer, MaxSize = 20000, ExpirationSeconds = 1800)]
    [InMemoryTable(
        FlushIntervalSeconds = 30,
        MaxRowCount = 100000,
        EnforceConstraints = true,
        ValidateOnWrite = true,
        TrackOperations = true)]
    [StagingTable(
        SyncIntervalSeconds = 60,
        UseTransaction = true)]
    [AutoAudit(
        TrackCreated = true,
        TrackModified = true)]
    [SoftDelete] // Use defaults: IsDeleted, DeletedDate, DeletedBy
    [NamedQuery("ByCategory", nameof(Category))]
    [NamedQuery("ByName", nameof(Name), IsSingle = true)]
    [NamedQuery("ByNames", nameof(Name))]
    [NamedQuery("ByIsCommonAllergen", nameof(IsCommonAllergen))]
    [NamedQuery("ByCategoryAndAllergen", nameof(Category), nameof(IsCommonAllergen))]
    [MessagePackObject]
    public partial class IngredientEntity
    {
        [Key(0)]
        [PrimaryKey]
        public Guid Id { get; set; }

        [Key(1)]
        [Index(IsUnique = true)]
        [Column(TypeName = "NVARCHAR(450)")]
        public string Name { get; set; } = string.Empty;

        [Key(2)]
        public string? AlternativeNames { get; set; }

        [Key(3)]
        public string? Description { get; set; }

        [Key(4)]
        [Index]
        [Column(TypeName = "NVARCHAR(450)")]
        public string? Category { get; set; }

        [Key(5)]
        public bool IsCommonAllergen { get; set; } = false;
    }
}