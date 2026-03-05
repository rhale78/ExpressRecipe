using ExpressRecipe.Data.Common;
using ExpressRecipe.Shared.Services;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Data.Common;
using System.Text;

namespace ExpressRecipe.PriceService.Data;

public class PriceRepository : SqlHelper, IPriceRepository
{
    private readonly HybridCacheService? _cache;
    private readonly ILogger<PriceRepository>? _logger;

    public PriceRepository(string connectionString, HybridCacheService? cache = null, ILogger<PriceRepository>? logger = null)
        : base(connectionString)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<Guid> AddStoreAsync(string name, string? address, string? city, string? state, string? zipCode, string? chain)
    {
        const string sql = @"
            INSERT INTO Store (Name, Address, City, State, ZipCode, Chain, CreatedAt)
            OUTPUT INSERTED.Id
            VALUES (@Name, @Address, @City, @State, @ZipCode, @Chain, GETUTCDATE())";

        var result = await ExecuteScalarAsync<Guid>(sql,
            CreateParameter("@Name", name),
            CreateParameter("@Address", address),
            CreateParameter("@City", city),
            CreateParameter("@State", state),
            CreateParameter("@ZipCode", zipCode),
            CreateParameter("@Chain", chain));

        return result;
    }

    public async Task<List<StoreDto>> GetStoresAsync(string? city = null, string? state = null, string? chain = null)
    {
        var sql = new StringBuilder("SELECT Id, Name, Address, City, State, ZipCode, Chain, CreatedAt FROM Store WHERE 1=1");
        var parameters = new List<DbParameter>();

        if (city != null) { sql.Append(" AND City = @City"); parameters.Add(CreateParameter("@City", city)); }
        if (state != null) { sql.Append(" AND State = @State"); parameters.Add(CreateParameter("@State", state)); }
        if (chain != null) { sql.Append(" AND Chain = @Chain"); parameters.Add(CreateParameter("@Chain", chain)); }

        return await ExecuteReaderAsync(sql.ToString(), MapStore, parameters.ToArray());
    }

    public async Task<StoreDto?> GetStoreAsync(Guid storeId)
    {
        const string sql = "SELECT Id, Name, Address, City, State, ZipCode, Chain, CreatedAt FROM Store WHERE Id = @StoreId";
        return await ExecuteReaderSingleAsync(sql, MapStore, CreateParameter("@StoreId", storeId));
    }

    public async Task<Guid> RecordPriceAsync(Guid productId, Guid storeId, decimal price, Guid? userId, DateTime? observedAt)
    {
        const string sql = @"
            INSERT INTO PriceObservation (ProductId, StoreId, Price, UserId, ObservedAt)
            OUTPUT INSERTED.Id
            VALUES (@ProductId, @StoreId, @Price, @UserId, @ObservedAt)";

        var result = await ExecuteScalarAsync<Guid>(sql,
            CreateParameter("@ProductId", productId),
            CreateParameter("@StoreId", storeId),
            CreateParameter("@Price", price),
            CreateParameter("@UserId", userId),
            CreateParameter("@ObservedAt", observedAt ?? DateTime.UtcNow));

        return result;
    }

    public async Task<List<PriceObservationDto>> GetProductPricesAsync(Guid productId, Guid? storeId = null, int daysBack = 90)
    {
        var sql = new StringBuilder(@"
            SELECT po.Id, po.ProductId, po.StoreId, s.Name AS StoreName, po.Price, po.UserId, po.ObservedAt
            FROM PriceObservation po
            INNER JOIN Store s ON po.StoreId = s.Id
            WHERE po.ProductId = @ProductId
              AND po.ObservedAt >= DATEADD(day, -@DaysBack, GETUTCDATE())");

        var parameters = new List<DbParameter>
        {
            CreateParameter("@ProductId", productId),
            CreateParameter("@DaysBack", daysBack)
        };

        if (storeId.HasValue)
        {
            sql.Append(" AND po.StoreId = @StoreId");
            parameters.Add(CreateParameter("@StoreId", storeId.Value));
        }

        sql.Append(" ORDER BY po.ObservedAt DESC");

        return await ExecuteReaderAsync(sql.ToString(), r => new PriceObservationDto
        {
            Id = GetGuid(r, "Id"),
            ProductId = GetGuid(r, "ProductId"),
            StoreId = GetGuid(r, "StoreId"),
            StoreName = GetString(r, "StoreName") ?? string.Empty,
            Price = GetDecimal(r, "Price"),
            UserId = GetGuidNullable(r, "UserId"),
            ObservedAt = GetDateTime(r, "ObservedAt")
        }, parameters.ToArray());
    }

    public async Task<PriceTrendDto> GetPriceTrendAsync(Guid productId, Guid? storeId = null)
    {
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

        var result = await ExecuteScalarAsync<Guid>(sql,
            CreateParameter("@ProductId", productId),
            CreateParameter("@StoreId", storeId),
            CreateParameter("@DealType", dealType),
            CreateParameter("@OriginalPrice", originalPrice),
            CreateParameter("@SalePrice", salePrice),
            CreateParameter("@SavingsPercent", savingsPercent),
            CreateParameter("@StartDate", startDate),
            CreateParameter("@EndDate", endDate));

        return result;
    }

    public async Task<List<DealDto>> GetActiveDealsAsync(Guid? storeId = null, Guid? productId = null)
    {
        var sql = new StringBuilder(@"
            SELECT d.Id, d.ProductId, '' AS ProductName, d.StoreId, s.Name AS StoreName,
                   d.DealType, d.OriginalPrice, d.SalePrice, d.SavingsPercent, d.StartDate, d.EndDate
            FROM Deal d
            INNER JOIN Store s ON d.StoreId = s.Id
            WHERE GETUTCDATE() BETWEEN d.StartDate AND d.EndDate");

        var parameters = new List<DbParameter>();
        if (storeId.HasValue) { sql.Append(" AND d.StoreId = @StoreId"); parameters.Add(CreateParameter("@StoreId", storeId.Value)); }
        if (productId.HasValue) { sql.Append(" AND d.ProductId = @ProductId"); parameters.Add(CreateParameter("@ProductId", productId.Value)); }

        return await ExecuteReaderAsync(sql.ToString(), r => new DealDto
        {
            Id = GetGuid(r, "Id"),
            ProductId = GetGuid(r, "ProductId"),
            ProductName = GetString(r, "ProductName") ?? string.Empty,
            StoreId = GetGuid(r, "StoreId"),
            StoreName = GetString(r, "StoreName") ?? string.Empty,
            DealType = GetString(r, "DealType") ?? string.Empty,
            OriginalPrice = GetDecimal(r, "OriginalPrice"),
            SalePrice = GetDecimal(r, "SalePrice"),
            SavingsPercent = GetDecimal(r, "SavingsPercent"),
            StartDate = GetDateTime(r, "StartDate"),
            EndDate = GetDateTime(r, "EndDate")
        }, parameters.ToArray());
    }

    public async Task<List<DealDto>> GetDealsNearMeAsync(string city, string state, int limit = 50)
    {
        return await GetActiveDealsAsync();
    }

    public async Task<Guid> SavePricePredictionAsync(Guid productId, Guid storeId, decimal predictedPrice, decimal confidence, DateTime predictedFor)
    {
        const string sql = @"
            INSERT INTO PricePrediction (ProductId, StoreId, PredictedPrice, Confidence, PredictedFor, CalculatedAt)
            OUTPUT INSERTED.Id
            VALUES (@ProductId, @StoreId, @PredictedPrice, @Confidence, @PredictedFor, GETUTCDATE())";

        var result = await ExecuteScalarAsync<Guid>(sql,
            CreateParameter("@ProductId", productId),
            CreateParameter("@StoreId", storeId),
            CreateParameter("@PredictedPrice", predictedPrice),
            CreateParameter("@Confidence", confidence),
            CreateParameter("@PredictedFor", predictedFor));

        return result;
    }

    public Task<PricePredictionDto?> GetPricePredictionAsync(Guid productId, Guid storeId)
    {
        return Task.FromResult<PricePredictionDto?>(null);
    }

    public Task<List<StorePriceComparisonDto>> ComparePricesAsync(List<Guid> productIds, List<Guid> storeIds)
    {
        return Task.FromResult(new List<StorePriceComparisonDto>());
    }

    // ── ProductPrice new methods ──────────────────────────────────────────────

    public async Task<List<ProductPriceDto>> SearchPricesAsync(PriceSearchRequest request)
    {
        var cacheKey = $"prices:search:{request.ProductId}:{request.Upc}:{request.ProductName}:{request.StoreName}:{request.DataSource}:{request.Page}:{request.PageSize}";
        if (_cache != null)
        {
            var cached = await _cache.GetAsync<List<ProductPriceDto>>(cacheKey);
            if (cached != null) return cached;
        }

        var (sql, parameters) = BuildSearchQuery(request, countOnly: false);
        var results = await ExecuteReaderAsync(sql.ToString(), MapProductPrice, parameters.ToArray());

        if (_cache != null)
            await _cache.SetAsync(cacheKey, results, TimeSpan.FromMinutes(5));

        return results;
    }

    public async Task<int> GetSearchCountAsync(PriceSearchRequest request)
    {
        var (sql, parameters) = BuildSearchQuery(request, countOnly: true);
        var result = await ExecuteScalarAsync<int>(sql.ToString(), parameters.ToArray());
        return result;
    }

    public async Task<List<ProductPriceDto>> GetPricesByUpcAsync(string upc, int limit = 50)
    {
        const string sql = @"
            SELECT TOP (@Limit) Id, ProductId, Upc, ProductName, StoreId, StoreName, StoreChain,
                   City, State, Price, Currency, Unit, Quantity, PricePerUnit,
                   DataSource, ExternalId, ObservedAt, ImportedAt
            FROM ProductPrice
            WHERE Upc = @Upc AND IsActive = 1
            ORDER BY ObservedAt DESC";

        return await ExecuteReaderAsync(sql, MapProductPrice,
            CreateParameter("@Upc", upc),
            CreateParameter("@Limit", limit));
    }

    public async Task<List<ProductPriceDto>> GetPricesByProductNameAsync(string productName, int limit = 50)
    {
        const string sql = @"
            SELECT TOP (@Limit) Id, ProductId, Upc, ProductName, StoreId, StoreName, StoreChain,
                   City, State, Price, Currency, Unit, Quantity, PricePerUnit,
                   DataSource, ExternalId, ObservedAt, ImportedAt
            FROM ProductPrice
            WHERE ProductName LIKE @ProductName AND IsActive = 1
            ORDER BY ObservedAt DESC";

        return await ExecuteReaderAsync(sql, MapProductPrice,
            CreateParameter("@ProductName", $"%{productName}%"),
            CreateParameter("@Limit", limit));
    }

    public async Task<List<ProductPriceDto>> GetBestPricesAsync(Guid productId, int limit = 10)
    {
        var cacheKey = $"prices:best:{productId}:{limit}";
        if (_cache != null)
        {
            var cached = await _cache.GetAsync<List<ProductPriceDto>>(cacheKey);
            if (cached != null) return cached;
        }

        const string sql = @"
            SELECT TOP (@Limit) Id, ProductId, Upc, ProductName, StoreId, StoreName, StoreChain,
                   City, State, Price, Currency, Unit, Quantity, PricePerUnit,
                   DataSource, ExternalId, ObservedAt, ImportedAt
            FROM ProductPrice
            WHERE ProductId = @ProductId AND IsActive = 1
              AND ObservedAt >= DATEADD(day, -90, GETUTCDATE())
            ORDER BY Price ASC";

        var results = await ExecuteReaderAsync(sql, MapProductPrice,
            CreateParameter("@ProductId", productId),
            CreateParameter("@Limit", limit));

        if (_cache != null)
            await _cache.SetAsync(cacheKey, results, TimeSpan.FromMinutes(15));

        return results;
    }

    public async Task<List<ProductPriceDto>> GetBatchPricesAsync(IEnumerable<Guid> productIds)
    {
        var ids = productIds.ToList();
        if (ids.Count == 0) return new List<ProductPriceDto>();

        // Build IN clause with parameterized values
        var paramNames = ids.Select((_, i) => $"@p{i}").ToList();
        var sql = $@"
            SELECT Id, ProductId, Upc, ProductName, StoreId, StoreName, StoreChain,
                   City, State, Price, Currency, Unit, Quantity, PricePerUnit,
                   DataSource, ExternalId, ObservedAt, ImportedAt
            FROM ProductPrice
            WHERE ProductId IN ({string.Join(",", paramNames)}) AND IsActive = 1
            ORDER BY ProductId, Price ASC";

        var parameters = ids.Select((id, i) => CreateParameter($"@p{i}", id)).ToArray();
        return await ExecuteReaderAsync(sql, MapProductPrice, parameters);
    }

    public async Task<Guid> UpsertProductPriceAsync(UpsertProductPriceRequest request)
    {
        var pricePerUnit = request.Quantity.HasValue && request.Quantity > 0
            ? (decimal?)(request.Price / request.Quantity.Value)
            : null;

        const string sql = @"
            MERGE ProductPrice AS target
            USING (SELECT @ExternalId AS ExternalId, @DataSource AS DataSource) AS source
            ON target.ExternalId = source.ExternalId AND target.DataSource = source.DataSource
            WHEN MATCHED THEN
                UPDATE SET
                    Price = @Price,
                    ProductName = @ProductName,
                    StoreName = @StoreName,
                    StoreChain = @StoreChain,
                    City = @City,
                    State = @State,
                    Unit = @Unit,
                    Quantity = @Quantity,
                    PricePerUnit = @PricePerUnit,
                    ObservedAt = @ObservedAt,
                    ImportedAt = GETUTCDATE(),
                    IsActive = 1
            WHEN NOT MATCHED THEN
                INSERT (ProductId, Upc, ProductName, StoreId, StoreName, StoreChain,
                        City, State, Price, Currency, Unit, Quantity, PricePerUnit,
                        DataSource, ExternalId, ObservedAt, ImportedAt, IsActive)
                VALUES (@ProductId, @Upc, @ProductName, @StoreId, @StoreName, @StoreChain,
                        @City, @State, @Price, @Currency, @Unit, @Quantity, @PricePerUnit,
                        @DataSource, @ExternalId, @ObservedAt, GETUTCDATE(), 1)
            OUTPUT INSERTED.Id;";

        var result = await ExecuteScalarAsync<Guid>(sql,
            CreateParameter("@ProductId", request.ProductId),
            CreateParameter("@Upc", request.Upc),
            CreateParameter("@ProductName", request.ProductName),
            CreateParameter("@StoreId", request.StoreId),
            CreateParameter("@StoreName", request.StoreName),
            CreateParameter("@StoreChain", request.StoreChain),
            CreateParameter("@City", request.City),
            CreateParameter("@State", request.State),
            CreateParameter("@Price", request.Price),
            CreateParameter("@Currency", request.Currency),
            CreateParameter("@Unit", request.Unit),
            CreateParameter("@Quantity", request.Quantity),
            CreateParameter("@PricePerUnit", pricePerUnit),
            CreateParameter("@DataSource", request.DataSource),
            CreateParameter("@ExternalId", request.ExternalId),
            CreateParameter("@ObservedAt", request.ObservedAt));

        return result;
    }

    public async Task<int> BulkUpsertProductPricesAsync(IEnumerable<UpsertProductPriceRequest> prices)
    {
        const int BatchSize = 500;
        var batch = new List<UpsertProductPriceRequest>(BatchSize);
        var totalImported = 0;

        foreach (var price in prices)
        {
            batch.Add(price);
            if (batch.Count >= BatchSize)
            {
                totalImported += await ProcessBatchAsync(batch);
                batch.Clear();
            }
        }

        if (batch.Count > 0)
            totalImported += await ProcessBatchAsync(batch);

        return totalImported;
    }

    private async Task<int> ProcessBatchAsync(List<UpsertProductPriceRequest> batch)
    {
        var imported = 0;
        foreach (var request in batch)
        {
            try
            {
                await UpsertProductPriceAsync(request);
                imported++;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to upsert price for product {ProductId}", request.ProductId);
            }
        }
        return imported;
    }

    public async Task<PriceImportLogDto> LogImportAsync(PriceImportLogDto log)
    {
        // Insert and retrieve the generated Id and server-assigned ImportedAt timestamp
        const string sql = @"
            INSERT INTO PriceImportLog (DataSource, ImportedAt, RecordsProcessed, RecordsImported,
                RecordsUpdated, RecordsSkipped, ErrorCount, ErrorMessage, Success)
            OUTPUT INSERTED.Id, INSERTED.ImportedAt
            VALUES (@DataSource, GETUTCDATE(), @RecordsProcessed, @RecordsImported,
                @RecordsUpdated, @RecordsSkipped, @ErrorCount, @ErrorMessage, @Success)";

        var results = await ExecuteReaderAsync(sql, r => new { Id = GetGuid(r, "Id"), ImportedAt = GetDateTime(r, "ImportedAt") },
            CreateParameter("@DataSource", log.DataSource),
            CreateParameter("@RecordsProcessed", log.RecordsProcessed),
            CreateParameter("@RecordsImported", log.RecordsImported),
            CreateParameter("@RecordsUpdated", log.RecordsUpdated),
            CreateParameter("@RecordsSkipped", log.RecordsSkipped),
            CreateParameter("@ErrorCount", log.ErrorCount),
            CreateParameter("@ErrorMessage", log.ErrorMessage),
            CreateParameter("@Success", log.Success));

        var inserted = results.FirstOrDefault();
        if (inserted != null)
        {
            log.Id = inserted.Id;
            log.ImportedAt = inserted.ImportedAt;
        }

        return log;
    }

    public async Task<PriceImportLogDto?> GetLastImportAsync(string dataSource)
    {
        const string sql = @"
            SELECT TOP 1 Id, DataSource, ImportedAt, RecordsProcessed, RecordsImported,
                   RecordsUpdated, RecordsSkipped, ErrorCount, ErrorMessage, Success
            FROM PriceImportLog
            WHERE DataSource = @DataSource
            ORDER BY ImportedAt DESC";

        return await ExecuteReaderSingleAsync(sql, r => new PriceImportLogDto
        {
            Id = GetGuid(r, "Id"),
            DataSource = GetString(r, "DataSource") ?? string.Empty,
            ImportedAt = GetDateTime(r, "ImportedAt"),
            RecordsProcessed = GetInt32(r, "RecordsProcessed"),
            RecordsImported = GetInt32(r, "RecordsImported"),
            RecordsUpdated = GetInt32(r, "RecordsUpdated"),
            RecordsSkipped = GetInt32(r, "RecordsSkipped"),
            ErrorCount = GetInt32(r, "ErrorCount"),
            ErrorMessage = GetNullableString(r, "ErrorMessage"),
            Success = GetBoolean(r, "Success")
        }, CreateParameter("@DataSource", dataSource));
    }

    public async Task<int> GetProductPriceCountAsync()
    {
        const string sql = "SELECT COUNT(1) FROM ProductPrice WHERE IsActive = 1";
        return await ExecuteScalarAsync<int>(sql);
    }

    // ── Product lifecycle reactions ──────────────────────────────────────────

    // ── Product lifecycle reactions ──────────────────────────────────────────
    // Note: SqlHelper.ExecuteNonQueryAsync has no CancellationToken overload, so ct
    // is accepted for interface compliance but cannot be forwarded to the base class.

    public async Task<int> DeactivatePricesByProductIdAsync(Guid productId, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE ProductPrice
            SET    IsActive = 0, UpdatedAt = GETUTCDATE()
            WHERE  ProductId = @ProductId AND IsActive = 1";

        return await ExecuteNonQueryAsync(sql, CreateParameter("@ProductId", productId));
    }

    public async Task<int> UpdateProductNameOnPricesAsync(Guid productId, string newName, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);

        const string sql = @"
            UPDATE ProductPrice
            SET    ProductName = @NewName, UpdatedAt = GETUTCDATE()
            WHERE  ProductId = @ProductId";

        return await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@ProductId", productId),
            CreateParameter("@NewName", newName));
    }

    public async Task<int> UpdateProductUpcOnPricesAsync(Guid productId, string? newUpc, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE ProductPrice
            SET    Upc = @NewUpc, UpdatedAt = GETUTCDATE()
            WHERE  ProductId = @ProductId";

        return await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@ProductId", productId),
            CreateParameter("@NewUpc", (object?)newUpc ?? DBNull.Value));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static StoreDto MapStore(IDataRecord r) => new StoreDto
    {
        Id = GetGuid(r, "Id"),
        Name = GetString(r, "Name") ?? string.Empty,
        Address = GetNullableString(r, "Address"),
        City = GetNullableString(r, "City"),
        State = GetNullableString(r, "State"),
        ZipCode = GetNullableString(r, "ZipCode"),
        Chain = GetNullableString(r, "Chain"),
        CreatedAt = GetDateTime(r, "CreatedAt")
    };

    private static ProductPriceDto MapProductPrice(IDataRecord r) => new ProductPriceDto
    {
        Id = GetGuid(r, "Id"),
        ProductId = GetGuid(r, "ProductId"),
        Upc = GetNullableString(r, "Upc"),
        ProductName = GetString(r, "ProductName") ?? string.Empty,
        StoreId = GetGuidNullable(r, "StoreId"),
        StoreName = GetNullableString(r, "StoreName"),
        StoreChain = GetNullableString(r, "StoreChain"),
        City = GetNullableString(r, "City"),
        State = GetNullableString(r, "State"),
        Price = GetDecimal(r, "Price"),
        Currency = GetString(r, "Currency") ?? "USD",
        Unit = GetNullableString(r, "Unit"),
        Quantity = GetDecimalNullable(r, "Quantity"),
        PricePerUnit = GetDecimalNullable(r, "PricePerUnit"),
        DataSource = GetString(r, "DataSource") ?? string.Empty,
        ExternalId = GetNullableString(r, "ExternalId"),
        ObservedAt = GetDateTime(r, "ObservedAt"),
        ImportedAt = GetDateTime(r, "ImportedAt")
    };

    private static (string Sql, List<DbParameter> Parameters) BuildSearchQuery(PriceSearchRequest request, bool countOnly)
    {
        var select = countOnly
            ? "SELECT COUNT(1) FROM ProductPrice WHERE IsActive = 1"
            : @"SELECT Id, ProductId, Upc, ProductName, StoreId, StoreName, StoreChain,
                       City, State, Price, Currency, Unit, Quantity, PricePerUnit,
                       DataSource, ExternalId, ObservedAt, ImportedAt
                FROM ProductPrice WHERE IsActive = 1";

        var sql = new StringBuilder(select);
        var parameters = new List<DbParameter>();

        sql.Append(" AND ObservedAt >= DATEADD(day, -@DaysBack, GETUTCDATE())");
        parameters.Add(CreateParameter("@DaysBack", request.DaysBack));

        if (request.ProductId.HasValue)
        {
            sql.Append(" AND ProductId = @ProductId");
            parameters.Add(CreateParameter("@ProductId", request.ProductId.Value));
        }
        if (!string.IsNullOrEmpty(request.Upc))
        {
            sql.Append(" AND Upc = @Upc");
            parameters.Add(CreateParameter("@Upc", request.Upc));
        }
        if (!string.IsNullOrEmpty(request.ProductName))
        {
            sql.Append(" AND ProductName LIKE @ProductName");
            parameters.Add(CreateParameter("@ProductName", $"%{request.ProductName}%"));
        }
        if (!string.IsNullOrEmpty(request.StoreName))
        {
            sql.Append(" AND StoreName LIKE @StoreName");
            parameters.Add(CreateParameter("@StoreName", $"%{request.StoreName}%"));
        }
        if (!string.IsNullOrEmpty(request.StoreChain))
        {
            sql.Append(" AND StoreChain = @StoreChain");
            parameters.Add(CreateParameter("@StoreChain", request.StoreChain));
        }
        if (!string.IsNullOrEmpty(request.City))
        {
            sql.Append(" AND City = @City");
            parameters.Add(CreateParameter("@City", request.City));
        }
        if (!string.IsNullOrEmpty(request.State))
        {
            sql.Append(" AND State = @State");
            parameters.Add(CreateParameter("@State", request.State));
        }
        if (!string.IsNullOrEmpty(request.DataSource))
        {
            sql.Append(" AND DataSource = @DataSource");
            parameters.Add(CreateParameter("@DataSource", request.DataSource));
        }
        if (request.MinPrice.HasValue)
        {
            sql.Append(" AND Price >= @MinPrice");
            parameters.Add(CreateParameter("@MinPrice", request.MinPrice.Value));
        }
        if (request.MaxPrice.HasValue)
        {
            sql.Append(" AND Price <= @MaxPrice");
            parameters.Add(CreateParameter("@MaxPrice", request.MaxPrice.Value));
        }

        if (!countOnly)
        {
            sql.Append(" ORDER BY ObservedAt DESC");
            var offset = (request.Page - 1) * request.PageSize;
            sql.Append(" OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY");
            parameters.Add(CreateParameter("@Offset", offset));
            parameters.Add(CreateParameter("@PageSize", request.PageSize));
        }

        return (sql.ToString(), parameters);
    }
}
