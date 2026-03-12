using ExpressRecipe.Data.Common;
using ExpressRecipe.Shared.Services;
using System.Text.Json;

namespace ExpressRecipe.ScannerService.Data;

public class ScannerRepository : SqlHelper, IScannerRepository
{
    private readonly ILogger<ScannerRepository> _logger;
    private readonly HybridCacheService? _cache;
    private const string CachePrefix = "scan:";

    public ScannerRepository(string connectionString, ILogger<ScannerRepository> logger, HybridCacheService? cache = null)
        : base(connectionString)
    {
        _logger = logger;
        _cache = cache;
    }

    public async Task<Guid> CreateScanAsync(Guid userId, string barcode, string? productId, bool wasRecognized, string scanType)
    {
        const string sql = @"
            INSERT INTO ScanHistory (UserId, Barcode, ProductId, WasRecognized, ScanType, CreatedAt)
            OUTPUT INSERTED.Id
            VALUES (@UserId, @Barcode, @ProductId, @WasRecognized, @ScanType, GETUTCDATE())";

        return (await ExecuteScalarAsync<Guid>(sql,
            CreateParameter("@UserId", userId),
            CreateParameter("@Barcode", barcode),
            CreateParameter("@ProductId", string.IsNullOrEmpty(productId) ? (object)DBNull.Value : Guid.Parse(productId)),
            CreateParameter("@WasRecognized", wasRecognized),
            CreateParameter("@ScanType", scanType)))!;
    }

    public async Task<List<ScanHistoryDto>> GetUserScansAsync(Guid userId, int limit = 50)
    {
        const string sql = @"
            SELECT TOP (@Limit) Id, UserId, Barcode, ProductId, WasRecognized, ScanType, CreatedAt
            FROM ScanHistory
            WHERE UserId = @UserId
            ORDER BY CreatedAt DESC";

        return await ExecuteReaderAsync<ScanHistoryDto>(sql, reader => new ScanHistoryDto
        {
            Id = GetGuid(reader, "Id"),
            UserId = GetGuid(reader, "UserId"),
            Barcode = GetString(reader, "Barcode")!,
            ProductId = GetGuidNullable(reader, "ProductId"),
            WasRecognized = GetBoolean(reader, "WasRecognized"),
            ScanType = GetString(reader, "ScanType")!,
            CreatedAt = GetDateTime(reader, "CreatedAt")
        },
        CreateParameter("@UserId", userId),
        CreateParameter("@Limit", limit));
    }

    public async Task<ScanHistoryDto?> GetScanAsync(Guid scanId)
    {
        if (_cache != null)
        {
            return await _cache.GetOrSetAsync(
                $"{CachePrefix}id:{scanId}",
                async (ct) => await GetScanFromDbAsync(scanId),
                expiration: TimeSpan.FromHours(24));
        }

        return await GetScanFromDbAsync(scanId);
    }

    private async Task<ScanHistoryDto?> GetScanFromDbAsync(Guid scanId)
    {
        const string sql = @"
            SELECT Id, UserId, Barcode, ProductId, WasRecognized, ScanType, CreatedAt
            FROM ScanHistory
            WHERE Id = @ScanId";

        var results = await ExecuteReaderAsync<ScanHistoryDto>(sql, reader => new ScanHistoryDto
        {
            Id = GetGuid(reader, "Id"),
            UserId = GetGuid(reader, "UserId"),
            Barcode = GetString(reader, "Barcode")!,
            ProductId = GetGuidNullable(reader, "ProductId"),
            WasRecognized = GetBoolean(reader, "WasRecognized"),
            ScanType = GetString(reader, "ScanType")!,
            CreatedAt = GetDateTime(reader, "CreatedAt")
        },
        CreateParameter("@ScanId", scanId));

        return results.FirstOrDefault();
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

        return (await ExecuteScalarAsync<Guid>(sql,
            CreateParameter("@ScanId", scanId),
            CreateParameter("@UserId", userId),
            CreateParameter("@AlertType", alertType),
            CreateParameter("@Severity", severity),
            CreateParameter("@Message", message),
            CreateParameter("@Allergens", JsonSerializer.Serialize(allergens))))!;
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

        return await ExecuteReaderAsync<ScanAlertDto>(sql, reader => new ScanAlertDto
        {
            Id = GetGuid(reader, "Id"),
            ScanId = GetGuid(reader, "ScanId"),
            UserId = GetGuid(reader, "UserId"),
            AlertType = GetString(reader, "AlertType")!,
            Severity = GetString(reader, "Severity")!,
            Message = GetString(reader, "Message")!,
            TriggeredAllergens = JsonSerializer.Deserialize<List<string>>(GetString(reader, "TriggeredAllergens")!) ?? new(),
            IsRead = GetBoolean(reader, "IsRead"),
            CreatedAt = GetDateTime(reader, "CreatedAt")
        },
        CreateParameter("@UserId", userId));
    }

    public async Task MarkAlertAsReadAsync(Guid alertId)
    {
        const string sql = "UPDATE ScanAlert SET IsRead = 1, ReadAt = GETUTCDATE() WHERE Id = @AlertId";
        await ExecuteNonQueryAsync(sql, CreateParameter("@AlertId", alertId));
    }

    public async Task<Guid> ReportUnknownProductAsync(Guid userId, string barcode, string? productName, string? brand, byte[]? photo)
    {
        const string sql = @"
            INSERT INTO UnknownProduct (UserId, Barcode, ProductName, Brand, Photo, Status, CreatedAt)
            OUTPUT INSERTED.Id
            VALUES (@UserId, @Barcode, @ProductName, @Brand, @Photo, 'Pending', GETUTCDATE())";

        return (await ExecuteScalarAsync<Guid>(sql,
            CreateParameter("@UserId", userId),
            CreateParameter("@Barcode", barcode),
            CreateParameter("@ProductName", (object?)productName ?? DBNull.Value),
            CreateParameter("@Brand", (object?)brand ?? DBNull.Value),
            CreateParameter("@Photo", (object?)photo ?? DBNull.Value)))!;
    }

    public async Task<List<UnknownProductDto>> GetUnknownProductsAsync(int limit = 100)
    {
        const string sql = @"
            SELECT TOP (@Limit) Id, UserId, Barcode, ProductName, Brand, Photo, Status, ResolvedProductId, CreatedAt
            FROM UnknownProduct
            WHERE Status = 'Pending'
            ORDER BY CreatedAt DESC";

        return await ExecuteReaderAsync<UnknownProductDto>(sql, reader => new UnknownProductDto
        {
            Id = GetGuid(reader, "Id"),
            UserId = GetGuid(reader, "UserId"),
            Barcode = GetString(reader, "Barcode")!,
            ProductName = GetString(reader, "ProductName"),
            Brand = GetString(reader, "Brand"),
            Photo = reader.IsDBNull(reader.GetOrdinal("Photo")) ? null : (byte[])reader["Photo"],
            Status = GetString(reader, "Status")!,
            ResolvedProductId = GetGuidNullable(reader, "ResolvedProductId"),
            CreatedAt = GetDateTime(reader, "CreatedAt")
        },
        CreateParameter("@Limit", limit));
    }

    public async Task UpdateUnknownProductStatusAsync(Guid unknownProductId, string status, Guid? resolvedProductId)
    {
        const string sql = @"
            UPDATE UnknownProduct
            SET Status = @Status, ResolvedProductId = @ResolvedProductId, ResolvedAt = GETUTCDATE()
            WHERE Id = @Id";

        await ExecuteNonQueryAsync(sql,
            CreateParameter("@Id", unknownProductId),
            CreateParameter("@Status", status),
            CreateParameter("@ResolvedProductId", resolvedProductId.HasValue ? (object)resolvedProductId.Value : DBNull.Value));
    }

    public async Task<Guid> SaveOCRResultAsync(Guid userId, byte[] image, string extractedText, decimal confidence, string? productMatch)
    {
        const string sql = @"
            INSERT INTO OCRResult (UserId, Image, ExtractedText, Confidence, ProductMatch, CreatedAt)
            OUTPUT INSERTED.Id
            VALUES (@UserId, @Image, @ExtractedText, @Confidence, @ProductMatch, GETUTCDATE())";

        return (await ExecuteScalarAsync<Guid>(sql,
            CreateParameter("@UserId", userId),
            CreateParameter("@Image", image),
            CreateParameter("@ExtractedText", extractedText),
            CreateParameter("@Confidence", confidence),
            CreateParameter("@ProductMatch", (object?)productMatch ?? DBNull.Value)))!;
    }

    public async Task<List<OCRResultDto>> GetUserOCRResultsAsync(Guid userId, int limit = 50)
    {
        const string sql = @"
            SELECT TOP (@Limit) Id, UserId, ExtractedText, Confidence, ProductMatch, CreatedAt
            FROM OCRResult
            WHERE UserId = @UserId
            ORDER BY CreatedAt DESC";

        return await ExecuteReaderAsync<OCRResultDto>(sql, reader => new OCRResultDto
        {
            Id = GetGuid(reader, "Id"),
            UserId = GetGuid(reader, "UserId"),
            ExtractedText = GetString(reader, "ExtractedText")!,
            Confidence = GetDecimal(reader, "Confidence"),
            ProductMatch = GetString(reader, "ProductMatch"),
            CreatedAt = GetDateTime(reader, "CreatedAt")
        },
        CreateParameter("@UserId", userId),
        CreateParameter("@Limit", limit));
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

        var result = await ExecuteScalarAsync<Guid>(sql, ct,
            CreateParameter("@UserId", capture.UserId),
            CreateParameter("@ScanHistoryId", capture.ScanHistoryId.HasValue ? (object)capture.ScanHistoryId.Value : DBNull.Value),
            CreateParameter("@CaptureImageJpeg", capture.CaptureImageJpeg),
            CreateParameter("@DetectedBarcode", capture.DetectedBarcode ?? (object)DBNull.Value),
            CreateParameter("@DetectedProductName", capture.DetectedProductName ?? (object)DBNull.Value),
            CreateParameter("@DetectedBrand", capture.DetectedBrand ?? (object)DBNull.Value),
            CreateParameter("@ProviderUsed", capture.ProviderUsed ?? (object)DBNull.Value),
            CreateParameter("@Confidence", capture.Confidence.HasValue ? (object)capture.Confidence.Value : DBNull.Value),
            CreateParameter("@ProductFoundInDb", capture.ProductFoundInDb),
            CreateParameter("@ResolvedProductId", capture.ResolvedProductId.HasValue ? (object)capture.ResolvedProductId.Value : DBNull.Value),
            CreateParameter("@IsTrainingData", capture.IsTrainingData));

        if (result == default)
            throw new InvalidOperationException("Failed to insert VisionCapture record — no ID returned.");

        return result;
    }

    public async Task UpdateVisionCaptureProductAsync(Guid captureId, Guid productId, bool found, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE VisionCapture
            SET ProductFoundInDb = @Found, ResolvedProductId = @ProductId
            WHERE Id = @CaptureId";

        await ExecuteNonQueryAsync(sql, ct,
            CreateParameter("@CaptureId", captureId),
            CreateParameter("@ProductId", productId),
            CreateParameter("@Found", found));
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

        return await ExecuteReaderAsync<VisionCaptureRecord>(sql, reader => new VisionCaptureRecord
        {
            Id = GetGuid(reader, "Id"),
            UserId = GetGuid(reader, "UserId"),
            ScanHistoryId = GetGuidNullable(reader, "ScanHistoryId"),
            CaptureImageJpeg = (byte[])reader["CaptureImageJpeg"],
            DetectedBarcode = GetString(reader, "DetectedBarcode"),
            DetectedProductName = GetString(reader, "DetectedProductName"),
            DetectedBrand = GetString(reader, "DetectedBrand"),
            ProviderUsed = GetString(reader, "ProviderUsed"),
            Confidence = GetDecimalNullable(reader, "Confidence"),
            ProductFoundInDb = GetBoolean(reader, "ProductFoundInDb"),
            ResolvedProductId = GetGuidNullable(reader, "ResolvedProductId"),
            IsTrainingData = GetBoolean(reader, "IsTrainingData"),
            CapturedAt = GetDateTime(reader, "CapturedAt")
        },
        ct,
        CreateParameter("@Limit", limit));
    }

    public async Task<Guid> CreateCorrectionReportAsync(CorrectionReportRecord report, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO CorrectionReport
                (VisionCaptureId, UserId, AiGuess, UserCorrection, UserNote, Status, CreatedAt)
            OUTPUT INSERTED.Id
            VALUES
                (@VisionCaptureId, @UserId, @AiGuess, @UserCorrection, @UserNote, 'Pending', GETUTCDATE())";

        var result = await ExecuteScalarAsync<Guid>(sql, ct,
            CreateParameter("@VisionCaptureId", report.VisionCaptureId),
            CreateParameter("@UserId", report.UserId),
            CreateParameter("@AiGuess", report.AiGuess ?? (object)DBNull.Value),
            CreateParameter("@UserCorrection", report.UserCorrection ?? (object)DBNull.Value),
            CreateParameter("@UserNote", report.UserNote ?? (object)DBNull.Value));

        if (result == default)
            throw new InvalidOperationException("Failed to insert CorrectionReport record — no ID returned.");

        return result;
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

        return await ExecuteReaderAsync<CorrectionReportRecord>(sql, reader => new CorrectionReportRecord
        {
            Id = GetGuid(reader, "Id"),
            VisionCaptureId = GetGuid(reader, "VisionCaptureId"),
            UserId = GetGuid(reader, "UserId"),
            AiGuess = GetString(reader, "AiGuess"),
            UserCorrection = GetString(reader, "UserCorrection"),
            UserNote = GetString(reader, "UserNote"),
            Status = GetString(reader, "Status")!,
            ReviewedBy = GetGuidNullable(reader, "ReviewedBy"),
            ReviewedAt = GetNullableDateTime(reader, "ReviewedAt"),
            CreatedAt = GetDateTime(reader, "CreatedAt")
        },
        ct,
        CreateParameter("@Limit", limit),
        CreateParameter("@Status", (object?)status ?? DBNull.Value));
    }

    public async Task UpdateCorrectionStatusAsync(Guid reportId, string status, Guid reviewedBy, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE CorrectionReport
            SET Status = @Status, ReviewedBy = @ReviewedBy, ReviewedAt = GETUTCDATE()
            WHERE Id = @ReportId";

        await ExecuteNonQueryAsync(sql, ct,
            CreateParameter("@ReportId", reportId),
            CreateParameter("@Status", status),
            CreateParameter("@ReviewedBy", reviewedBy));
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

        return await ExecuteReaderAsync<TrainingExportRow>(sql, reader => new TrainingExportRow
        {
            CaptureId = GetGuid(reader, "Id"),
            DetectedBarcode = GetString(reader, "DetectedBarcode"),
            DetectedProductName = GetString(reader, "DetectedProductName"),
            DetectedBrand = GetString(reader, "DetectedBrand"),
            ProviderUsed = GetString(reader, "ProviderUsed"),
            Confidence = GetDecimalNullable(reader, "Confidence"),
            CorrectedProductName = GetString(reader, "UserCorrection"),
            CapturedAt = GetDateTime(reader, "CapturedAt")
        },
        ct,
        CreateParameter("@Limit", limit));
    }

    public async Task DeleteUserDataAsync(Guid userId, CancellationToken ct = default)
    {
        const string sql = @"
DELETE FROM VisionCapture   WHERE UserId = @UserId;
DELETE FROM OCRResult       WHERE UserId = @UserId;
DELETE FROM ScanAlert       WHERE UserId = @UserId;
DELETE FROM ScanHistory     WHERE UserId = @UserId;
DELETE FROM UnknownProduct  WHERE UserId = @UserId;";

        await ExecuteNonQueryAsync(sql, CreateParameter("@UserId", userId));
    }
}
