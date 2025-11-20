namespace ExpressRecipe.PriceService.Data;

public interface IPriceRepository
{
    // Stores
    Task<Guid> AddStoreAsync(string name, string? address, string? city, string? state, string? zipCode, string? chain);
    Task<List<StoreDto>> GetStoresAsync(string? city = null, string? state = null, string? chain = null);
    Task<StoreDto?> GetStoreAsync(Guid storeId);

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
