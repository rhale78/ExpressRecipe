namespace ExpressRecipe.InventoryService.Data;

public interface IInventoryRepository
{
    // Inventory Items
    Task<Guid> AddInventoryItemAsync(Guid userId, Guid? productId, string? customName, Guid storageLocationId,
        decimal quantity, string? unit, DateTime? expirationDate, string? barcode);
    Task<List<InventoryItemDto>> GetUserInventoryAsync(Guid userId);
    Task<InventoryItemDto?> GetInventoryItemByIdAsync(Guid id);
    Task UpdateInventoryQuantityAsync(Guid itemId, decimal newQuantity, string actionType, string? reason, Guid? recipeId = null);
    Task DeleteInventoryItemAsync(Guid itemId);
    Task<List<InventoryItemDto>> GetExpiringItemsAsync(Guid userId, int daysAhead = 7);

    // Storage Locations
    Task<Guid> CreateStorageLocationAsync(Guid userId, string name, string? description, string? temperature);
    Task<List<StorageLocationDto>> GetStorageLocationsAsync(Guid userId);
    Task<StorageLocationDto?> GetStorageLocationByIdAsync(Guid id);

    // Expiration Alerts
    Task CreateExpirationAlertsAsync(Guid userId);
    Task<List<ExpirationAlertDto>> GetActiveAlertsAsync(Guid userId);
    Task DismissAlertAsync(Guid alertId);

    // Usage History
    Task<List<InventoryHistoryDto>> GetUsageHistoryAsync(Guid itemId, int limit = 50);
    Task<List<UsagePredictionDto>> GetUsagePredictionsAsync(Guid userId);
}

public class InventoryItemDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? ProductId { get; set; }
    public string? ProductName { get; set; }
    public string? CustomName { get; set; }
    public Guid StorageLocationId { get; set; }
    public string StorageLocationName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string? Unit { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public DateTime? OpenedDate { get; set; }
    public bool IsOpened { get; set; }
    public string? Barcode { get; set; }
    public decimal? Price { get; set; }
    public string? Store { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class StorageLocationDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Temperature { get; set; }
    public bool IsDefault { get; set; }
    public int ItemCount { get; set; }
}

public class ExpirationAlertDto
{
    public Guid Id { get; set; }
    public Guid InventoryItemId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string AlertType { get; set; } = string.Empty;
    public int DaysUntilExpiration { get; set; }
    public DateTime AlertDate { get; set; }
}

public class InventoryHistoryDto
{
    public Guid Id { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public decimal QuantityChange { get; set; }
    public decimal QuantityAfter { get; set; }
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class UsagePredictionDto
{
    public Guid Id { get; set; }
    public Guid? ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal PredictedUsagePerWeek { get; set; }
    public decimal ConfidenceScore { get; set; }
    public decimal? ReorderThreshold { get; set; }
    public decimal? SuggestedQuantity { get; set; }
    public DateTime CalculatedAt { get; set; }
}
