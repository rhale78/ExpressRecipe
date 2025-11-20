using System.ComponentModel.DataAnnotations;

namespace ExpressRecipe.Shared.DTOs.User;

// Report Types and Management

public class ReportTypeDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = string.Empty; // Shopping, Nutrition, Inventory, Financial, Activity
    public string? RequiresSubscription { get; set; } // NULL for all, 'Plus', 'Premium'
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
}

public class SavedReportDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ReportTypeId { get; set; }
    public string? ReportTypeName { get; set; }
    public string ReportName { get; set; } = string.Empty;
    public string? Parameters { get; set; } // JSON parameters for the report
    public string? Schedule { get; set; } // NULL for one-time, 'Daily', 'Weekly', 'Monthly'
    public int? ScheduleDay { get; set; }
    public DateTime? LastRunAt { get; set; }
    public DateTime? NextRunAt { get; set; }
    public bool IsActive { get; set; }
    public bool EmailResults { get; set; }
    public string? EmailAddress { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class ReportHistoryDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ReportTypeId { get; set; }
    public string? ReportTypeName { get; set; }
    public Guid? SavedReportId { get; set; }
    public string ReportName { get; set; } = string.Empty;
    public string? Parameters { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string Format { get; set; } = string.Empty; // PDF, Excel, CSV, HTML
    public string? FileUrl { get; set; }
    public long? FileSize { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime GeneratedAt { get; set; }
    public string Status { get; set; } = string.Empty; // Pending, Completed, Failed
    public string? ErrorMessage { get; set; }
}

public class CreateSavedReportRequest
{
    [Required]
    public Guid ReportTypeId { get; set; }

    [Required]
    [StringLength(200)]
    public string ReportName { get; set; } = string.Empty;

    public string? Parameters { get; set; }

    [StringLength(100)]
    public string? Schedule { get; set; }

    [Range(1, 31)]
    public int? ScheduleDay { get; set; }

    public bool EmailResults { get; set; }

    [EmailAddress]
    [StringLength(200)]
    public string? EmailAddress { get; set; }
}

public class UpdateSavedReportRequest
{
    [Required]
    [StringLength(200)]
    public string ReportName { get; set; } = string.Empty;

    public string? Parameters { get; set; }

    [StringLength(100)]
    public string? Schedule { get; set; }

    [Range(1, 31)]
    public int? ScheduleDay { get; set; }

    public bool IsActive { get; set; }

    public bool EmailResults { get; set; }

    [EmailAddress]
    [StringLength(200)]
    public string? EmailAddress { get; set; }
}

public class GenerateReportRequest
{
    [Required]
    public Guid ReportTypeId { get; set; }

    [Required]
    [StringLength(200)]
    public string ReportName { get; set; } = string.Empty;

    public string? Parameters { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    [Required]
    [StringLength(50)]
    public string Format { get; set; } = string.Empty; // PDF, Excel, CSV, HTML
}

public class ReportSummaryDto
{
    public int TotalReports { get; set; }
    public int SavedReports { get; set; }
    public int ScheduledReports { get; set; }
    public DateTime? LastGeneratedAt { get; set; }
    public List<SavedReportDto>? RecentSavedReports { get; set; }
    public List<ReportHistoryDto>? RecentReports { get; set; }
}

// User Lists

public class UserListDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string ListType { get; set; } = string.Empty; // Shopping, Wishlist, Inventory, Custom
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsShared { get; set; }
    public string? ShareCode { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    // Navigation properties
    public int ItemCount { get; set; }
    public List<UserListItemDto>? Items { get; set; }
    public List<ListSharingDto>? SharedWith { get; set; }
}

public class UserListItemDto
{
    public Guid Id { get; set; }
    public Guid ListId { get; set; }
    public string ItemType { get; set; } = string.Empty; // Product, Ingredient, Recipe, Custom
    public Guid? ItemId { get; set; }
    public string? ItemName { get; set; }
    public decimal? Quantity { get; set; }
    public string? Unit { get; set; }
    public string? Notes { get; set; }
    public bool IsChecked { get; set; }
    public DateTime? CheckedAt { get; set; }
    public int OrderIndex { get; set; }
    public DateTime AddedAt { get; set; }
}

public class ListSharingDto
{
    public Guid Id { get; set; }
    public Guid ListId { get; set; }
    public Guid? SharedWithUserId { get; set; }
    public string? SharedWithUserName { get; set; }
    public bool CanEdit { get; set; }
    public Guid SharedBy { get; set; }
    public string? SharedByName { get; set; }
    public DateTime SharedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

public class CreateUserListRequest
{
    [Required]
    [StringLength(50)]
    public string ListType { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    public bool IsShared { get; set; }
}

public class UpdateUserListRequest
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    public bool IsShared { get; set; }
}

public class AddListItemRequest
{
    [Required]
    [StringLength(50)]
    public string ItemType { get; set; } = string.Empty;

    public Guid? ItemId { get; set; }

    [StringLength(300)]
    public string? ItemName { get; set; }

    public decimal? Quantity { get; set; }

    [StringLength(50)]
    public string? Unit { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    public int OrderIndex { get; set; }
}

public class UpdateListItemRequest
{
    public decimal? Quantity { get; set; }

    [StringLength(50)]
    public string? Unit { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    public bool IsChecked { get; set; }

    public int OrderIndex { get; set; }
}

public class ShareListRequest
{
    public Guid? SharedWithUserId { get; set; }

    public bool CanEdit { get; set; }

    public DateTime? ExpiresAt { get; set; }
}

public class ListSummaryDto
{
    public int TotalLists { get; set; }
    public int ShoppingLists { get; set; }
    public int Wishlists { get; set; }
    public int SharedLists { get; set; }
    public int TotalItems { get; set; }
    public int CheckedItems { get; set; }
    public List<UserListDto>? RecentLists { get; set; }
}
