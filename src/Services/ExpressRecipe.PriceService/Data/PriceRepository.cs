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
        var sql = new StringBuilder("SELECT Id, Name, Address, City, State, ZipCode, Chain, CreatedAt, IsOnline, BaseDeliveryFee, FreeDeliveryMin, AvgDeliveryDays, ShippingNotes FROM Store WHERE 1=1");
        var parameters = new List<DbParameter>();

        if (city != null) { sql.Append(" AND City = @City"); parameters.Add(CreateParameter("@City", city)); }
        if (state != null) { sql.Append(" AND State = @State"); parameters.Add(CreateParameter("@State", state)); }
        if (chain != null) { sql.Append(" AND Chain = @Chain"); parameters.Add(CreateParameter("@Chain", chain)); }

        return await ExecuteReaderAsync(sql.ToString(), MapStore, parameters.ToArray());
    }

    public async Task<StoreDto?> GetStoreAsync(Guid storeId)
    {
        const string sql = "SELECT Id, Name, Address, City, State, ZipCode, Chain, CreatedAt, IsOnline, BaseDeliveryFee, FreeDeliveryMin, AvgDeliveryDays, ShippingNotes FROM Store WHERE Id = @StoreId";
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
        CreatedAt = GetDateTime(r, "CreatedAt"),
        IsOnline = GetBoolean(r, "IsOnline"),
        BaseDeliveryFee = GetDecimalNullable(r, "BaseDeliveryFee"),
        FreeDeliveryMin = GetDecimalNullable(r, "FreeDeliveryMin"),
        AvgDeliveryDays = GetDecimalNullable(r, "AvgDeliveryDays"),
        ShippingNotes = GetNullableString(r, "ShippingNotes")
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

    // ── Price History (append-only) ──────────────────────────────────────────

    public async Task RecordPriceHistoryAsync(PriceHistoryRecord record, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO PriceHistory
                (ProductId, Upc, ProductName, StoreId, StoreName, StoreChain, IsOnline,
                 BasePrice, FinalPrice, Currency, Unit, Quantity, PricePerOz, PricePerHundredG,
                 DataSource, ExternalId, ObservedAt, ImportedAt)
            VALUES
                (@ProductId, @Upc, @ProductName, @StoreId, @StoreName, @StoreChain, @IsOnline,
                 @BasePrice, @FinalPrice, @Currency, @Unit, @Quantity, @PricePerOz, @PricePerHundredG,
                 @DataSource, @ExternalId, @ObservedAt, GETUTCDATE())";

        await ExecuteNonQueryAsync(sql,
            CreateParameter("@ProductId", record.ProductId),
            CreateParameter("@Upc", record.Upc),
            CreateParameter("@ProductName", record.ProductName),
            CreateParameter("@StoreId", record.StoreId),
            CreateParameter("@StoreName", record.StoreName),
            CreateParameter("@StoreChain", record.StoreChain),
            CreateParameter("@IsOnline", record.IsOnline),
            CreateParameter("@BasePrice", record.BasePrice),
            CreateParameter("@FinalPrice", record.FinalPrice),
            CreateParameter("@Currency", record.Currency),
            CreateParameter("@Unit", record.Unit),
            CreateParameter("@Quantity", record.Quantity),
            CreateParameter("@PricePerOz", record.PricePerOz),
            CreateParameter("@PricePerHundredG", record.PricePerHundredG),
            CreateParameter("@DataSource", record.DataSource),
            CreateParameter("@ExternalId", record.ExternalId),
            CreateParameter("@ObservedAt", record.ObservedAt.UtcDateTime));
    }

    public async Task BulkInsertPriceHistoryAsync(IEnumerable<PriceHistoryRecord> records, CancellationToken ct = default)
    {
        // Use a connection-scoped local temp table (#) so parallel imports on different
        // connections never collide. Global temp tables (##) are shared across all sessions.
        var tempName = "#PriceHistoryBulk";
        var table = new DataTable();
        table.TableName = tempName;
        table.Columns.Add("ProductId", typeof(Guid));
        table.Columns.Add("Upc", typeof(string));
        table.Columns.Add("ProductName", typeof(string));
        table.Columns.Add("StoreId", typeof(Guid));
        table.Columns.Add("StoreName", typeof(string));
        table.Columns.Add("StoreChain", typeof(string));
        table.Columns.Add("IsOnline", typeof(bool));
        table.Columns.Add("BasePrice", typeof(decimal));
        table.Columns.Add("FinalPrice", typeof(decimal));
        table.Columns.Add("Currency", typeof(string));
        table.Columns.Add("Unit", typeof(string));
        table.Columns.Add("Quantity", typeof(decimal));
        table.Columns.Add("PricePerOz", typeof(decimal));
        table.Columns.Add("PricePerHundredG", typeof(decimal));
        table.Columns.Add("DataSource", typeof(string));
        table.Columns.Add("ExternalId", typeof(string));
        table.Columns.Add("ObservedAt", typeof(DateTime));
        table.Columns.Add("ImportedAt", typeof(DateTime));

        foreach (var r in records)
        {
            table.Rows.Add(
                r.ProductId,
                r.Upc ?? (object)DBNull.Value,
                r.ProductName,
                r.StoreId.HasValue ? r.StoreId.Value : (object)DBNull.Value,
                r.StoreName ?? (object)DBNull.Value,
                r.StoreChain ?? (object)DBNull.Value,
                r.IsOnline,
                r.BasePrice,
                r.FinalPrice,
                r.Currency,
                r.Unit ?? (object)DBNull.Value,
                r.Quantity.HasValue ? r.Quantity.Value : (object)DBNull.Value,
                r.PricePerOz.HasValue ? r.PricePerOz.Value : (object)DBNull.Value,
                r.PricePerHundredG.HasValue ? r.PricePerHundredG.Value : (object)DBNull.Value,
                r.DataSource,
                r.ExternalId ?? (object)DBNull.Value,
                r.ObservedAt.UtcDateTime,
                DateTime.UtcNow);
        }

        if (table.Rows.Count == 0) { return; }

        var createTempSql = $@"
            CREATE TABLE {tempName} (
                ProductId        UNIQUEIDENTIFIER NOT NULL,
                Upc              NVARCHAR(100) NULL,
                ProductName      NVARCHAR(300) NOT NULL,
                StoreId          UNIQUEIDENTIFIER NULL,
                StoreName        NVARCHAR(200) NULL,
                StoreChain       NVARCHAR(200) NULL,
                IsOnline         BIT NOT NULL,
                BasePrice        DECIMAL(10,4) NOT NULL,
                FinalPrice       DECIMAL(10,4) NOT NULL,
                Currency         NVARCHAR(10) NOT NULL,
                Unit             NVARCHAR(50) NULL,
                Quantity         DECIMAL(10,4) NULL,
                PricePerOz       DECIMAL(10,6) NULL,
                PricePerHundredG DECIMAL(10,6) NULL,
                DataSource       NVARCHAR(100) NOT NULL,
                ExternalId       NVARCHAR(200) NULL,
                ObservedAt       DATETIME2 NOT NULL,
                ImportedAt       DATETIME2 NOT NULL
            )";

        var insertSql = $@"
            INSERT INTO PriceHistory
                (ProductId, Upc, ProductName, StoreId, StoreName, StoreChain, IsOnline,
                 BasePrice, FinalPrice, Currency, Unit, Quantity, PricePerOz, PricePerHundredG,
                 DataSource, ExternalId, ObservedAt, ImportedAt)
            SELECT ProductId, Upc, ProductName, StoreId, StoreName, StoreChain, IsOnline,
                   BasePrice, FinalPrice, Currency, Unit, Quantity, PricePerOz, PricePerHundredG,
                   DataSource, ExternalId, ObservedAt, ImportedAt
            FROM {tempName}";

        await BulkMergeAsync(createTempSql, table, insertSql);
    }

    public async Task<List<PriceHistoryRecord>> GetPriceHistoryAsync(Guid productId, Guid? storeId, int daysBack, CancellationToken ct = default)
    {
        var sql = new StringBuilder(@"
            SELECT Id, ProductId, Upc, ProductName, StoreId, StoreName, StoreChain, IsOnline,
                   BasePrice, FinalPrice, Currency, Unit, Quantity, PricePerOz, PricePerHundredG,
                   DataSource, ExternalId, ObservedAt, ImportedAt
            FROM PriceHistory
            WHERE ProductId = @ProductId
              AND ObservedAt >= DATEADD(day, -@DaysBack, GETUTCDATE())");

        var parameters = new List<DbParameter>
        {
            CreateParameter("@ProductId", productId),
            CreateParameter("@DaysBack", daysBack)
        };

        if (storeId.HasValue)
        {
            sql.Append(" AND StoreId = @StoreId");
            parameters.Add(CreateParameter("@StoreId", storeId.Value));
        }

        sql.Append(" ORDER BY ObservedAt DESC");

        return await ExecuteReaderAsync(sql.ToString(), MapPriceHistory, parameters.ToArray());
    }

    public async Task<PriceHistoryStatsDto> GetPriceStatsAsync(Guid productId, Guid? storeId, int daysBack, CancellationToken ct = default)
    {
        // Build the store sub-filter once so both the outer query and the CurrentPrice
        // subquery use the same predicate (keeps CurrentPrice consistent with the stats set).
        var storeSubFilter = storeId.HasValue ? " AND StoreId = @StoreIdSub" : string.Empty;

        var sql = new StringBuilder($@"
            SELECT
                COUNT(*)                                                    AS ObservationCount,
                MIN(FinalPrice)                                             AS MinPrice,
                MAX(FinalPrice)                                             AS MaxPrice,
                AVG(FinalPrice)                                             AS AveragePrice,
                (SELECT TOP 1 FinalPrice FROM PriceHistory
                 WHERE ProductId = @ProductIdSub{storeSubFilter}
                 ORDER BY ObservedAt DESC)                                  AS CurrentPrice,
                MIN(ObservedAt)                                             AS OldestObservation,
                MAX(ObservedAt)                                             AS NewestObservation,
                AVG(PricePerOz)                                             AS AvgPricePerOz,
                AVG(PricePerHundredG)                                       AS AvgPricePerHundredG
            FROM PriceHistory ph
            WHERE ph.ProductId = @ProductId
              AND ph.ObservedAt >= DATEADD(day, -@DaysBack, GETUTCDATE())");

        var parameters = new List<DbParameter>
        {
            CreateParameter("@ProductId", productId),
            CreateParameter("@ProductIdSub", productId),
            CreateParameter("@DaysBack", daysBack)
        };

        if (storeId.HasValue)
        {
            sql.Append(" AND ph.StoreId = @StoreId");
            parameters.Add(CreateParameter("@StoreId", storeId.Value));
            parameters.Add(CreateParameter("@StoreIdSub", storeId.Value));
        }

        var rows = await ExecuteReaderAsync(sql.ToString(), r => new
        {
            ObservationCount = GetInt32(r, "ObservationCount"),
            MinPrice = GetDecimal(r, "MinPrice"),
            MaxPrice = GetDecimal(r, "MaxPrice"),
            AveragePrice = GetDecimal(r, "AveragePrice"),
            CurrentPrice = GetDecimal(r, "CurrentPrice"),
            OldestObservation = GetNullableDateTime(r, "OldestObservation"),
            NewestObservation = GetNullableDateTime(r, "NewestObservation"),
            AvgPricePerOz = GetDecimalNullable(r, "AvgPricePerOz"),
            AvgPricePerHundredG = GetDecimalNullable(r, "AvgPricePerHundredG")
        }, parameters.ToArray());

        var row = rows.FirstOrDefault();
        if (row == null || row.ObservationCount == 0)
        {
            return new PriceHistoryStatsDto { ProductId = productId, StoreId = storeId, Trend = "Unknown" };
        }

        // Compute 30-day price change
        decimal priceChange30Days = 0;
        decimal? priceChange30DaysPct = null;
        if (daysBack > 30)
        {
            var p30Params = new List<DbParameter> { CreateParameter("@ProductId30", productId) };
            if (storeId.HasValue) { p30Params.Add(CreateParameter("@StoreId30", storeId.Value)); }
            var storeFilter = storeId.HasValue ? " AND StoreId = @StoreId30" : string.Empty;
            var p30Sql = $@"
                SELECT TOP 1 FinalPrice FROM PriceHistory
                WHERE ProductId = @ProductId30{storeFilter}
                  AND ObservedAt >= DATEADD(day, -30, GETUTCDATE())
                ORDER BY ObservedAt ASC";
            var oldPrices = await ExecuteReaderAsync(p30Sql, r => GetDecimal(r, "FinalPrice"), p30Params.ToArray());
            if (oldPrices.Count > 0)
            {
                priceChange30Days = row.CurrentPrice - oldPrices[0];
                if (oldPrices[0] != 0)
                {
                    priceChange30DaysPct = (priceChange30Days / oldPrices[0]) * 100m;
                }
            }
        }

        var trend = priceChange30Days > 0.01m ? "Rising" : priceChange30Days < -0.01m ? "Falling" : "Stable";

        return new PriceHistoryStatsDto
        {
            ProductId = productId,
            StoreId = storeId,
            CurrentPrice = row.CurrentPrice,
            AveragePrice = row.AveragePrice,
            MinPrice = row.MinPrice,
            MaxPrice = row.MaxPrice,
            PriceChange30Days = priceChange30Days,
            PriceChange30DaysPct = priceChange30DaysPct,
            Trend = trend,
            ObservationCount = row.ObservationCount,
            OldestObservation = row.OldestObservation,
            NewestObservation = row.NewestObservation,
            AvgPricePerOz = row.AvgPricePerOz,
            AvgPricePerHundredG = row.AvgPricePerHundredG
        };
    }

    public async Task<List<UnitPriceComparisonDto>> CompareByUnitAsync(IEnumerable<Guid> productIds, string targetUnit, CancellationToken ct = default)
    {
        var ids = productIds.ToList();
        if (ids.Count == 0) { return new List<UnitPriceComparisonDto>(); }

        var paramNames = ids.Select((_, i) => $"@pid{i}").ToList();
        var inClause = string.Join(",", paramNames);
        var sql = $@"
            SELECT h.ProductId, h.ProductName, h.Upc, h.StoreName,
                   h.BasePrice, h.FinalPrice, h.Unit, h.Quantity,
                   h.PricePerOz, h.PricePerHundredG, h.ObservedAt
            FROM PriceHistory h
            INNER JOIN (
                SELECT ProductId, MAX(ObservedAt) AS LatestAt
                FROM PriceHistory
                WHERE ProductId IN ({inClause})
                GROUP BY ProductId
            ) latest ON h.ProductId = latest.ProductId AND h.ObservedAt = latest.LatestAt
            WHERE h.ProductId IN ({inClause})";

        var parameters = ids.Select((id, i) => CreateParameter($"@pid{i}", id)).ToList();

        return await ExecuteReaderAsync(sql, r => new UnitPriceComparisonDto
        {
            ProductId = GetGuid(r, "ProductId"),
            ProductName = GetString(r, "ProductName") ?? string.Empty,
            Upc = GetNullableString(r, "Upc"),
            StoreName = GetNullableString(r, "StoreName"),
            BasePrice = GetDecimal(r, "BasePrice"),
            FinalPrice = GetDecimal(r, "FinalPrice"),
            OriginalUnit = GetNullableString(r, "Unit"),
            OriginalQuantity = GetDecimalNullable(r, "Quantity"),
            TargetUnit = targetUnit,
            PricePerOz = GetDecimalNullable(r, "PricePerOz"),
            PricePerHundredG = GetDecimalNullable(r, "PricePerHundredG"),
            PricePerTargetUnit = targetUnit.Equals("oz", StringComparison.OrdinalIgnoreCase)
                ? GetDecimalNullable(r, "PricePerOz")
                : targetUnit.Equals("100g", StringComparison.OrdinalIgnoreCase)
                    ? GetDecimalNullable(r, "PricePerHundredG")
                    : null,
            ObservedAt = GetDateTime(r, "ObservedAt")
        }, parameters.ToArray());
    }

    // ── Store-product linking ─────────────────────────────────────────────────

    public async Task UpsertStoreProductLinkAsync(Guid storeId, Guid productId, string? upc, string dataSource, CancellationToken ct = default)
    {
        const string sql = @"
            MERGE StoreProductLink AS target
            USING (SELECT @StoreId AS StoreId, @ProductId AS ProductId) AS source
            ON target.StoreId = source.StoreId AND target.ProductId = source.ProductId
            WHEN MATCHED THEN
                UPDATE SET LastSeenAt = GETUTCDATE(), IsInStock = 1,
                           Upc = COALESCE(@Upc, target.Upc),
                           DataSource = @DataSource
            WHEN NOT MATCHED THEN
                INSERT (StoreId, ProductId, Upc, IsInStock, LastSeenAt, DataSource)
                VALUES (@StoreId, @ProductId, @Upc, 1, GETUTCDATE(), @DataSource);";

        await ExecuteNonQueryAsync(sql,
            CreateParameter("@StoreId", storeId),
            CreateParameter("@ProductId", productId),
            CreateParameter("@Upc", upc),
            CreateParameter("@DataSource", dataSource));
    }

    public async Task<List<StoreProductLinkDto>> GetStoresForProductAsync(Guid productId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT Id, StoreId, ProductId, Upc, IsInStock, Aisle, LastSeenAt, LastPriceId, DataSource
            FROM StoreProductLink
            WHERE ProductId = @ProductId
            ORDER BY LastSeenAt DESC";

        return await ExecuteReaderAsync(sql, MapStoreProductLink, CreateParameter("@ProductId", productId));
    }

    public async Task<List<StoreProductLinkDto>> GetProductsForStoreAsync(Guid storeId, int page, int pageSize, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT Id, StoreId, ProductId, Upc, IsInStock, Aisle, LastSeenAt, LastPriceId, DataSource
            FROM StoreProductLink
            WHERE StoreId = @StoreId
            ORDER BY LastSeenAt DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

        return await ExecuteReaderAsync(sql, MapStoreProductLink,
            CreateParameter("@StoreId", storeId),
            CreateParameter("@Offset", (page - 1) * pageSize),
            CreateParameter("@PageSize", pageSize));
    }

    // ── Enhanced deals ───────────────────────────────────────────────────────

    public async Task<Guid> CreateEnhancedDealAsync(CreateEnhancedDealRequest request, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO Deal
                (ProductId, StoreId, DealType, OriginalPrice, SalePrice, SavingsPercent,
                 StartDate, EndDate, CreatedAt,
                 DiscountType, BuyQuantity, GetQuantity, GetPercentOff, CouponCode,
                 RebateAmount, FlyerSource, FlyerPageRef, IsDigital, IsStackable, MaxPerTransaction)
            OUTPUT INSERTED.Id
            VALUES
                (@ProductId, @StoreId, @DealType, @OriginalPrice, @SalePrice, @SavingsPercent,
                 @StartDate, @EndDate, GETUTCDATE(),
                 @DiscountType, @BuyQuantity, @GetQuantity, @GetPercentOff, @CouponCode,
                 @RebateAmount, @FlyerSource, @FlyerPageRef, @IsDigital, @IsStackable, @MaxPerTransaction)";

        var savingsPercent = request.OriginalPrice > 0
            ? ((request.OriginalPrice - request.SalePrice) / request.OriginalPrice) * 100
            : 0;

        return await ExecuteScalarAsync<Guid>(sql,
            CreateParameter("@ProductId", request.ProductId),
            CreateParameter("@StoreId", request.StoreId),
            CreateParameter("@DealType", request.DealType),
            CreateParameter("@OriginalPrice", request.OriginalPrice),
            CreateParameter("@SalePrice", request.SalePrice),
            CreateParameter("@SavingsPercent", savingsPercent),
            CreateParameter("@StartDate", request.StartDate),
            CreateParameter("@EndDate", request.EndDate),
            CreateParameter("@DiscountType", request.DiscountType),
            CreateParameter("@BuyQuantity", request.BuyQuantity),
            CreateParameter("@GetQuantity", request.GetQuantity),
            CreateParameter("@GetPercentOff", request.GetPercentOff),
            CreateParameter("@CouponCode", request.CouponCode),
            CreateParameter("@RebateAmount", request.RebateAmount),
            CreateParameter("@FlyerSource", request.FlyerSource),
            CreateParameter("@FlyerPageRef", request.FlyerPageRef),
            CreateParameter("@IsDigital", request.IsDigital),
            CreateParameter("@IsStackable", request.IsStackable),
            CreateParameter("@MaxPerTransaction", request.MaxPerTransaction));
    }

    public async Task<EffectivePriceDto> CalculateEffectivePriceAsync(Guid productId, Guid storeId, int quantity, CancellationToken ct = default)
    {
        // Fetch the best active deal for this product/store
        const string sql = @"
            SELECT TOP 1
                d.OriginalPrice, d.SalePrice, d.SavingsPercent,
                d.DiscountType, d.BuyQuantity, d.GetQuantity,
                d.GetPercentOff, d.CouponCode, d.RebateAmount
            FROM Deal d
            WHERE d.ProductId = @ProductId
              AND d.StoreId = @StoreId
              AND GETUTCDATE() BETWEEN d.StartDate AND d.EndDate
            ORDER BY d.SavingsPercent DESC";

        var deals = await ExecuteReaderAsync(sql, r => new
        {
            OriginalPrice = GetDecimal(r, "OriginalPrice"),
            SalePrice = GetDecimal(r, "SalePrice"),
            DiscountType = GetNullableString(r, "DiscountType"),
            BuyQuantity = GetIntNullable(r, "BuyQuantity"),
            GetQuantity = GetIntNullable(r, "GetQuantity"),
            GetPercentOff = GetDecimalNullable(r, "GetPercentOff"),
            CouponCode = GetNullableString(r, "CouponCode"),
            RebateAmount = GetDecimalNullable(r, "RebateAmount")
        }, CreateParameter("@ProductId", productId), CreateParameter("@StoreId", storeId));

        var dto = new EffectivePriceDto
        {
            ProductId = productId,
            StoreId = storeId,
            Quantity = quantity
        };

        if (deals.Count == 0)
        {
            // Fall back to latest ProductPrice
            const string priceSql = @"
                SELECT TOP 1 Price FROM ProductPrice
                WHERE ProductId = @ProductId AND StoreId = @StoreId AND IsActive = 1
                ORDER BY ObservedAt DESC";
            var prices = await ExecuteReaderAsync(priceSql, r => GetDecimal(r, "Price"),
                CreateParameter("@ProductId", productId), CreateParameter("@StoreId", storeId));
            dto.BasePrice = prices.Count > 0 ? prices[0] : 0;
            dto.EffectivePrice = dto.BasePrice;
            dto.TotalCost = dto.EffectivePrice * quantity;
            return dto;
        }

        var deal = deals[0];
        dto.BasePrice = deal.OriginalPrice;
        dto.CouponCode = deal.CouponCode;
        dto.AppliedDealType = deal.DiscountType;

        // Compute effective price based on discount type
        if (deal.DiscountType == "BuyOneGetOne" || deal.DiscountType == "BuyNGetMFree")
        {
            var buyQty = deal.BuyQuantity ?? 1;
            var getQty = deal.GetQuantity ?? 1;
            var cycleSize = buyQty + getQty;
            var fullCycles = quantity / cycleSize;
            var remainder = quantity % cycleSize;
            var paidUnits = (fullCycles * buyQty) + Math.Min(remainder, buyQty);
            dto.TotalCost = paidUnits * deal.OriginalPrice;
            dto.EffectivePrice = quantity > 0 ? dto.TotalCost / quantity : deal.OriginalPrice;
        }
        else if (deal.GetPercentOff.HasValue)
        {
            dto.EffectivePrice = deal.OriginalPrice * (1m - deal.GetPercentOff.Value / 100m);
            dto.TotalCost = dto.EffectivePrice * quantity;
        }
        else if (deal.RebateAmount.HasValue)
        {
            dto.EffectivePrice = Math.Max(0, deal.OriginalPrice - deal.RebateAmount.Value);
            dto.TotalCost = dto.EffectivePrice * quantity;
        }
        else
        {
            dto.EffectivePrice = deal.SalePrice;
            dto.TotalCost = dto.EffectivePrice * quantity;
        }

        dto.Savings = (deal.OriginalPrice - dto.EffectivePrice) * quantity;
        dto.SavingsPct = deal.OriginalPrice > 0
            ? ((deal.OriginalPrice - dto.EffectivePrice) / deal.OriginalPrice) * 100m
            : 0;

        return dto;
    }

    // ── Additional helpers ───────────────────────────────────────────────────

    private static PriceHistoryRecord MapPriceHistory(IDataRecord r) => new PriceHistoryRecord
    {
        Id = GetGuid(r, "Id"),
        ProductId = GetGuid(r, "ProductId"),
        Upc = GetNullableString(r, "Upc"),
        ProductName = GetString(r, "ProductName") ?? string.Empty,
        StoreId = GetGuidNullable(r, "StoreId"),
        StoreName = GetNullableString(r, "StoreName"),
        StoreChain = GetNullableString(r, "StoreChain"),
        IsOnline = GetBoolean(r, "IsOnline"),
        BasePrice = GetDecimal(r, "BasePrice"),
        FinalPrice = GetDecimal(r, "FinalPrice"),
        Currency = GetString(r, "Currency") ?? "USD",
        Unit = GetNullableString(r, "Unit"),
        Quantity = GetDecimalNullable(r, "Quantity"),
        PricePerOz = GetDecimalNullable(r, "PricePerOz"),
        PricePerHundredG = GetDecimalNullable(r, "PricePerHundredG"),
        DataSource = GetString(r, "DataSource") ?? string.Empty,
        ExternalId = GetNullableString(r, "ExternalId"),
        ObservedAt = new DateTimeOffset(GetDateTime(r, "ObservedAt"), TimeSpan.Zero),
        ImportedAt = GetDateTime(r, "ImportedAt")
    };

    private static StoreProductLinkDto MapStoreProductLink(IDataRecord r) => new StoreProductLinkDto
    {
        Id = GetGuid(r, "Id"),
        StoreId = GetGuid(r, "StoreId"),
        ProductId = GetGuid(r, "ProductId"),
        Upc = GetNullableString(r, "Upc"),
        IsInStock = GetBoolean(r, "IsInStock"),
        Aisle = GetNullableString(r, "Aisle"),
        LastSeenAt = GetDateTime(r, "LastSeenAt"),
        LastPriceId = GetGuidNullable(r, "LastPriceId"),
        DataSource = GetString(r, "DataSource") ?? string.Empty
    };
}
