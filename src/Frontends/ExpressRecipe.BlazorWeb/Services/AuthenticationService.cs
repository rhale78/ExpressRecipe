using ExpressRecipe.Shared.DTOs.Auth;
using System.Net.Http.Json;
using System.Text.Json;

namespace ExpressRecipe.BlazorWeb.Services;

public interface IAuthenticationService
{
    Task<AuthResponse?> LoginAsync(LoginRequest request);
    Task<AuthResponse?> RegisterAsync(RegisterRequest request);
    Task LogoutAsync();
    Task<UserDto?> GetCurrentUserAsync();
    bool IsAuthenticated { get; }
    string? AccessToken { get; }
}

public class AuthenticationService : IAuthenticationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AuthenticationService> _logger;
    private string? _accessToken;
    private UserDto? _currentUser;

    public AuthenticationService(
        IHttpClientFactory httpClientFactory,
        ILogger<AuthenticationService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("AuthService");
        _logger = logger;
    }

    public bool IsAuthenticated => _currentUser != null && !string.IsNullOrEmpty(_accessToken);
    public string? AccessToken => _accessToken;

    public async Task<AuthResponse?> LoginAsync(LoginRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/auth/login", request);

            if (response.IsSuccessStatusCode)
            {
                var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();
                if (authResponse != null)
                {
                    _accessToken = authResponse.AccessToken;
                    _currentUser = authResponse.User;
                    _logger.LogInformation("User {Email} logged in successfully", request.Email);
                }
                return authResponse;
            }

            var error = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Login failed for {Email}: {Error}", request.Email, error);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for {Email}", request.Email);
            return null;
        }
    }

    public async Task<AuthResponse?> RegisterAsync(RegisterRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/auth/register", request);

            if (response.IsSuccessStatusCode)
            {
                var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();
                if (authResponse != null)
                {
                    _accessToken = authResponse.AccessToken;
                    _currentUser = authResponse.User;
                    _logger.LogInformation("User {Email} registered successfully", request.Email);
                }
                return authResponse;
            }

            var error = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Registration failed for {Email}: {Error}", request.Email, error);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during registration for {Email}", request.Email);
            return null;
        }
    }

    public Task LogoutAsync()
    {
        _accessToken = null;
        _currentUser = null;
        _logger.LogInformation("User logged out");
        return Task.CompletedTask;
    }

    public async Task<UserDto?> GetCurrentUserAsync()
    {
        if (_currentUser != null)
            return _currentUser;

        if (string.IsNullOrEmpty(_accessToken))
            return null;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);

            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                _currentUser = await response.Content.ReadFromJsonAsync<UserDto>();
                return _currentUser;
            }

            _logger.LogWarning("Failed to get current user: {StatusCode}", response.StatusCode);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current user");
            return null;
        }
    }
}
