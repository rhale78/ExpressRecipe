using ExpressRecipe.Data.Common;
using ExpressRecipe.Shared.Services;

namespace ExpressRecipe.RecallService.Data;

public class RecallRepository : SqlHelper, IRecallRepository
{
    private readonly ILogger<RecallRepository> _logger;
    private readonly HybridCacheService? _cache;
    private const string CachePrefix = "recall:";

    public RecallRepository(string connectionString, ILogger<RecallRepository> logger, HybridCacheService? cache = null)
        : base(connectionString)
    {
        _logger = logger;
        _cache = cache;
    }

    public async Task<Guid> CreateRecallAsync(string recallNumber, string source, string title, string description, string severity, DateTime recallDate, string? reason, string? distributionArea)
    {
        // Map to common DAL schema: ExternalId, Source, Title, Description, Severity, RecallDate, PublishedDate, Reason, Status
        const string sql = @"
            INSERT INTO Recall (ExternalId, Source, Title, Description, Severity, RecallDate, PublishedDate, Reason, Status, ImportedAt)
            OUTPUT INSERTED.Id
            VALUES (@ExternalId, @Source, @Title, @Description, @Severity, @RecallDate, @PublishedDate, @Reason, 'Active', GETUTCDATE())";

        var id = (await ExecuteScalarAsync<Guid>(sql,
            CreateParameter("@ExternalId", recallNumber),
            CreateParameter("@Source", source),
            CreateParameter("@Title", title),
            CreateParameter("@Description", description),
            CreateParameter("@Severity", severity),
            CreateParameter("@RecallDate", recallDate),
            CreateParameter("@PublishedDate", recallDate),
            CreateParameter("@Reason", (object?)reason ?? DBNull.Value)))!;

        // Optionally store distributionArea via RecallProduct with a generic product entry
        if (!string.IsNullOrEmpty(distributionArea))
        {
            await AddProductToRecallAsync(id, title, null, null, distributionArea);
        }

        // A new recall changes the "recent" list — evict common limit variants
        if (_cache != null)
        {
            foreach (var lim in new[] { 50, 100, 200 })
                await _cache.RemoveAsync($"{CachePrefix}recent:{lim}");
        }

        return id;
    }

    private static RecallDto MapRecall(System.Data.IDataRecord reader) => new RecallDto
    {
        Id = SqlHelper.GetGuid(reader, "Id"),
        RecallNumber = SqlHelper.GetString(reader, "ExternalId")!,
        Source = SqlHelper.GetString(reader, "Source")!,
        Title = SqlHelper.GetString(reader, "Title")!,
        Description = SqlHelper.GetString(reader, "Description") ?? string.Empty,
        Severity = SqlHelper.GetString(reader, "Severity")!,
        RecallDate = SqlHelper.GetDateTime(reader, "RecallDate"),
        Reason = SqlHelper.GetString(reader, "Reason"),
        Status = SqlHelper.GetString(reader, "Status")!,
        CreatedAt = SqlHelper.GetDateTime(reader, "PublishedDate"),
        AffectedProductCount = SqlHelper.GetInt32(reader, "AffectedProductCount")
    };

    public async Task<List<RecallDto>> GetRecentRecallsAsync(int limit = 100)
    {
        if (_cache != null)
        {
            return await _cache.GetOrSetAsync(
                $"{CachePrefix}recent:{limit}",
                async (ct) => await GetRecentRecallsFromDbAsync(limit),
                expiration: TimeSpan.FromMinutes(15)) ?? new List<RecallDto>();
        }

        return await GetRecentRecallsFromDbAsync(limit);
    }

    private async Task<List<RecallDto>> GetRecentRecallsFromDbAsync(int limit)
    {
        const string sql = @"
            SELECT TOP (@Limit) r.Id, r.ExternalId, r.Source, r.Title, r.Description, r.Severity, r.RecallDate, r.Reason, r.Status, r.PublishedDate,
                   (SELECT COUNT(*) FROM RecallProduct rp WHERE rp.RecallId = r.Id) AS AffectedProductCount
            FROM Recall r
            ORDER BY r.PublishedDate DESC";

        return await ExecuteReaderAsync<RecallDto>(sql, MapRecall, CreateParameter("@Limit", limit));
    }

    public async Task<List<RecallDto>> SearchRecallsAsync(string searchTerm, string? severity = null, DateTime? startDate = null, DateTime? endDate = null)
    {
        var sql = @"SELECT r.Id, r.ExternalId, r.Source, r.Title, r.Description, r.Severity, r.RecallDate, r.Reason, r.Status, r.PublishedDate,
                           (SELECT COUNT(*) FROM RecallProduct rp WHERE rp.RecallId = r.Id) AS AffectedProductCount
                    FROM Recall r
                    WHERE (r.Title LIKE @Search OR r.Description LIKE @Search)";
        if (!string.IsNullOrEmpty(severity)) sql += " AND r.Severity = @Severity";
        if (startDate.HasValue) sql += " AND r.PublishedDate >= @StartDate";
        if (endDate.HasValue) sql += " AND r.PublishedDate <= @EndDate";
        sql += " ORDER BY r.PublishedDate DESC";

        var paramList = new List<System.Data.Common.DbParameter>
        {
            CreateParameter("@Search", $"%{searchTerm}%")
        };
        if (!string.IsNullOrEmpty(severity)) paramList.Add(CreateParameter("@Severity", severity));
        if (startDate.HasValue) paramList.Add(CreateParameter("@StartDate", startDate.Value));
        if (endDate.HasValue) paramList.Add(CreateParameter("@EndDate", endDate.Value));

        return await ExecuteReaderAsync<RecallDto>(sql, MapRecall, paramList.ToArray());
    }

    public async Task<RecallDto?> GetRecallAsync(Guid recallId)
    {
        if (_cache != null)
        {
            return await _cache.GetOrSetAsync(
                $"{CachePrefix}id:{recallId}",
                async (ct) => await GetRecallFromDbAsync(recallId),
                expiration: TimeSpan.FromHours(2));
        }

        return await GetRecallFromDbAsync(recallId);
    }

    private async Task<RecallDto?> GetRecallFromDbAsync(Guid recallId)
    {
        const string sql = @"
            SELECT r.Id, r.ExternalId, r.Source, r.Title, r.Description, r.Severity, r.RecallDate, r.Reason, r.Status, r.PublishedDate,
                   (SELECT COUNT(*) FROM RecallProduct rp WHERE rp.RecallId = r.Id) AS AffectedProductCount
            FROM Recall r
            WHERE r.Id = @RecallId";

        var results = await ExecuteReaderAsync<RecallDto>(sql, MapRecall, CreateParameter("@RecallId", recallId));
        return results.FirstOrDefault();
    }

    public async Task UpdateRecallAsync(Guid recallId, string status)
    {
        const string sql = "UPDATE Recall SET Status = @Status WHERE Id = @RecallId";
        await ExecuteNonQueryAsync(sql,
            CreateParameter("@RecallId", recallId),
            CreateParameter("@Status", status));

        if (_cache != null)
            await _cache.RemoveAsync($"{CachePrefix}id:{recallId}");
    }

    public async Task AddProductToRecallAsync(Guid recallId, string productName, string? brand, string? upc, string? lotCode)
    {
        // Map to common DAL schema: RecallProduct has LotNumber, DistributionArea, no UPC/LotCode fields in DTO. Store UPC/LotCode into LotNumber.
        const string sql = @"
            INSERT INTO RecallProduct (RecallId, ProductName, Brand, LotNumber, DistributionArea)
            VALUES (@RecallId, @ProductName, @Brand, @LotNumber, @DistributionArea)";

        await ExecuteNonQueryAsync(sql,
            CreateParameter("@RecallId", recallId),
            CreateParameter("@ProductName", productName),
            CreateParameter("@Brand", (object?)brand ?? DBNull.Value),
            CreateParameter("@LotNumber", (object?)(upc ?? lotCode) ?? DBNull.Value),
            CreateParameter("@DistributionArea", DBNull.Value));
    }

    public async Task<List<RecallProductDto>> GetRecallProductsAsync(Guid recallId)
    {
        const string sql = "SELECT Id, RecallId, ProductName, Brand, LotNumber, DistributionArea FROM RecallProduct WHERE RecallId = @RecallId";

        return await ExecuteReaderAsync<RecallProductDto>(sql, reader => new RecallProductDto
        {
            Id = GetGuid(reader, "Id"),
            RecallId = GetGuid(reader, "RecallId"),
            ProductName = GetString(reader, "ProductName")!,
            Brand = GetString(reader, "Brand"),
            UPC = null,
            LotCode = GetString(reader, "LotNumber")
        },
        CreateParameter("@RecallId", recallId));
    }

    public async Task<List<RecallDto>> GetRecallsByProductAsync(string productName, string? brand = null, string? upc = null)
    {
        var sql = @"SELECT r.Id, r.ExternalId, r.Source, r.Title, r.Description, r.Severity, r.RecallDate, r.Reason, r.Status, r.PublishedDate,
                           (SELECT COUNT(*) FROM RecallProduct rp WHERE rp.RecallId = r.Id) AS AffectedProductCount
                    FROM Recall r
                    WHERE EXISTS (
                        SELECT 1 FROM RecallProduct rp
                        WHERE rp.RecallId = r.Id AND rp.ProductName LIKE @ProductName";
        if (!string.IsNullOrEmpty(brand)) sql += " AND rp.Brand = @Brand";
        if (!string.IsNullOrEmpty(upc)) sql += " AND rp.LotNumber = @UPC";
        sql += ") ORDER BY r.PublishedDate DESC";

        var paramList = new List<System.Data.Common.DbParameter>
        {
            CreateParameter("@ProductName", $"%{productName}%")
        };
        if (!string.IsNullOrEmpty(brand)) paramList.Add(CreateParameter("@Brand", brand));
        if (!string.IsNullOrEmpty(upc)) paramList.Add(CreateParameter("@UPC", upc));

        return await ExecuteReaderAsync<RecallDto>(sql, MapRecall, paramList.ToArray());
    }

    public async Task<Guid> CreateRecallAlertAsync(Guid userId, Guid recallId, string matchType, string matchedValue, bool isAcknowledged)
    {
        // Map to RecallAlert: AlertType, IsRead; no MatchedValue column exists in schema, so ignore
        const string sql = @"
            INSERT INTO RecallAlert (UserId, RecallId, AlertType, IsRead)
            OUTPUT INSERTED.Id
            VALUES (@UserId, @RecallId, @AlertType, @IsRead)";

        return (await ExecuteScalarAsync<Guid>(sql,
            CreateParameter("@UserId", userId),
            CreateParameter("@RecallId", recallId),
            CreateParameter("@AlertType", matchType),
            CreateParameter("@IsRead", isAcknowledged)))!;
    }

    public async Task<List<RecallAlertDto>> GetUserAlertsAsync(Guid userId, bool unacknowledgedOnly = true)
    {
        var sql = @"SELECT a.Id, a.UserId, a.RecallId, r.Title, r.Severity, a.AlertType, a.IsRead, a.ReadAt, a.CreatedAt
                    FROM RecallAlert a
                    JOIN Recall r ON r.Id = a.RecallId
                    WHERE a.UserId = @UserId";
        if (unacknowledgedOnly) sql += " AND a.IsRead = 0";
        sql += " ORDER BY a.CreatedAt DESC";

        return await ExecuteReaderAsync<RecallAlertDto>(sql, reader => new RecallAlertDto
        {
            Id = GetGuid(reader, "Id"),
            UserId = GetGuid(reader, "UserId"),
            RecallId = GetGuid(reader, "RecallId"),
            RecallTitle = GetString(reader, "Title")!,
            Severity = GetString(reader, "Severity")!,
            MatchType = GetString(reader, "AlertType")!,
            MatchedValue = string.Empty,
            IsAcknowledged = GetBoolean(reader, "IsRead"),
            AcknowledgedAt = GetNullableDateTime(reader, "ReadAt"),
            CreatedAt = GetDateTime(reader, "CreatedAt")
        },
        CreateParameter("@UserId", userId));
    }

    public async Task AcknowledgeAlertAsync(Guid alertId)
    {
        const string sql = "UPDATE RecallAlert SET IsRead = 1, ReadAt = GETUTCDATE() WHERE Id = @Id";
        await ExecuteNonQueryAsync(sql, CreateParameter("@Id", alertId));
    }

    public async Task<int> GetUnacknowledgedCountAsync(Guid userId)
    {
        const string sql = "SELECT COUNT(*) FROM RecallAlert WHERE UserId = @UserId AND IsRead = 0";
        return (await ExecuteScalarAsync<int>(sql, CreateParameter("@UserId", userId)))!;
    }

    public async Task<Guid> SubscribeToRecallsAsync(Guid userId, string? category = null, string? brand = null, string? keyword = null)
    {
        const string sql = @"
            INSERT INTO RecallSubscription (UserId, SubscriptionType, FilterValue)
            OUTPUT INSERTED.Id
            VALUES (@UserId, @Type, @Value)";

        var type = !string.IsNullOrEmpty(category) ? "ByCategory" : (!string.IsNullOrEmpty(brand) ? "ByBrand" : "Keyword");
        var value = category ?? brand ?? keyword ?? string.Empty;

        return (await ExecuteScalarAsync<Guid>(sql,
            CreateParameter("@UserId", userId),
            CreateParameter("@Type", type),
            CreateParameter("@Value", value)))!;
    }

    public async Task<List<RecallSubscriptionDto>> GetUserSubscriptionsAsync(Guid userId)
    {
        const string sql = @"SELECT Id, UserId, SubscriptionType, FilterValue, IsActive, CreatedAt FROM RecallSubscription WHERE UserId = @UserId";

        return await ExecuteReaderAsync<RecallSubscriptionDto>(sql, reader =>
        {
            var subType = GetString(reader, "SubscriptionType")!;
            var filterValue = GetString(reader, "FilterValue")!;
            return new RecallSubscriptionDto
            {
                Id = GetGuid(reader, "Id"),
                UserId = GetGuid(reader, "UserId"),
                Category = subType == "ByCategory" ? filterValue : null,
                Brand = subType == "ByBrand" ? filterValue : null,
                Keyword = subType == "Keyword" ? filterValue : null,
                IsActive = GetBoolean(reader, "IsActive"),
                CreatedAt = GetDateTime(reader, "CreatedAt")
            };
        },
        CreateParameter("@UserId", userId));
    }

    public async Task UnsubscribeAsync(Guid subscriptionId)
    {
        const string sql = "UPDATE RecallSubscription SET IsActive = 0 WHERE Id = @Id";
        await ExecuteNonQueryAsync(sql, CreateParameter("@Id", subscriptionId));
    }

    public async Task<List<Guid>> GetAffectedUsersAsync(Guid recallId)
    {
        // Simple join: users with subscriptions matching brand keyword in RecallProduct brand
        const string sql = @"
            SELECT DISTINCT rs.UserId
            FROM RecallSubscription rs
            JOIN RecallProduct rp ON rp.RecallId = @RecallId
            WHERE rs.IsActive = 1 AND (
                (rs.SubscriptionType = 'ByBrand' AND rs.FilterValue = rp.Brand) OR
                (rs.SubscriptionType = 'Keyword' AND rp.ProductName LIKE '%' + rs.FilterValue + '%')
            )";

        return await ExecuteReaderAsync<Guid>(sql,
            reader => GetGuid(reader, "UserId"),
            CreateParameter("@RecallId", recallId));
    }

    public async Task DeleteUserDataAsync(Guid userId, CancellationToken ct = default)
    {
        const string sql = @"
DELETE FROM RecallAlert        WHERE UserId = @UserId;
DELETE FROM RecallSubscription WHERE UserId = @UserId;";

        await ExecuteNonQueryAsync(sql, ct, CreateParameter("@UserId", userId));
    }
}
