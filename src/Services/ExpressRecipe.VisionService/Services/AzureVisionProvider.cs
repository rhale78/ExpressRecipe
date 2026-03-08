using System.Text.Json;
using System.Text.Json.Serialization;

namespace ExpressRecipe.VisionService.Services;

public class AzureVisionOptions
{
    public bool Enabled { get; set; } = false;
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
}

/// <summary>
/// Azure Computer Vision 4.0 provider. Only active when Enabled=true.
/// Calls imageanalysis:analyze with Read, Tags, Objects, Caption features.
/// </summary>
public class AzureVisionProvider : IVisionProvider
{
    private const string ApiVersion = "2024-02-01";
    private const string Features = "Read,Tags,Objects,Caption";
    private readonly AzureVisionOptions _options;
    private readonly HttpClient _httpClient;
    private readonly ILogger<AzureVisionProvider> _logger;
    private bool? _keyHealthCache;
    private DateTime _healthCacheExpiry = DateTime.MinValue;
    private static readonly TimeSpan HealthCacheDuration = TimeSpan.FromMinutes(10);

    public string ProviderName => "AzureVision";
    public bool IsEnabled => _options.Enabled && !string.IsNullOrWhiteSpace(_options.Endpoint) && !string.IsNullOrWhiteSpace(_options.ApiKey);

    public AzureVisionProvider(AzureVisionOptions options, HttpClient httpClient, ILogger<AzureVisionProvider> logger)
    {
        _options = options;
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<VisionResult> AnalyzeAsync(byte[] imageBytes, CancellationToken ct = default)
    {
        if (!IsEnabled)
        {
            return new VisionResult { Success = false, ProviderUsed = ProviderName, ErrorMessage = "Azure Vision disabled" };
        }

        bool healthy = await CheckKeyHealthAsync(ct);
        if (!healthy)
        {
            return new VisionResult { Success = false, ProviderUsed = ProviderName, ErrorMessage = "Azure Vision key invalid" };
        }

        try
        {
            string url = $"{_options.Endpoint.TrimEnd('/')}/computervision/imageanalysis:analyze?api-version={ApiVersion}&features={Features}";

            using ByteArrayContent content = new ByteArrayContent(imageBytes);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("Ocp-Apim-Subscription-Key", _options.ApiKey);
            request.Content = content;

            HttpResponseMessage response = await _httpClient.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                return new VisionResult { Success = false, ProviderUsed = ProviderName, ErrorMessage = $"HTTP {(int)response.StatusCode}" };
            }

            string json = await response.Content.ReadAsStringAsync(ct);
            return ParseAzureResponse(json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Azure Vision analysis failed");
            return new VisionResult { Success = false, ProviderUsed = ProviderName, ErrorMessage = ex.Message };
        }
    }

    public async Task<List<VisionResult>> AnalyzeMultiItemAsync(byte[] imageBytes, CancellationToken ct = default)
    {
        VisionResult single = await AnalyzeAsync(imageBytes, ct);
        return single.Success ? new List<VisionResult> { single } : new List<VisionResult>();
    }

    private VisionResult ParseAzureResponse(string json)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            string? productName = null;
            if (root.TryGetProperty("captionResult", out JsonElement caption) &&
                caption.TryGetProperty("text", out JsonElement captionText))
            {
                productName = captionText.GetString();
            }

            List<string> labels = new List<string>();
            if (root.TryGetProperty("tagsResult", out JsonElement tagsResult) &&
                tagsResult.TryGetProperty("values", out JsonElement tags))
            {
                foreach (JsonElement tag in tags.EnumerateArray())
                {
                    if (tag.TryGetProperty("name", out JsonElement tagName))
                    {
                        string? name = tagName.GetString();
                        if (name != null)
                        {
                            labels.Add(name);
                        }
                    }
                }
            }

            List<string> detectedText = new List<string>();
            if (root.TryGetProperty("readResult", out JsonElement readResult) &&
                readResult.TryGetProperty("blocks", out JsonElement blocks))
            {
                foreach (JsonElement block in blocks.EnumerateArray())
                {
                    if (block.TryGetProperty("lines", out JsonElement lines))
                    {
                        foreach (JsonElement line in lines.EnumerateArray())
                        {
                            if (line.TryGetProperty("text", out JsonElement lineText))
                            {
                                string? text = lineText.GetString();
                                if (text != null)
                                {
                                    detectedText.Add(text);
                                }
                            }
                        }
                    }
                }
            }

            return new VisionResult
            {
                Success = !string.IsNullOrWhiteSpace(productName) || labels.Count > 0,
                ProductName = productName,
                Labels = labels.ToArray(),
                DetectedText = detectedText.ToArray(),
                Confidence = 0.9,
                ProviderUsed = ProviderName
            };
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse Azure Vision response");
            return new VisionResult { Success = false, ProviderUsed = ProviderName, ErrorMessage = "JSON parse error" };
        }
    }

    private async Task<bool> CheckKeyHealthAsync(CancellationToken ct)
    {
        if (_keyHealthCache.HasValue && DateTime.UtcNow < _healthCacheExpiry)
        {
            return _keyHealthCache.Value;
        }

        try
        {
            string url = $"{_options.Endpoint.TrimEnd('/')}/computervision/imageanalysis:analyze?api-version={ApiVersion}&features=Tags";
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("Ocp-Apim-Subscription-Key", _options.ApiKey);
            request.Content = new ByteArrayContent(Array.Empty<byte>());
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

            using CancellationTokenSource checkCts = new CancellationTokenSource(5000);
            using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, checkCts.Token);
            HttpResponseMessage response = await _httpClient.SendAsync(request, linkedCts.Token);
            // 400 (bad request) means the key is valid but the body is empty — that's fine
            _keyHealthCache = response.StatusCode != System.Net.HttpStatusCode.Unauthorized;
        }
        catch
        {
            _keyHealthCache = false;
        }

        _healthCacheExpiry = DateTime.UtcNow.Add(HealthCacheDuration);
        return _keyHealthCache!.Value;
    }
}
