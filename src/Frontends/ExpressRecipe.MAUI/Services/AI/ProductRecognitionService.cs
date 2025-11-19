using ExpressRecipe.Client.Shared.Services;

namespace ExpressRecipe.MAUI.Services.AI;

/// <summary>
/// Main product recognition service that orchestrates cloud and local AI
/// </summary>
public class ProductRecognitionService : IProductRecognitionService
{
    private readonly IOllamaService _ollamaService;
    private readonly ICloudAIService _cloudAIService;
    private readonly IScannerApiClient _scannerApiClient;
    private readonly ILogger<ProductRecognitionService> _logger;

    public ProductRecognitionService(
        IOllamaService ollamaService,
        ICloudAIService cloudAIService,
        IScannerApiClient scannerApiClient,
        ILogger<ProductRecognitionService> logger)
    {
        _ollamaService = ollamaService;
        _cloudAIService = cloudAIService;
        _scannerApiClient = scannerApiClient;
        _logger = logger;
    }

    public async Task<ProductRecognitionResult> RecognizeProductAsync(byte[] imageData, bool useLocalAI = false)
    {
        try
        {
            _logger.LogInformation("Starting product recognition (useLocalAI: {UseLocal})", useLocalAI);

            ProductRecognitionResult result;

            if (useLocalAI)
            {
                // Try local AI first (Ollama)
                var isOllamaAvailable = await _ollamaService.IsAvailableAsync();

                if (isOllamaAvailable)
                {
                    _logger.LogInformation("Using local Ollama AI for product recognition");
                    var ollamaInfo = await _ollamaService.ExtractProductInfoAsync(imageData);

                    result = new ProductRecognitionResult
                    {
                        Success = ollamaInfo.Success,
                        ProductName = ollamaInfo.ProductName,
                        Brand = ollamaInfo.Brand,
                        DetectedText = new List<string> { ollamaInfo.Description },
                        DetectedLabels = ollamaInfo.Allergens,
                        Confidence = ollamaInfo.Success ? 0.85 : 0.0,
                        UsedLocalAI = true
                    };

                    if (result.Success)
                    {
                        _logger.LogInformation("Local AI recognized product: {Product} by {Brand}",
                            result.ProductName, result.Brand);
                        return result;
                    }
                }
                else
                {
                    _logger.LogWarning("Local AI requested but Ollama not available, falling back to cloud");
                }
            }

            // Use cloud AI (Azure Computer Vision or backend service)
            _logger.LogInformation("Using cloud AI for product recognition");
            var cloudResult = await _cloudAIService.AnalyzeProductImageAsync(imageData);

            if (!cloudResult.Success)
            {
                _logger.LogWarning("Cloud AI analysis failed: {Error}", cloudResult.ErrorMessage);
                return new ProductRecognitionResult
                {
                    Success = false,
                    ErrorMessage = cloudResult.ErrorMessage,
                    UsedLocalAI = false
                };
            }

            // Extract product name and brand from cloud AI results
            var productName = ExtractProductName(cloudResult);
            var brand = cloudResult.DetectedBrands.FirstOrDefault() ?? ExtractBrand(cloudResult);

            result = new ProductRecognitionResult
            {
                Success = true,
                ProductName = productName,
                Brand = brand,
                DetectedText = cloudResult.DetectedText,
                DetectedLabels = cloudResult.DetectedLabels,
                Confidence = cloudResult.Confidence,
                UsedLocalAI = false
            };

            _logger.LogInformation("Cloud AI recognized product: {Product} by {Brand}",
                result.ProductName, result.Brand);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during product recognition");
            return new ProductRecognitionResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                UsedLocalAI = useLocalAI
            };
        }
    }

    public async Task<bool> IsLocalAIAvailableAsync()
    {
        return await _ollamaService.IsAvailableAsync();
    }

    public async Task<List<ProductSuggestion>> GetProductSuggestionsAsync(string recognitionText)
    {
        try
        {
            _logger.LogInformation("Getting product suggestions for: {Text}", recognitionText);

            // Search backend for matching products
            // This would typically call the ProductService search endpoint
            // For now, return empty list

            return new List<ProductSuggestion>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting product suggestions");
            return new List<ProductSuggestion>();
        }
    }

    private string ExtractProductName(CloudAIResult cloudResult)
    {
        // Priority: Description > Detected text > Labels
        if (!string.IsNullOrWhiteSpace(cloudResult.Description))
        {
            return cloudResult.Description;
        }

        if (cloudResult.DetectedText.Any())
        {
            // Take the longest text line (usually the product name)
            return cloudResult.DetectedText.OrderByDescending(t => t.Length).FirstOrDefault() ?? "Unknown Product";
        }

        if (cloudResult.DetectedLabels.Any())
        {
            // Combine first few labels
            return string.Join(" ", cloudResult.DetectedLabels.Take(3));
        }

        return "Unknown Product";
    }

    private string ExtractBrand(CloudAIResult cloudResult)
    {
        // Look for brand-like text in detected text
        // Brands are usually all caps or title case
        foreach (var text in cloudResult.DetectedText)
        {
            // Simple heuristic: if text is short and mostly uppercase, might be a brand
            if (text.Length >= 3 && text.Length <= 20 && text.Any(char.IsUpper))
            {
                return text;
            }
        }

        return string.Empty;
    }
}
