using System.ComponentModel.DataAnnotations;

namespace ExpressRecipe.Shared.DTOs.Product;

public class StoreDto
{
    public Guid Id { get; set; }
    public string ChainName { get; set; } = string.Empty;
    public string? StoreName { get; set; }
    public string? StoreNumber { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
    public string? Country { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string? StoreHours { get; set; } // JSON
    public bool HasPharmacy { get; set; }
    public bool HasDeli { get; set; }
    public bool HasBakery { get; set; }
    public bool AcceptsManufacturerCoupons { get; set; }
    public bool AllowsCouponDoubling { get; set; }
    public decimal? CouponDoublingLimit { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateStoreRequest
{
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string ChainName { get; set; } = string.Empty;

    [StringLength(200)]
    public string? StoreName { get; set; }

    [StringLength(50)]
    public string? StoreNumber { get; set; }

    [StringLength(500)]
    public string? Address { get; set; }

    [StringLength(100)]
    public string? City { get; set; }

    [StringLength(50)]
    public string? State { get; set; }

    [StringLength(20)]
    public string? ZipCode { get; set; }

    [StringLength(100)]
    public string? Country { get; set; }

    [Range(-90, 90)]
    public decimal? Latitude { get; set; }

    [Range(-180, 180)]
    public decimal? Longitude { get; set; }

    [Phone]
    [StringLength(20)]
    public string? Phone { get; set; }

    [EmailAddress]
    [StringLength(200)]
    public string? Email { get; set; }

    [Url]
    [StringLength(500)]
    public string? Website { get; set; }

    public string? StoreHours { get; set; }
    public bool HasPharmacy { get; set; }
    public bool HasDeli { get; set; }
    public bool HasBakery { get; set; }
    public bool AcceptsManufacturerCoupons { get; set; } = true;
    public bool AllowsCouponDoubling { get; set; }

    [Range(0, 100)]
    public decimal? CouponDoublingLimit { get; set; }
}

public class UpdateStoreRequest : CreateStoreRequest
{
    public bool IsActive { get; set; } = true;
}

public class StoreSearchRequest
{
    public string? SearchTerm { get; set; }
    public string? ChainName { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public double? RadiusMiles { get; set; }
    public bool? HasPharmacy { get; set; }
    public bool? HasDeli { get; set; }
    public bool? HasBakery { get; set; }
    public bool? AllowsCouponDoubling { get; set; }
    public bool OnlyActive { get; set; } = true;
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class ProductStorePriceDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string? ProductName { get; set; }
    public Guid StoreId { get; set; }
    public string? StoreName { get; set; }
    public decimal Price { get; set; }
    public decimal? SalePrice { get; set; }
    public DateTime? SaleStartDate { get; set; }
    public DateTime? SaleEndDate { get; set; }
    public string? UnitSize { get; set; }
    public decimal? PricePerUnit { get; set; }
    public string? StandardUnit { get; set; }
    public bool IsOnClearance { get; set; }
    public bool InStock { get; set; }
    public Guid SubmittedBy { get; set; }
    public Guid? VerifiedBy { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public int VerificationCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class ReportPriceRequest
{
    [Required]
    public Guid ProductId { get; set; }

    [Required]
    public Guid StoreId { get; set; }

    [Required]
    [Range(0.01, 999999.99)]
    public decimal Price { get; set; }

    [Range(0.01, 999999.99)]
    public decimal? SalePrice { get; set; }

    public DateTime? SaleStartDate { get; set; }
    public DateTime? SaleEndDate { get; set; }

    [StringLength(100)]
    public string? UnitSize { get; set; }

    [Range(0.01, 999999.99)]
    public decimal? PricePerUnit { get; set; }

    [StringLength(50)]
    public string? StandardUnit { get; set; }

    public bool IsOnClearance { get; set; }
    public bool InStock { get; set; } = true;
}

public class VerifyPriceRequest
{
    [Required]
    public bool IsAccurate { get; set; }

    [Range(0.01, 999999.99)]
    public decimal? ReportedPrice { get; set; }
}

public class CouponDto
{
    public Guid Id { get; set; }
    public string? Code { get; set; }
    public string Description { get; set; } = string.Empty;
    public string CouponType { get; set; } = string.Empty;
    public string DiscountType { get; set; } = string.Empty;
    public decimal? DiscountAmount { get; set; }
    public decimal? MinimumPurchaseAmount { get; set; }
    public int? MinimumQuantity { get; set; }
    public int? MaximumQuantity { get; set; }
    public int? MaxUsesPerUser { get; set; }
    public Guid? ProductId { get; set; }
    public string? ProductName { get; set; }
    public Guid? StoreId { get; set; }
    public string? StoreName { get; set; }
    public string? ManufacturerName { get; set; }
    public string? ImageUrl { get; set; }
    public string? SourceUrl { get; set; }
    public bool CanBeDoubled { get; set; }
    public bool CanBeCombined { get; set; }
    public bool RequiresLoyaltyCard { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public bool IsActive { get; set; }
    public Guid SubmittedBy { get; set; }
    public bool IsApproved { get; set; }
    public Guid? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateCouponRequest
{
    [StringLength(100)]
    public string? Code { get; set; }

    [Required]
    [StringLength(500, MinimumLength = 1)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string CouponType { get; set; } = string.Empty; // Manufacturer, Store, Digital, Printable, MailIn

    [Required]
    [StringLength(50)]
    public string DiscountType { get; set; } = string.Empty; // FixedAmount, Percentage, BOGO, BuyXGetY

    [Range(0.01, 999999.99)]
    public decimal? DiscountAmount { get; set; }

    [Range(0.01, 999999.99)]
    public decimal? MinimumPurchaseAmount { get; set; }

    [Range(1, 1000)]
    public int? MinimumQuantity { get; set; }

    [Range(1, 1000)]
    public int? MaximumQuantity { get; set; }

    [Range(1, 1000)]
    public int? MaxUsesPerUser { get; set; } = 1;

    public Guid? ProductId { get; set; }
    public Guid? StoreId { get; set; }

    [StringLength(200)]
    public string? ManufacturerName { get; set; }

    [Url]
    [StringLength(500)]
    public string? ImageUrl { get; set; }

    [Url]
    [StringLength(500)]
    public string? SourceUrl { get; set; }

    public bool CanBeDoubled { get; set; }
    public bool CanBeCombined { get; set; } = true;
    public bool RequiresLoyaltyCard { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? ExpirationDate { get; set; }
}

public class UpdateCouponRequest : CreateCouponRequest
{
    public bool IsActive { get; set; } = true;
}

public class CouponSearchRequest
{
    public string? SearchTerm { get; set; }
    public string? CouponType { get; set; }
    public Guid? ProductId { get; set; }
    public Guid? StoreId { get; set; }
    public string? ManufacturerName { get; set; }
    public bool? CanBeDoubled { get; set; }
    public bool? RequiresLoyaltyCard { get; set; }
    public bool OnlyActive { get; set; } = true;
    public bool OnlyApproved { get; set; } = true;
    public bool OnlyNotExpired { get; set; } = true;
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class UserCouponDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid CouponId { get; set; }
    public CouponDto? Coupon { get; set; }
    public DateTime ClippedAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public Guid? UsedAtStoreId { get; set; }
    public string? UsedAtStoreName { get; set; }
    public decimal? SavedAmount { get; set; }
    public string? Notes { get; set; }
}

public class ClipCouponRequest
{
    [Required]
    public Guid CouponId { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }
}

public class UseCouponRequest
{
    [Required]
    public Guid CouponId { get; set; }

    [Required]
    public Guid StoreId { get; set; }

    [Required]
    [Range(0.01, 999999.99)]
    public decimal SavedAmount { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }
}
