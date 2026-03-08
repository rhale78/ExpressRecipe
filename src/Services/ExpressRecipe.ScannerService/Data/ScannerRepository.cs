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

    public async Task<Guid> SaveVisionCaptureAsync(VisionCaptureRecord capture, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO VisionCapture
                (UserId, ScanHistoryId, CaptureImageJpeg, DetectedBarcode, DetectedProductName,
                 DetectedBrand, ProviderUsed, Confidence, ProductFoundInDb, ResolvedProductId,
                 IsTrainingData, CapturedAt)
            OUTPUT INSERTED.Id
            VALUES
                (@UserId, @ScanHistoryId, @CaptureImageJpeg, @DetectedBarcode, @DetectedProductName,
                 @DetectedBrand, @ProviderUsed, @Confidence, @ProductFoundInDb, @ResolvedProductId,
                 @IsTrainingData, GETUTCDATE())";

        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using SqlCommand command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", capture.UserId);
        command.Parameters.AddWithValue("@ScanHistoryId", capture.ScanHistoryId.HasValue ? capture.ScanHistoryId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@CaptureImageJpeg", capture.CaptureImageJpeg);
        command.Parameters.AddWithValue("@DetectedBarcode", capture.DetectedBarcode ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@DetectedProductName", capture.DetectedProductName ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@DetectedBrand", capture.DetectedBrand ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@ProviderUsed", capture.ProviderUsed ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Confidence", capture.Confidence.HasValue ? capture.Confidence.Value : DBNull.Value);
        command.Parameters.AddWithValue("@ProductFoundInDb", capture.ProductFoundInDb);
        command.Parameters.AddWithValue("@ResolvedProductId", capture.ResolvedProductId.HasValue ? capture.ResolvedProductId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@IsTrainingData", capture.IsTrainingData);

        return await ExecuteScalarGuidAsync(command, "VisionCapture", ct);
    }

    public async Task UpdateVisionCaptureProductAsync(Guid captureId, Guid productId, bool found, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE VisionCapture
            SET ProductFoundInDb = @Found, ResolvedProductId = @ProductId
            WHERE Id = @CaptureId";

        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using SqlCommand command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@CaptureId", captureId);
        command.Parameters.AddWithValue("@ProductId", productId);
        command.Parameters.AddWithValue("@Found", found);

        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<VisionCaptureRecord>> GetCapturePendingReviewAsync(int limit, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT TOP (@Limit)
                Id, UserId, ScanHistoryId, CaptureImageJpeg, DetectedBarcode,
                DetectedProductName, DetectedBrand, ProviderUsed, Confidence,
                ProductFoundInDb, ResolvedProductId, IsTrainingData, CapturedAt
            FROM VisionCapture
            WHERE IsTrainingData = 0
            ORDER BY CapturedAt DESC";

        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using SqlCommand command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Limit", limit);

        List<VisionCaptureRecord> captures = new List<VisionCaptureRecord>();
        await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            captures.Add(new VisionCaptureRecord
            {
                Id = reader.GetGuid(0),
                UserId = reader.GetGuid(1),
                ScanHistoryId = reader.IsDBNull(2) ? null : reader.GetGuid(2),
                CaptureImageJpeg = (byte[])reader[3],
                DetectedBarcode = reader.IsDBNull(4) ? null : reader.GetString(4),
                DetectedProductName = reader.IsDBNull(5) ? null : reader.GetString(5),
                DetectedBrand = reader.IsDBNull(6) ? null : reader.GetString(6),
                ProviderUsed = reader.IsDBNull(7) ? null : reader.GetString(7),
                Confidence = reader.IsDBNull(8) ? null : reader.GetDecimal(8),
                ProductFoundInDb = reader.GetBoolean(9),
                ResolvedProductId = reader.IsDBNull(10) ? null : reader.GetGuid(10),
                IsTrainingData = reader.GetBoolean(11),
                CapturedAt = reader.GetDateTime(12)
            });
        }

        return captures;
    }

    public async Task<Guid> CreateCorrectionReportAsync(CorrectionReportRecord report, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO CorrectionReport
                (VisionCaptureId, UserId, AiGuess, UserCorrection, UserNote, Status, CreatedAt)
            OUTPUT INSERTED.Id
            VALUES
                (@VisionCaptureId, @UserId, @AiGuess, @UserCorrection, @UserNote, 'Pending', GETUTCDATE())";

        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using SqlCommand command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@VisionCaptureId", report.VisionCaptureId);
        command.Parameters.AddWithValue("@UserId", report.UserId);
        command.Parameters.AddWithValue("@AiGuess", report.AiGuess ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@UserCorrection", report.UserCorrection ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@UserNote", report.UserNote ?? (object)DBNull.Value);

        return await ExecuteScalarGuidAsync(command, "CorrectionReport", ct);
    }

    public async Task<List<CorrectionReportRecord>> GetCorrectionReportsAsync(string? status, int limit, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT TOP (@Limit)
                Id, VisionCaptureId, UserId, AiGuess, UserCorrection,
                UserNote, Status, ReviewedBy, ReviewedAt, CreatedAt
            FROM CorrectionReport
            WHERE (@Status IS NULL OR Status = @Status)
            ORDER BY CreatedAt DESC";

        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using SqlCommand command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Limit", limit);
        command.Parameters.AddWithValue("@Status", status ?? (object)DBNull.Value);

        List<CorrectionReportRecord> reports = new List<CorrectionReportRecord>();
        await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            reports.Add(new CorrectionReportRecord
            {
                Id = reader.GetGuid(0),
                VisionCaptureId = reader.GetGuid(1),
                UserId = reader.GetGuid(2),
                AiGuess = reader.IsDBNull(3) ? null : reader.GetString(3),
                UserCorrection = reader.IsDBNull(4) ? null : reader.GetString(4),
                UserNote = reader.IsDBNull(5) ? null : reader.GetString(5),
                Status = reader.GetString(6),
                ReviewedBy = reader.IsDBNull(7) ? null : reader.GetGuid(7),
                ReviewedAt = reader.IsDBNull(8) ? null : reader.GetDateTime(8),
                CreatedAt = reader.GetDateTime(9)
            });
        }

        return reports;
    }

    public async Task UpdateCorrectionStatusAsync(Guid reportId, string status, Guid reviewedBy, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE CorrectionReport
            SET Status = @Status, ReviewedBy = @ReviewedBy, ReviewedAt = GETUTCDATE()
            WHERE Id = @ReportId";

        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using SqlCommand command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ReportId", reportId);
        command.Parameters.AddWithValue("@Status", status);
        command.Parameters.AddWithValue("@ReviewedBy", reviewedBy);

        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<TrainingExportRow>> GetTrainingExportAsync(int limit, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT TOP (@Limit)
                vc.Id,
                vc.DetectedBarcode,
                vc.DetectedProductName,
                vc.DetectedBrand,
                vc.ProviderUsed,
                vc.Confidence,
                cr.UserCorrection,
                vc.CapturedAt
            FROM VisionCapture vc
            LEFT JOIN CorrectionReport cr ON cr.VisionCaptureId = vc.Id AND cr.Status = 'Approved'
            WHERE vc.IsTrainingData = 1
            ORDER BY vc.CapturedAt DESC";

        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using SqlCommand command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Limit", limit);

        List<TrainingExportRow> rows = new List<TrainingExportRow>();
        await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new TrainingExportRow
            {
                CaptureId = reader.GetGuid(0),
                DetectedBarcode = reader.IsDBNull(1) ? null : reader.GetString(1),
                DetectedProductName = reader.IsDBNull(2) ? null : reader.GetString(2),
                DetectedBrand = reader.IsDBNull(3) ? null : reader.GetString(3),
                ProviderUsed = reader.IsDBNull(4) ? null : reader.GetString(4),
                Confidence = reader.IsDBNull(5) ? null : reader.GetDecimal(5),
                CorrectedProductName = reader.IsDBNull(6) ? null : reader.GetString(6),
                CapturedAt = reader.GetDateTime(7)
            });
        }

        return rows;
    }

    private static async Task<Guid> ExecuteScalarGuidAsync(SqlCommand command, string entityName, CancellationToken ct)
    {
        object? result = await command.ExecuteScalarAsync(ct);
        if (result == null || result == DBNull.Value)
        {
            throw new InvalidOperationException($"Failed to insert {entityName} record — no ID returned.");
        }

        return (Guid)result;
    }
}
