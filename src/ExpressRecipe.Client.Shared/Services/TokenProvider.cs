using Blazored.LocalStorage;
using Microsoft.JSInterop;
using ExpressRecipe.Shared.Services;

namespace ExpressRecipe.Client.Shared.Services;

/// <summary>
/// Token provider implementation using Blazored.LocalStorage
/// Can be replaced with SecureStorage for MAUI or other storage for WinUI 3
/// </summary>
public class LocalStorageTokenProvider : ITokenProvider
{
    private readonly ILocalStorageService _localStorage;
    private const string AccessTokenKey = "access_token";
    private const string RefreshTokenKey = "refresh_token";

    public LocalStorageTokenProvider(ILocalStorageService localStorage)
    {
        _localStorage = localStorage;
    }

    public async Task<string?> GetAccessTokenAsync()
    {
        try
        {
            var token = await _localStorage.GetItemAsStringAsync(AccessTokenKey);
            // Remove quotes if present (Blazored.LocalStorage adds them)
            var cleanToken = token?.Trim('"');
            Console.WriteLine($"[TokenProvider] GetAccessToken: {(string.IsNullOrEmpty(cleanToken) ? "NULL/EMPTY" : $"Found ({cleanToken.Length} chars)")}");
            return cleanToken;
        }
        catch (JSException)
        {
            // JavaScript interop not available during prerendering
            return null;
        }
        catch (InvalidOperationException)
        {
            // JavaScript interop not available during prerendering
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TokenProvider] GetAccessToken failed: {ex.GetType().Name} - {ex.Message}");
            return null;
        }
    }

    public async Task<string?> GetRefreshTokenAsync()
    {
        try
        {
            var token = await _localStorage.GetItemAsStringAsync(RefreshTokenKey);
            // Remove quotes if present (Blazored.LocalStorage adds them)
            return token?.Trim('"');
        }
        catch (JSException)
        {
            // JavaScript interop not available during prerendering
            return null;
        }
        catch (InvalidOperationException)
        {
            // JavaScript interop not available during prerendering
            return null;
        }
    }

    public async Task SetTokensAsync(string accessToken, string refreshToken)
    {
        try
        {
            Console.WriteLine($"[TokenProvider] SetTokens: AccessToken length={accessToken?.Length ?? 0}");
            await _localStorage.SetItemAsStringAsync(AccessTokenKey, accessToken);
            await _localStorage.SetItemAsStringAsync(RefreshTokenKey, refreshToken);
            Console.WriteLine($"[TokenProvider] Tokens saved successfully");
        }
        catch (JSException)
        {
        }
        catch (InvalidOperationException)
        {
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TokenProvider] SetTokens failed: {ex.GetType().Name} - {ex.Message}");
        }
    }

    public async Task ClearTokensAsync()
    {
        try
        {
            Console.WriteLine("[TokenProvider] Removing access token");
            await _localStorage.RemoveItemAsync(AccessTokenKey);
            Console.WriteLine("[TokenProvider] Access token removed");

            Console.WriteLine("[TokenProvider] Removing refresh token");
            await _localStorage.RemoveItemAsync(RefreshTokenKey);
            Console.WriteLine("[TokenProvider] Refresh token removed");

            Console.WriteLine("[TokenProvider] Tokens cleared successfully");
        }
        catch (JSException ex)
        {
            Console.WriteLine($"[TokenProvider] ClearTokens JSException: {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"[TokenProvider] ClearTokens InvalidOperationException: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TokenProvider] ClearTokens unexpected error: {ex.GetType().Name} - {ex.Message}");
        }
    }
}
