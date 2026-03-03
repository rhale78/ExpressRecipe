namespace ExpressRecipe.GroceryStoreLocationService.Data;

public interface IGroceryStoreRepository
{
    Task<GroceryStoreDto?> GetByIdAsync(Guid id);
    Task<GroceryStoreDto?> GetByExternalIdAsync(string externalId, string dataSource);
    Task<List<GroceryStoreDto>> SearchAsync(GroceryStoreSearchRequest request);
    Task<int> GetSearchCountAsync(GroceryStoreSearchRequest request);
    Task<List<GroceryStoreDto>> GetNearbyAsync(double latitude, double longitude, double radiusMiles, int limit = 50);
    Task<Guid> UpsertAsync(UpsertGroceryStoreRequest request);
    Task<int> BulkUpsertAsync(IEnumerable<UpsertGroceryStoreRequest> stores);
    Task<StoreImportLogDto> LogImportAsync(StoreImportLogDto log);
    Task<StoreImportLogDto?> GetLastImportAsync(string dataSource);
    Task<int> GetStoreCountAsync();
}

public class GroceryStoreDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Chain { get; set; }
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
    public bool AcceptsSnap { get; set; }
    public bool IsActive { get; set; }
    public string? OpeningHours { get; set; }
    public double? DistanceMiles { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class GroceryStoreSearchRequest
{
    public string? Name { get; set; }
    public string? Chain { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
    public string? StoreType { get; set; }
    public bool? AcceptsSnap { get; set; }
    public bool? IsActive { get; set; } = true;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public class UpsertGroceryStoreRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Chain { get; set; }
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
    public bool AcceptsSnap { get; set; }
    public bool IsActive { get; set; } = true;
    public string? OpeningHours { get; set; }
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
