using System;
using HighSpeedDAL.Core.Attributes;
using MessagePack;

namespace ExpressRecipe.ProductService.Entities
{
    [DalEntity]
    [Table("Product", PrimaryKeyType = PrimaryKeyType.Guid)]
    [Cache(CacheStrategy.TwoLayer, MaxSize = 50000, ExpirationSeconds = 3600)]
    [InMemoryTable(
        FlushIntervalSeconds = 30,
        MaxRowCount = 1000000,
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
    [NamedQuery("ByBarcode", nameof(Barcode), IsSingle = true)]
    [NamedQuery("ByBrand", nameof(Brand))]
    [NamedQuery("ByCategory", nameof(Category))]
    [NamedQuery("ByApprovalStatus", nameof(ApprovalStatus))]
    [NamedQuery("ByBrandAndCategory", nameof(Brand), nameof(Category))]
    [NamedQuery("ByCategoryAndApprovalStatus", nameof(Category), nameof(ApprovalStatus))]
    [MessagePackObject]
    public partial class ProductEntity
    {
        [Key(0)]
        [PrimaryKey]
        public Guid Id { get; set; }

        [Key(1)]
        [Index]
        [Column(TypeName = "NVARCHAR(450)")]
        public string Name { get; set; } = string.Empty;

        [Key(2)]
        [Index]
        [Column(TypeName = "NVARCHAR(450)")]
        public string? Brand { get; set; }

        [Key(3)]
        [Index(IsUnique = true)]
        [Column(TypeName = "NVARCHAR(450)")]
        public string? Barcode { get; set; }

        [Key(4)]
        public string? BarcodeType { get; set; }

        [Key(5)]
        public string? Description { get; set; }

        [Key(6)]
        [Index]
        [Column(TypeName = "NVARCHAR(450)")]
        public string? Category { get; set; }

        [Key(7)]
        public string? ServingSize { get; set; }

        [Key(8)]
        public string? ServingUnit { get; set; }

        [Key(9)]
        public string? ImageUrl { get; set; }

        [Key(10)]
        [Index]
        [Column(TypeName = "NVARCHAR(450)")]
        public string ApprovalStatus { get; set; } = "Pending";

        [Key(11)]
        public string? ApprovedBy { get; set; }

        [Key(12)]
        public DateTime? ApprovedAt { get; set; }

        [Key(13)]
        public string? RejectionReason { get; set; }

        [Key(14)]
        [Index]
        public string? SubmittedBy { get; set; }

        [Key(15)]
        public string? ExternalId { get; set; }

        [Key(16)]
        public string? ExternalSource { get; set; }
    }
}
