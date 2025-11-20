using Microsoft.Data.SqlClient;

namespace ExpressRecipe.RecallService.Data;

public class RecallRepository : IRecallRepository
{
    private readonly string _connectionString;
    private readonly ILogger<RecallRepository> _logger;

    public RecallRepository(string connectionString, ILogger<RecallRepository> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task<Guid> CreateRecallAsync(string recallNumber, string source, string title, string description, string severity, DateTime recallDate, string? reason, string? distributionArea)
    {
        const string sql = @"
            INSERT INTO Recall (RecallNumber, Source, Title, Description, Severity, RecallDate, Reason, DistributionArea, Status, CreatedAt)
            OUTPUT INSERTED.Id
            VALUES (@RecallNumber, @Source, @Title, @Description, @Severity, @RecallDate, @Reason, @DistributionArea, 'Active', GETUTCDATE())";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@RecallNumber", recallNumber);
        command.Parameters.AddWithValue("@Source", source);
        command.Parameters.AddWithValue("@Title", title);
        command.Parameters.AddWithValue("@Description", description);
        command.Parameters.AddWithValue("@Severity", severity);
        command.Parameters.AddWithValue("@RecallDate", recallDate);
        command.Parameters.AddWithValue("@Reason", reason ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@DistributionArea", distributionArea ?? (object)DBNull.Value);

        return (Guid)await command.ExecuteScalarAsync()!;
    }

    public async Task<List<RecallDto>> GetRecentRecallsAsync(int limit = 100)
    {
        const string sql = @"
            SELECT TOP (@Limit) r.Id, r.RecallNumber, r.Source, r.Title, r.Description, r.Severity, r.RecallDate, r.Reason, r.DistributionArea, r.Status, r.CreatedAt,
                   (SELECT COUNT(*) FROM RecallProduct WHERE RecallId = r.Id) AS AffectedProductCount
            FROM Recall r
            ORDER BY r.RecallDate DESC";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Limit", limit);

        var recalls = new List<RecallDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            recalls.Add(new RecallDto
            {
                Id = reader.GetGuid(0),
                RecallNumber = reader.GetString(1),
                Source = reader.GetString(2),
                Title = reader.GetString(3),
                Description = reader.GetString(4),
                Severity = reader.GetString(5),
                RecallDate = reader.GetDateTime(6),
                Reason = reader.IsDBNull(7) ? null : reader.GetString(7),
                DistributionArea = reader.IsDBNull(8) ? null : reader.GetString(8),
                Status = reader.GetString(9),
                CreatedAt = reader.GetDateTime(10),
                AffectedProductCount = reader.GetInt32(11)
            });
        }

        return recalls;
    }

    public async Task<List<RecallDto>> SearchRecallsAsync(string searchTerm, string? severity = null, DateTime? startDate = null, DateTime? endDate = null)
    {
        return await GetRecentRecallsAsync(100); // Stub
    }

    public async Task<RecallDto?> GetRecallAsync(Guid recallId)
    {
        const string sql = @"
            SELECT r.Id, r.RecallNumber, r.Source, r.Title, r.Description, r.Severity, r.RecallDate, r.Reason, r.DistributionArea, r.Status, r.CreatedAt,
                   (SELECT COUNT(*) FROM RecallProduct WHERE RecallId = r.Id) AS AffectedProductCount
            FROM Recall r
            WHERE r.Id = @RecallId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@RecallId", recallId);

        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new RecallDto
            {
                Id = reader.GetGuid(0),
                RecallNumber = reader.GetString(1),
                Source = reader.GetString(2),
                Title = reader.GetString(3),
                Description = reader.GetString(4),
                Severity = reader.GetString(5),
                RecallDate = reader.GetDateTime(6),
                Reason = reader.IsDBNull(7) ? null : reader.GetString(7),
                DistributionArea = reader.IsDBNull(8) ? null : reader.GetString(8),
                Status = reader.GetString(9),
                CreatedAt = reader.GetDateTime(10),
                AffectedProductCount = reader.GetInt32(11)
            };
        }

        return null;
    }

    public async Task UpdateRecallAsync(Guid recallId, string status)
    {
        const string sql = "UPDATE Recall SET Status = @Status WHERE Id = @RecallId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@RecallId", recallId);
        command.Parameters.AddWithValue("@Status", status);

        await command.ExecuteNonQueryAsync();
    }

    public async Task AddProductToRecallAsync(Guid recallId, string productName, string? brand, string? upc, string? lotCode)
    {
        const string sql = @"
            INSERT INTO RecallProduct (RecallId, ProductName, Brand, UPC, LotCode)
            VALUES (@RecallId, @ProductName, @Brand, @UPC, @LotCode)";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@RecallId", recallId);
        command.Parameters.AddWithValue("@ProductName", productName);
        command.Parameters.AddWithValue("@Brand", brand ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@UPC", upc ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@LotCode", lotCode ?? (object)DBNull.Value);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<List<RecallProductDto>> GetRecallProductsAsync(Guid recallId)
    {
        const string sql = "SELECT Id, RecallId, ProductName, Brand, UPC, LotCode FROM RecallProduct WHERE RecallId = @RecallId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@RecallId", recallId);

        var products = new List<RecallProductDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            products.Add(new RecallProductDto
            {
                Id = reader.GetGuid(0),
                RecallId = reader.GetGuid(1),
                ProductName = reader.GetString(2),
                Brand = reader.IsDBNull(3) ? null : reader.GetString(3),
                UPC = reader.IsDBNull(4) ? null : reader.GetString(4),
                LotCode = reader.IsDBNull(5) ? null : reader.GetString(5)
            });
        }

        return products;
    }

    public async Task<List<RecallDto>> GetRecallsByProductAsync(string productName, string? brand = null, string? upc = null)
    {
        return new List<RecallDto>(); // Stub
    }

    public async Task<Guid> CreateRecallAlertAsync(Guid userId, Guid recallId, string matchType, string matchedValue, bool isAcknowledged)
    {
        const string sql = @"
            INSERT INTO RecallAlert (UserId, RecallId, MatchType, MatchedValue, IsAcknowledged, CreatedAt)
            OUTPUT INSERTED.Id
            VALUES (@UserId, @RecallId, @MatchType, @MatchedValue, @IsAcknowledged, GETUTCDATE())";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@RecallId", recallId);
        command.Parameters.AddWithValue("@MatchType", matchType);
        command.Parameters.AddWithValue("@MatchedValue", matchedValue);
        command.Parameters.AddWithValue("@IsAcknowledged", isAcknowledged);

        return (Guid)await command.ExecuteScalarAsync()!;
    }

    public async Task<List<RecallAlertDto>> GetUserAlertsAsync(Guid userId, bool unacknowledgedOnly = true)
    {
        var sql = @"
            SELECT ra.Id, ra.UserId, ra.RecallId, r.Title AS RecallTitle, r.Severity, ra.MatchType, ra.MatchedValue, ra.IsAcknowledged, ra.AcknowledgedAt, ra.CreatedAt
            FROM RecallAlert ra
            INNER JOIN Recall r ON ra.RecallId = r.Id
            WHERE ra.UserId = @UserId";

        if (unacknowledgedOnly)
            sql += " AND ra.IsAcknowledged = 0";

        sql += " ORDER BY ra.CreatedAt DESC";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);

        var alerts = new List<RecallAlertDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            alerts.Add(new RecallAlertDto
            {
                Id = reader.GetGuid(0),
                UserId = reader.GetGuid(1),
                RecallId = reader.GetGuid(2),
                RecallTitle = reader.GetString(3),
                Severity = reader.GetString(4),
                MatchType = reader.GetString(5),
                MatchedValue = reader.GetString(6),
                IsAcknowledged = reader.GetBoolean(7),
                AcknowledgedAt = reader.IsDBNull(8) ? null : reader.GetDateTime(8),
                CreatedAt = reader.GetDateTime(9)
            });
        }

        return alerts;
    }

    public async Task AcknowledgeAlertAsync(Guid alertId)
    {
        const string sql = "UPDATE RecallAlert SET IsAcknowledged = 1, AcknowledgedAt = GETUTCDATE() WHERE Id = @AlertId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@AlertId", alertId);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<int> GetUnacknowledgedCountAsync(Guid userId)
    {
        const string sql = "SELECT COUNT(*) FROM RecallAlert WHERE UserId = @UserId AND IsAcknowledged = 0";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);

        return (int)await command.ExecuteScalarAsync()!;
    }

    public async Task<Guid> SubscribeToRecallsAsync(Guid userId, string? category = null, string? brand = null, string? keyword = null)
    {
        const string sql = @"
            INSERT INTO RecallSubscription (UserId, Category, Brand, Keyword, IsActive, CreatedAt)
            OUTPUT INSERTED.Id
            VALUES (@UserId, @Category, @Brand, @Keyword, 1, GETUTCDATE())";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@Category", category ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Brand", brand ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Keyword", keyword ?? (object)DBNull.Value);

        return (Guid)await command.ExecuteScalarAsync()!;
    }

    public async Task<List<RecallSubscriptionDto>> GetUserSubscriptionsAsync(Guid userId)
    {
        const string sql = @"
            SELECT Id, UserId, Category, Brand, Keyword, IsActive, CreatedAt
            FROM RecallSubscription
            WHERE UserId = @UserId
            ORDER BY CreatedAt DESC";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);

        var subs = new List<RecallSubscriptionDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            subs.Add(new RecallSubscriptionDto
            {
                Id = reader.GetGuid(0),
                UserId = reader.GetGuid(1),
                Category = reader.IsDBNull(2) ? null : reader.GetString(2),
                Brand = reader.IsDBNull(3) ? null : reader.GetString(3),
                Keyword = reader.IsDBNull(4) ? null : reader.GetString(4),
                IsActive = reader.GetBoolean(5),
                CreatedAt = reader.GetDateTime(6)
            });
        }

        return subs;
    }

    public async Task UnsubscribeAsync(Guid subscriptionId)
    {
        const string sql = "UPDATE RecallSubscription SET IsActive = 0 WHERE Id = @SubscriptionId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@SubscriptionId", subscriptionId);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<List<Guid>> GetAffectedUsersAsync(Guid recallId)
    {
        return new List<Guid>(); // Stub
    }
}
