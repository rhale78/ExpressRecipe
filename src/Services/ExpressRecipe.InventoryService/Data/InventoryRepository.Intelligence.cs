using Microsoft.Data.SqlClient;
using System.Data;

namespace ExpressRecipe.InventoryService.Data;

// Partial class for Purchase Patterns, Price Watch, and Abandoned Product Intelligence
public partial class InventoryRepository
{
    #region Purchase Events

    public async Task<Guid> RecordPurchaseEventAsync(PurchaseEventRecord record, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO PurchaseEvent
            (UserId, HouseholdId, ProductId, IngredientId, CustomName, Barcode, Quantity, Unit, Price,
             StoreId, StoreName, PurchasedAt, Source)
            OUTPUT INSERTED.Id
            VALUES (@UserId, @HouseholdId, @ProductId, @IngredientId, @CustomName, @Barcode, @Quantity, @Unit, @Price,
                    @StoreId, @StoreName, @PurchasedAt, @Source)";

        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using SqlCommand command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", record.UserId);
        command.Parameters.AddWithValue("@HouseholdId", record.HouseholdId.HasValue ? record.HouseholdId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@ProductId", record.ProductId.HasValue ? record.ProductId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@IngredientId", record.IngredientId.HasValue ? record.IngredientId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@CustomName", record.CustomName ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Barcode", record.Barcode ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Quantity", record.Quantity);
        command.Parameters.AddWithValue("@Unit", record.Unit ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Price", record.Price.HasValue ? record.Price.Value : DBNull.Value);
        command.Parameters.AddWithValue("@StoreId", record.StoreId.HasValue ? record.StoreId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@StoreName", record.StoreName ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@PurchasedAt", record.PurchasedAt);
        command.Parameters.AddWithValue("@Source", record.Source);

        Guid id = (Guid)(await command.ExecuteScalarAsync(ct))!;
        _logger.LogInformation("Recorded purchase event {EventId} for user {UserId}", id, record.UserId);
        return id;
    }

    public async Task<List<PurchaseEventDto>> GetPurchaseHistoryAsync(Guid userId, Guid? productId, int daysBack, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT Id, UserId, HouseholdId, ProductId, IngredientId, CustomName, Barcode,
                   Quantity, Unit, Price, StoreId, StoreName, PurchasedAt, Source
            FROM PurchaseEvent
            WHERE UserId = @UserId
              AND (@ProductId IS NULL OR ProductId = @ProductId)
              AND PurchasedAt >= DATEADD(day, -@DaysBack, GETUTCDATE())
            ORDER BY PurchasedAt DESC";

        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using SqlCommand command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@ProductId", productId.HasValue ? productId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@DaysBack", daysBack);

        List<PurchaseEventDto> events = new List<PurchaseEventDto>();
        await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            events.Add(MapPurchaseEvent(reader));
        }

        return events;
    }

    private static PurchaseEventDto MapPurchaseEvent(SqlDataReader reader)
    {
        return new PurchaseEventDto
        {
            Id = reader.GetGuid(0),
            UserId = reader.GetGuid(1),
            HouseholdId = reader.IsDBNull(2) ? null : reader.GetGuid(2),
            ProductId = reader.IsDBNull(3) ? null : reader.GetGuid(3),
            IngredientId = reader.IsDBNull(4) ? null : reader.GetGuid(4),
            CustomName = reader.IsDBNull(5) ? null : reader.GetString(5),
            Barcode = reader.IsDBNull(6) ? null : reader.GetString(6),
            Quantity = reader.GetDecimal(7),
            Unit = reader.IsDBNull(8) ? null : reader.GetString(8),
            Price = reader.IsDBNull(9) ? null : reader.GetDecimal(9),
            StoreId = reader.IsDBNull(10) ? null : reader.GetGuid(10),
            StoreName = reader.IsDBNull(11) ? null : reader.GetString(11),
            PurchasedAt = reader.GetDateTime(12),
            Source = reader.GetString(13)
        };
    }

    #endregion

    #region Consumption Patterns

    public async Task UpsertConsumptionPatternAsync(ProductConsumptionPatternRecord pattern, CancellationToken ct = default)
    {
        const string sql = @"
            MERGE ProductConsumptionPattern AS target
            USING (SELECT @UserId AS UserId, @ProductId AS ProductId, @IngredientId AS IngredientId) AS source
            ON (target.UserId = source.UserId
                AND (target.ProductId = source.ProductId OR (target.ProductId IS NULL AND source.ProductId IS NULL))
                AND (target.IngredientId = source.IngredientId OR (target.IngredientId IS NULL AND source.IngredientId IS NULL)))
            WHEN MATCHED THEN
                UPDATE SET
                    HouseholdId               = @HouseholdId,
                    CustomName                = @CustomName,
                    AvgDaysBetweenPurchases   = @AvgDaysBetweenPurchases,
                    StdDevDays                = @StdDevDays,
                    PurchaseCount             = @PurchaseCount,
                    FirstPurchasedAt          = @FirstPurchasedAt,
                    LastPurchasedAt           = @LastPurchasedAt,
                    EstimatedNextPurchaseDate = @EstimatedNextPurchaseDate,
                    LowStockAlertDaysAhead    = @LowStockAlertDaysAhead,
                    IsAbandoned               = @IsAbandoned,
                    AbandonedAfterCount       = @AbandonedAfterCount,
                    CalculatedAt              = GETUTCDATE()
            WHEN NOT MATCHED THEN
                INSERT (UserId, HouseholdId, ProductId, IngredientId, CustomName,
                        AvgDaysBetweenPurchases, StdDevDays, PurchaseCount, FirstPurchasedAt,
                        LastPurchasedAt, EstimatedNextPurchaseDate, LowStockAlertDaysAhead,
                        IsAbandoned, AbandonedAfterCount, CalculatedAt)
                VALUES (@UserId, @HouseholdId, @ProductId, @IngredientId, @CustomName,
                        @AvgDaysBetweenPurchases, @StdDevDays, @PurchaseCount, @FirstPurchasedAt,
                        @LastPurchasedAt, @EstimatedNextPurchaseDate, @LowStockAlertDaysAhead,
                        @IsAbandoned, @AbandonedAfterCount, GETUTCDATE());";

        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using SqlCommand command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", pattern.UserId);
        command.Parameters.AddWithValue("@HouseholdId", pattern.HouseholdId.HasValue ? pattern.HouseholdId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@ProductId", pattern.ProductId.HasValue ? pattern.ProductId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@IngredientId", pattern.IngredientId.HasValue ? pattern.IngredientId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@CustomName", pattern.CustomName ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@AvgDaysBetweenPurchases", pattern.AvgDaysBetweenPurchases.HasValue ? pattern.AvgDaysBetweenPurchases.Value : DBNull.Value);
        command.Parameters.AddWithValue("@StdDevDays", pattern.StdDevDays.HasValue ? pattern.StdDevDays.Value : DBNull.Value);
        command.Parameters.AddWithValue("@PurchaseCount", pattern.PurchaseCount);
        command.Parameters.AddWithValue("@FirstPurchasedAt", pattern.FirstPurchasedAt.HasValue ? pattern.FirstPurchasedAt.Value : DBNull.Value);
        command.Parameters.AddWithValue("@LastPurchasedAt", pattern.LastPurchasedAt.HasValue ? pattern.LastPurchasedAt.Value : DBNull.Value);
        command.Parameters.AddWithValue("@EstimatedNextPurchaseDate", pattern.EstimatedNextPurchaseDate.HasValue ? pattern.EstimatedNextPurchaseDate.Value : DBNull.Value);
        command.Parameters.AddWithValue("@LowStockAlertDaysAhead", pattern.LowStockAlertDaysAhead);
        command.Parameters.AddWithValue("@IsAbandoned", pattern.IsAbandoned);
        command.Parameters.AddWithValue("@AbandonedAfterCount", pattern.AbandonedAfterCount.HasValue ? pattern.AbandonedAfterCount.Value : DBNull.Value);

        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<ProductConsumptionPatternDto>> GetConsumptionPatternsAsync(Guid userId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT Id, UserId, HouseholdId, ProductId, IngredientId, CustomName,
                   AvgDaysBetweenPurchases, StdDevDays, PurchaseCount, FirstPurchasedAt,
                   LastPurchasedAt, EstimatedNextPurchaseDate, LowStockAlertDaysAhead,
                   IsAbandoned, AbandonedAfterCount, CalculatedAt
            FROM ProductConsumptionPattern
            WHERE UserId = @UserId
            ORDER BY CalculatedAt DESC";

        return await ReadConsumptionPatternsAsync(sql, userId, null, ct);
    }

    public async Task<List<ProductConsumptionPatternDto>> GetAbandonedProductsAsync(Guid userId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT Id, UserId, HouseholdId, ProductId, IngredientId, CustomName,
                   AvgDaysBetweenPurchases, StdDevDays, PurchaseCount, FirstPurchasedAt,
                   LastPurchasedAt, EstimatedNextPurchaseDate, LowStockAlertDaysAhead,
                   IsAbandoned, AbandonedAfterCount, CalculatedAt
            FROM ProductConsumptionPattern
            WHERE UserId = @UserId AND IsAbandoned = 1
            ORDER BY LastPurchasedAt ASC";

        return await ReadConsumptionPatternsAsync(sql, userId, null, ct);
    }

    public async Task<List<ProductConsumptionPatternDto>> GetLowStockByPredictionAsync(Guid userId, int daysAhead, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT p.Id, p.UserId, p.HouseholdId, p.ProductId, p.IngredientId, p.CustomName,
                   p.AvgDaysBetweenPurchases, p.StdDevDays, p.PurchaseCount, p.FirstPurchasedAt,
                   p.LastPurchasedAt, p.EstimatedNextPurchaseDate, p.LowStockAlertDaysAhead,
                   p.IsAbandoned, p.AbandonedAfterCount, p.CalculatedAt
            FROM ProductConsumptionPattern p
            WHERE p.UserId = @UserId
              AND p.IsAbandoned = 0
              AND p.EstimatedNextPurchaseDate IS NOT NULL
              AND p.EstimatedNextPurchaseDate <= DATEADD(day, @DaysAhead, GETUTCDATE())
            ORDER BY p.EstimatedNextPurchaseDate ASC";

        return await ReadConsumptionPatternsAsync(sql, userId, daysAhead, ct);
    }

    private async Task<List<ProductConsumptionPatternDto>> ReadConsumptionPatternsAsync(
        string sql, Guid userId, int? daysAhead, CancellationToken ct)
    {
        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using SqlCommand command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        if (daysAhead.HasValue)
        {
            command.Parameters.AddWithValue("@DaysAhead", daysAhead.Value);
        }

        List<ProductConsumptionPatternDto> patterns = new List<ProductConsumptionPatternDto>();
        await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            patterns.Add(new ProductConsumptionPatternDto
            {
                Id = reader.GetGuid(0),
                UserId = reader.GetGuid(1),
                HouseholdId = reader.IsDBNull(2) ? null : reader.GetGuid(2),
                ProductId = reader.IsDBNull(3) ? null : reader.GetGuid(3),
                IngredientId = reader.IsDBNull(4) ? null : reader.GetGuid(4),
                CustomName = reader.IsDBNull(5) ? null : reader.GetString(5),
                AvgDaysBetweenPurchases = reader.IsDBNull(6) ? null : reader.GetDecimal(6),
                StdDevDays = reader.IsDBNull(7) ? null : reader.GetDecimal(7),
                PurchaseCount = reader.GetInt32(8),
                FirstPurchasedAt = reader.IsDBNull(9) ? null : reader.GetDateTime(9),
                LastPurchasedAt = reader.IsDBNull(10) ? null : reader.GetDateTime(10),
                EstimatedNextPurchaseDate = reader.IsDBNull(11) ? null : reader.GetDateTime(11),
                LowStockAlertDaysAhead = reader.GetInt32(12),
                IsAbandoned = reader.GetBoolean(13),
                AbandonedAfterCount = reader.IsDBNull(14) ? null : reader.GetInt32(14),
                CalculatedAt = reader.GetDateTime(15)
            });
        }

        return patterns;
    }

    #endregion

    #region Price Watch Alerts

    public async Task<Guid> CreatePriceWatchAlertAsync(PriceWatchAlertRecord record, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO PriceWatchAlert
            (UserId, HouseholdId, ProductId, InventoryItemId, TargetPrice, WatchStartedAt, DealFound, IsResolved)
            OUTPUT INSERTED.Id
            VALUES (@UserId, @HouseholdId, @ProductId, @InventoryItemId, @TargetPrice, GETUTCDATE(), 0, 0)";

        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using SqlCommand command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", record.UserId);
        command.Parameters.AddWithValue("@HouseholdId", record.HouseholdId.HasValue ? record.HouseholdId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@ProductId", record.ProductId.HasValue ? record.ProductId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@InventoryItemId", record.InventoryItemId.HasValue ? record.InventoryItemId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@TargetPrice", record.TargetPrice.HasValue ? record.TargetPrice.Value : DBNull.Value);

        Guid id = (Guid)(await command.ExecuteScalarAsync(ct))!;
        _logger.LogInformation("Created price watch alert {AlertId} for user {UserId}", id, record.UserId);
        return id;
    }

    public async Task<List<PriceWatchAlertDto>> GetActiveWatchAlertsAsync(CancellationToken ct = default)
    {
        const string sql = @"
            SELECT Id, UserId, HouseholdId, ProductId, InventoryItemId, TargetPrice,
                   WatchStartedAt, AlertSentAt, DealFound, DealStoreId, DealPrice,
                   DealEndsAt, IsResolved, ResolvedAt
            FROM PriceWatchAlert
            WHERE IsResolved = 0
            ORDER BY WatchStartedAt DESC";

        return await ReadPriceWatchAlertsAsync(sql, null, ct);
    }

    public async Task<List<PriceWatchAlertDto>> GetActiveWatchAlertsByUserAsync(Guid userId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT Id, UserId, HouseholdId, ProductId, InventoryItemId, TargetPrice,
                   WatchStartedAt, AlertSentAt, DealFound, DealStoreId, DealPrice,
                   DealEndsAt, IsResolved, ResolvedAt
            FROM PriceWatchAlert
            WHERE UserId = @UserId AND IsResolved = 0
            ORDER BY WatchStartedAt DESC";

        return await ReadPriceWatchAlertsAsync(sql, userId, ct);
    }

    private async Task<List<PriceWatchAlertDto>> ReadPriceWatchAlertsAsync(string sql, Guid? userId, CancellationToken ct)
    {
        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using SqlCommand command = new SqlCommand(sql, connection);
        if (userId.HasValue)
        {
            command.Parameters.AddWithValue("@UserId", userId.Value);
        }

        List<PriceWatchAlertDto> alerts = new List<PriceWatchAlertDto>();
        await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            alerts.Add(new PriceWatchAlertDto
            {
                Id = reader.GetGuid(0),
                UserId = reader.GetGuid(1),
                HouseholdId = reader.IsDBNull(2) ? null : reader.GetGuid(2),
                ProductId = reader.IsDBNull(3) ? null : reader.GetGuid(3),
                InventoryItemId = reader.IsDBNull(4) ? null : reader.GetGuid(4),
                TargetPrice = reader.IsDBNull(5) ? null : reader.GetDecimal(5),
                WatchStartedAt = reader.GetDateTime(6),
                AlertSentAt = reader.IsDBNull(7) ? null : reader.GetDateTime(7),
                DealFound = reader.GetBoolean(8),
                DealStoreId = reader.IsDBNull(9) ? null : reader.GetGuid(9),
                DealPrice = reader.IsDBNull(10) ? null : reader.GetDecimal(10),
                DealEndsAt = reader.IsDBNull(11) ? null : reader.GetDateTime(11),
                IsResolved = reader.GetBoolean(12),
                ResolvedAt = reader.IsDBNull(13) ? null : reader.GetDateTime(13)
            });
        }

        return alerts;
    }

    public async Task UpdatePriceWatchDealFoundAsync(Guid alertId, Guid storeId, decimal dealPrice, DateTime dealEndsAt, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE PriceWatchAlert
            SET DealFound = 1, DealStoreId = @StoreId, DealPrice = @DealPrice,
                DealEndsAt = @DealEndsAt, AlertSentAt = GETUTCDATE()
            WHERE Id = @AlertId";

        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using SqlCommand command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@AlertId", alertId);
        command.Parameters.AddWithValue("@StoreId", storeId);
        command.Parameters.AddWithValue("@DealPrice", dealPrice);
        command.Parameters.AddWithValue("@DealEndsAt", dealEndsAt);

        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task ResolvePriceWatchAlertAsync(Guid alertId, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE PriceWatchAlert
            SET IsResolved = 1, ResolvedAt = GETUTCDATE()
            WHERE Id = @AlertId";

        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using SqlCommand command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@AlertId", alertId);

        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task SetPriceWatchTargetPriceAsync(Guid alertId, decimal targetPrice, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE PriceWatchAlert
            SET TargetPrice = @TargetPrice
            WHERE Id = @AlertId";

        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using SqlCommand command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@AlertId", alertId);
        command.Parameters.AddWithValue("@TargetPrice", targetPrice);

        await command.ExecuteNonQueryAsync(ct);
    }

    #endregion

    #region Abandoned Product Inquiry

    public async Task<Guid> CreateAbandonedInquiryAsync(Guid userId, Guid? productId, string? customName, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO AbandonedProductInquiry (UserId, ProductId, CustomName, NotificationSentAt, IsActioned)
            OUTPUT INSERTED.Id
            VALUES (@UserId, @ProductId, @CustomName, GETUTCDATE(), 0)";

        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using SqlCommand command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@ProductId", productId.HasValue ? productId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@CustomName", customName ?? (object)DBNull.Value);

        Guid id = (Guid)(await command.ExecuteScalarAsync(ct))!;
        _logger.LogInformation("Created abandoned inquiry {InquiryId} for user {UserId}", id, userId);
        return id;
    }

    public async Task RecordInquiryResponseAsync(Guid inquiryId, string response, string? note, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE AbandonedProductInquiry
            SET Response = @Response, ResponseNote = @Note, RespondedAt = GETUTCDATE()
            WHERE Id = @InquiryId";

        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using SqlCommand command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@InquiryId", inquiryId);
        command.Parameters.AddWithValue("@Response", response);
        command.Parameters.AddWithValue("@Note", note ?? (object)DBNull.Value);

        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<AbandonedProductInquiryDto>> GetPendingInquiriesAsync(Guid userId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT Id, UserId, ProductId, CustomName, NotificationSentAt,
                   Response, ResponseNote, RespondedAt, IsActioned
            FROM AbandonedProductInquiry
            WHERE UserId = @UserId AND Response IS NULL
            ORDER BY NotificationSentAt DESC";

        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using SqlCommand command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);

        List<AbandonedProductInquiryDto> inquiries = new List<AbandonedProductInquiryDto>();
        await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            inquiries.Add(new AbandonedProductInquiryDto
            {
                Id = reader.GetGuid(0),
                UserId = reader.GetGuid(1),
                ProductId = reader.IsDBNull(2) ? null : reader.GetGuid(2),
                CustomName = reader.IsDBNull(3) ? null : reader.GetString(3),
                NotificationSentAt = reader.GetDateTime(4),
                Response = reader.IsDBNull(5) ? null : reader.GetString(5),
                ResponseNote = reader.IsDBNull(6) ? null : reader.GetString(6),
                RespondedAt = reader.IsDBNull(7) ? null : reader.GetDateTime(7),
                IsActioned = reader.GetBoolean(8)
            });
        }

        return inquiries;
    }

    #endregion

    #region Waste Report

    public async Task<List<WasteReportMonthDto>> GetWasteReportAsync(Guid userId, Guid? householdId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT
                YEAR(h.CreatedAt)  AS Year,
                MONTH(h.CreatedAt) AS Month,
                SUM(CASE WHEN h.DisposalReason = 'Expired' THEN 1 ELSE 0 END)        AS ExpiredItemsDisposed,
                SUM(CASE WHEN h.DisposalReason = 'CausedAllergy' THEN 1 ELSE 0 END)  AS AllergyDisposed,
                SUM(CASE WHEN h.DisposalReason = 'Bad' THEN 1 ELSE 0 END)            AS BadDisposed,
                SUM(CASE WHEN h.DisposalReason NOT IN ('Expired','CausedAllergy','Bad') AND h.DisposalReason IS NOT NULL THEN 1 ELSE 0 END) AS OtherDisposed,
                SUM(CASE WHEN i.Price IS NOT NULL THEN ABS(h.QuantityChange) * i.Price ELSE 0 END) AS TotalDisposedValue
            FROM InventoryHistory h
            INNER JOIN InventoryItem i ON h.InventoryItemId = i.Id
            WHERE h.UserId = @UserId
              AND (@HouseholdId IS NULL OR i.HouseholdId = @HouseholdId)
              AND h.ActionType IN ('Disposed', 'Removed')
              AND h.DisposalReason IS NOT NULL
            GROUP BY YEAR(h.CreatedAt), MONTH(h.CreatedAt)
            ORDER BY Year DESC, Month DESC";

        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using SqlCommand command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@HouseholdId", householdId.HasValue ? householdId.Value : DBNull.Value);

        List<WasteReportMonthDto> rows = new List<WasteReportMonthDto>();
        await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new WasteReportMonthDto
            {
                Year = reader.GetInt32(0),
                Month = reader.GetInt32(1),
                ExpiredItemsDisposed = reader.GetInt32(2),
                AllergyDisposed = reader.GetInt32(3),
                BadDisposed = reader.GetInt32(4),
                OtherDisposed = reader.GetInt32(5),
                TotalDisposedValue = reader.GetDecimal(6)
            });
        }

        return rows;
    }

    #endregion

    #region Intelligence Helpers

    public async Task<List<Guid>> GetDistinctUserIdsWithInventoryAsync(CancellationToken ct = default)
    {
        const string sql = @"
            SELECT DISTINCT UserId
            FROM InventoryItem
            WHERE IsDeleted = 0 AND ExpirationDate IS NOT NULL";

        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using SqlCommand command = new SqlCommand(sql, connection);

        List<Guid> userIds = new List<Guid>();
        await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            userIds.Add(reader.GetGuid(0));
        }

        return userIds;
    }

    public async Task<List<Guid>> GetDistinctUserIdsWithPurchaseHistoryAsync(CancellationToken ct = default)
    {
        const string sql = "SELECT DISTINCT UserId FROM PurchaseEvent";

        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using SqlCommand command = new SqlCommand(sql, connection);

        List<Guid> userIds = new List<Guid>();
        await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            userIds.Add(reader.GetGuid(0));
        }

        return userIds;
    }

    public async Task WriteInventoryHistoryDirectAsync(
        Guid itemId, Guid userId, string actionType,
        decimal quantityChange, decimal quantityBefore, decimal quantityAfter,
        string? reason, Guid? recipeId, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO InventoryHistory
            (InventoryItemId, UserId, ActionType, QuantityChange, QuantityBefore, QuantityAfter, Reason, RecipeId, CreatedAt)
            VALUES (@ItemId, @UserId, @ActionType, @Change, @Before, @After, @Reason, @RecipeId, GETUTCDATE())";

        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using SqlCommand command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ItemId", itemId);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@ActionType", actionType);
        command.Parameters.AddWithValue("@Change", quantityChange);
        command.Parameters.AddWithValue("@Before", quantityBefore);
        command.Parameters.AddWithValue("@After", quantityAfter);
        command.Parameters.AddWithValue("@Reason", reason ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@RecipeId", recipeId.HasValue ? recipeId.Value : DBNull.Value);

        await command.ExecuteNonQueryAsync(ct);
    }

    #endregion
}
