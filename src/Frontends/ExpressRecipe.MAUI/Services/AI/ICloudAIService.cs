namespace ExpressRecipe.MAUI.Services.AI;

/// <summary>
/// Service for cloud-based AI product recognition (Azure Computer Vision, Google Vision API, etc.)
/// </summary>
public interface ICloudAIService
{
    /// <summary>
    /// Analyze product image using cloud AI
    /// </summary>
    Task<CloudAIResult> AnalyzeProductImageAsync(byte[] imageData);

    /// <summary>
    /// Extract text from image (OCR)
    /// </summary>
    Task<List<string>> ExtractTextAsync(byte[] imageData);

    /// <summary>
    /// Detect objects and labels in image
    /// </summary>
    Task<List<string>> DetectLabelsAsync(byte[] imageData);

    /// <summary>
    /// Check if cloud AI is configured and available
    /// </summary>
    Task<bool> IsAvailableAsync();
}

/// <summary>
/// Result from cloud AI analysis
/// </summary>
public class CloudAIResult
{
    public bool Success { get; set; }
    public List<string> DetectedText { get; set; } = new();
    public List<string> DetectedLabels { get; set; } = new();
    public List<string> DetectedBrands { get; set; } = new();
    public string Description { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public string? ErrorMessage { get; set; }
}
