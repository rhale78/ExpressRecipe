using ExpressRecipe.Shared.DTOs.Auth;
using System.Net.Http.Json;
using System.Text.Json;

namespace ExpressRecipe.BlazorWeb.Services
{
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
                HttpResponseMessage response = await _httpClient.PostAsJsonAsync("/api/auth/login", request);

                if (response.IsSuccessStatusCode)
                {
                    AuthResponse? authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();
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
                HttpResponseMessage response = await _httpClient.PostAsJsonAsync("/api/auth/register", request);

                if (response.IsSuccessStatusCode)
                {
                    AuthResponse? authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();
                    if (authResponse != null)
                    {
                        _accessToken = authResponse.AccessToken;
                        _currentUser = authResponse.User;
                        _logger.LogInformation("User {Email} registered successfully", request.Email);
                    }
                    return authResponse;
                }

                // Read error response from API
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Registration failed for {Email}: {StatusCode} - {Error}",
                    request.Email, response.StatusCode, errorContent);

                // Try to parse error message from different response formats
                try
                {
                    JsonDocument errorResponse = System.Text.Json.JsonDocument.Parse(errorContent);
                
                    // Check for ASP.NET Core ValidationProblemDetails format
                    if (errorResponse.RootElement.TryGetProperty("errors", out JsonElement errorsElement))
                    {
                        List<string> errorMessages = [];
                        foreach (JsonProperty error in errorsElement.EnumerateObject())
                        {
                            var fieldName = error.Name;
                            IEnumerable<string?> messages = error.Value.EnumerateArray().Select(m => m.GetString()).Where(m => m != null);
                            errorMessages.Add($"{fieldName}: {string.Join(", ", messages)}");
                        }
                        throw new InvalidOperationException(string.Join("; ", errorMessages));
                    }
                
                    // Check for simple { message: "..." } format
                    if (errorResponse.RootElement.TryGetProperty("message", out JsonElement messageElement))
                    {
                        throw new InvalidOperationException(messageElement.GetString() ?? "Registration failed");
                    }
                
                    // Check for { title: "..." } format (ProblemDetails)
                    if (errorResponse.RootElement.TryGetProperty("title", out JsonElement titleElement))
                    {
                        throw new InvalidOperationException(titleElement.GetString() ?? "Registration failed");
                    }
                }
                catch (System.Text.Json.JsonException)
                {
                    // If not JSON, throw the raw error
                    throw new InvalidOperationException(errorContent);
                }

                throw new InvalidOperationException($"Registration failed with status: {response.StatusCode}");
            }
            catch (InvalidOperationException)
            {
                throw; // Re-throw operation exceptions (these contain API error messages)
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error during registration for {Email}", request.Email);
                throw new InvalidOperationException("Could not connect to authentication service. Please try again later.", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during registration for {Email}", request.Email);
                throw new InvalidOperationException("An unexpected error occurred during registration.", ex);
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
            {
                return _currentUser;
            }

            if (string.IsNullOrEmpty(_accessToken))
            {
                return null;
            }

            try
            {
                using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);

                HttpResponseMessage response = await _httpClient.SendAsync(request);

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
}
