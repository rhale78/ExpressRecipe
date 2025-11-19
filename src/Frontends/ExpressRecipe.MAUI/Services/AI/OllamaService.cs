using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace ExpressRecipe.MAUI.Services.AI;

/// <summary>
/// Implementation of Ollama service for local AI inference
/// Ollama runs locally on user's device or local network
/// Default URL: http://localhost:11434
/// </summary>
public class OllamaService : IOllamaService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaService> _logger;
    private readonly string _ollamaUrl;
    private const string DefaultModel = "llava"; // Vision-capable model

    public OllamaService(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<OllamaService> logger)
    {
        _httpClient = httpClientFactory.CreateClient();
        _logger = logger;

        // Check appsettings for Ollama URL, default to localhost
        _ollamaUrl = configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
        _httpClient.BaseAddress = new Uri(_ollamaUrl);
        _httpClient.Timeout = TimeSpan.FromMinutes(2); // AI inference can take time
    }

    public async Task<OllamaVisionResult> AnalyzeImageAsync(byte[] imageData, string prompt)
    {
        try
        {
            _logger.LogInformation("Analyzing image with Ollama using prompt: {Prompt}", prompt);

            // Convert image to base64
            var base64Image = Convert.ToBase64String(imageData);

            var request = new
            {
                model = DefaultModel,
                prompt = prompt,
                images = new[] { base64Image },
                stream = false
            };

            var response = await _httpClient.PostAsJsonAsync("/api/generate", request);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Ollama API error: {Error}", error);
                return new OllamaVisionResult
                {
                    Success = false,
                    ErrorMessage = $"Ollama API returned {response.StatusCode}"
                };
            }

            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            var responseText = result.GetProperty("response").GetString() ?? string.Empty;

            _logger.LogInformation("Ollama analysis successful");

            return new OllamaVisionResult
            {
                Success = true,
                Response = responseText,
                Model = DefaultModel
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Ollama service not reachable at {Url}", _ollamaUrl);
            return new OllamaVisionResult
            {
                Success = false,
                ErrorMessage = "Ollama service not available. Make sure Ollama is running."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing image with Ollama");
            return new OllamaVisionResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/tags");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Ollama not available");
            return false;
        }
    }

    public async Task<List<string>> GetAvailableModelsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/tags");
            if (!response.IsSuccessStatusCode)
                return new List<string>();

            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            var models = new List<string>();

            if (result.TryGetProperty("models", out var modelsArray))
            {
                foreach (var model in modelsArray.EnumerateArray())
                {
                    if (model.TryGetProperty("name", out var name))
                    {
                        models.Add(name.GetString() ?? string.Empty);
                    }
                }
            }

            return models;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available models from Ollama");
            return new List<string>();
        }
    }

    public async Task<OllamaProductInfo> ExtractProductInfoAsync(byte[] imageData)
    {
        try
        {
            const string prompt = @"Analyze this food product image and extract the following information:
1. Product name
2. Brand name
3. List of ingredients (if visible)
4. Allergen warnings (if visible)
5. Brief description

Format your response as JSON with these fields: productName, brand, ingredients (array), allergens (array), description.";

            var result = await AnalyzeImageAsync(imageData, prompt);

            if (!result.Success)
            {
                return new OllamaProductInfo { Success = false };
            }

            // Try to parse JSON response
            try
            {
                var jsonResponse = JsonSerializer.Deserialize<OllamaProductInfo>(result.Response);
                if (jsonResponse != null)
                {
                    jsonResponse.Success = true;
                    return jsonResponse;
                }
            }
            catch
            {
                // If not valid JSON, try to extract information from text
                _logger.LogWarning("Ollama response was not valid JSON, attempting text extraction");
            }

            // Fallback: extract info from text response
            var lines = result.Response.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var productInfo = new OllamaProductInfo { Success = true };

            foreach (var line in lines)
            {
                var lower = line.ToLower();
                if (lower.Contains("product") && lower.Contains("name"))
                {
                    productInfo.ProductName = ExtractValue(line);
                }
                else if (lower.Contains("brand"))
                {
                    productInfo.Brand = ExtractValue(line);
                }
                else if (lower.Contains("description"))
                {
                    productInfo.Description = ExtractValue(line);
                }
            }

            return productInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting product info with Ollama");
            return new OllamaProductInfo { Success = false };
        }
    }

    private string ExtractValue(string line)
    {
        // Extract text after colon or equals sign
        var separators = new[] { ':', '=' };
        foreach (var sep in separators)
        {
            var index = line.IndexOf(sep);
            if (index >= 0 && index < line.Length - 1)
            {
                return line.Substring(index + 1).Trim();
            }
        }
        return line.Trim();
    }
}
