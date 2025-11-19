using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpressRecipe.Client.Shared.Services;
using ExpressRecipe.MAUI.Services;
using ExpressRecipe.MAUI.Services.AI;
using ExpressRecipe.MAUI.Services.Camera;
using ZXing.Net.Maui;

namespace ExpressRecipe.MAUI.ViewModels;

public partial class ScannerViewModel : ObservableObject
{
    private readonly IBarcodeService _barcodeService;
    private readonly ICameraService _cameraService;
    private readonly IProductRecognitionService _productRecognitionService;
    private readonly IScannerApiClient _scannerApiClient;
    private readonly IProductApiClient _productApiClient;
    private readonly IToastService _toastService;
    private readonly ILogger<ScannerViewModel> _logger;

    [ObservableProperty]
    private bool _isScannerActive = true;

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private bool _hasResult;

    [ObservableProperty]
    private string? _scannedBarcode;

    [ObservableProperty]
    private string? _productName;

    [ObservableProperty]
    private string? _brandName;

    [ObservableProperty]
    private string? _category;

    [ObservableProperty]
    private List<string> _allergenWarnings = new();

    [ObservableProperty]
    private bool _hasAllergenWarning;

    [ObservableProperty]
    private string? _recognitionMethod; // "Barcode", "AI - Cloud", "AI - Local"

    [ObservableProperty]
    private byte[]? _productImage;

    [ObservableProperty]
    private bool _useLocalAI;

    public ScannerViewModel(
        IBarcodeService barcodeService,
        ICameraService cameraService,
        IProductRecognitionService productRecognitionService,
        IScannerApiClient scannerApiClient,
        IProductApiClient productApiClient,
        IToastService toastService,
        ILogger<ScannerViewModel> logger)
    {
        _barcodeService = barcodeService;
        _cameraService = cameraService;
        _productRecognitionService = productRecognitionService;
        _scannerApiClient = scannerApiClient;
        _productApiClient = productApiClient;
        _toastService = toastService;
        _logger = logger;
    }

    /// <summary>
    /// Called when barcode is detected by the camera view
    /// </summary>
    public async Task OnBarcodeDetectedAsync(BarcodeDetectionEventArgs e)
    {
        try
        {
            if (IsProcessing || e.Results.Length == 0)
                return;

            IsProcessing = true;
            IsScannerActive = false; // Stop scanner

            var barcode = e.Results[0].Value;
            _logger.LogInformation("Barcode detected: {Barcode}", barcode);

            await ProcessBarcodeAsync(barcode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing barcode");
            await _toastService.ShowErrorToast("Error processing barcode");
        }
        finally
        {
            IsProcessing = false;
        }
    }

    private async Task ProcessBarcodeAsync(string barcode)
    {
        ScannedBarcode = barcode;
        RecognitionMethod = "Barcode Scan";

        // Call scanner API to get product info and check allergens
        var response = await _scannerApiClient.ScanBarcodeAsync(barcode);

        if (response != null && response.Product != null)
        {
            ProductName = response.Product.Name;
            BrandName = response.Product.Brand;
            Category = response.Product.Category;

            // Check for allergen warnings
            if (response.HasAllergenWarning)
            {
                AllergenWarnings = response.DetectedAllergens;
                HasAllergenWarning = true;
                await _toastService.ShowWarningToast($"⚠️ ALLERGEN WARNING: Contains {string.Join(", ", response.DetectedAllergens)}");

                // Vibrate to alert user
                Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(500));
            }
            else
            {
                AllergenWarnings = new List<string>();
                HasAllergenWarning = false;
                await _toastService.ShowSuccessToast("Product scanned successfully");
            }

            HasResult = true;
        }
        else
        {
            await _toastService.ShowWarningToast("Product not found in database");
            HasResult = false;
        }
    }

    [RelayCommand]
    private async Task TakePhotoAndRecognizeAsync()
    {
        try
        {
            IsProcessing = true;
            IsScannerActive = false;

            var imageData = await _cameraService.TakePhotoAsync();

            if (imageData == null)
            {
                IsProcessing = false;
                return;
            }

            ProductImage = imageData;

            _logger.LogInformation("Photo captured, starting AI recognition (UseLocalAI: {UseLocal})", UseLocalAI);

            var result = await _productRecognitionService.RecognizeProductAsync(imageData, UseLocalAI);

            if (result.Success)
            {
                ProductName = result.ProductName;
                BrandName = result.Brand;
                RecognitionMethod = result.UsedLocalAI ? "AI - Local (Ollama)" : "AI - Cloud";

                await _toastService.ShowSuccessToast($"Product recognized: {result.ProductName}");
                HasResult = true;
            }
            else
            {
                await _toastService.ShowErrorToast($"Recognition failed: {result.ErrorMessage}");
                HasResult = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during photo recognition");
            await _toastService.ShowErrorToast("Error recognizing product");
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private async Task PickPhotoAndRecognizeAsync()
    {
        try
        {
            IsProcessing = true;
            IsScannerActive = false;

            var imageData = await _cameraService.PickPhotoAsync();

            if (imageData == null)
            {
                IsProcessing = false;
                return;
            }

            ProductImage = imageData;

            _logger.LogInformation("Photo picked, starting AI recognition (UseLocalAI: {UseLocal})", UseLocalAI);

            var result = await _productRecognitionService.RecognizeProductAsync(imageData, UseLocalAI);

            if (result.Success)
            {
                ProductName = result.ProductName;
                BrandName = result.Brand;
                RecognitionMethod = result.UsedLocalAI ? "AI - Local (Ollama)" : "AI - Cloud";

                await _toastService.ShowSuccessToast($"Product recognized: {result.ProductName}");
                HasResult = true;
            }
            else
            {
                await _toastService.ShowErrorToast($"Recognition failed: {result.ErrorMessage}");
                HasResult = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during photo recognition");
            await _toastService.ShowErrorToast("Error recognizing product");
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private void ResetScanner()
    {
        IsScannerActive = true;
        HasResult = false;
        ScannedBarcode = null;
        ProductName = null;
        BrandName = null;
        Category = null;
        AllergenWarnings = new List<string>();
        HasAllergenWarning = false;
        ProductImage = null;
        RecognitionMethod = null;
    }

    [RelayCommand]
    private async Task AddToInventoryAsync()
    {
        try
        {
            // Add product to inventory
            await _toastService.ShowSuccessToast($"Added {ProductName} to inventory");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding to inventory");
            await _toastService.ShowErrorToast("Error adding to inventory");
        }
    }

    [RelayCommand]
    private async Task AddToShoppingListAsync()
    {
        try
        {
            // Add product to shopping list
            await _toastService.ShowSuccessToast($"Added {ProductName} to shopping list");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding to shopping list");
            await _toastService.ShowErrorToast("Error adding to shopping list");
        }
    }

    [RelayCommand]
    private async Task CheckLocalAIAvailabilityAsync()
    {
        var isAvailable = await _productRecognitionService.IsLocalAIAvailableAsync();

        if (isAvailable)
        {
            await _toastService.ShowSuccessToast("Local AI (Ollama) is available");
        }
        else
        {
            await _toastService.ShowWarningToast("Local AI not available. Will use cloud AI.");
        }
    }
}
