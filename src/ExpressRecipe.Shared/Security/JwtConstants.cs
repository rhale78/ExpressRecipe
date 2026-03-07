namespace ExpressRecipe.Shared.Security;

/// <summary>
/// Shared JWT constants that must be consistent between token issuers and validators.
/// </summary>
public static class JwtConstants
{
    /// <summary>
    /// Key ID embedded in every signed JWT header and matched by all validators.
    /// Must be identical in <see cref="ExpressRecipe.Shared.Services.ServiceTokenProvider"/>
    /// and <see cref="ExpressRecipe.AuthService.Services.TokenService"/>.
    /// </summary>
    public const string SigningKeyId = "er-key-v1";
}
