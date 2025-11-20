using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace ExpressRecipe.ScannerService.Data;

public class ScannerRepository : IScannerRepository
{
    private readonly string _connectionString;
    private readonly ILogger<ScannerRepository> _logger;

    public ScannerRepository(string connectionString, ILogger<ScannerRepository> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task<Guid> CreateScanAsync(Guid userId, string barcode, string? productId, bool wasRecognized, string scanType)
    {
        const string sql = @"
            INSERT INTO ScanHistory (UserId, Barcode, ProductId, WasRecognized, ScanType, CreatedAt)
            OUTPUT INSERTED.Id
            VALUES (@UserId, @Barcode, @ProductId, @WasRecognized, @ScanType, GETUTCDATE())";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@Barcode", barcode);
        command.Parameters.AddWithValue("@ProductId", string.IsNullOrEmpty(productId) ? DBNull.Value : Guid.Parse(productId));
        command.Parameters.AddWithValue("@WasRecognized", wasRecognized);
        command.Parameters.AddWithValue("@ScanType", scanType);

        return (Guid)await command.ExecuteScalarAsync()!;
    }

    public async Task<List<ScanHistoryDto>> GetUserScansAsync(Guid userId, int limit = 50)
    {
        const string sql = @"
            SELECT TOP (@Limit) Id, UserId, Barcode, ProductId, WasRecognized, ScanType, CreatedAt
            FROM ScanHistory
            WHERE UserId = @UserId
            ORDER BY CreatedAt DESC";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@Limit", limit);

        var scans = new List<ScanHistoryDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            scans.Add(new ScanHistoryDto
            {
                Id = reader.GetGuid(0),
                UserId = reader.GetGuid(1),
                Barcode = reader.GetString(2),
                ProductId = reader.IsDBNull(3) ? null : reader.GetGuid(3),
                WasRecognized = reader.GetBoolean(4),
                ScanType = reader.GetString(5),
                CreatedAt = reader.GetDateTime(6)
            });
        }

        return scans;
    }

    public async Task<ScanHistoryDto?> GetScanAsync(Guid scanId)
    {
        const string sql = @"
            SELECT Id, UserId, Barcode, ProductId, WasRecognized, ScanType, CreatedAt
            FROM ScanHistory
            WHERE Id = @ScanId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ScanId", scanId);

        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new ScanHistoryDto
            {
                Id = reader.GetGuid(0),
                UserId = reader.GetGuid(1),
                Barcode = reader.GetString(2),
                ProductId = reader.IsDBNull(3) ? null : reader.GetGuid(3),
                WasRecognized = reader.GetBoolean(4),
                ScanType = reader.GetString(5),
                CreatedAt = reader.GetDateTime(6)
            };
        }

        return null;
    }

    public async Task<List<ScanAlertDto>> CheckAllergensAsync(Guid userId, Guid productId)
    {
        // This would call UserService to get user allergens and ProductService to get product ingredients
        // For now, return empty list - implement cross-service communication later
        return new List<ScanAlertDto>();
    }

    public async Task<Guid> CreateScanAlertAsync(Guid scanId, Guid userId, string alertType, string severity, string message, List<string> allergens)
    {
        const string sql = @"
            INSERT INTO ScanAlert (ScanId, UserId, AlertType, Severity, Message, TriggeredAllergens, CreatedAt)
            OUTPUT INSERTED.Id
            VALUES (@ScanId, @UserId, @AlertType, @Severity, @Message, @Allergens, GETUTCDATE())";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ScanId", scanId);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@AlertType", alertType);
        command.Parameters.AddWithValue("@Severity", severity);
        command.Parameters.AddWithValue("@Message", message);
        command.Parameters.AddWithValue("@Allergens", JsonSerializer.Serialize(allergens));

        return (Guid)await command.ExecuteScalarAsync()!;
    }

    public async Task<List<ScanAlertDto>> GetUserAlertsAsync(Guid userId, bool unreadOnly = true)
    {
        var sql = @"
            SELECT Id, ScanId, UserId, AlertType, Severity, Message, TriggeredAllergens, IsRead, CreatedAt
            FROM ScanAlert
            WHERE UserId = @UserId";

        if (unreadOnly)
            sql += " AND IsRead = 0";

        sql += " ORDER BY CreatedAt DESC";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);

        var alerts = new List<ScanAlertDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            alerts.Add(new ScanAlertDto
            {
                Id = reader.GetGuid(0),
                ScanId = reader.GetGuid(1),
                UserId = reader.GetGuid(2),
                AlertType = reader.GetString(3),
                Severity = reader.GetString(4),
                Message = reader.GetString(5),
                TriggeredAllergens = JsonSerializer.Deserialize<List<string>>(reader.GetString(6)) ?? new(),
                IsRead = reader.GetBoolean(7),
                CreatedAt = reader.GetDateTime(8)
            });
        }

        return alerts;
    }

    public async Task MarkAlertAsReadAsync(Guid alertId)
    {
        const string sql = "UPDATE ScanAlert SET IsRead = 1, ReadAt = GETUTCDATE() WHERE Id = @AlertId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@AlertId", alertId);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<Guid> ReportUnknownProductAsync(Guid userId, string barcode, string? productName, string? brand, byte[]? photo)
    {
        const string sql = @"
            INSERT INTO UnknownProduct (UserId, Barcode, ProductName, Brand, Photo, Status, CreatedAt)
            OUTPUT INSERTED.Id
            VALUES (@UserId, @Barcode, @ProductName, @Brand, @Photo, 'Pending', GETUTCDATE())";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@Barcode", barcode);
        command.Parameters.AddWithValue("@ProductName", productName ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Brand", brand ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Photo", photo ?? (object)DBNull.Value);

        return (Guid)await command.ExecuteScalarAsync()!;
    }

    public async Task<List<UnknownProductDto>> GetUnknownProductsAsync(int limit = 100)
    {
        const string sql = @"
            SELECT TOP (@Limit) Id, UserId, Barcode, ProductName, Brand, Photo, Status, ResolvedProductId, CreatedAt
            FROM UnknownProduct
            WHERE Status = 'Pending'
            ORDER BY CreatedAt DESC";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Limit", limit);

        var products = new List<UnknownProductDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            products.Add(new UnknownProductDto
            {
                Id = reader.GetGuid(0),
                UserId = reader.GetGuid(1),
                Barcode = reader.GetString(2),
                ProductName = reader.IsDBNull(3) ? null : reader.GetString(3),
                Brand = reader.IsDBNull(4) ? null : reader.GetString(4),
                Photo = reader.IsDBNull(5) ? null : (byte[])reader[5],
                Status = reader.GetString(6),
                ResolvedProductId = reader.IsDBNull(7) ? null : reader.GetGuid(7),
                CreatedAt = reader.GetDateTime(8)
            });
        }

        return products;
    }

    public async Task UpdateUnknownProductStatusAsync(Guid unknownProductId, string status, Guid? resolvedProductId)
    {
        const string sql = @"
            UPDATE UnknownProduct
            SET Status = @Status, ResolvedProductId = @ResolvedProductId, ResolvedAt = GETUTCDATE()
            WHERE Id = @Id";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", unknownProductId);
        command.Parameters.AddWithValue("@Status", status);
        command.Parameters.AddWithValue("@ResolvedProductId", resolvedProductId.HasValue ? resolvedProductId.Value : DBNull.Value);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<Guid> SaveOCRResultAsync(Guid userId, byte[] image, string extractedText, decimal confidence, string? productMatch)
    {
        const string sql = @"
            INSERT INTO OCRResult (UserId, Image, ExtractedText, Confidence, ProductMatch, CreatedAt)
            OUTPUT INSERTED.Id
            VALUES (@UserId, @Image, @ExtractedText, @Confidence, @ProductMatch, GETUTCDATE())";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@Image", image);
        command.Parameters.AddWithValue("@ExtractedText", extractedText);
        command.Parameters.AddWithValue("@Confidence", confidence);
        command.Parameters.AddWithValue("@ProductMatch", productMatch ?? (object)DBNull.Value);

        return (Guid)await command.ExecuteScalarAsync()!;
    }

    public async Task<List<OCRResultDto>> GetUserOCRResultsAsync(Guid userId, int limit = 50)
    {
        const string sql = @"
            SELECT TOP (@Limit) Id, UserId, ExtractedText, Confidence, ProductMatch, CreatedAt
            FROM OCRResult
            WHERE UserId = @UserId
            ORDER BY CreatedAt DESC";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@Limit", limit);

        var results = new List<OCRResultDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new OCRResultDto
            {
                Id = reader.GetGuid(0),
                UserId = reader.GetGuid(1),
                ExtractedText = reader.GetString(2),
                Confidence = reader.GetDecimal(3),
                ProductMatch = reader.IsDBNull(4) ? null : reader.GetString(4),
                CreatedAt = reader.GetDateTime(5)
            });
        }

        return results;
    }
}
