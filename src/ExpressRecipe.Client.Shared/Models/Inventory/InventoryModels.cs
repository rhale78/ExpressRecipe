namespace ExpressRecipe.Client.Shared.Models.Inventory;

public class InventoryItemDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    // Product linkage (if item is from product database)
    public Guid? ProductId { get; set; }
    public string? ProductName { get; set; }
    public string? ProductBrand { get; set; }
    public string? ProductImageUrl { get; set; }
    public List<string> ProductAllergens { get; set; } = new();

    // Manual entry fields (if not linked to product)
    public string Name { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string? Category { get; set; } // Dairy, Meat, Produce, Grains, etc.

    // Quantity and storage
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty; // oz, lb, kg, g, ml, L, count, etc.
    public string Location { get; set; } = "Pantry"; // Pantry, Fridge, Freezer, Other

    // Dates
    public DateTime? PurchaseDate { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public DateTime? OpenedDate { get; set; } // For items that expire faster once opened

    // Status
    public bool IsExpired => ExpirationDate.HasValue && ExpirationDate.Value < DateTime.UtcNow;
    public bool IsExpiringSoon => ExpirationDate.HasValue &&
                                   ExpirationDate.Value >= DateTime.UtcNow &&
                                   ExpirationDate.Value <= DateTime.UtcNow.AddDays(7);
    public bool IsLowStock { get; set; } // User-defined flag

    // Additional info
    public string? Notes { get; set; }
    public List<string> Tags { get; set; } = new(); // organic, gluten-free, local, etc.

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateInventoryItemRequest
{
    // Product linkage (optional)
    public Guid? ProductId { get; set; }

    // Manual entry (required if no ProductId)
    public string Name { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string? Category { get; set; }

    // Quantity and storage
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string Location { get; set; } = "Pantry";

    // Household context
    public Guid? HouseholdId { get; set; }
    public Guid? AddressId { get; set; }
    public Guid? StorageLocationId { get; set; }

    // Dates
    public DateTime? PurchaseDate { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public DateTime? OpenedDate { get; set; }

    // Status
    public bool IsLowStock { get; set; }

    // Additional info
    public string? Notes { get; set; }
    public List<string> Tags { get; set; } = new();
}

public class UpdateInventoryItemRequest : CreateInventoryItemRequest
{
}

public class InventorySearchRequest
{
    public string? SearchTerm { get; set; }
    public List<string>? Locations { get; set; } // Filter by location
    public List<string>? Categories { get; set; } // Filter by category
    public List<string>? Tags { get; set; }
    public bool? IsExpired { get; set; } // Show only expired items
    public bool? IsExpiringSoon { get; set; } // Show items expiring within 7 days
    public bool? IsLowStock { get; set; } // Show low stock items
    public bool? HasAllergens { get; set; } // Show items with allergens
    public List<string>? ExcludeAllergens { get; set; } // Exclude items with specific allergens

    public Guid? HouseholdId { get; set; }
    public Guid? AddressId { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public string? SortBy { get; set; } = "ExpirationDate"; // ExpirationDate, Name, PurchaseDate, CreatedAt
    public bool SortDescending { get; set; } = false; // Default ascending for expiration dates
}

public class InventorySearchResult
{
    public List<InventoryItemDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);

    // Summary statistics
    public int ExpiredCount { get; set; }
    public int ExpiringSoonCount { get; set; }
    public int LowStockCount { get; set; }
    public int TotalItems { get; set; }
}

public class InventorySummaryDto
{
    public int TotalItems { get; set; }
    public int ExpiredItems { get; set; }
    public int ExpiringSoonItems { get; set; }
    public int LowStockItems { get; set; }
    public Dictionary<string, int> ItemsByLocation { get; set; } = new();
    public Dictionary<string, int> ItemsByCategory { get; set; } = new();
}

public class AdjustInventoryQuantityRequest
{
    public Guid InventoryItemId { get; set; }
    public decimal QuantityChange { get; set; } // Positive for add, negative for consume
    public string? Reason { get; set; } // "Used in recipe", "Purchased more", "Expired/thrown out"
}

public class BulkAddInventoryItemsRequest
{
    public List<CreateInventoryItemRequest> Items { get; set; } = new();
}

public class InventoryItemValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

// Household Management
public class HouseholdDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public int MemberCount { get; set; }
    public int AddressCount { get; set; }
    public string? UserRole { get; set; } // User's role in this household
}

public class HouseholdMemberDto
{
    public Guid Id { get; set; }
    public Guid HouseholdId { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Role { get; set; } = "Member"; // Owner, Admin, Member, Viewer
    public bool CanManageInventory { get; set; }
    public bool CanManageShopping { get; set; }
    public bool CanManageMembers { get; set; }
    public DateTime JoinedAt { get; set; }
}

public class CreateHouseholdRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class AddHouseholdMemberRequest
{
    public Guid HouseholdId { get; set; }
    public Guid UserId { get; set; }
    public string Role { get; set; } = "Member";
    public bool CanManageInventory { get; set; }
    public bool CanManageShopping { get; set; }
    public bool CanManageMembers { get; set; }
}

// Address Management
public class AddressDto
{
    public Guid Id { get; set; }
    public Guid HouseholdId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Street { get; set; } = string.Empty;
    public string? Street2 { get; set; }
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public string? Country { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public double? DistanceKm { get; set; } // Distance from current location
    public bool IsPrimary { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateAddressRequest
{
    public Guid HouseholdId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Street { get; set; } = string.Empty;
    public string? Street2 { get; set; }
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public string? Country { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public bool IsPrimary { get; set; }
}

public class DetectAddressRequest
{
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public int RadiusKm { get; set; } = 1;
}

// Storage Location
public class StorageLocationDto
{
    public Guid Id { get; set; }
    public Guid AddressId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string LocationType { get; set; } = string.Empty; // Fridge, Freezer, Pantry, Cabinet, etc.
    public string? Description { get; set; }
    public string? SubLocation { get; set; }
    public int OrderIndex { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateStorageLocationRequest
{
    public Guid AddressId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string LocationType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? SubLocation { get; set; }
    public int OrderIndex { get; set; }
}

// Scan Sessions
public class InventoryScanSessionDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? AddressId { get; set; }
    public Guid? StorageLocationId { get; set; }
    public string SessionType { get; set; } = string.Empty; // Adding, Using, Disposing, Purchasing
    public string Status { get; set; } = "Active";
    public int ItemsScanned { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
}

public class StartInventoryScanRequest
{
    public Guid? HouseholdId { get; set; }
    public Guid? AddressId { get; set; }
    public Guid? StorageLocationId { get; set; }
    public string SessionType { get; set; } = "Adding";
}

public class ScanItemRequest
{
    public Guid SessionId { get; set; }
    public Guid? ProductId { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public decimal? Quantity { get; set; }
}

public class DisposeItemRequest
{
    public Guid SessionId { get; set; }
    public Guid? InventoryItemId { get; set; }
    public string? Barcode { get; set; }
    public string Reason { get; set; } = string.Empty; // Bad, Expired, Allergy, Other
    public string? Notes { get; set; }
}

public class ScanSessionResultDto
{
    public Guid SessionId { get; set; }
    public int TotalItemsScanned { get; set; }
    public int AddedCount { get; set; }
    public int UsedCount { get; set; }
    public int DisposedCount { get; set; }
    public DateTime CompletedAt { get; set; }
    public List<string> ProcessedItems { get; set; } = new();
}

// Allergen Discovery
public class AllergenDiscoveryDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid InventoryItemId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string AllergenDetected { get; set; } = string.Empty;
    public string IngredientName => AllergenDetected;
    public string Reaction { get; set; } = string.Empty;
    public bool AddedToProfile { get; set; }
    public DateTime DiscoveredAt { get; set; }
}

// Reports
public class InventoryReportDto
{
    public int TotalItems { get; set; }
    public int LowStockItems { get; set; }
    public int ExpiringItems { get; set; }
    public int ExpiredItems { get; set; }
    public Dictionary<string, int> ItemsByLocation { get; set; } = new();
    public Dictionary<string, int> ItemsByCategory { get; set; } = new();
    public List<InventoryItemDto> LowStockList { get; set; } = new();
    public List<InventoryItemDto> ExpiringList { get; set; } = new();
}

// Additional Update Requests
public class UpdateHouseholdRequest
{
    public string Name { get; set; } = string.Empty;
}

public class UpdateMemberRoleRequest
{
    public string Role { get; set; } = "Member";
    public bool CanManageInventory { get; set; }
    public bool CanManageShopping { get; set; }
    public bool CanManageMembers { get; set; }
}

public class UpdateAddressRequest : CreateAddressRequest
{
}

public class UpdateStorageLocationRequest : CreateStorageLocationRequest
{
}

public class StartScanSessionRequest : StartInventoryScanRequest
{
}

public class ScanSessionDto : InventoryScanSessionDto
{
}

public class ScanAddItemRequest : ScanItemRequest
{
}

public class ScanUseItemRequest : ScanItemRequest
{
}

public class ScanDisposeItemRequest : DisposeItemRequest
{
}
