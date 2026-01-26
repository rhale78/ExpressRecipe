using System.Net.Http.Json;

namespace ExpressRecipe.Client.Shared.Services
{
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
        public List<AllergenAlert> AllergenAlerts { get; set; } = [];
        public bool Found { get; set; }
        public DateTime ScannedAt { get; set; }
    }

    public class ProductScanInfo
    {
        public Guid ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string? Category { get; set; }
        public List<string> Ingredients { get; set; } = [];
        public List<string> Allergens { get; set; } = [];
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

    public interface IScannerApiClient
    {
        Task<BarcodeScanResult?> ScanBarcodeAsync(string barcode, string format = "UPC-A");
        Task<BarcodeScanResult?> ScanBarcodeWithUserProfileAsync(string barcode, Guid userId);
        Task<List<ScanHistoryItem>> GetScanHistoryAsync(int limit = 50);
        Task<bool> ReportMissingProductAsync(string barcode, string productName);
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
            BarcodeScanRequest request = new BarcodeScanRequest
            {
                Barcode = barcode,
                Format = format
            };

            HttpResponseMessage response = await _httpClient.PostAsJsonAsync("/api/scanner/scan", request);

            return !response.IsSuccessStatusCode ? null : await response.Content.ReadFromJsonAsync<BarcodeScanResult>();
        }

        public async Task<BarcodeScanResult?> ScanBarcodeWithUserProfileAsync(string barcode, Guid userId)
        {
            HttpResponseMessage response = await _httpClient.PostAsJsonAsync($"/api/scanner/scan/{userId}", new { Barcode = barcode });

            return !response.IsSuccessStatusCode ? null : await response.Content.ReadFromJsonAsync<BarcodeScanResult>();
        }

        public async Task<List<ScanHistoryItem>> GetScanHistoryAsync(int limit = 50)
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"/api/scanner/history?limit={limit}");

            return !response.IsSuccessStatusCode ? [] : await response.Content.ReadFromJsonAsync<List<ScanHistoryItem>>() ?? [];
        }

        public async Task<bool> ReportMissingProductAsync(string barcode, string productName)
        {
            HttpResponseMessage response = await _httpClient.PostAsJsonAsync("/api/scanner/report-missing", new
            {
                Barcode = barcode,
                ProductName = productName
            });

            return response.IsSuccessStatusCode;
        }
    }
}
