using System;
using HighSpeedDAL.Core.Attributes;
using HighSpeedDAL.Core.Interfaces;
using MessagePack;

namespace ExpressRecipe.ProductService.Entities
{
    [DalEntity]
    [Table("ProductStaging", PrimaryKeyType = PrimaryKeyType.Guid)]
    [Cache(CacheStrategy.Memory, MaxSize = 10000, ExpirationSeconds = 300)]
    [InMemoryTable(
        FlushIntervalSeconds = 15,     // Faster flush for staging data
        MaxRowCount = 50000,
        EnforceConstraints = false,
        ValidateOnWrite = false,
        TrackOperations = false)]
    [StagingTable(
        SyncIntervalSeconds = 30,      // Sync frequently
        UseTransaction = true)]
    [AutoAudit(
        TrackCreated = true,
        TrackModified = true)]
    [SoftDelete(
        DeletedColumn = "IsDeleted",
        DeletedDateColumn = "DeletedDate")]
    [NamedQuery("ByProcessingStatus", nameof(ProcessingStatus))]
    [NamedQuery("ByBarcode", nameof(Barcode), IsSingle = true)]
    [NamedQuery("ByExternalId", nameof(ExternalId), IsSingle = true)]
    [MessagePackObject]
    public partial class ProductStagingEntity : IAuditableEntity, ISoftDeleteEntity
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
        public string ProcessingStatus { get; set; } = "Pending";

        [Key(22)]
        public DateTime? ProcessedAt { get; set; }

        [Key(23)]
        public string? ProcessingError { get; set; }

        [Key(24)]
        public int ProcessingAttempts { get; set; } = 0;

        // IAuditableEntity & ISoftDeleteEntity Explicit Implementation
        // Explicitly defining these properties ensures they exist for the interface
        // and overrides source generator behavior to prevent type mismatches.

        [Key(25)]
        public DateTime CreatedDate { get; set; }

        [Key(26)]
        public string? CreatedBy { get; set; }

        [Key(27)]
        public DateTime? ModifiedDate { get; set; }

        [Key(28)]
        public string? ModifiedBy { get; set; }

        [Key(29)]
        public bool IsDeleted { get; set; }

        [Key(30)]
        public DateTime? DeletedDate { get; set; }

        [Key(31)]
        public string? DeletedBy { get; set; }
    }
}
