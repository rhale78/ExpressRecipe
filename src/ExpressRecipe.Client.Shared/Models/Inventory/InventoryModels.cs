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
