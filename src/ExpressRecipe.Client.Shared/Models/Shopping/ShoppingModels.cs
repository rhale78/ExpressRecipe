namespace ExpressRecipe.Client.Shared.Models.Shopping;

public class ShoppingListDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? ShoppingDate { get; set; }
    public string Status { get; set; } = "Active"; // Active, Completed, Archived
    public List<ShoppingListItemDto> Items { get; set; } = new();

    // Summary stats
    public int TotalItems => Items.Count;
    public int CompletedItems => Items.Count(i => i.IsPurchased);
    public int RemainingItems => Items.Count(i => !i.IsPurchased);
    public decimal CompletionPercentage => TotalItems > 0 ? (CompletedItems / (decimal)TotalItems) * 100 : 0;

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class ShoppingListItemDto
{
    public Guid Id { get; set; }
    public Guid ShoppingListId { get; set; }
    public int OrderIndex { get; set; }

    // Product linkage (optional)
    public Guid? ProductId { get; set; }
    public string? ProductName { get; set; }
    public string? ProductBrand { get; set; }
    public string? ProductImageUrl { get; set; }
    public List<string> ProductAllergens { get; set; } = new();

    // Manual entry
    public string Name { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string? Category { get; set; } // Produce, Dairy, Meat, Bakery, etc.

    // Quantity
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;

    // Status
    public bool IsPurchased { get; set; }
    public DateTime? PurchasedAt { get; set; }
    public decimal? ActualPrice { get; set; }
    public string? Store { get; set; }

    // Metadata
    public string Priority { get; set; } = "Normal"; // High, Normal, Low
    public string? Notes { get; set; }
    public string? Source { get; set; } // "Recipe", "Inventory", "Manual"
    public Guid? SourceId { get; set; } // RecipeId or InventoryItemId

    public DateTime CreatedAt { get; set; }
}

public class CreateShoppingListRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? ShoppingDate { get; set; }
}

public class UpdateShoppingListRequest : CreateShoppingListRequest
{
    public string Status { get; set; } = "Active";
}

public class AddShoppingListItemRequest
{
    public Guid ShoppingListId { get; set; }

    // Product linkage (optional)
    public Guid? ProductId { get; set; }

    // Manual entry
    public string Name { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string? Category { get; set; }

    // Quantity
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;

    // Metadata
    public string Priority { get; set; } = "Normal";
    public string? Notes { get; set; }
    public string? Source { get; set; }
    public Guid? SourceId { get; set; }
}

public class UpdateShoppingListItemRequest : AddShoppingListItemRequest
{
}

public class MarkItemPurchasedRequest
{
    public Guid ItemId { get; set; }
    public bool IsPurchased { get; set; }
    public decimal? ActualPrice { get; set; }
    public string? Store { get; set; }
}

public class AddItemsFromRecipeRequest
{
    public Guid ShoppingListId { get; set; }
    public Guid RecipeId { get; set; }
    public int Servings { get; set; } = 1; // Scale ingredients by servings
    public bool SubtractInventory { get; set; } = true; // Don't add items already in inventory
}

public class AddLowStockItemsRequest
{
    public Guid ShoppingListId { get; set; }
    public List<Guid>? InventoryItemIds { get; set; } // Specific items, or null for all low stock
}

public class ReorderItemsRequest
{
    public Guid ShoppingListId { get; set; }
    public List<Guid> ItemIdsInOrder { get; set; } = new();
}

public class ShoppingListSearchRequest
{
    public string? SearchTerm { get; set; }
    public string? Status { get; set; } // Active, Completed, Archived
    public DateTime? ShoppingDateFrom { get; set; }
    public DateTime? ShoppingDateTo { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SortBy { get; set; } = "CreatedAt";
    public bool SortDescending { get; set; } = true;
}

public class ShoppingListSearchResult
{
    public List<ShoppingListDto> Lists { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public class ShoppingCategorySummaryDto
{
    public string Category { get; set; } = string.Empty;
    public int TotalItems { get; set; }
    public int PurchasedItems { get; set; }
    public int RemainingItems => TotalItems - PurchasedItems;
}

public class ShoppingSummaryDto
{
    public int TotalActiveLists { get; set; }
    public int TotalActiveItems { get; set; }
    public int CompletedItemsThisWeek { get; set; }
    public decimal EstimatedTotal { get; set; }
    public List<ShoppingCategorySummaryDto> CategoriesSummary { get; set; } = new();
}

// Favorite Items
public class FavoriteItemDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? HouseholdId { get; set; }
    public Guid? ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string? Category { get; set; }
    public decimal? TypicalQuantity { get; set; }
    public string? TypicalUnit { get; set; }
    public int UsageCount { get; set; }
    public DateTime LastUsed { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AddFavoriteItemRequest
{
    public Guid? HouseholdId { get; set; }
    public Guid? ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string? Category { get; set; }
    public decimal? TypicalQuantity { get; set; }
    public string? TypicalUnit { get; set; }
}

// Stores
public class StoreDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Chain { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Website { get; set; }
    public bool IsPreferred { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateStoreRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Chain { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Website { get; set; }
}

public class NearbyStoresRequest
{
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public int RadiusKm { get; set; } = 10;
}

public class SetPreferredStoreRequest
{
    public Guid StoreId { get; set; }
    public Guid UserId { get; set; }
}

// Store Layout
public class StoreLayoutDto
{
    public Guid Id { get; set; }
    public Guid StoreId { get; set; }
    public string Section { get; set; } = string.Empty;
    public string? Category { get; set; }
    public int Aisle { get; set; }
    public int OrderIndex { get; set; }
}

public class CreateStoreLayoutRequest
{
    public Guid StoreId { get; set; }
    public string Section { get; set; } = string.Empty;
    public string? Category { get; set; }
    public int Aisle { get; set; }
    public int OrderIndex { get; set; }
}

public class UpdateStoreLayoutRequest : CreateStoreLayoutRequest
{
}

// Price Comparison
public class PriceComparisonDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public Guid StoreId { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? UnitPrice { get; set; }
    public string? Unit { get; set; }
    public string? DealType { get; set; } // "Regular", "Sale", "BOGO", "BOGO50"
    public string? DealDescription { get; set; }
    public DateTime RecordedAt { get; set; }
}

public class RecordPriceRequest
{
    public Guid ProductId { get; set; }
    public Guid StoreId { get; set; }
    public decimal Price { get; set; }
    public decimal? UnitPrice { get; set; }
    public string? Unit { get; set; }
    public string? DealType { get; set; }
    public string? DealDescription { get; set; }
}

public class BestPriceDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public List<PriceComparisonDto> Prices { get; set; } = new();
    public PriceComparisonDto? BestPrice { get; set; }
    public decimal? PotentialSavings { get; set; }
}

// Templates
public class ShoppingListTemplateDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? HouseholdId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int UsageCount { get; set; }
    public DateTime LastUsed { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ShoppingListTemplateItemDto
{
    public Guid Id { get; set; }
    public Guid TemplateId { get; set; }
    public Guid? ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string? Category { get; set; }
    public decimal? DefaultQuantity { get; set; }
    public string? DefaultUnit { get; set; }
    public int OrderIndex { get; set; }
}

public class CreateTemplateRequest
{
    public Guid? HouseholdId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class AddTemplateItemRequest
{
    public Guid TemplateId { get; set; }
    public Guid? ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string? Category { get; set; }
    public decimal? DefaultQuantity { get; set; }
    public string? DefaultUnit { get; set; }
    public int OrderIndex { get; set; }
}

public class CreateListFromTemplateRequest
{
    public Guid TemplateId { get; set; }
    public string? ListName { get; set; }
    public DateTime? ShoppingDate { get; set; }
}

// Shopping Scan Sessions
public class ShoppingScanSessionDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? ShoppingListId { get; set; }
    public Guid? StoreId { get; set; }
    public string Status { get; set; } = "Active";
    public int ItemsScanned { get; set; }
    public decimal RunningTotal { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
}

public class StartShoppingScanRequest
{
    public Guid? ShoppingListId { get; set; }
    public Guid? StoreId { get; set; }
}

public class ScanPurchaseRequest
{
    public Guid SessionId { get; set; }
    public Guid ShoppingListItemId { get; set; }
    public string? Barcode { get; set; }
    public decimal ActualPrice { get; set; }
}

public class ShoppingScanSessionResultDto
{
    public Guid SessionId { get; set; }
    public int TotalItemsScanned { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime CompletedAt { get; set; }
    public List<string> PurchasedItems { get; set; } = new();
}

public class AddPurchasedToInventoryRequest
{
    public Guid SessionId { get; set; }
    public Guid AddressId { get; set; }
    public Guid StorageLocationId { get; set; }
}

// Additional Request DTOs
public class MoveItemRequest
{
    public Guid ItemId { get; set; }
    public Guid TargetListId { get; set; }
}

public class UpdateStoreRequest : CreateStoreRequest
{
}
