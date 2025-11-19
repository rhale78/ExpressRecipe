namespace ExpressRecipe.MAUI.Services;

/// <summary>
/// Service for barcode and QR code scanning
/// </summary>
public interface IBarcodeService
{
    /// <summary>
    /// Scan a barcode using the device camera
    /// </summary>
    Task<BarcodeScanResult?> ScanBarcodeAsync();

    /// <summary>
    /// Check if camera permission is granted
    /// </summary>
    Task<bool> CheckCameraPermissionAsync();

    /// <summary>
    /// Request camera permission
    /// </summary>
    Task<bool> RequestCameraPermissionAsync();
}

/// <summary>
/// Result of a barcode scan
/// </summary>
public class BarcodeScanResult
{
    public string Barcode { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}
