namespace ExpressRecipe.GroceryStoreLocationService.Data;

public interface IGroceryStoreRepository
{
    Task<GroceryStoreDto?> GetByIdAsync(Guid id);
    Task<GroceryStoreDto?> GetByExternalIdAsync(string externalId, string dataSource);
    Task<GroceryStoreDto?> GetByOsmIdAsync(long osmId);
    Task<GroceryStoreDto?> GetByGersIdAsync(string gersId);
    Task<List<GroceryStoreDto>> SearchAsync(GroceryStoreSearchRequest request);
    Task<int> GetSearchCountAsync(GroceryStoreSearchRequest request);
    Task<List<GroceryStoreDto>> GetNearbyAsync(double latitude, double longitude, double radiusMiles, int limit = 50);
    Task<List<GroceryStoreDto>> GetByChainAsync(string normalizedChain, int limit = 100);
    Task<Guid> UpsertAsync(UpsertGroceryStoreRequest request);
    Task<int> BulkUpsertAsync(IEnumerable<UpsertGroceryStoreRequest> stores);
    Task<int> MarkVerifiedAsync(Guid storeId, string verifiedSource);
    Task<List<StoreHoursDto>> GetStoreHoursAsync(Guid storeId);
    Task<int> UpsertStoreHoursAsync(Guid storeId, IEnumerable<StoreHoursRequest> hours);
    Task<List<StoreChainDto>> GetAllChainsAsync();
    Task<StoreImportLogDto> LogImportAsync(StoreImportLogDto log);
    Task<StoreImportLogDto?> GetLastImportAsync(string dataSource);
    Task<int> GetStoreCountAsync();
}

public class GroceryStoreDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Chain { get; set; }
    public string? NormalizedChain { get; set; }
    public string? StoreType { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
    public string? County { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Website { get; set; }
    public string? ExternalId { get; set; }
    public string? DataSource { get; set; }
    public long? OsmId { get; set; }
    public string? GersId { get; set; }
    public string? SnapStoreId { get; set; }
    public string? HifldId { get; set; }
    public bool AcceptsSnap { get; set; }
    public bool IsActive { get; set; }
    public bool IsOnline { get; set; }
    public bool DeliveryAvailable { get; set; }
    public bool PickupAvailable { get; set; }
    public decimal? BaseDeliveryFee { get; set; }
    public decimal? FreeDeliveryMin { get; set; }
    public decimal? AvgDeliveryDays { get; set; }
    public bool IsVerified { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public string? VerifiedSource { get; set; }
    public string? OpeningHours { get; set; }
    public double? DistanceMiles { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class GroceryStoreSearchRequest
{
    public string? Name { get; set; }
    public string? Chain { get; set; }
    public string? NormalizedChain { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
    public string? StoreType { get; set; }
    public bool? AcceptsSnap { get; set; }
    public bool? IsActive { get; set; } = true;
    public bool? IsVerified { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public class UpsertGroceryStoreRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Chain { get; set; }
    public string? NormalizedChain { get; set; }
    public string? StoreType { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
    public string? County { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Website { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public string DataSource { get; set; } = string.Empty;
    public long? OsmId { get; set; }
    public string? GersId { get; set; }
    public string? SnapStoreId { get; set; }
    public string? HifldId { get; set; }
    public bool AcceptsSnap { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsOnline { get; set; }
    public bool DeliveryAvailable { get; set; }
    public bool PickupAvailable { get; set; }
    public string? OpeningHours { get; set; }
}

public class StoreHoursDto
{
    public Guid Id { get; set; }
    public Guid StoreId { get; set; }
    public byte DayOfWeek { get; set; }
    public TimeSpan? OpenTime { get; set; }
    public TimeSpan? CloseTime { get; set; }
    public bool IsClosed { get; set; }
    public bool IsHoliday { get; set; }
    public DateTime? HolidayDate { get; set; }
}

public class StoreHoursRequest
{
    public byte DayOfWeek { get; set; }
    public TimeSpan? OpenTime { get; set; }
    public TimeSpan? CloseTime { get; set; }
    public bool IsClosed { get; set; }
    public bool IsHoliday { get; set; }
    public DateTime? HolidayDate { get; set; }
}

public class StoreChainDto
{
    public Guid Id { get; set; }
    public string CanonicalName { get; set; } = string.Empty;
    public string? Aliases { get; set; }
    public string? LogoUrl { get; set; }
    public string? Website { get; set; }
    public bool IsNational { get; set; }
    public bool IsOnlineOnly { get; set; }
}

public class StoreImportLogDto
{
    public Guid Id { get; set; }
    public string DataSource { get; set; } = string.Empty;
    public DateTime ImportedAt { get; set; }
    public int RecordsProcessed { get; set; }
    public int RecordsImported { get; set; }
    public int RecordsUpdated { get; set; }
    public int RecordsSkipped { get; set; }
    public int ErrorCount { get; set; }
    public string? ErrorMessage { get; set; }
    public bool Success { get; set; }
}
