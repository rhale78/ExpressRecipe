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
