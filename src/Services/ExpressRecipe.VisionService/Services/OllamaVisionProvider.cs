using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ExpressRecipe.VisionService.Services;

public class OllamaVisionOptions
{
    public bool Enabled { get; set; } = true;
    public string Model { get; set; } = "llava";
    public string Endpoint { get; set; } = "http://localhost:11434";
    public int TimeoutMs { get; set; } = 10000;
}

/// <summary>
/// Ollama llava/bakllava vision provider. Sends base64 image to Ollama's /api/generate.
/// Times out after TimeoutMs (default 10s). Returns Success=false on timeout without throwing.
/// </summary>
public class OllamaVisionProvider : IVisionProvider
{
    private const string VisionPrompt = "Look at this product image. Return JSON only: {\"productName\": \"\", \"brand\": \"\", \"detectedText\": [], \"confidence\": 0.0, \"labels\": []}. No explanation.";

    private readonly OllamaVisionOptions _options;
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaVisionProvider> _logger;
    private bool? _ollamaAvailable;
    private DateTime _availabilityCacheExpiry = DateTime.MinValue;
    private static readonly TimeSpan AvailabilityCacheDuration = TimeSpan.FromMinutes(5);

    public string ProviderName => "OllamaVision";
    public bool IsEnabled => _options.Enabled;

    public OllamaVisionProvider(OllamaVisionOptions options, HttpClient httpClient, ILogger<OllamaVisionProvider> logger)
    {
        _options = options;
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<VisionResult> AnalyzeAsync(byte[] imageBytes, CancellationToken ct = default)
    {
        if (!_options.Enabled)
        {
            return new VisionResult { Success = false, ProviderUsed = ProviderName, ErrorMessage = "OllamaVision disabled" };
        }

        bool available = await CheckAvailabilityAsync(ct);
        if (!available)
        {
            return new VisionResult { Success = false, ProviderUsed = ProviderName, ErrorMessage = "Ollama not available" };
        }

        try
        {
            using CancellationTokenSource timeoutCts = new CancellationTokenSource(_options.TimeoutMs);
            using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            string base64 = Convert.ToBase64String(imageBytes);

            OllamaGenerateRequest requestBody = new OllamaGenerateRequest
            {
                Model = _options.Model,
                Prompt = VisionPrompt,
                Images = new[] { base64 },
                Stream = false
            };

            HttpResponseMessage response = await _httpClient.PostAsJsonAsync("/api/generate", requestBody, linkedCts.Token);

            if (!response.IsSuccessStatusCode)
            {
                return new VisionResult { Success = false, ProviderUsed = ProviderName, ErrorMessage = $"HTTP {(int)response.StatusCode}" };
            }

            OllamaGenerateResponse? ollamaResponse = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>(cancellationToken: linkedCts.Token);

            if (ollamaResponse?.Response == null)
            {
                return new VisionResult { Success = false, ProviderUsed = ProviderName };
            }

            return ParseOllamaResponse(ollamaResponse.Response);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("OllamaVision timed out after {TimeoutMs}ms", _options.TimeoutMs);
            return new VisionResult { Success = false, ProviderUsed = ProviderName, ErrorMessage = "Timeout" };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OllamaVision analysis failed");
            return new VisionResult { Success = false, ProviderUsed = ProviderName, ErrorMessage = ex.Message };
        }
    }

    public async Task<List<VisionResult>> AnalyzeMultiItemAsync(byte[] imageBytes, CancellationToken ct = default)
    {
        VisionResult single = await AnalyzeAsync(imageBytes, ct);
        return single.Success ? new List<VisionResult> { single } : new List<VisionResult>();
    }

    private VisionResult ParseOllamaResponse(string rawResponse)
    {
        try
        {
            int startIndex = rawResponse.IndexOf('{');
            int endIndex = rawResponse.LastIndexOf('}');

            if (startIndex < 0 || endIndex < 0 || endIndex <= startIndex)
            {
                return new VisionResult { Success = false, ProviderUsed = ProviderName, ErrorMessage = "No JSON in response" };
            }

            string json = rawResponse.Substring(startIndex, endIndex - startIndex + 1);
            OllamaVisionJson? parsed = JsonSerializer.Deserialize<OllamaVisionJson>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (parsed == null)
            {
                return new VisionResult { Success = false, ProviderUsed = ProviderName };
            }

            return new VisionResult
            {
                Success = !string.IsNullOrWhiteSpace(parsed.ProductName),
                ProductName = parsed.ProductName,
                Brand = parsed.Brand,
                DetectedText = parsed.DetectedText ?? Array.Empty<string>(),
                Labels = parsed.Labels ?? Array.Empty<string>(),
                Confidence = parsed.Confidence,
                ProviderUsed = ProviderName
            };
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse Ollama JSON response");
            return new VisionResult { Success = false, ProviderUsed = ProviderName, ErrorMessage = "JSON parse error" };
        }
    }

    private async Task<bool> CheckAvailabilityAsync(CancellationToken ct)
    {
        if (_ollamaAvailable.HasValue && DateTime.UtcNow < _availabilityCacheExpiry)
        {
            return _ollamaAvailable.Value;
        }

        try
        {
            using CancellationTokenSource checkCts = new CancellationTokenSource(3000);
            using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, checkCts.Token);
            HttpResponseMessage response = await _httpClient.GetAsync("/api/tags", linkedCts.Token);
            _ollamaAvailable = response.IsSuccessStatusCode;
        }
        catch
        {
            _ollamaAvailable = false;
        }

        _availabilityCacheExpiry = DateTime.UtcNow.Add(AvailabilityCacheDuration);
        return _ollamaAvailable.Value;
    }

    private sealed class OllamaGenerateRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;
        [JsonPropertyName("prompt")]
        public string Prompt { get; set; } = string.Empty;
        [JsonPropertyName("images")]
        public string[] Images { get; set; } = Array.Empty<string>();
        [JsonPropertyName("stream")]
        public bool Stream { get; set; }
    }

    private sealed class OllamaGenerateResponse
    {
        [JsonPropertyName("response")]
        public string? Response { get; set; }
    }

    private sealed class OllamaVisionJson
    {
        public string? ProductName { get; set; }
        public string? Brand { get; set; }
        public string[]? DetectedText { get; set; }
        public string[]? Labels { get; set; }
        public double Confidence { get; set; }
    }
}
