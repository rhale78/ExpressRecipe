namespace ExpressRecipe.MAUI.Services.AI;

/// <summary>
/// Service for AI-powered product recognition from images
/// </summary>
public interface IProductRecognitionService
{
    /// <summary>
    /// Recognize a product from an image using AI (cloud or local)
    /// </summary>
    Task<ProductRecognitionResult> RecognizeProductAsync(byte[] imageData, bool useLocalAI = false);

    /// <summary>
    /// Check if local AI (Ollama) is available
    /// </summary>
    Task<bool> IsLocalAIAvailableAsync();

    /// <summary>
    /// Get suggested products based on the recognition result
    /// </summary>
    Task<List<ProductSuggestion>> GetProductSuggestionsAsync(string recognitionText);
}

/// <summary>
/// Result of product recognition
/// </summary>
public class ProductRecognitionResult
{
    public bool Success { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public List<string> DetectedText { get; set; } = new();
    public List<string> DetectedLabels { get; set; } = new();
    public double Confidence { get; set; }
    public string? ErrorMessage { get; set; }
    public bool UsedLocalAI { get; set; }
}

/// <summary>
/// Product suggestion based on AI recognition
/// </summary>
public class ProductSuggestion
{
    public Guid ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string? UPC { get; set; }
    public double MatchScore { get; set; }
    public string? ImageUrl { get; set; }
}
