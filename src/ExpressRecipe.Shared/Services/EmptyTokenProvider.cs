namespace ExpressRecipe.Shared.Services;

/// <summary>
/// Interface for providing authentication tokens
/// </summary>
public interface ITokenProvider
{
    Task<string?> GetAccessTokenAsync();
    Task<string?> GetRefreshTokenAsync();
    Task SetTokensAsync(string accessToken, string refreshToken);
    Task ClearTokensAsync();
}

/// <summary>
/// A placeholder token provider that returns null tokens.
/// Used for service-to-service communication where authentication is handled by the network layer.
/// </summary>
public class EmptyTokenProvider : ITokenProvider
{
    public Task<string?> GetAccessTokenAsync() => Task.FromResult<string?>(null);
    public Task<string?> GetRefreshTokenAsync() => Task.FromResult<string?>(null);
    public Task SetTokensAsync(string accessToken, string refreshToken) => Task.CompletedTask;
    public Task ClearTokensAsync() => Task.CompletedTask;
}
