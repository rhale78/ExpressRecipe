using ExpressRecipe.Client.Shared.Models.Auth;
using System.Net.Http.Json;
using System.Text.Json;

namespace ExpressRecipe.Client.Shared.Services
{
    public interface IAuthService
    {
        Task<TokenResponse?> LoginAsync(LoginRequest request);
        Task<TokenResponse?> RegisterAsync(RegisterRequest request);
        Task<bool> RefreshTokenAsync();
        Task LogoutAsync();
        Task<UserProfile?> GetCurrentUserAsync();
        event EventHandler<bool>? AuthenticationStateChanged;
    }

    public class AuthService : IAuthService
    {
        private readonly HttpClient _httpClient;
        private readonly ITokenProvider _tokenProvider;
        private UserProfile? _currentUser;

        public event EventHandler<bool>? AuthenticationStateChanged;

        public AuthService(HttpClient httpClient, ITokenProvider tokenProvider)
        {
            _httpClient = httpClient;
            _tokenProvider = tokenProvider;
        }

        public async Task<TokenResponse?> LoginAsync(LoginRequest request)
        {
            try
            {
                HttpResponseMessage response = await _httpClient.PostAsJsonAsync("/api/auth/login", request);

                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                TokenResponse? tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>();

                if (tokenResponse != null)
                {
                    await _tokenProvider.SetTokensAsync(tokenResponse.AccessToken, tokenResponse.RefreshToken);

                    _currentUser = new UserProfile
                    {
                        UserId = tokenResponse.UserId,
                        Email = tokenResponse.Email,
                        FirstName = tokenResponse.FirstName,
                        LastName = tokenResponse.LastName
                    };

                    AuthenticationStateChanged?.Invoke(this, true);
                }

                return tokenResponse;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<TokenResponse?> RegisterAsync(RegisterRequest request)
        {
            try
            {
                Console.WriteLine($"=== REGISTRATION DEBUG ===");
                Console.WriteLine($"HttpClient BaseAddress: {_httpClient.BaseAddress}");
                Console.WriteLine($"Client RegisterAsync called with: Email={request.Email}, FirstName={request.FirstName}, LastName={request.LastName}, PasswordLength={request.Password?.Length ?? 0}");
            
                // Map to API DTO - use exact property names to match API
                var apiRequest = new
                {
                    email = request.Email,
                    password = request.Password,
                    confirmPassword = request.Password,
                    firstName = request.FirstName,
                    lastName = request.LastName
                };

                var jsonPayload = System.Text.Json.JsonSerializer.Serialize(apiRequest);
                Console.WriteLine($"Sending JSON with camelCase: {jsonPayload}");
                Console.WriteLine($"Full URL: {_httpClient.BaseAddress}api/auth/register");

                HttpResponseMessage response = await _httpClient.PostAsJsonAsync("/api/auth/register", apiRequest);

                Console.WriteLine($"Response status: {response.StatusCode}");
                Console.WriteLine($"Response reason: {response.ReasonPhrase}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Registration failed: {response.StatusCode} - {errorContent}");
                    return null;
                }

                // API returns AuthResponse with nested User
                var responseContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Response content: {responseContent}");

                JsonElement apiResponse = await response.Content.ReadFromJsonAsync<JsonElement>();
            
                if (apiResponse.ValueKind == JsonValueKind.Undefined)
                {
                    return null;
                }

                var accessToken = apiResponse.GetProperty("accessToken").GetString() ?? "";
                var refreshToken = apiResponse.GetProperty("refreshToken").GetString() ?? "";
                DateTime expiresAt = apiResponse.GetProperty("expiresAt").GetDateTime();
                JsonElement user = apiResponse.GetProperty("user");

                TokenResponse tokenResponse = new TokenResponse
                {
                    AccessToken = accessToken,
                    RefreshToken = refreshToken,
                    ExpiresAt = expiresAt,
                    UserId = user.GetProperty("id").GetGuid(),
                    Email = user.GetProperty("email").GetString() ?? "",
                    FirstName = request.FirstName, // Use from request since API doesn't return it
                    LastName = request.LastName
                };

                await _tokenProvider.SetTokensAsync(tokenResponse.AccessToken, tokenResponse.RefreshToken);

                _currentUser = new UserProfile
                {
                    UserId = tokenResponse.UserId,
                    Email = tokenResponse.Email,
                    FirstName = tokenResponse.FirstName,
                    LastName = tokenResponse.LastName
                };

                AuthenticationStateChanged?.Invoke(this, true);

                return tokenResponse;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Registration exception: {ex.GetType().Name} - {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        public async Task<bool> RefreshTokenAsync()
        {
            try
            {
                var refreshToken = await _tokenProvider.GetRefreshTokenAsync();

                if (string.IsNullOrEmpty(refreshToken))
                {
                    return false;
                }

                HttpResponseMessage response = await _httpClient.PostAsJsonAsync("/api/auth/refresh",
                    new { RefreshToken = refreshToken });

                if (!response.IsSuccessStatusCode)
                {
                    await LogoutAsync();
                    return false;
                }

                TokenResponse? tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>();

                if (tokenResponse != null)
                {
                    await _tokenProvider.SetTokensAsync(tokenResponse.AccessToken, tokenResponse.RefreshToken);
                    return true;
                }

                return false;
            }
            catch (Exception)
            {
                await LogoutAsync();
                return false;
            }
        }

        public async Task LogoutAsync()
        {
            Console.WriteLine("[AuthService] LogoutAsync started");
            try
            {
                var token = await _tokenProvider.GetAccessTokenAsync();
                Console.WriteLine($"[AuthService] Token retrieved: {(string.IsNullOrEmpty(token) ? "NULL/EMPTY" : "EXISTS")}");

                if (!string.IsNullOrEmpty(token))
                {
                    _httpClient.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                    Console.WriteLine("[AuthService] Calling API logout endpoint");
                    HttpResponseMessage response = await _httpClient.PostAsync("/api/auth/logout", null);
                    Console.WriteLine($"[AuthService] API logout response: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AuthService] Logout API call error (ignoring): {ex.Message}");
            }
            finally
            {
                Console.WriteLine("[AuthService] Clearing tokens");
                await _tokenProvider.ClearTokensAsync();

                Console.WriteLine("[AuthService] Clearing current user");
                _currentUser = null;

                Console.WriteLine("[AuthService] Firing AuthenticationStateChanged event");
                AuthenticationStateChanged?.Invoke(this, false);

                Console.WriteLine("[AuthService] LogoutAsync completed");
            }
        }

        public async Task<UserProfile?> GetCurrentUserAsync()
        {
            // Always check token validity, even if we have a cached user
            var token = await _tokenProvider.GetAccessTokenAsync();

            if (string.IsNullOrEmpty(token))
            {
                _currentUser = null;
                return null;
            }

            // Decode JWT token to get user info
            try
            {
                var tokenParts = token.Split('.');
                if (tokenParts.Length != 3)
                {
                    return null;
                }

                var payload = tokenParts[1];
                var paddedPayload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
                var payloadBytes = Convert.FromBase64String(paddedPayload);
                var payloadJson = System.Text.Encoding.UTF8.GetString(payloadBytes);

                Dictionary<string, JsonElement>? claims = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(payloadJson);

                if (claims == null)
                {
                    return null;
                }

                // Check expiration
                if (claims.TryGetValue("exp", out JsonElement expElement) && expElement.TryGetInt64(out var exp))
                {
                    DateTime expirationTime = DateTimeOffset.FromUnixTimeSeconds(exp).UtcDateTime;
                    // Add a small buffer (e.g., 10 seconds) to account for clock skew
                    if (expirationTime <= DateTime.UtcNow.AddSeconds(10))
                    {
                        Console.WriteLine($"[AuthService] Token expired at {expirationTime}. Attempting refresh...");
                        var refreshed = await RefreshTokenAsync();
                        if (refreshed)
                        {
                            // Recursively call to get the new user from the new token
                            return await GetCurrentUserAsync();
                        }
                        else
                        {
                            Console.WriteLine("[AuthService] Token refresh failed.");
                            return null;
                        }
                    }
                }

                _currentUser = new UserProfile
                {
                    UserId = Guid.Parse(claims.TryGetValue("sub", out JsonElement sub) ? sub.GetString() ?? Guid.Empty.ToString() : Guid.Empty.ToString()),
                    Email = claims.TryGetValue("email", out JsonElement email) ? email.GetString() ?? string.Empty : string.Empty,
                    FirstName = claims.TryGetValue("given_name", out JsonElement givenName) ? givenName.GetString() ?? string.Empty : string.Empty,
                    LastName = claims.TryGetValue("family_name", out JsonElement familyName) ? familyName.GetString() ?? string.Empty : string.Empty
                };

                return _currentUser;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AuthService] Error parsing token: {ex.Message}");
                return null;
            }
        }
    }
}
