namespace ExpressRecipe.ShoppingService.Data;

public interface IShoppingRepository
{
    // Shopping Lists
    Task<Guid> CreateShoppingListAsync(Guid userId, string name, string? description);
    Task<List<ShoppingListDto>> GetUserListsAsync(Guid userId);
    Task<ShoppingListDto?> GetShoppingListAsync(Guid listId, Guid userId);
    Task UpdateShoppingListAsync(Guid listId, string name, string? description);
    Task DeleteShoppingListAsync(Guid listId, Guid userId);

    // Shopping List Items
    Task<Guid> AddItemToListAsync(Guid listId, Guid userId, Guid? productId, string? customName, decimal quantity, string? unit, string? category);
    Task<List<ShoppingListItemDto>> GetListItemsAsync(Guid listId, Guid userId);
    Task UpdateItemQuantityAsync(Guid itemId, decimal quantity);
    Task ToggleItemCheckedAsync(Guid itemId);
    Task RemoveItemFromListAsync(Guid itemId);

    // List Sharing
    Task<Guid> ShareListAsync(Guid listId, Guid ownerId, Guid sharedWithUserId, string permission);
    Task<List<ListShareDto>> GetListSharesAsync(Guid listId);
    Task<List<ShoppingListDto>> GetSharedListsAsync(Guid userId);
    Task RevokeListAccessAsync(Guid shareId);

    // Store Layouts
    Task<Guid> CreateStoreLayoutAsync(Guid userId, string storeName, string? address);
    Task<List<StoreLayoutDto>> GetUserStoresAsync(Guid userId);
    Task AddCategoryToStoreAsync(Guid storeId, string categoryName, int displayOrder);
}

public class ShoppingListDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int ItemCount { get; set; }
    public int CheckedCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class ShoppingListItemDto
{
    public Guid Id { get; set; }
    public Guid ShoppingListId { get; set; }
    public Guid? ProductId { get; set; }
    public string? CustomName { get; set; }
    public decimal Quantity { get; set; }
    public string? Unit { get; set; }
    public string? Category { get; set; }
    public bool IsChecked { get; set; }
    public int SortOrder { get; set; }
    public DateTime AddedAt { get; set; }
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

public class StoreLayoutDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public string? Address { get; set; }
    public List<StoreCategoryDto> Categories { get; set; } = new();
}

public class StoreCategoryDto
{
    public string CategoryName { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
}
