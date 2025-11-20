using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ExpressRecipe.MAUI.Services.AI;

/// <summary>
/// Implementation of cloud AI service using Azure Computer Vision API
/// Falls back to backend AI service if Azure is not configured
/// </summary>
public class CloudAIService : ICloudAIService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CloudAIService> _logger;
    private readonly string? _azureEndpoint;
    private readonly string? _azureKey;
    private readonly string? _backendAIUrl;

    public CloudAIService(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<CloudAIService> logger)
    {
        _httpClient = httpClientFactory.CreateClient();
        _logger = logger;

        // Azure Computer Vision configuration
        _azureEndpoint = configuration["Azure:ComputerVision:Endpoint"];
        _azureKey = configuration["Azure:ComputerVision:Key"];

        // Backend AI service fallback
        _backendAIUrl = configuration["ApiBaseUrl"] + "/ai/recognize";

        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<CloudAIResult> AnalyzeProductImageAsync(byte[] imageData)
    {
        try
        {
            // Try Azure Computer Vision first if configured
            if (!string.IsNullOrEmpty(_azureEndpoint) && !string.IsNullOrEmpty(_azureKey))
            {
                return await AnalyzeWithAzureAsync(imageData);
            }

            // Fallback to backend AI service
            return await AnalyzeWithBackendAsync(imageData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing product image with cloud AI");
            return new CloudAIResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task<CloudAIResult> AnalyzeWithAzureAsync(byte[] imageData)
    {
        try
        {
            _logger.LogInformation("Analyzing image with Azure Computer Vision");

            var url = $"{_azureEndpoint}/vision/v3.2/analyze?visualFeatures=Brands,Tags,Description,Objects&details=Landmarks";

            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("Ocp-Apim-Subscription-Key", _azureKey);
            request.Content = new ByteArrayContent(imageData);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Azure Computer Vision error: {Error}", error);
                return new CloudAIResult
                {
                    Success = false,
                    ErrorMessage = $"Azure API returned {response.StatusCode}"
                };
            }

            var result = await response.Content.ReadFromJsonAsync<JsonElement>();

            var cloudResult = new CloudAIResult { Success = true };

            // Extract brands
            if (result.TryGetProperty("brands", out var brands))
            {
                foreach (var brand in brands.EnumerateArray())
                {
                    if (brand.TryGetProperty("name", out var name))
                    {
                        cloudResult.DetectedBrands.Add(name.GetString() ?? string.Empty);
                    }
                }
            }

            // Extract tags
            if (result.TryGetProperty("tags", out var tags))
            {
                foreach (var tag in tags.EnumerateArray())
                {
                    if (tag.TryGetProperty("name", out var name))
                    {
                        cloudResult.DetectedLabels.Add(name.GetString() ?? string.Empty);
                    }
                }
            }

            // Extract description
            if (result.TryGetProperty("description", out var desc))
            {
                if (desc.TryGetProperty("captions", out var captions))
                {
                    var firstCaption = captions.EnumerateArray().FirstOrDefault();
                    if (firstCaption.TryGetProperty("text", out var text))
                    {
                        cloudResult.Description = text.GetString() ?? string.Empty;
                    }
                    if (firstCaption.TryGetProperty("confidence", out var conf))
                    {
                        cloudResult.Confidence = conf.GetDouble();
                    }
                }
            }

            // Get OCR text
            var ocrText = await ExtractTextAsync(imageData);
            cloudResult.DetectedText.AddRange(ocrText);

            _logger.LogInformation("Azure analysis complete. Found {Brands} brands, {Labels} labels, {Text} text items",
                cloudResult.DetectedBrands.Count, cloudResult.DetectedLabels.Count, cloudResult.DetectedText.Count);

            return cloudResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error with Azure Computer Vision");
            return new CloudAIResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task<CloudAIResult> AnalyzeWithBackendAsync(byte[] imageData)
    {
        try
        {
            _logger.LogInformation("Analyzing image with backend AI service");

            var content = new MultipartFormDataContent();
            content.Add(new ByteArrayContent(imageData), "image", "product.jpg");

            var response = await _httpClient.PostAsync(_backendAIUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                return new CloudAIResult
                {
                    Success = false,
                    ErrorMessage = $"Backend AI service returned {response.StatusCode}"
                };
            }

            var result = await response.Content.ReadFromJsonAsync<CloudAIResult>();
            return result ?? new CloudAIResult { Success = false };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error with backend AI service");
            return new CloudAIResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<List<string>> ExtractTextAsync(byte[] imageData)
    {
        try
        {
            if (string.IsNullOrEmpty(_azureEndpoint) || string.IsNullOrEmpty(_azureKey))
                return new List<string>();

            _logger.LogInformation("Extracting text from image with OCR");

            var url = $"{_azureEndpoint}/vision/v3.2/ocr?detectOrientation=true";

            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("Ocp-Apim-Subscription-Key", _azureKey);
            request.Content = new ByteArrayContent(imageData);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("OCR failed with status {Status}", response.StatusCode);
                return new List<string>();
            }

            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            var textList = new List<string>();

            if (result.TryGetProperty("regions", out var regions))
            {
                foreach (var region in regions.EnumerateArray())
                {
                    if (region.TryGetProperty("lines", out var lines))
                    {
                        foreach (var line in lines.EnumerateArray())
                        {
                            if (line.TryGetProperty("words", out var words))
                            {
                                var lineText = string.Join(" ", words.EnumerateArray()
                                    .Select(w => w.GetProperty("text").GetString() ?? ""));
                                textList.Add(lineText);
                            }
                        }
                    }
                }
            }

            _logger.LogInformation("Extracted {Count} text lines", textList.Count);
            return textList;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting text from image");
            return new List<string>();
        }
    }

    public async Task<List<string>> DetectLabelsAsync(byte[] imageData)
    {
        var result = await AnalyzeProductImageAsync(imageData);
        return result.DetectedLabels;
    }

    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            // Check if Azure is configured
            if (!string.IsNullOrEmpty(_azureEndpoint) && !string.IsNullOrEmpty(_azureKey))
            {
                return true;
            }

            // Check if backend AI service is available
            if (!string.IsNullOrEmpty(_backendAIUrl))
            {
                var response = await _httpClient.GetAsync(_backendAIUrl.Replace("/recognize", "/health"));
                return response.IsSuccessStatusCode;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }
}
