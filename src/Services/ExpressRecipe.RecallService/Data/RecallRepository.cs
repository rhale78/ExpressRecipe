using Microsoft.Data.SqlClient;

namespace ExpressRecipe.RecallService.Data
{
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
            // Map to common DAL schema: ExternalId, Source, Title, Description, Severity, RecallDate, PublishedDate, Reason, Status
            const string sql = @"
            INSERT INTO Recall (ExternalId, Source, Title, Description, Severity, RecallDate, PublishedDate, Reason, Status, ImportedAt)
            OUTPUT INSERTED.Id
            VALUES (@ExternalId, @Source, @Title, @Description, @Severity, @RecallDate, @PublishedDate, @Reason, 'Active', GETUTCDATE())";

            await using SqlConnection connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            await using SqlCommand command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@ExternalId", recallNumber);
            command.Parameters.AddWithValue("@Source", source);
            command.Parameters.AddWithValue("@Title", title);
            command.Parameters.AddWithValue("@Description", description);
            command.Parameters.AddWithValue("@Severity", severity);
            command.Parameters.AddWithValue("@RecallDate", recallDate);
            command.Parameters.AddWithValue("@PublishedDate", recallDate);
            command.Parameters.AddWithValue("@Reason", (object?)reason ?? DBNull.Value);

            Guid id = (Guid)await command.ExecuteScalarAsync()!;

            // Optionally store distributionArea via RecallProduct with a generic product entry
            if (!string.IsNullOrEmpty(distributionArea))
            {
                await AddProductToRecallAsync(id, title, null, null, distributionArea);
            }

            return id;
        }

        public async Task<List<RecallDto>> GetRecentRecallsAsync(int limit = 100)
        {
            const string sql = @"
            SELECT TOP (@Limit) r.Id, r.ExternalId, r.Source, r.Title, r.Description, r.Severity, r.RecallDate, r.Reason, r.Status, r.PublishedDate,
                   (SELECT COUNT(*) FROM RecallProduct rp WHERE rp.RecallId = r.Id) AS AffectedProductCount
            FROM Recall r
            ORDER BY r.PublishedDate DESC";

            await using SqlConnection connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            await using SqlCommand command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@Limit", limit);

            List<RecallDto> recalls = [];
            await using SqlDataReader reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                recalls.Add(new RecallDto
                {
                    Id = reader.GetGuid(0),
                    RecallNumber = reader.GetString(1),
                    Source = reader.GetString(2),
                    Title = reader.GetString(3),
                    Description = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                    Severity = reader.GetString(5),
                    RecallDate = reader.GetDateTime(6),
                    Reason = reader.IsDBNull(7) ? null : reader.GetString(7),
                    Status = reader.GetString(8),
                    CreatedAt = reader.GetDateTime(9),
                    AffectedProductCount = reader.GetInt32(10)
                });
            }

            return recalls;
        }

        public async Task<List<RecallDto>> SearchRecallsAsync(string searchTerm, string? severity = null, DateTime? startDate = null, DateTime? endDate = null)
        {
            var sql = @"SELECT r.Id, r.ExternalId, r.Source, r.Title, r.Description, r.Severity, r.RecallDate, r.Reason, r.Status, r.PublishedDate,
                           (SELECT COUNT(*) FROM RecallProduct rp WHERE rp.RecallId = r.Id) AS AffectedProductCount
                    FROM Recall r
                    WHERE (r.Title LIKE @Search OR r.Description LIKE @Search)";
            if (!string.IsNullOrEmpty(severity))
            {
                sql += " AND r.Severity = @Severity";
            }

            if (startDate.HasValue)
            {
                sql += " AND r.PublishedDate >= @StartDate";
            }

            if (endDate.HasValue)
            {
                sql += " AND r.PublishedDate <= @EndDate";
            }

            sql += " ORDER BY r.PublishedDate DESC";

            await using SqlConnection connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            await using SqlCommand command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@Search", $"%{searchTerm}%");
            if (!string.IsNullOrEmpty(severity))
            {
                command.Parameters.AddWithValue("@Severity", severity);
            }

            if (startDate.HasValue)
            {
                command.Parameters.AddWithValue("@StartDate", startDate.Value);
            }

            if (endDate.HasValue)
            {
                command.Parameters.AddWithValue("@EndDate", endDate.Value);
            }

            List<RecallDto> recalls = [];
            await using SqlDataReader reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                recalls.Add(new RecallDto
                {
                    Id = reader.GetGuid(0),
                    RecallNumber = reader.GetString(1),
                    Source = reader.GetString(2),
                    Title = reader.GetString(3),
                    Description = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                    Severity = reader.GetString(5),
                    RecallDate = reader.GetDateTime(6),
                    Reason = reader.IsDBNull(7) ? null : reader.GetString(7),
                    Status = reader.GetString(8),
                    CreatedAt = reader.GetDateTime(9),
                    AffectedProductCount = reader.GetInt32(10)
                });
            }

            return recalls;
        }

        public async Task<RecallDto?> GetRecallAsync(Guid recallId)
        {
            const string sql = @"
            SELECT r.Id, r.ExternalId, r.Source, r.Title, r.Description, r.Severity, r.RecallDate, r.Reason, r.Status, r.PublishedDate,
                   (SELECT COUNT(*) FROM RecallProduct rp WHERE rp.RecallId = r.Id) AS AffectedProductCount
            FROM Recall r
            WHERE r.Id = @RecallId";

            await using SqlConnection connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            await using SqlCommand command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@RecallId", recallId);

            await using SqlDataReader reader = await command.ExecuteReaderAsync();
            return await reader.ReadAsync()
                ? new RecallDto
                {
                    Id = reader.GetGuid(0),
                    RecallNumber = reader.GetString(1),
                    Source = reader.GetString(2),
                    Title = reader.GetString(3),
                    Description = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                    Severity = reader.GetString(5),
                    RecallDate = reader.GetDateTime(6),
                    Reason = reader.IsDBNull(7) ? null : reader.GetString(7),
                    Status = reader.GetString(8),
                    CreatedAt = reader.GetDateTime(9),
                    AffectedProductCount = reader.GetInt32(10)
                }
                : null;
        }

        public async Task UpdateRecallAsync(Guid recallId, string status)
        {
            const string sql = "UPDATE Recall SET Status = @Status WHERE Id = @RecallId";

            await using SqlConnection connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            await using SqlCommand command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@RecallId", recallId);
            command.Parameters.AddWithValue("@Status", status);

            await command.ExecuteNonQueryAsync();
        }

        public async Task AddProductToRecallAsync(Guid recallId, string productName, string? brand, string? upc, string? lotCode)
        {
            // Map to common DAL schema: RecallProduct has LotNumber, DistributionArea, no UPC/LotCode fields in DTO. Store UPC/LotCode into LotNumber.
            const string sql = @"
            INSERT INTO RecallProduct (RecallId, ProductName, Brand, LotNumber, DistributionArea)
            VALUES (@RecallId, @ProductName, @Brand, @LotNumber, @DistributionArea)";

            await using SqlConnection connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            await using SqlCommand command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@RecallId", recallId);
            command.Parameters.AddWithValue("@ProductName", productName);
            command.Parameters.AddWithValue("@Brand", (object?)brand ?? DBNull.Value);
            command.Parameters.AddWithValue("@LotNumber", (object?)(upc ?? lotCode) ?? DBNull.Value);
            command.Parameters.AddWithValue("@DistributionArea", DBNull.Value);

            await command.ExecuteNonQueryAsync();
        }

        public async Task<List<RecallProductDto>> GetRecallProductsAsync(Guid recallId)
        {
            const string sql = "SELECT Id, RecallId, ProductName, Brand, LotNumber, DistributionArea FROM RecallProduct WHERE RecallId = @RecallId";

            await using SqlConnection connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            await using SqlCommand command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@RecallId", recallId);

            List<RecallProductDto> products = [];
            await using SqlDataReader reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                products.Add(new RecallProductDto
                {
                    Id = reader.GetGuid(0),
                    RecallId = reader.GetGuid(1),
                    ProductName = reader.GetString(2),
                    Brand = reader.IsDBNull(3) ? null : reader.GetString(3),
                    UPC = null,
                    LotCode = reader.IsDBNull(4) ? null : reader.GetString(4)
                });
            }

            return products;
        }

        public async Task<List<RecallDto>> GetRecallsByProductAsync(string productName, string? brand = null, string? upc = null)
        {
            var sql = @"SELECT r.Id, r.ExternalId, r.Source, r.Title, r.Description, r.Severity, r.RecallDate, r.Reason, r.Status, r.PublishedDate,
                           (SELECT COUNT(*) FROM RecallProduct rp WHERE rp.RecallId = r.Id) AS AffectedProductCount
                    FROM Recall r
                    WHERE EXISTS (
                        SELECT 1 FROM RecallProduct rp
                        WHERE rp.RecallId = r.Id AND rp.ProductName LIKE @ProductName";
            if (!string.IsNullOrEmpty(brand))
            {
                sql += " AND rp.Brand = @Brand";
            }

            if (!string.IsNullOrEmpty(upc))
            {
                sql += " AND rp.LotNumber = @UPC";
            }

            sql += ") ORDER BY r.PublishedDate DESC";

            await using SqlConnection connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            await using SqlCommand command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@ProductName", $"%{productName}%");
            if (!string.IsNullOrEmpty(brand))
            {
                command.Parameters.AddWithValue("@Brand", brand);
            }

            if (!string.IsNullOrEmpty(upc))
            {
                command.Parameters.AddWithValue("@UPC", upc);
            }

            List<RecallDto> recalls = [];
            await using SqlDataReader reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                recalls.Add(new RecallDto
                {
                    Id = reader.GetGuid(0),
                    RecallNumber = reader.GetString(1),
                    Source = reader.GetString(2),
                    Title = reader.GetString(3),
                    Description = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                    Severity = reader.GetString(5),
                    RecallDate = reader.GetDateTime(6),
                    Reason = reader.IsDBNull(7) ? null : reader.GetString(7),
                    Status = reader.GetString(8),
                    CreatedAt = reader.GetDateTime(9),
                    AffectedProductCount = reader.GetInt32(10)
                });
            }

            return recalls;
        }

        public async Task<Guid> CreateRecallAlertAsync(Guid userId, Guid recallId, string matchType, string matchedValue, bool isAcknowledged)
        {
            // Map to RecallAlert: AlertType, IsRead; no MatchedValue column exists in schema, so ignore
            const string sql = @"
            INSERT INTO RecallAlert (UserId, RecallId, AlertType, IsRead)
            OUTPUT INSERTED.Id
            VALUES (@UserId, @RecallId, @AlertType, @IsRead)";

            await using SqlConnection connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            await using SqlCommand command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@UserId", userId);
            command.Parameters.AddWithValue("@RecallId", recallId);
            command.Parameters.AddWithValue("@AlertType", matchType);
            command.Parameters.AddWithValue("@IsRead", isAcknowledged);

            return (Guid)await command.ExecuteScalarAsync()!;
        }

        public async Task<List<RecallAlertDto>> GetUserAlertsAsync(Guid userId, bool unacknowledgedOnly = true)
        {
            var sql = @"SELECT a.Id, a.UserId, a.RecallId, r.Title, r.Severity, a.AlertType, a.IsRead, a.ReadAt, a.CreatedAt
                    FROM RecallAlert a
                    JOIN Recall r ON r.Id = a.RecallId
                    WHERE a.UserId = @UserId";
            if (unacknowledgedOnly)
            {
                sql += " AND a.IsRead = 0";
            }

            sql += " ORDER BY a.CreatedAt DESC";

            await using SqlConnection connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            await using SqlCommand command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@UserId", userId);

            List<RecallAlertDto> alerts = [];
            await using SqlDataReader reader = await command.ExecuteReaderAsync();
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
                    MatchedValue = string.Empty,
                    IsAcknowledged = reader.GetBoolean(6),
                    AcknowledgedAt = reader.IsDBNull(7) ? null : reader.GetDateTime(7),
                    CreatedAt = reader.GetDateTime(8)
                });
            }

            return alerts;
        }

        public async Task AcknowledgeAlertAsync(Guid alertId)
        {
            const string sql = "UPDATE RecallAlert SET IsRead = 1, ReadAt = GETUTCDATE() WHERE Id = @Id";
            await using SqlConnection connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            await using SqlCommand command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@Id", alertId);
            await command.ExecuteNonQueryAsync();
        }

        public async Task<int> GetUnacknowledgedCountAsync(Guid userId)
        {
            const string sql = "SELECT COUNT(*) FROM RecallAlert WHERE UserId = @UserId AND IsRead = 0";
            await using SqlConnection connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            await using SqlCommand command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@UserId", userId);
            var count = (int)(await command.ExecuteScalarAsync() ?? 0);
            return count;
        }

        public async Task<Guid> SubscribeToRecallsAsync(Guid userId, string? category = null, string? brand = null, string? keyword = null)
        {
            const string sql = @"
            INSERT INTO RecallSubscription (UserId, SubscriptionType, FilterValue)
            OUTPUT INSERTED.Id
            VALUES (@UserId, @Type, @Value)";

            var type = !string.IsNullOrEmpty(category) ? "ByCategory" : (!string.IsNullOrEmpty(brand) ? "ByBrand" : "Keyword");
            var value = category ?? brand ?? keyword ?? string.Empty;

            await using SqlConnection connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            await using SqlCommand command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@UserId", userId);
            command.Parameters.AddWithValue("@Type", type);
            command.Parameters.AddWithValue("@Value", value);
            return (Guid)await command.ExecuteScalarAsync()!;
        }

        public async Task<List<RecallSubscriptionDto>> GetUserSubscriptionsAsync(Guid userId)
        {
            const string sql = @"SELECT Id, UserId, SubscriptionType, FilterValue, IsActive, CreatedAt FROM RecallSubscription WHERE UserId = @UserId";
            await using SqlConnection connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            await using SqlCommand command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@UserId", userId);
            List<RecallSubscriptionDto> subscriptions = [];
            await using SqlDataReader reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                subscriptions.Add(new RecallSubscriptionDto
                {
                    Id = reader.GetGuid(0),
                    UserId = reader.GetGuid(1),
                    Category = reader.GetString(2) == "ByCategory" ? reader.GetString(3) : null,
                    Brand = reader.GetString(2) == "ByBrand" ? reader.GetString(3) : null,
                    Keyword = reader.GetString(2) == "Keyword" ? reader.GetString(3) : null,
                    IsActive = reader.GetBoolean(4),
                    CreatedAt = reader.GetDateTime(5)
                });
            }
            return subscriptions;
        }

        public async Task UnsubscribeAsync(Guid subscriptionId)
        {
            const string sql = "UPDATE RecallSubscription SET IsActive = 0 WHERE Id = @Id";
            await using SqlConnection connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            await using SqlCommand command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@Id", subscriptionId);
            await command.ExecuteNonQueryAsync();
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

            await using SqlConnection connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            await using SqlCommand command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@RecallId", recallId);
            List<Guid> users = [];
            await using SqlDataReader reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                users.Add(reader.GetGuid(0));
            }
            return users;
        }
    }
}
