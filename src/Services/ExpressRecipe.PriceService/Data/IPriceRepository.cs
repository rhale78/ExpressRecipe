namespace ExpressRecipe.PriceService.Data;

public interface IPriceRepository
{
    // Stores
    Task<Guid> AddStoreAsync(string name, string? address, string? city, string? state, string? zipCode, string? chain);
    Task<List<StoreDto>> GetStoresAsync(string? city = null, string? state = null, string? chain = null);
    Task<StoreDto?> GetStoreAsync(Guid storeId);

    // ProductPrice search/query
    Task<List<ProductPriceDto>> SearchPricesAsync(PriceSearchRequest request);
    Task<int> GetSearchCountAsync(PriceSearchRequest request);
    Task<List<ProductPriceDto>> GetPricesByUpcAsync(string upc, int limit = 50);
    Task<List<ProductPriceDto>> GetPricesByProductNameAsync(string productName, int limit = 50);
    Task<List<ProductPriceDto>> GetBestPricesAsync(Guid productId, int limit = 10);
    Task<List<ProductPriceDto>> GetBatchPricesAsync(IEnumerable<Guid> productIds);
    Task<Guid> UpsertProductPriceAsync(UpsertProductPriceRequest request);
    Task<int> BulkUpsertProductPricesAsync(IEnumerable<UpsertProductPriceRequest> prices);
    Task<PriceImportLogDto> LogImportAsync(PriceImportLogDto log);
    Task<PriceImportLogDto?> GetLastImportAsync(string dataSource);
    Task<int> GetProductPriceCountAsync();

    // Price Observations
    Task<Guid> RecordPriceAsync(Guid productId, Guid storeId, decimal price, Guid? userId, DateTime? observedAt);
    Task<List<PriceObservationDto>> GetProductPricesAsync(Guid productId, Guid? storeId = null, int daysBack = 90);
    Task<PriceTrendDto> GetPriceTrendAsync(Guid productId, Guid? storeId = null);

    // Deals
    Task<Guid> CreateDealAsync(Guid productId, Guid storeId, string dealType, decimal originalPrice, decimal salePrice, DateTime startDate, DateTime endDate);
    Task<List<DealDto>> GetActiveDealsAsync(Guid? storeId = null, Guid? productId = null);
    Task<List<DealDto>> GetDealsNearMeAsync(string city, string state, int limit = 50);

    // Price Predictions
    Task<Guid> SavePricePredictionAsync(Guid productId, Guid storeId, decimal predictedPrice, decimal confidence, DateTime predictedFor);
    Task<PricePredictionDto?> GetPricePredictionAsync(Guid productId, Guid storeId);

    // Price Comparisons
    Task<List<StorePriceComparisonDto>> ComparePricesAsync(List<Guid> productIds, List<Guid> storeIds);
}

public class StoreDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
    public string? Chain { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PriceObservationDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public Guid StoreId { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public Guid? UserId { get; set; }
    public DateTime ObservedAt { get; set; }
}

public class PriceTrendDto
{
    public Guid ProductId { get; set; }
    public Guid? StoreId { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal AveragePrice { get; set; }
    public decimal MinPrice { get; set; }
    public decimal MaxPrice { get; set; }
    public decimal PriceChange30Days { get; set; }
    public string Trend { get; set; } = string.Empty;
}

public class DealDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public Guid StoreId { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public string DealType { get; set; } = string.Empty;
    public decimal OriginalPrice { get; set; }
    public decimal SalePrice { get; set; }
    public decimal SavingsPercent { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

public class PricePredictionDto
{
    public Guid ProductId { get; set; }
    public Guid StoreId { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal PredictedPrice { get; set; }
    public decimal Confidence { get; set; }
    public DateTime PredictedFor { get; set; }
    public string Recommendation { get; set; } = string.Empty;
}

public class StorePriceComparisonDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public List<StorePrice> Stores { get; set; } = new();
    public Guid? BestPriceStoreId { get; set; }
}

public class StorePrice
{
    public Guid StoreId { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public DateTime PriceDate { get; set; }
}

public class ProductPriceDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string? Upc { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public Guid? StoreId { get; set; }
    public string? StoreName { get; set; }
    public string? StoreChain { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = "USD";
    public string? Unit { get; set; }
    public decimal? Quantity { get; set; }
    public decimal? PricePerUnit { get; set; }
    public string DataSource { get; set; } = string.Empty;
    public string? ExternalId { get; set; }
    public DateTime ObservedAt { get; set; }
    public DateTime ImportedAt { get; set; }
}

public class PriceSearchRequest
{
    public Guid? ProductId { get; set; }
    public string? Upc { get; set; }
    public string? ProductName { get; set; }
    public string? StoreName { get; set; }
    public string? StoreChain { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? DataSource { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public int DaysBack { get; set; } = 90;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public class UpsertProductPriceRequest
{
    public Guid ProductId { get; set; }
    public string? Upc { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public Guid? StoreId { get; set; }
    public string? StoreName { get; set; }
    public string? StoreChain { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = "USD";
    public string? Unit { get; set; }
    public decimal? Quantity { get; set; }
    public string DataSource { get; set; } = string.Empty;
    public string? ExternalId { get; set; }
    public DateTime ObservedAt { get; set; }
}

public class PriceImportLogDto
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
