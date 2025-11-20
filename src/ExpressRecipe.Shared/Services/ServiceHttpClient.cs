using System.Net.Http.Json;
using ExpressRecipe.Shared.Resilience;
using Polly;

namespace ExpressRecipe.Shared.Services;

/// <summary>
/// Helper for cross-service HTTP communication with resilience policies
/// </summary>
public class ServiceHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ServiceHttpClient> _logger;
    private readonly IAsyncPolicy<HttpResponseMessage> _resiliencePolicy;

    public ServiceHttpClient(HttpClient httpClient, ILogger<ServiceHttpClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _resiliencePolicy = ResiliencePolicies.GetHttpPolicy(logger, "ServiceHttpClient");
    }

    public async Task<T?> GetAsync<T>(string serviceUrl, string endpoint)
    {
        try
        {
            var response = await _resiliencePolicy.ExecuteAsync(async () =>
                await _httpClient.GetAsync($"{serviceUrl}{endpoint}"));

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
            var response = await _resiliencePolicy.ExecuteAsync(async () =>
                await _httpClient.PostAsJsonAsync($"{serviceUrl}{endpoint}", data));

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
            var response = await _resiliencePolicy.ExecuteAsync(async () =>
                await _httpClient.PostAsJsonAsync($"{serviceUrl}{endpoint}", data));

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling POST {ServiceUrl}{Endpoint}", serviceUrl, endpoint);
            return false;
        }
    }

    public async Task<bool> PutAsync<TRequest>(string serviceUrl, string endpoint, TRequest data)
    {
        try
        {
            var response = await _resiliencePolicy.ExecuteAsync(async () =>
                await _httpClient.PutAsJsonAsync($"{serviceUrl}{endpoint}", data));

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling PUT {ServiceUrl}{Endpoint}", serviceUrl, endpoint);
            return false;
        }
    }

    public async Task<bool> DeleteAsync(string serviceUrl, string endpoint)
    {
        try
        {
            var response = await _resiliencePolicy.ExecuteAsync(async () =>
                await _httpClient.DeleteAsync($"{serviceUrl}{endpoint}"));

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling DELETE {ServiceUrl}{Endpoint}", serviceUrl, endpoint);
            return false;
        }
    }
}
