namespace ExpressRecipe.ScannerService.Data;

public interface IScannerRepository
{
    // Scan History
    Task<Guid> CreateScanAsync(Guid userId, string barcode, string? productId, bool wasRecognized, string scanType);
    Task<List<ScanHistoryDto>> GetUserScansAsync(Guid userId, int limit = 50);
    Task<ScanHistoryDto?> GetScanAsync(Guid scanId);

    // Allergen Detection
    Task<List<ScanAlertDto>> CheckAllergensAsync(Guid userId, Guid productId);
    Task<Guid> CreateScanAlertAsync(Guid scanId, Guid userId, string alertType, string severity, string message, List<string> allergens);
    Task<List<ScanAlertDto>> GetUserAlertsAsync(Guid userId, bool unreadOnly = true);
    Task MarkAlertAsReadAsync(Guid alertId);

    // Unknown Products
    Task<Guid> ReportUnknownProductAsync(Guid userId, string barcode, string? productName, string? brand, byte[]? photo);
    Task<List<UnknownProductDto>> GetUnknownProductsAsync(int limit = 100);
    Task UpdateUnknownProductStatusAsync(Guid unknownProductId, string status, Guid? resolvedProductId);

    // OCR Results
    Task<Guid> SaveOCRResultAsync(Guid userId, byte[] image, string extractedText, decimal confidence, string? productMatch);
    Task<List<OCRResultDto>> GetUserOCRResultsAsync(Guid userId, int limit = 50);
}

public class ScanHistoryDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public Guid? ProductId { get; set; }
    public bool WasRecognized { get; set; }
    public string ScanType { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class ScanAlertDto
{
    public Guid Id { get; set; }
    public Guid ScanId { get; set; }
    public Guid UserId { get; set; }
    public string AlertType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public List<string> TriggeredAllergens { get; set; } = new();
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class UnknownProductDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public string? ProductName { get; set; }
    public string? Brand { get; set; }
    public byte[]? Photo { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid? ResolvedProductId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class OCRResultDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string ExtractedText { get; set; } = string.Empty;
    public decimal Confidence { get; set; }
    public string? ProductMatch { get; set; }
    public DateTime CreatedAt { get; set; }
}
