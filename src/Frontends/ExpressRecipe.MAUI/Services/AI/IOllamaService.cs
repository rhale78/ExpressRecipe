namespace ExpressRecipe.MAUI.Services.AI;

/// <summary>
/// Service for interacting with local Ollama AI models
/// Ollama provides local LLMs including vision models like LLaVA
/// </summary>
public interface IOllamaService
{
    /// <summary>
    /// Analyze an image using Ollama's vision model (e.g., LLaVA)
    /// </summary>
    Task<OllamaVisionResult> AnalyzeImageAsync(byte[] imageData, string prompt);

    /// <summary>
    /// Check if Ollama is running and accessible
    /// </summary>
    Task<bool> IsAvailableAsync();

    /// <summary>
    /// Get list of available models
    /// </summary>
    Task<List<string>> GetAvailableModelsAsync();

    /// <summary>
    /// Extract product information from image
    /// </summary>
    Task<OllamaProductInfo> ExtractProductInfoAsync(byte[] imageData);
}

/// <summary>
/// Result from Ollama vision analysis
/// </summary>
public class OllamaVisionResult
{
    public bool Success { get; set; }
    public string Response { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Product information extracted by Ollama
/// </summary>
public class OllamaProductInfo
{
    public string ProductName { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public List<string> Ingredients { get; set; } = new();
    public List<string> Allergens { get; set; } = new();
    public string Description { get; set; } = string.Empty;
    public bool Success { get; set; }
}
