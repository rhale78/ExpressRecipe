using ExpressRecipe.Client.Shared.Services;

namespace ExpressRecipe.MAUI.Services;

/// <summary>
/// Token provider using MAUI SecureStorage
/// </summary>
public class SecureStorageTokenProvider : ITokenProvider
{
    private const string TokenKey = "auth_token";

    public async Task<string?> GetTokenAsync()
    {
        try
        {
            return await SecureStorage.Default.GetAsync(TokenKey);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task SetTokenAsync(string token)
    {
        try
        {
            await SecureStorage.Default.SetAsync(TokenKey, token);
        }
        catch (Exception)
        {
            // Log error
        }
    }

    public async Task ClearTokenAsync()
    {
        try
        {
            SecureStorage.Default.Remove(TokenKey);
            await Task.CompletedTask;
        }
        catch (Exception)
        {
            // Log error
        }
    }
}
