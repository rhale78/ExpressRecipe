namespace ExpressRecipe.ShoppingService.Data;

public interface IShoppingRepository
{
    // Shopping Lists
    Task<Guid> CreateShoppingListAsync(Guid userId, Guid? householdId, string name, string? description, string listType = "Standard", Guid? storeId = null);
    Task<List<ShoppingListDto>> GetUserListsAsync(Guid userId);
    Task<List<ShoppingListDto>> GetHouseholdListsAsync(Guid householdId);
    Task<ShoppingListDto?> GetShoppingListAsync(Guid listId, Guid userId);
    Task UpdateShoppingListAsync(Guid listId, string name, string? description, Guid? storeId = null);
    Task DeleteShoppingListAsync(Guid listId, Guid userId);
    Task CompleteShoppingListAsync(Guid listId);
    Task ArchiveShoppingListAsync(Guid listId);

    // Shopping List Items
    Task<Guid> AddItemToListAsync(Guid listId, Guid userId, Guid? productId, string? customName, decimal quantity, string? unit, string? category, 
        bool isFavorite = false, bool isGeneric = false, string? preferredBrand = null, Guid? storeId = null);
    Task<ShoppingListItemDto?> GetShoppingListItemAsync(Guid itemId);
    Task<List<ShoppingListItemDto>> GetListItemsAsync(Guid listId, Guid userId);
    Task UpdateItemQuantityAsync(Guid itemId, decimal quantity);
    Task UpdateItemPriceAsync(Guid itemId, decimal? estimatedPrice, decimal? actualPrice);
    Task ToggleItemCheckedAsync(Guid itemId);
    Task RemoveItemFromListAsync(Guid itemId);
    Task BulkAddItemsAsync(Guid listId, Guid userId, List<ShoppingListItemDto> items);
    Task MoveItemToListAsync(Guid itemId, Guid targetListId);

    // Favorite Items
    Task<Guid> AddFavoriteItemAsync(Guid userId, Guid? householdId, Guid? productId, string? customName, string? preferredBrand, 
        decimal typicalQuantity, string? typicalUnit, string? category, bool isGeneric);
    Task<List<FavoriteItemDto>> GetUserFavoritesAsync(Guid userId);
    Task<List<FavoriteItemDto>> GetHouseholdFavoritesAsync(Guid householdId);
    Task UpdateFavoriteUsageAsync(Guid favoriteId);
    Task RemoveFavoriteAsync(Guid favoriteId);

    // List Sharing
    Task<Guid> ShareListAsync(Guid listId, Guid ownerId, Guid sharedWithUserId, string permission);
    Task<List<ListShareDto>> GetListSharesAsync(Guid listId);
    Task<List<ShoppingListDto>> GetSharedListsAsync(Guid userId);
    Task RevokeListAccessAsync(Guid shareId);

    // Stores
    Task<Guid> CreateStoreAsync(Guid userId, string name, string? chain, string? address, string? city, string? state, 
        string? zipCode, decimal? latitude, decimal? longitude);
    Task<List<StoreDto>> GetUserStoresAsync(Guid userId);
    Task<List<StoreDto>> GetNearbyStoresAsync(decimal latitude, decimal longitude, double maxDistanceKm = 10.0);
    Task<StoreDto?> GetStoreByIdAsync(Guid storeId);
    Task UpdateStoreAsync(Guid storeId, string name, string? address, decimal? latitude, decimal? longitude);
    Task SetPreferredStoreAsync(Guid userId, Guid storeId);

    // Store Layouts
    Task<Guid> CreateStoreLayoutAsync(Guid userId, Guid storeId, string categoryName, string? aisle, int orderIndex);
    Task<List<StoreLayoutDto>> GetStoreLayoutAsync(Guid storeId);
    Task UpdateStoreLayoutAsync(Guid layoutId, string? aisle, int orderIndex);

    // Price Comparison
    Task<Guid> RecordPriceComparisonAsync(Guid shoppingListItemId, Guid? productId, Guid storeId, decimal price, 
        decimal? unitPrice, decimal? size, string? unit, bool hasDeal, string? dealType, DateTime? dealEndDate);
    Task<List<PriceComparisonDto>> GetPriceComparisonsAsync(Guid shoppingListItemId);
    Task<List<PriceComparisonDto>> GetBestPricesAsync(Guid productId, Guid? preferredStoreId = null);
    Task UpdateBestPriceForItemAsync(Guid itemId);

    // Templates
    Task<Guid> CreateTemplateAsync(Guid userId, Guid? householdId, string name, string? description, string? category);
    Task<List<ShoppingListTemplateDto>> GetUserTemplatesAsync(Guid userId);
    Task<List<ShoppingListTemplateDto>> GetHouseholdTemplatesAsync(Guid householdId);
    Task<Guid> AddItemToTemplateAsync(Guid templateId, Guid? productId, string? customName, decimal quantity, string? unit, string? category);
    Task<List<TemplateItemDto>> GetTemplateItemsAsync(Guid templateId);
    Task<Guid> CreateListFromTemplateAsync(Guid templateId, Guid userId, string listName);
    Task UpdateTemplateUsageAsync(Guid templateId);
    Task DeleteTemplateAsync(Guid templateId);

    // Recipe Integration
    Task<Guid> AddItemsFromRecipeAsync(Guid listId, Guid userId, Guid recipeId, int servings);
    Task<List<ShoppingListItemDto>> GetRecipeIngredientsAsItemsAsync(Guid recipeId, int servings);

    // Inventory Integration
    Task<Guid> AddLowStockItemsAsync(Guid listId, Guid userId, decimal threshold = 2.0m);
    Task<List<ShoppingListItemDto>> GetLowStockItemsFromInventoryAsync(Guid userId, decimal threshold);

    // Scanning
    Task<Guid> StartShoppingScanSessionAsync(Guid userId, Guid shoppingListId, Guid? storeId);
    Task<ShoppingScanSessionDto?> GetActiveShoppingScanSessionAsync(Guid userId);
    Task<Guid> ScanPurchaseItemAsync(Guid sessionId, string barcode, decimal quantity, decimal price);
    Task EndShoppingScanSessionAsync(Guid sessionId, decimal? totalSpent);
    Task AddPurchasedItemsToInventoryAsync(Guid listId);

    // Reports
    Task<ShoppingReportDto> GetShoppingReportAsync(Guid userId, Guid? householdId = null);
}

public class ShoppingListDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? HouseholdId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = "Active"; // Active, Completed, Archived
    public string ListType { get; set; } = "Standard"; // Standard, Future, Template
    public Guid? StoreId { get; set; }
    public string? StoreName { get; set; }
    public DateTime? ScheduledFor { get; set; }
    public int ItemCount { get; set; }
    public int CheckedCount { get; set; }
    public decimal? TotalEstimatedCost { get; set; }
    public decimal? ActualCost { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class ShoppingListItemDto
{
    public Guid Id { get; set; }
    public Guid ShoppingListId { get; set; }
    public Guid? ProductId { get; set; }
    public string? ProductName { get; set; }
    public string? CustomName { get; set; }
    public decimal Quantity { get; set; }
    public string? Unit { get; set; }
    public string? Category { get; set; }
    public string? Aisle { get; set; }
    public bool IsChecked { get; set; }
    public bool IsFavorite { get; set; }
    public bool IsGeneric { get; set; }
    public string? PreferredBrand { get; set; }
    public decimal? EstimatedPrice { get; set; }
    public decimal? ActualPrice { get; set; }
    public decimal? BestPrice { get; set; }
    public Guid? BestPriceStoreId { get; set; }
    public string? BestPriceStoreName { get; set; }
    public decimal? UnitPrice { get; set; }
    public bool HasDeal { get; set; }
    public string? DealType { get; set; }
    public string? DealDescription { get; set; }
    public Guid? AddedFromRecipeId { get; set; }
    public bool AddToInventoryOnPurchase { get; set; }
    public int OrderIndex { get; set; }
    public DateTime AddedAt { get; set; }
    public DateTime? CheckedAt { get; set; }
    public DateTime? PurchasedAt { get; set; }
}

public class FavoriteItemDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? HouseholdId { get; set; }
    public Guid? ProductId { get; set; }
    public string? ProductName { get; set; }
    public string? CustomName { get; set; }
    public string? PreferredBrand { get; set; }
    public decimal TypicalQuantity { get; set; }
    public string? TypicalUnit { get; set; }
    public string? Category { get; set; }
    public bool IsGeneric { get; set; }
    public int UseCount { get; set; }
    public DateTime? LastUsed { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ListShareDto
{
    public Guid Id { get; set; }
    public Guid ShoppingListId { get; set; }
    public Guid OwnerId { get; set; }
    public Guid SharedWithUserId { get; set; }
    public string Permission { get; set; } = string.Empty;
    public DateTime SharedAt { get; set; }
}

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
    public string? Phone { get; set; }
    public bool IsPreferred { get; set; }
    public double? DistanceKm { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class StoreLayoutDto
{
    public Guid Id { get; set; }
    public Guid StoreId { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string? Aisle { get; set; }
    public int OrderIndex { get; set; }
}

public class PriceComparisonDto
{
    public Guid Id { get; set; }
    public Guid ShoppingListItemId { get; set; }
    public Guid? ProductId { get; set; }
    public Guid StoreId { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal? Size { get; set; }
    public string? Unit { get; set; }
    public bool HasDeal { get; set; }
    public string? DealType { get; set; }
    public DateTime? DealEndDate { get; set; }
    public bool IsAvailable { get; set; }
    public DateTime LastChecked { get; set; }
}

public class ShoppingListTemplateDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? HouseholdId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public int ItemCount { get; set; }
    public int UseCount { get; set; }
    public DateTime? LastUsed { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TemplateItemDto
{
    public Guid Id { get; set; }
    public Guid TemplateId { get; set; }
    public Guid? ProductId { get; set; }
    public string? ProductName { get; set; }
    public string? CustomName { get; set; }
    public decimal Quantity { get; set; }
    public string? Unit { get; set; }
    public string? Category { get; set; }
    public int OrderIndex { get; set; }
}

public class ShoppingScanSessionDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ShoppingListId { get; set; }
    public string ListName { get; set; } = string.Empty;
    public Guid? StoreId { get; set; }
    public string? StoreName { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public int ItemsScanned { get; set; }
    public decimal? TotalSpent { get; set; }
    public bool IsActive { get; set; }
}

public class ShoppingReportDto
{
    public int TotalLists { get; set; }
    public int ActiveLists { get; set; }
    public int CompletedListsThisMonth { get; set; }
    public decimal TotalSpentThisMonth { get; set; }
    public int TotalItemsPurchased { get; set; }
    public int MostUsedStoreId { get; set; }
    public string MostUsedStoreName { get; set; } = string.Empty;
    public Dictionary<string, int> ItemsByCategory { get; set; } = new();
    public List<FavoriteItemDto> TopFavorites { get; set; } = new();
}
