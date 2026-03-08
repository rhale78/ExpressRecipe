using System.Net.Http.Json;

namespace ExpressRecipe.Client.Shared.Services;

// DTOs for Scanner Service
public class BarcodeScanRequest
{
    public string Barcode { get; set; } = string.Empty;
    public string Format { get; set; } = "UPC-A"; // UPC-A, EAN-13, etc.
}

public class BarcodeScanResult
{
    public string Barcode { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public ProductScanInfo? Product { get; set; }
    public List<AllergenAlert> AllergenAlerts { get; set; } = new();
    public bool Found { get; set; }
    public DateTime ScannedAt { get; set; }
}

public class ProductScanInfo
{
    public Guid ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string? Category { get; set; }
    public List<string> Ingredients { get; set; } = new();
    public List<string> Allergens { get; set; } = new();
    public string? ImageUrl { get; set; }
    public NutritionInfo? Nutrition { get; set; }
}

public class AllergenAlert
{
    public string Allergen { get; set; } = string.Empty;
    public string Severity { get; set; } = "Warning"; // Info, Warning, Danger
    public string Message { get; set; } = string.Empty;
    public bool ContainsTrace { get; set; }
}

public class NutritionInfo
{
    public int Calories { get; set; }
    public decimal Protein { get; set; }
    public decimal Carbohydrates { get; set; }
    public decimal Fat { get; set; }
    public decimal Fiber { get; set; }
    public decimal Sugar { get; set; }
    public decimal Sodium { get; set; }
}

public class ScanHistoryItem
{
    public Guid Id { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public DateTime ScannedAt { get; set; }
    public bool HadAllergenAlert { get; set; }
}

public class VisionCaptureRequest
{
    public string CaptureBase64 { get; set; } = string.Empty;
    public string? DetectedBarcode { get; set; }
    public string? DetectedProductName { get; set; }
    public string? DetectedBrand { get; set; }
    public string? ProviderUsed { get; set; }
    public double? Confidence { get; set; }
    public Guid? ScanHistoryId { get; set; }
    public bool IsTrainingData { get; set; }
}

public class CorrectionReportRequest
{
    public Guid VisionCaptureId { get; set; }
    public string? AiGuess { get; set; }
    public string? UserCorrection { get; set; }
    public string? UserNote { get; set; }
}

// Response DTO for the vision capture creation endpoint
internal sealed class VisionCaptureCreatedResponse
{
    public Guid? Id { get; set; }
}

public class CorrectionReportItem
{
    public Guid Id { get; set; }
    public Guid VisionCaptureId { get; set; }
    public string? AiGuess { get; set; }
    public string? UserCorrection { get; set; }
    public string? UserNote { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class TrainingExportItem
{
    public Guid CaptureId { get; set; }
    public string? DetectedBarcode { get; set; }
    public string? DetectedProductName { get; set; }
    public string? DetectedBrand { get; set; }
    public string? ProviderUsed { get; set; }
    public decimal? Confidence { get; set; }
    public string? CorrectedProductName { get; set; }
    public DateTime CapturedAt { get; set; }
}

public interface IScannerApiClient
{
    Task<BarcodeScanResult?> ScanBarcodeAsync(string barcode, string format = "UPC-A");
    Task<BarcodeScanResult?> ScanBarcodeWithUserProfileAsync(string barcode, Guid userId);
    Task<List<ScanHistoryItem>> GetScanHistoryAsync(int limit = 50);
    Task<bool> ReportMissingProductAsync(string barcode, string productName);
    Task<Guid?> SaveVisionCaptureAsync(VisionCaptureRequest capture, CancellationToken ct = default);
    Task<bool> CreateCorrectionReportAsync(CorrectionReportRequest report, CancellationToken ct = default);
    Task<List<CorrectionReportItem>> GetAdminCorrectionReportsAsync(string? status, int limit = 100, CancellationToken ct = default);
    Task<bool> UpdateAdminCorrectionStatusAsync(Guid reportId, string status, CancellationToken ct = default);
    Task<List<TrainingExportItem>> GetTrainingExportAsync(int limit = 500, CancellationToken ct = default);
}

public class ScannerApiClient : IScannerApiClient
{
    private readonly HttpClient _httpClient;

    public ScannerApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<BarcodeScanResult?> ScanBarcodeAsync(string barcode, string format = "UPC-A")
    {
        var request = new BarcodeScanRequest
        {
            Barcode = barcode,
            Format = format
        };

        var response = await _httpClient.PostAsJsonAsync("/api/scanner/scan", request);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<BarcodeScanResult>();
    }

    public async Task<BarcodeScanResult?> ScanBarcodeWithUserProfileAsync(string barcode, Guid userId)
    {
        var response = await _httpClient.PostAsJsonAsync($"/api/scanner/scan/{userId}", new { Barcode = barcode });

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<BarcodeScanResult>();
    }

    public async Task<List<ScanHistoryItem>> GetScanHistoryAsync(int limit = 50)
    {
        var response = await _httpClient.GetAsync($"/api/scanner/history?limit={limit}");

        if (!response.IsSuccessStatusCode)
        {
            return new List<ScanHistoryItem>();
        }

        return await response.Content.ReadFromJsonAsync<List<ScanHistoryItem>>() ?? new List<ScanHistoryItem>();
    }

    public async Task<bool> ReportMissingProductAsync(string barcode, string productName)
    {
        HttpResponseMessage response = await _httpClient.PostAsJsonAsync("/api/scanner/unknown", new
        {
            Barcode = barcode,
            ProductName = productName
        });

        return response.IsSuccessStatusCode;
    }

    public async Task<Guid?> SaveVisionCaptureAsync(VisionCaptureRequest capture, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(capture.CaptureBase64))
        {
            return null;
        }

        byte[] buffer = new byte[((capture.CaptureBase64.Length + 3) / 4) * 3];
        if (!Convert.TryFromBase64String(capture.CaptureBase64, buffer, out int bytesWritten))
        {
            return null;
        }

        byte[] imageBytes = new byte[bytesWritten];
        Array.Copy(buffer, imageBytes, bytesWritten);

        object requestBody = new
        {
            captureImageJpeg = imageBytes,
            capture.DetectedBarcode,
            capture.DetectedProductName,
            capture.DetectedBrand,
            capture.ProviderUsed,
            confidence = (decimal?)capture.Confidence,
            capture.ScanHistoryId,
            capture.IsTrainingData
        };

        HttpResponseMessage response = await _httpClient.PostAsJsonAsync("/api/scanner/vision/capture", requestBody, ct);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        VisionCaptureCreatedResponse? result = await response.Content.ReadFromJsonAsync<VisionCaptureCreatedResponse>(cancellationToken: ct);
        return result?.Id;
    }

    public async Task<bool> CreateCorrectionReportAsync(CorrectionReportRequest report, CancellationToken ct = default)
    {
        HttpResponseMessage response = await _httpClient.PostAsJsonAsync("/api/scanner/correction", report, ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<List<CorrectionReportItem>> GetAdminCorrectionReportsAsync(string? status, int limit = 100, CancellationToken ct = default)
    {
        string url = status == null
            ? $"/api/admin/scanner/corrections?limit={limit}"
            : $"/api/admin/scanner/corrections?status={Uri.EscapeDataString(status)}&limit={limit}";

        HttpResponseMessage response = await _httpClient.GetAsync(url, ct);

        if (!response.IsSuccessStatusCode)
        {
            return new List<CorrectionReportItem>();
        }

        return await response.Content.ReadFromJsonAsync<List<CorrectionReportItem>>(cancellationToken: ct)
               ?? new List<CorrectionReportItem>();
    }

    public async Task<bool> UpdateAdminCorrectionStatusAsync(Guid reportId, string status, CancellationToken ct = default)
    {
        HttpResponseMessage response = await _httpClient.PutAsJsonAsync($"/api/admin/scanner/corrections/{reportId}", new { status }, ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<List<TrainingExportItem>> GetTrainingExportAsync(int limit = 500, CancellationToken ct = default)
    {
        HttpResponseMessage response = await _httpClient.GetAsync($"/api/admin/scanner/training-export?limit={limit}", ct);

        if (!response.IsSuccessStatusCode)
        {
            return new List<TrainingExportItem>();
        }

        return await response.Content.ReadFromJsonAsync<List<TrainingExportItem>>(cancellationToken: ct)
               ?? new List<TrainingExportItem>();
    }
}
