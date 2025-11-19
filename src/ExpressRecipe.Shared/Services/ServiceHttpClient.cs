using System.Net.Http.Json;

namespace ExpressRecipe.Shared.Services;

/// <summary>
/// Helper for cross-service HTTP communication
/// </summary>
public class ServiceHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ServiceHttpClient> _logger;

    public ServiceHttpClient(HttpClient httpClient, ILogger<ServiceHttpClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string serviceUrl, string endpoint)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{serviceUrl}{endpoint}");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling GET {ServiceUrl}{Endpoint}", serviceUrl, endpoint);
            return default;
        }
    }

    public async Task<TResponse?> PostAsync<TRequest, TResponse>(string serviceUrl, string endpoint, TRequest data)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"{serviceUrl}{endpoint}", data);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<TResponse>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling POST {ServiceUrl}{Endpoint}", serviceUrl, endpoint);
            return default;
        }
    }

    public async Task<bool> PostAsync<TRequest>(string serviceUrl, string endpoint, TRequest data)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"{serviceUrl}{endpoint}", data);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling POST {ServiceUrl}{Endpoint}", serviceUrl, endpoint);
            return false;
        }
    }
}
