using ZXing.Net.Maui;

namespace ExpressRecipe.MAUI.Services;

/// <summary>
/// Implementation of barcode scanning using ZXing.Net.Maui
/// </summary>
public class BarcodeService : IBarcodeService
{
    private readonly ILogger<BarcodeService> _logger;

    public BarcodeService(ILogger<BarcodeService> logger)
    {
        _logger = logger;
    }

    public async Task<BarcodeScanResult?> ScanBarcodeAsync()
    {
        try
        {
            // Check camera permission first
            var hasPermission = await CheckCameraPermissionAsync();
            if (!hasPermission)
            {
                hasPermission = await RequestCameraPermissionAsync();
                if (!hasPermission)
                {
                    return new BarcodeScanResult
                    {
                        Success = false,
                        ErrorMessage = "Camera permission denied"
                    };
                }
            }

            // Note: Actual scanning happens in the ScannerPage using CameraBarcodeReaderView
            // This service is for permission management and helper methods
            // The UI will handle the actual scan result

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during barcode scan");
            return new BarcodeScanResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<bool> CheckCameraPermissionAsync()
    {
        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
            return status == PermissionStatus.Granted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking camera permission");
            return false;
        }
    }

    public async Task<bool> RequestCameraPermissionAsync()
    {
        try
        {
            var status = await Permissions.RequestAsync<Permissions.Camera>();
            return status == PermissionStatus.Granted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error requesting camera permission");
            return false;
        }
    }
}
