using Microsoft.Data.SqlClient;

namespace ExpressRecipe.PriceService.Data;

public class PriceRepository : IPriceRepository
{
    private readonly string _connectionString;
    private readonly ILogger<PriceRepository> _logger;

    public PriceRepository(string connectionString, ILogger<PriceRepository> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task<Guid> AddStoreAsync(string name, string? address, string? city, string? state, string? zipCode, string? chain)
    {
        const string sql = @"
            INSERT INTO Store (Name, Address, City, State, ZipCode, Chain, CreatedAt)
            OUTPUT INSERTED.Id
            VALUES (@Name, @Address, @City, @State, @ZipCode, @Chain, GETUTCDATE())";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Name", name);
        command.Parameters.AddWithValue("@Address", address ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@City", city ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@State", state ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@ZipCode", zipCode ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Chain", chain ?? (object)DBNull.Value);

        return (Guid)await command.ExecuteScalarAsync()!;
    }

    public async Task<List<StoreDto>> GetStoresAsync(string? city = null, string? state = null, string? chain = null)
    {
        var sql = "SELECT Id, Name, Address, City, State, ZipCode, Chain, CreatedAt FROM Store WHERE 1=1";
        if (city != null) sql += " AND City = @City";
        if (state != null) sql += " AND State = @State";
        if (chain != null) sql += " AND Chain = @Chain";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        if (city != null) command.Parameters.AddWithValue("@City", city);
        if (state != null) command.Parameters.AddWithValue("@State", state);
        if (chain != null) command.Parameters.AddWithValue("@Chain", chain);

        var stores = new List<StoreDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            stores.Add(new StoreDto
            {
                Id = reader.GetGuid(0),
                Name = reader.GetString(1),
                Address = reader.IsDBNull(2) ? null : reader.GetString(2),
                City = reader.IsDBNull(3) ? null : reader.GetString(3),
                State = reader.IsDBNull(4) ? null : reader.GetString(4),
                ZipCode = reader.IsDBNull(5) ? null : reader.GetString(5),
                Chain = reader.IsDBNull(6) ? null : reader.GetString(6),
                CreatedAt = reader.GetDateTime(7)
            });
        }

        return stores;
    }

    public async Task<StoreDto?> GetStoreAsync(Guid storeId)
    {
        const string sql = "SELECT Id, Name, Address, City, State, ZipCode, Chain, CreatedAt FROM Store WHERE Id = @StoreId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@StoreId", storeId);

        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new StoreDto
            {
                Id = reader.GetGuid(0),
                Name = reader.GetString(1),
                Address = reader.IsDBNull(2) ? null : reader.GetString(2),
                City = reader.IsDBNull(3) ? null : reader.GetString(3),
                State = reader.IsDBNull(4) ? null : reader.GetString(4),
                ZipCode = reader.IsDBNull(5) ? null : reader.GetString(5),
                Chain = reader.IsDBNull(6) ? null : reader.GetString(6),
                CreatedAt = reader.GetDateTime(7)
            };
        }

        return null;
    }

    public async Task<Guid> RecordPriceAsync(Guid productId, Guid storeId, decimal price, Guid? userId, DateTime? observedAt)
    {
        const string sql = @"
            INSERT INTO PriceObservation (ProductId, StoreId, Price, UserId, ObservedAt)
            OUTPUT INSERTED.Id
            VALUES (@ProductId, @StoreId, @Price, @UserId, @ObservedAt)";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ProductId", productId);
        command.Parameters.AddWithValue("@StoreId", storeId);
        command.Parameters.AddWithValue("@Price", price);
        command.Parameters.AddWithValue("@UserId", userId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@ObservedAt", observedAt ?? DateTime.UtcNow);

        return (Guid)await command.ExecuteScalarAsync()!;
    }

    public async Task<List<PriceObservationDto>> GetProductPricesAsync(Guid productId, Guid? storeId = null, int daysBack = 90)
    {
        var sql = @"
            SELECT po.Id, po.ProductId, po.StoreId, s.Name AS StoreName, po.Price, po.UserId, po.ObservedAt
            FROM PriceObservation po
            INNER JOIN Store s ON po.StoreId = s.Id
            WHERE po.ProductId = @ProductId
              AND po.ObservedAt >= DATEADD(day, -@DaysBack, GETUTCDATE())";

        if (storeId.HasValue)
            sql += " AND po.StoreId = @StoreId";

        sql += " ORDER BY po.ObservedAt DESC";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ProductId", productId);
        command.Parameters.AddWithValue("@DaysBack", daysBack);
        if (storeId.HasValue)
            command.Parameters.AddWithValue("@StoreId", storeId.Value);

        var prices = new List<PriceObservationDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            prices.Add(new PriceObservationDto
            {
                Id = reader.GetGuid(0),
                ProductId = reader.GetGuid(1),
                StoreId = reader.GetGuid(2),
                StoreName = reader.GetString(3),
                Price = reader.GetDecimal(4),
                UserId = reader.IsDBNull(5) ? null : reader.GetGuid(5),
                ObservedAt = reader.GetDateTime(6)
            });
        }

        return prices;
    }

    public async Task<PriceTrendDto> GetPriceTrendAsync(Guid productId, Guid? storeId = null)
    {
        // Stub - would calculate from price history
        return new PriceTrendDto
        {
            ProductId = productId,
            StoreId = storeId,
            CurrentPrice = 0,
            AveragePrice = 0,
            MinPrice = 0,
            MaxPrice = 0,
            PriceChange30Days = 0,
            Trend = "Stable"
        };
    }

    public async Task<Guid> CreateDealAsync(Guid productId, Guid storeId, string dealType, decimal originalPrice, decimal salePrice, DateTime startDate, DateTime endDate)
    {
        const string sql = @"
            INSERT INTO Deal (ProductId, StoreId, DealType, OriginalPrice, SalePrice, SavingsPercent, StartDate, EndDate, CreatedAt)
            OUTPUT INSERTED.Id
            VALUES (@ProductId, @StoreId, @DealType, @OriginalPrice, @SalePrice, @SavingsPercent, @StartDate, @EndDate, GETUTCDATE())";

        var savingsPercent = originalPrice > 0 ? ((originalPrice - salePrice) / originalPrice) * 100 : 0;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ProductId", productId);
        command.Parameters.AddWithValue("@StoreId", storeId);
        command.Parameters.AddWithValue("@DealType", dealType);
        command.Parameters.AddWithValue("@OriginalPrice", originalPrice);
        command.Parameters.AddWithValue("@SalePrice", salePrice);
        command.Parameters.AddWithValue("@SavingsPercent", savingsPercent);
        command.Parameters.AddWithValue("@StartDate", startDate);
        command.Parameters.AddWithValue("@EndDate", endDate);

        return (Guid)await command.ExecuteScalarAsync()!;
    }

    public async Task<List<DealDto>> GetActiveDealsAsync(Guid? storeId = null, Guid? productId = null)
    {
        var sql = @"
            SELECT d.Id, d.ProductId, '' AS ProductName, d.StoreId, s.Name AS StoreName,
                   d.DealType, d.OriginalPrice, d.SalePrice, d.SavingsPercent, d.StartDate, d.EndDate
            FROM Deal d
            INNER JOIN Store s ON d.StoreId = s.Id
            WHERE GETUTCDATE() BETWEEN d.StartDate AND d.EndDate";

        if (storeId.HasValue) sql += " AND d.StoreId = @StoreId";
        if (productId.HasValue) sql += " AND d.ProductId = @ProductId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        if (storeId.HasValue) command.Parameters.AddWithValue("@StoreId", storeId.Value);
        if (productId.HasValue) command.Parameters.AddWithValue("@ProductId", productId.Value);

        var deals = new List<DealDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            deals.Add(new DealDto
            {
                Id = reader.GetGuid(0),
                ProductId = reader.GetGuid(1),
                ProductName = reader.GetString(2),
                StoreId = reader.GetGuid(3),
                StoreName = reader.GetString(4),
                DealType = reader.GetString(5),
                OriginalPrice = reader.GetDecimal(6),
                SalePrice = reader.GetDecimal(7),
                SavingsPercent = reader.GetDecimal(8),
                StartDate = reader.GetDateTime(9),
                EndDate = reader.GetDateTime(10)
            });
        }

        return deals;
    }

    public async Task<List<DealDto>> GetDealsNearMeAsync(string city, string state, int limit = 50)
    {
        // Stub - same as GetActiveDealsAsync but filtered by location
        return await GetActiveDealsAsync();
    }

    public async Task<Guid> SavePricePredictionAsync(Guid productId, Guid storeId, decimal predictedPrice, decimal confidence, DateTime predictedFor)
    {
        const string sql = @"
            INSERT INTO PricePrediction (ProductId, StoreId, PredictedPrice, Confidence, PredictedFor, CalculatedAt)
            OUTPUT INSERTED.Id
            VALUES (@ProductId, @StoreId, @PredictedPrice, @Confidence, @PredictedFor, GETUTCDATE())";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ProductId", productId);
        command.Parameters.AddWithValue("@StoreId", storeId);
        command.Parameters.AddWithValue("@PredictedPrice", predictedPrice);
        command.Parameters.AddWithValue("@Confidence", confidence);
        command.Parameters.AddWithValue("@PredictedFor", predictedFor);

        return (Guid)await command.ExecuteScalarAsync()!;
    }

    public async Task<PricePredictionDto?> GetPricePredictionAsync(Guid productId, Guid storeId)
    {
        // Stub implementation
        return null;
    }

    public async Task<List<StorePriceComparisonDto>> ComparePricesAsync(List<Guid> productIds, List<Guid> storeIds)
    {
        // Stub implementation - would compare prices across stores
        return new List<StorePriceComparisonDto>();
    }
}
