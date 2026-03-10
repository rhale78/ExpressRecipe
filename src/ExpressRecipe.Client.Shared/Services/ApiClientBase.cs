using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ExpressRecipe.Shared.Services;

namespace ExpressRecipe.Client.Shared.Services;

/// <summary>
/// Base class for all API clients with authentication and error handling
/// </summary>
public abstract class ApiClientBase
{
    protected readonly HttpClient HttpClient;
    private readonly ITokenProvider _tokenProvider;
    protected readonly JsonSerializerOptions JsonOptions;

    protected ApiClientBase(HttpClient httpClient, ITokenProvider tokenProvider)
    {
        HttpClient = httpClient;
        _tokenProvider = tokenProvider;
        JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    protected async Task<T?> GetAsync<T>(string endpoint)
        => await GetAsync<T>(endpoint, CancellationToken.None);

    protected async Task<T?> GetAsync<T>(string endpoint, CancellationToken ct)
    {
        await SetAuthorizationHeaderAsync();

        try
        {
            var response = await HttpClient.GetAsync(endpoint, ct);

            if (!response.IsSuccessStatusCode)
            {
                // Don't throw on 404 - return null instead
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return default;
                }

                await HandleErrorResponseAsync(response);
                return default;
            }

            return await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new ApiException("Network error occurred", ex);
        }
    }

    protected async Task<TResponse?> PostAsync<TRequest, TResponse>(string endpoint, TRequest data)
        => await PostAsync<TRequest, TResponse>(endpoint, data, CancellationToken.None);

    protected async Task<TResponse?> PostAsync<TRequest, TResponse>(string endpoint, TRequest data, CancellationToken ct)
    {
        await SetAuthorizationHeaderAsync();

        try
        {
            var response = await HttpClient.PostAsJsonAsync(endpoint, data, JsonOptions, ct);

            if (!response.IsSuccessStatusCode)
            {
                await HandleErrorResponseAsync(response);
                return default;
            }

            return await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new ApiException("Network error occurred", ex);
        }
    }

    protected async Task<bool> PostAsync<TRequest>(string endpoint, TRequest data)
        => await PostAsync(endpoint, data, CancellationToken.None);

    protected async Task<bool> PostAsync<TRequest>(string endpoint, TRequest data, CancellationToken ct)
    {
        await SetAuthorizationHeaderAsync();

        try
        {
            var response = await HttpClient.PostAsJsonAsync(endpoint, data, JsonOptions, ct);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException ex)
        {
            throw new ApiException("Network error occurred", ex);
        }
    }

    protected async Task<TResponse?> PutAsync<TRequest, TResponse>(string endpoint, TRequest data)
    {
        await SetAuthorizationHeaderAsync();

        try
        {
            var response = await HttpClient.PutAsJsonAsync(endpoint, data, JsonOptions);

            if (!response.IsSuccessStatusCode)
            {
                await HandleErrorResponseAsync(response);
                return default;
            }

            return await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions);
        }
        catch (HttpRequestException ex)
        {
            throw new ApiException("Network error occurred", ex);
        }
    }

    protected async Task<bool> PutAsync<TRequest>(string endpoint, TRequest data)
    {
        await SetAuthorizationHeaderAsync();

        try
        {
            var response = await HttpClient.PutAsJsonAsync(endpoint, data, JsonOptions);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException ex)
        {
            throw new ApiException("Network error occurred", ex);
        }
    }

    protected async Task<bool> DeleteAsync(string endpoint)
    {
        await SetAuthorizationHeaderAsync();

        try
        {
            var response = await HttpClient.DeleteAsync(endpoint);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException ex)
        {
            throw new ApiException("Network error occurred", ex);
        }
    }

    private async Task SetAuthorizationHeaderAsync()
    {
        var token = await _tokenProvider.GetAccessTokenAsync();
        if (!string.IsNullOrEmpty(token))
        {
            // Ensure token doesn't have quotes
            token = token.Trim('"');
            HttpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
            Console.WriteLine($"[ApiClientBase] Authorization header set with token");
        }
        else
        {
            // Clear authorization header if no token
            HttpClient.DefaultRequestHeaders.Authorization = null;
            Console.WriteLine($"[ApiClientBase] WARNING: No token available, Authorization header cleared");
        }
    }

    private async Task HandleErrorResponseAsync(HttpResponseMessage response)
    {
        var errorContent = await response.Content.ReadAsStringAsync();

        throw response.StatusCode switch
        {
            System.Net.HttpStatusCode.Unauthorized => new ApiException("Unauthorized access", isAuthError: true),
            System.Net.HttpStatusCode.Forbidden => new ApiException("Access forbidden"),
            System.Net.HttpStatusCode.NotFound => new ApiException("Resource not found"),
            System.Net.HttpStatusCode.BadRequest => new ApiException($"Bad request: {errorContent}"),
            _ => new ApiException($"API error: {response.StatusCode}")
        };
    }
}

/// <summary>
/// Custom exception for API errors
/// </summary>
public class ApiException : Exception
{
    public bool IsAuthError { get; }

    public ApiException(string message, bool isAuthError = false)
        : base(message)
    {
        IsAuthError = isAuthError;
    }

    public ApiException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
