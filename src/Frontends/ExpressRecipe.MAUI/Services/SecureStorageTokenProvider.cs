using ExpressRecipe.Shared.Services;

namespace ExpressRecipe.MAUI.Services;

/// <summary>
/// Token provider using MAUI SecureStorage
/// </summary>
public class SecureStorageTokenProvider : ITokenProvider
{
    private const string AccessTokenKey = "access_token";
    private const string RefreshTokenKey = "refresh_token";

    public async Task<string?> GetAccessTokenAsync()
    {
        try
        {
            return await SecureStorage.Default.GetAsync(AccessTokenKey);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<string?> GetRefreshTokenAsync()
    {
        try
        {
            return await SecureStorage.Default.GetAsync(RefreshTokenKey);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task SetTokensAsync(string accessToken, string refreshToken)
    {
        try
        {
            await SecureStorage.Default.SetAsync(AccessTokenKey, accessToken);
            await SecureStorage.Default.SetAsync(RefreshTokenKey, refreshToken);
        }
        catch (Exception)
        {
            // Log error
        }
    }

    public async Task ClearTokensAsync()
    {
        try
        {
            SecureStorage.Default.Remove(AccessTokenKey);
            SecureStorage.Default.Remove(RefreshTokenKey);
            await Task.CompletedTask;
        }
        catch (Exception)
        {
            // Log error
        }
    }
}
