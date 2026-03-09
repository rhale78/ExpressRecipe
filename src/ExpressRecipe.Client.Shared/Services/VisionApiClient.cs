using System.Net.Http.Json;
using ExpressRecipe.Shared.Services;

namespace ExpressRecipe.Client.Shared.Services;

// DTOs for Vision Service
public class VisionAnalyzeResponse
{
    public bool Success { get; set; }
    public string? ProductName { get; set; }
    public string? Brand { get; set; }
    public string[] DetectedText { get; set; } = Array.Empty<string>();
    public string[] Labels { get; set; } = Array.Empty<string>();
    public double Confidence { get; set; }
    public string ProviderUsed { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}

public class VisionOptionsRequest
{
    public bool AllowOnnx { get; set; } = true;
    public bool AllowPaddleOcr { get; set; } = true;
    public bool AllowOllamaVision { get; set; } = true;
    public bool AllowAzureVision { get; set; } = false;
    public double MinConfidence { get; set; } = 0.55;
}

public class VisionHealthResponse
{
    public bool OnnxAvailable { get; set; }
    public bool PaddleOcrAvailable { get; set; }
    public bool OllamaVisionAvailable { get; set; }
    public bool AzureVisionAvailable { get; set; }
}

public interface IVisionApiClient
{
    Task<VisionAnalyzeResponse?> AnalyzeAsync(string base64Image, VisionOptionsRequest? options = null, CancellationToken ct = default);
    Task<VisionAnalyzeResponse?> ExtractTextAsync(string base64Image, CancellationToken ct = default);
    Task<VisionHealthResponse?> GetHealthAsync(CancellationToken ct = default);
}

public class VisionApiClient : ApiClientBase, IVisionApiClient
{
    public VisionApiClient(HttpClient httpClient, ITokenProvider tokenProvider)
        : base(httpClient, tokenProvider)
    {
    }

    public async Task<VisionAnalyzeResponse?> AnalyzeAsync(string base64Image, VisionOptionsRequest? options = null, CancellationToken ct = default)
    {
        object requestBody = new
        {
            base64Image,
            options
        };

        return await PostAsync<object, VisionAnalyzeResponse>("/api/vision/analyze", requestBody);
    }

    public async Task<VisionAnalyzeResponse?> ExtractTextAsync(string base64Image, CancellationToken ct = default)
    {
        object requestBody = new { base64Image };
        return await PostAsync<object, VisionAnalyzeResponse>("/api/vision/ocr", requestBody);
    }

    public async Task<VisionHealthResponse?> GetHealthAsync(CancellationToken ct = default)
    {
        return await GetAsync<VisionHealthResponse>("/api/vision/health");
    }
}
