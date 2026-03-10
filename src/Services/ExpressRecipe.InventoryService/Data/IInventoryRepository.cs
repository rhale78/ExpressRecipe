namespace ExpressRecipe.InventoryService.Data;

public interface IInventoryRepository
{
    // Household Management
    Task<Guid> CreateHouseholdAsync(Guid userId, string name, string? description);
    Task<List<HouseholdDto>> GetUserHouseholdsAsync(Guid userId);
    Task<HouseholdDto?> GetHouseholdByIdAsync(Guid householdId);
    Task<bool> IsUserMemberOfHouseholdAsync(Guid householdId, Guid userId);
    Task<Guid> AddHouseholdMemberAsync(Guid householdId, Guid userId, string role, Guid invitedBy);
    Task<List<HouseholdMemberDto>> GetHouseholdMembersAsync(Guid householdId);
    Task UpdateMemberPermissionsAsync(Guid memberId, bool canManageInventory, bool canManageShopping, bool canManageMembers);
    Task RemoveHouseholdMemberAsync(Guid memberId);

    // Address Management (Main Locations)
    Task<Guid> CreateAddressAsync(Guid householdId, string name, string street, string city, string state, string zipCode, string country, decimal? latitude, decimal? longitude, bool isPrimary);
    Task<List<AddressDto>> GetHouseholdAddressesAsync(Guid householdId);
    Task<AddressDto?> GetAddressByIdAsync(Guid addressId);
    Task<AddressDto?> DetectNearestAddressAsync(Guid householdId, decimal latitude, decimal longitude, double maxDistanceKm = 1.0);
    Task UpdateAddressCoordinatesAsync(Guid addressId, decimal latitude, decimal longitude);
    Task SetPrimaryAddressAsync(Guid householdId, Guid addressId);
    Task DeleteAddressAsync(Guid addressId);

    // Storage Locations (Sub-areas within addresses)
    Task<Guid> CreateStorageLocationAsync(Guid userId, Guid? householdId, Guid? addressId, string name, string? description, string? temperature);
    Task<List<StorageLocationDto>> GetStorageLocationsAsync(Guid userId);
    Task<List<StorageLocationDto>> GetStorageLocationsByAddressAsync(Guid addressId);
    Task<List<StorageLocationDto>> GetStorageLocationsByHouseholdAsync(Guid householdId);
    Task<StorageLocationDto?> GetStorageLocationByIdAsync(Guid id);
    Task UpdateStorageLocationAsync(Guid locationId, string name, string? description, string? temperature, Guid? addressId);
    Task DeleteStorageLocationAsync(Guid locationId);

    // Inventory Items
    Task<Guid> AddInventoryItemAsync(Guid userId, Guid? householdId, Guid? productId, string? customName, Guid storageLocationId,
        decimal quantity, string? unit, DateTime? expirationDate, string? barcode, decimal? price = null, string? preferredStore = null, string? storeLocation = null);
    Task<List<InventoryItemDto>> GetUserInventoryAsync(Guid userId);
    Task<List<InventoryItemDto>> GetHouseholdInventoryAsync(Guid householdId);
    Task<List<InventoryItemDto>> GetInventoryByAddressAsync(Guid addressId);
    Task<List<InventoryItemDto>> GetInventoryByStorageLocationAsync(Guid storageLocationId);
    Task<InventoryItemDto?> GetInventoryItemAsync(Guid itemId, Guid userId);
    Task UpdateInventoryQuantityAsync(Guid itemId, decimal newQuantity, string actionType, Guid changedBy, string? reason = null, string? disposalReason = null, string? allergenDetected = null);
    Task DeleteInventoryItemAsync(Guid itemId, Guid userId);
    Task<List<InventoryItemDto>> GetExpiringItemsAsync(Guid userId, int daysAhead = 7);
    Task<List<InventoryItemDto>> GetLowStockItemsAsync(Guid userId, decimal threshold = 2.0m);
    Task<List<InventoryItemDto>> GetItemsRunningOutAsync(Guid userId, int withinDays = 7);

    // Scanning Sessions
    Task<Guid> StartScanSessionAsync(Guid userId, Guid? householdId, string sessionType, Guid? storageLocationId);
    Task<ScanSessionDto?> GetActiveScanSessionAsync(Guid userId);
    Task<ScanSessionDto?> GetScanSessionByIdAsync(Guid sessionId);
    Task UpdateScanSessionItemCountAsync(Guid sessionId, int itemsScanned);
    Task EndScanSessionAsync(Guid sessionId);
    Task<Guid> ScanAddItemAsync(Guid sessionId, string barcode, decimal quantity, Guid storageLocationId);
    Task<Guid> ScanUseItemAsync(Guid sessionId, string barcode, decimal quantity);
    Task<Guid> ScanDisposeItemAsync(Guid sessionId, string barcode, string disposalReason, string? allergenDetected);

    // Allergen Discovery
    Task<Guid> CreateAllergenDiscoveryAsync(Guid userId, Guid? householdId, Guid inventoryHistoryId, Guid? productId, string allergenName, string severity, string? notes);
    Task<List<AllergenDiscoveryDto>> GetAllergenDiscoveriesAsync(Guid userId);
    Task MarkAllergenAddedToProfileAsync(Guid discoveryId);

    // Expiration Alerts
    Task CreateExpirationAlertsAsync(Guid userId);
    Task CreateSingleExpirationAlertAsync(Guid userId, Guid inventoryItemId, string alertType, int daysUntilExpiration);
    Task<List<ExpirationAlertDto>> GetActiveAlertsAsync(Guid userId);
    Task DismissAlertAsync(Guid alertId);

    // Usage History
    Task<List<InventoryHistoryDto>> GetUsageHistoryAsync(Guid itemId, int limit = 50);
    Task<List<UsagePredictionDto>> GetUsagePredictionsAsync(Guid userId);

    // Reports
    Task<InventoryReportDto> GetInventoryReportAsync(Guid userId, Guid? householdId);
    Task<List<InventoryItemDto>> GetItemsAboutToExpireAsync(Guid userId, int daysAhead = 3);

    // Purchase Events
    Task<Guid> RecordPurchaseEventAsync(PurchaseEventRecord record, CancellationToken ct = default);
    Task<List<PurchaseEventDto>> GetPurchaseHistoryAsync(Guid userId, Guid? productId, int daysBack, CancellationToken ct = default);

    // Consumption Patterns
    Task UpsertConsumptionPatternAsync(ProductConsumptionPatternRecord pattern, CancellationToken ct = default);
    Task<List<ProductConsumptionPatternDto>> GetConsumptionPatternsAsync(Guid userId, CancellationToken ct = default);
    Task<List<ProductConsumptionPatternDto>> GetAbandonedProductsAsync(Guid userId, CancellationToken ct = default);
    Task<List<ProductConsumptionPatternDto>> GetLowStockByPredictionAsync(Guid userId, int daysAhead, CancellationToken ct = default);

    // Price Watch
    Task<Guid> CreatePriceWatchAlertAsync(PriceWatchAlertRecord record, CancellationToken ct = default);
    Task<List<PriceWatchAlertDto>> GetActiveWatchAlertsAsync(CancellationToken ct = default);
    Task<List<PriceWatchAlertDto>> GetActiveWatchAlertsByUserAsync(Guid userId, CancellationToken ct = default);
    Task UpdatePriceWatchDealFoundAsync(Guid alertId, Guid storeId, decimal dealPrice, DateTime dealEndsAt, CancellationToken ct = default);
    Task ResolvePriceWatchAlertAsync(Guid alertId, CancellationToken ct = default);
    Task SetPriceWatchTargetPriceAsync(Guid userId, Guid alertId, decimal targetPrice, CancellationToken ct = default);

    // Abandoned Product Inquiry
    Task<Guid> CreateAbandonedInquiryAsync(Guid userId, Guid? productId, string? customName, CancellationToken ct = default);
    Task RecordInquiryResponseAsync(Guid userId, Guid inquiryId, string response, string? note, CancellationToken ct = default);
    Task<List<AbandonedProductInquiryDto>> GetPendingInquiriesAsync(Guid userId, CancellationToken ct = default);

    // Waste Report
    Task<List<WasteReportMonthDto>> GetWasteReportAsync(Guid userId, Guid? householdId, CancellationToken ct = default);

    // Intelligence helpers
    Task<List<Guid>> GetDistinctUserIdsWithInventoryAsync(CancellationToken ct = default);
    Task<List<Guid>> GetDistinctUserIdsWithPurchaseHistoryAsync(CancellationToken ct = default);
    Task WriteInventoryHistoryDirectAsync(Guid itemId, Guid userId, string actionType, decimal quantityChange, decimal quantityBefore, decimal quantityAfter, string? reason, Guid? recipeId, CancellationToken ct = default);

    // Garden & Long-Term Storage
    Task<Guid> CreateFromGardenHarvestAsync(Guid userId, Guid householdId, string plantName, decimal quantity, string unit, int freshnessDays, CancellationToken ct = default);
    Task<Guid> AddItemAsync(AddItemRequest request, CancellationToken ct = default);
    Task<List<InventoryItemDto>> GetItemsAsync(Guid householdId, bool isLongTermOnly = false, string? storageMethod = null, CancellationToken ct = default);

    // Thaw task support
    Task<List<FrozenIngredientResult>> GetFrozenIngredientsForRecipeAsync(Guid householdId, Guid recipeId, CancellationToken ct = default);

    // Allergy analysis support
    Task<List<SafeProductUsageResult>> GetSafeProductHistoryAsync(
        Guid householdId, int minUsageCount, CancellationToken ct = default);
}

public class FrozenIngredientResult
{
    public string ItemName { get; set; } = string.Empty;
    public string FoodCategory { get; set; } = string.Empty;
    public Guid StorageLocationId { get; set; }
}

public sealed record SafeProductUsageResult
{
    public Guid ProductId  { get; init; }
    public int  UsageCount { get; init; }
}

public class HouseholdDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public int MemberCount { get; set; }
    public int AddressCount { get; set; }
}

public class HouseholdMemberDto
{
    public Guid Id { get; set; }
    public Guid HouseholdId { get; set; }
    public Guid UserId { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool CanManageInventory { get; set; }
    public bool CanManageShopping { get; set; }
    public bool CanManageMembers { get; set; }
    public DateTime JoinedAt { get; set; }
    public bool IsActive { get; set; }
}

public class AddressDto
{
    public Guid Id { get; set; }
    public Guid HouseholdId { get; set; }
    public string Name { get; set; } = string.Empty; // "Main House", "Vacation Home", "Office"
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public bool IsPrimary { get; set; }
    public DateTime CreatedAt { get; set; }
    public int StorageLocationCount { get; set; }
    public int ItemCount { get; set; }
    public double? DistanceKm { get; set; } // Calculated distance from current location
}

public class StorageLocationDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? HouseholdId { get; set; }
    public Guid? AddressId { get; set; }
    public string? AddressName { get; set; } // "Main House", "Vacation Home"
    public string Name { get; set; } = string.Empty; // "Fridge", "Pantry", "Freezer"
    public string? Description { get; set; }
    public string? Temperature { get; set; }
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; }
    public int ItemCount { get; set; }
}

public class InventoryItemDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? HouseholdId { get; set; }
    public Guid? ProductId { get; set; }
    public string? ProductName { get; set; }
    public string? CustomName { get; set; }
    public Guid StorageLocationId { get; set; }
    public string StorageLocationName { get; set; } = string.Empty;
    public Guid? AddressId { get; set; }
    public string? AddressName { get; set; }
    public decimal Quantity { get; set; }
    public string? Unit { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public DateTime? OpenedDate { get; set; }
    public bool IsOpened { get; set; }
    public string? Barcode { get; set; }
    public decimal? Price { get; set; }
    public string? Store { get; set; }
    public string? PreferredStore { get; set; }
    public string? StoreLocation { get; set; }
    public string? Notes { get; set; }
    public Guid? AddedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    // Long-term storage fields (added by migration 006)
    public string? StorageMethod { get; set; }
    public bool IsLongTermStorage { get; set; }
    public string? StorageCapacityUnit { get; set; }
    public string? BatchLabel { get; set; }
    public string? Source { get; set; }
    public string? Temperature { get; set; }
}

public class ExpirationAlertDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid InventoryItemId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string AlertType { get; set; } = string.Empty;
    public int DaysUntilExpiration { get; set; }
    public bool IsDismissed { get; set; }
    public DateTime? DismissedAt { get; set; }
    public string? ItemName { get; set; }
    public Guid? ProductId { get; set; }
    public DateTime AlertDate { get; set; }
}

public class InventoryHistoryDto
{
    public Guid Id { get; set; }
    public Guid InventoryItemId { get; set; }
    public Guid UserId { get; set; }
    public Guid? ChangedBy { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public decimal QuantityChange { get; set; }
    public decimal QuantityBefore { get; set; }
    public decimal QuantityAfter { get; set; }
    public string? Reason { get; set; }
    public string? DisposalReason { get; set; } // Bad, CausedAllergy, Expired, Other
    public string? AllergenDetected { get; set; }
    public Guid? RecipeId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AllergenDiscoveryDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? HouseholdId { get; set; }
    public Guid InventoryHistoryId { get; set; }
    public Guid? ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string AllergenName { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public bool AddedToProfile { get; set; }
    public DateTime? AddedToProfileAt { get; set; }
    public string? Notes { get; set; }
    public DateTime DiscoveredAt { get; set; }
}

public class ScanSessionDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? HouseholdId { get; set; }
    public string SessionType { get; set; } = string.Empty; // Adding, Using, Disposing, Purchasing
    public Guid? StorageLocationId { get; set; }
    public string? StorageLocationName { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public int ItemsScanned { get; set; }
    public bool IsActive { get; set; }
}

public class InventoryReportDto
{
    public int TotalItems { get; set; }
    public int ExpiringSoonItems { get; set; }
    public int ExpiredItems { get; set; }
    public int LowStockItems { get; set; }
    public int RunningOutItems { get; set; }
    public Dictionary<string, int> ItemsByLocation { get; set; } = new();
    public Dictionary<string, int> ItemsByAddress { get; set; } = new();
    public decimal TotalEstimatedValue { get; set; }
}

public class UsagePredictionDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? ProductId { get; set; }
    public Guid? IngredientId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal PredictedUsagePerWeek { get; set; }
    public decimal ConfidenceScore { get; set; }
    public decimal? ReorderThreshold { get; set; }
    public decimal? SuggestedQuantity { get; set; }
    public int BasedOnDays { get; set; }
    public DateTime CalculatedAt { get; set; }
}

public class PurchaseEventRecord
{
    public Guid UserId { get; set; }
    public Guid? HouseholdId { get; set; }
    public Guid? ProductId { get; set; }
    public Guid? IngredientId { get; set; }
    public string? CustomName { get; set; }
    public string? Barcode { get; set; }
    public decimal Quantity { get; set; } = 1;
    public string? Unit { get; set; }
    public decimal? Price { get; set; }
    public Guid? StoreId { get; set; }
    public string? StoreName { get; set; }
    public DateTime PurchasedAt { get; set; } = DateTime.UtcNow;
    public string Source { get; set; } = "ManualAdd";
}

public class PurchaseEventDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? HouseholdId { get; set; }
    public Guid? ProductId { get; set; }
    public Guid? IngredientId { get; set; }
    public string? CustomName { get; set; }
    public string? Barcode { get; set; }
    public decimal Quantity { get; set; }
    public string? Unit { get; set; }
    public decimal? Price { get; set; }
    public Guid? StoreId { get; set; }
    public string? StoreName { get; set; }
    public DateTime PurchasedAt { get; set; }
    public string Source { get; set; } = string.Empty;
}

public class ProductConsumptionPatternRecord
{
    public Guid UserId { get; set; }
    public Guid? HouseholdId { get; set; }
    public Guid? ProductId { get; set; }
    public Guid? IngredientId { get; set; }
    public string? CustomName { get; set; }
    public decimal? AvgDaysBetweenPurchases { get; set; }
    public decimal? StdDevDays { get; set; }
    public int PurchaseCount { get; set; }
    public DateTime? FirstPurchasedAt { get; set; }
    public DateTime? LastPurchasedAt { get; set; }
    public DateTime? EstimatedNextPurchaseDate { get; set; }
    public int LowStockAlertDaysAhead { get; set; } = 3;
    public bool IsAbandoned { get; set; }
    public int? AbandonedAfterCount { get; set; }
}

public class ProductConsumptionPatternDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? HouseholdId { get; set; }
    public Guid? ProductId { get; set; }
    public Guid? IngredientId { get; set; }
    public string? CustomName { get; set; }
    public decimal? AvgDaysBetweenPurchases { get; set; }
    public decimal? StdDevDays { get; set; }
    public int PurchaseCount { get; set; }
    public DateTime? FirstPurchasedAt { get; set; }
    public DateTime? LastPurchasedAt { get; set; }
    public DateTime? EstimatedNextPurchaseDate { get; set; }
    public int LowStockAlertDaysAhead { get; set; }
    public bool IsAbandoned { get; set; }
    public int? AbandonedAfterCount { get; set; }
    public DateTime CalculatedAt { get; set; }
}

public class PriceWatchAlertRecord
{
    public Guid UserId { get; set; }
    public Guid? HouseholdId { get; set; }
    public Guid? ProductId { get; set; }
    public Guid? InventoryItemId { get; set; }
    public decimal? TargetPrice { get; set; }
}

public class PriceWatchAlertDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? HouseholdId { get; set; }
    public Guid? ProductId { get; set; }
    public Guid? InventoryItemId { get; set; }
    public decimal? TargetPrice { get; set; }
    public DateTime WatchStartedAt { get; set; }
    public DateTime? AlertSentAt { get; set; }
    public bool DealFound { get; set; }
    public Guid? DealStoreId { get; set; }
    public decimal? DealPrice { get; set; }
    public DateTime? DealEndsAt { get; set; }
    public bool IsResolved { get; set; }
    public DateTime? ResolvedAt { get; set; }
}

public class AbandonedProductInquiryDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? ProductId { get; set; }
    public string? CustomName { get; set; }
    public DateTime NotificationSentAt { get; set; }
    public string? Response { get; set; }
    public string? ResponseNote { get; set; }
    public DateTime? RespondedAt { get; set; }
    public bool IsActioned { get; set; }
}

public class WasteReportMonthDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public int ExpiredItemsDisposed { get; set; }
    public int AllergyDisposed { get; set; }
    public int BadDisposed { get; set; }
    public int OtherDisposed { get; set; }
    public decimal TotalDisposedValue { get; set; }
}

public class AddItemRequest
{
    public Guid UserId { get; set; }
    public Guid? HouseholdId { get; set; }
    public Guid? ProductId { get; set; }
    public string? Name { get; set; }
    public decimal Quantity { get; set; }
    public string? Unit { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public string? StorageMethod { get; set; }
    public bool IsLongTermStorage { get; set; }
    public string? StorageCapacityUnit { get; set; }
    public string? BatchLabel { get; set; }
    public string? Source { get; set; }
    public string? Temperature { get; set; }
}
