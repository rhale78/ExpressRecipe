using ExpressRecipe.Client.Shared.Models.Auth;
using System.Net.Http.Json;
using System.Text.Json;

namespace ExpressRecipe.Client.Shared.Services;

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
            var response = await _httpClient.PostAsJsonAsync("/api/auth/login", request);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>();

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
            var response = await _httpClient.PostAsJsonAsync("/api/auth/register", request);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>();

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

    public async Task<bool> RefreshTokenAsync()
    {
        try
        {
            var refreshToken = await _tokenProvider.GetRefreshTokenAsync();

            if (string.IsNullOrEmpty(refreshToken))
            {
                return false;
            }

            var response = await _httpClient.PostAsJsonAsync("/api/auth/refresh",
                new { RefreshToken = refreshToken });

            if (!response.IsSuccessStatusCode)
            {
                await LogoutAsync();
                return false;
            }

            var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>();

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
        try
        {
            var token = await _tokenProvider.GetAccessTokenAsync();

            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                await _httpClient.PostAsync("/api/auth/logout", null);
            }
        }
        catch
        {
            // Ignore logout errors
        }
        finally
        {
            await _tokenProvider.ClearTokensAsync();
            _currentUser = null;
            AuthenticationStateChanged?.Invoke(this, false);
        }
    }

    public async Task<UserProfile?> GetCurrentUserAsync()
    {
        if (_currentUser != null)
        {
            return _currentUser;
        }

        var token = await _tokenProvider.GetAccessTokenAsync();

        if (string.IsNullOrEmpty(token))
        {
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

            var claims = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(payloadJson);

            if (claims == null)
            {
                return null;
            }

            _currentUser = new UserProfile
            {
                UserId = Guid.Parse(claims["sub"].GetString() ?? Guid.Empty.ToString()),
                Email = claims["email"].GetString() ?? string.Empty,
                FirstName = claims["given_name"].GetString() ?? string.Empty,
                LastName = claims["family_name"].GetString() ?? string.Empty
            };

            return _currentUser;
        }
        catch
        {
            return null;
        }
    }
}
