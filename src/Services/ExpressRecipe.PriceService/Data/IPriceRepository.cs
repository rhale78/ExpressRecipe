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

    // Product lifecycle reactions (called by ProductEventSubscriber)

    /// <summary>
    /// Mark all price rows for the given product as inactive when the product is deleted.
    /// </summary>
    Task<int> DeactivatePricesByProductIdAsync(Guid productId, CancellationToken ct = default);

    /// <summary>
    /// Update the denormalised ProductName on ProductPrice rows when a product is renamed.
    /// </summary>
    Task<int> UpdateProductNameOnPricesAsync(Guid productId, string newName, CancellationToken ct = default);

    /// <summary>
    /// Update the denormalised Upc on ProductPrice rows when a product's barcode changes.
    /// </summary>
    Task<int> UpdateProductUpcOnPricesAsync(Guid productId, string? newUpc, CancellationToken ct = default);

    // Price History (append-only — never upsert)
    Task RecordPriceHistoryAsync(PriceHistoryRecord record, CancellationToken ct = default);
    Task BulkInsertPriceHistoryAsync(IEnumerable<PriceHistoryRecord> records, CancellationToken ct = default);
    Task<List<PriceHistoryRecord>> GetPriceHistoryAsync(Guid productId, Guid? storeId, int daysBack, CancellationToken ct = default);
    Task<PriceHistoryStatsDto> GetPriceStatsAsync(Guid productId, Guid? storeId, int daysBack, CancellationToken ct = default);

    // Unit-price comparison across products (e.g. 12-pack cans vs 2-liter)
    Task<List<UnitPriceComparisonDto>> CompareByUnitAsync(IEnumerable<Guid> productIds, string targetUnit, CancellationToken ct = default);

    // Store-product linking
    Task UpsertStoreProductLinkAsync(Guid storeId, Guid productId, string? upc, string dataSource, CancellationToken ct = default);
    Task<List<StoreProductLinkDto>> GetStoresForProductAsync(Guid productId, CancellationToken ct = default);
    Task<List<StoreProductLinkDto>> GetProductsForStoreAsync(Guid storeId, int page, int pageSize, CancellationToken ct = default);

    // Enhanced deals
    Task<Guid> CreateEnhancedDealAsync(CreateEnhancedDealRequest request, CancellationToken ct = default);
    Task<EffectivePriceDto> CalculateEffectivePriceAsync(Guid productId, Guid storeId, int quantity, CancellationToken ct = default);
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
    public bool IsOnline { get; set; }
    public decimal? BaseDeliveryFee { get; set; }
    public decimal? FreeDeliveryMin { get; set; }
    public decimal? AvgDeliveryDays { get; set; }
    public string? ShippingNotes { get; set; }
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
    // Enhanced deal fields (from migration 004)
    public string? DiscountType { get; set; }
    public int? BuyQuantity { get; set; }
    public int? GetQuantity { get; set; }
    public decimal? GetPercentOff { get; set; }
    public string? CouponCode { get; set; }
    public decimal? RebateAmount { get; set; }
    public string? FlyerSource { get; set; }
    public string? FlyerPageRef { get; set; }
    public bool IsDigital { get; set; }
    public bool IsStackable { get; set; }
    public int? MaxPerTransaction { get; set; }
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

/// <summary>
/// Append-only record representing a single observed price event.
/// Computed fields (PricePerOz, PricePerHundredG) are set by IPriceUnitNormalizer before insert.
/// </summary>
public class PriceHistoryRecord
{
    public Guid Id { get; init; }
    public Guid ProductId { get; init; }
    public string? Upc { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public Guid? StoreId { get; init; }
    public string? StoreName { get; init; }
    public string? StoreChain { get; init; }
    public bool IsOnline { get; init; }
    public decimal BasePrice { get; init; }
    public decimal FinalPrice { get; init; }
    public string Currency { get; init; } = "USD";
    public string? Unit { get; init; }
    public decimal? Quantity { get; init; }
    public string DataSource { get; init; } = string.Empty;
    public string? ExternalId { get; init; }
    public DateTimeOffset ObservedAt { get; init; }
    public DateTime ImportedAt { get; init; }
    // Computed by IPriceUnitNormalizer before insert
    public decimal? PricePerOz { get; set; }
    public decimal? PricePerHundredG { get; set; }
}

public class PriceHistoryStatsDto
{
    public Guid ProductId { get; set; }
    public Guid? StoreId { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal AveragePrice { get; set; }
    public decimal MinPrice { get; set; }
    public decimal MaxPrice { get; set; }
    public decimal PriceChange30Days { get; set; }
    public decimal? PriceChange30DaysPct { get; set; }
    public string Trend { get; set; } = "Stable";
    public int ObservationCount { get; set; }
    public DateTime? OldestObservation { get; set; }
    public DateTime? NewestObservation { get; set; }
    public decimal? AvgPricePerOz { get; set; }
    public decimal? AvgPricePerHundredG { get; set; }
}

public class UnitPriceComparisonDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Upc { get; set; }
    public string? StoreName { get; set; }
    public decimal BasePrice { get; set; }
    public decimal FinalPrice { get; set; }
    public string? OriginalUnit { get; set; }
    public decimal? OriginalQuantity { get; set; }
    public string TargetUnit { get; set; } = string.Empty;
    public decimal? PricePerTargetUnit { get; set; }
    public decimal? PricePerOz { get; set; }
    public decimal? PricePerHundredG { get; set; }
    public DateTime ObservedAt { get; set; }
}

public class StoreProductLinkDto
{
    public Guid Id { get; set; }
    public Guid StoreId { get; set; }
    public Guid ProductId { get; set; }
    public string? Upc { get; set; }
    public bool IsInStock { get; set; }
    public string? Aisle { get; set; }
    public DateTime LastSeenAt { get; set; }
    public Guid? LastPriceId { get; set; }
    public string DataSource { get; set; } = string.Empty;
}

public class EffectivePriceDto
{
    public Guid ProductId { get; set; }
    public Guid StoreId { get; set; }
    public int Quantity { get; set; }
    public decimal BasePrice { get; set; }
    public decimal EffectivePrice { get; set; }
    public decimal TotalCost { get; set; }
    public decimal Savings { get; set; }
    public decimal SavingsPct { get; set; }
    public string? AppliedDealType { get; set; }
    public string? CouponCode { get; set; }
}

public class CreateEnhancedDealRequest
{
    public Guid ProductId { get; set; }
    public Guid StoreId { get; set; }
    public string DealType { get; set; } = string.Empty;
    public string? DiscountType { get; set; }
    public decimal OriginalPrice { get; set; }
    public decimal SalePrice { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int? BuyQuantity { get; set; }
    public int? GetQuantity { get; set; }
    public decimal? GetPercentOff { get; set; }
    public string? CouponCode { get; set; }
    public decimal? RebateAmount { get; set; }
    public string? FlyerSource { get; set; }
    public string? FlyerPageRef { get; set; }
    public bool IsDigital { get; set; }
    public bool IsStackable { get; set; }
    public int? MaxPerTransaction { get; set; }
}

/// <summary>DTO shapes for the Blazor PriceHistoryChart component.</summary>
public class PriceChartDto
{
    public List<string> Labels { get; init; } = new();
    public List<PriceChartSeries> Series { get; init; } = new();
    public decimal? AveragePrice { get; init; }
    public decimal? MinPrice { get; init; }
    public decimal? MaxPrice { get; init; }
    public string? Trend { get; init; }
    public decimal? TrendPct { get; init; }
}

public class PriceChartSeries
{
    public string Label { get; init; } = string.Empty;
    public List<decimal?> Data { get; init; } = new();
    public string Color { get; init; } = string.Empty;
}
