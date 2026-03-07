using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ExpressRecipe.Shared.Services;

/// <summary>
/// Token provider for service-to-service authentication.
/// Generates JWT tokens that services can use to call each other without user context.
/// Uses the same JWT handler as ASP.NET Core to ensure compatibility.
/// </summary>
public class ServiceTokenProvider : ITokenProvider
{
    private readonly string _serviceName;
    private readonly string _secretKey;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly JwtSecurityTokenHandler _tokenHandler;
    private string? _cachedToken;
    private DateTime _tokenExpiry = DateTime.MinValue;

    public ServiceTokenProvider(string serviceName, IConfiguration configuration)
    {
        _serviceName = serviceName;
        _secretKey = configuration["JwtSettings:SecretKey"] ??
                     Environment.GetEnvironmentVariable("JWT_SECRET_KEY") ?? 
                     "development-secret-key-change-in-production-min-32-chars-required!";
        _issuer = configuration["JwtSettings:Issuer"] ?? "ExpressRecipe.AuthService";
        _audience = configuration["JwtSettings:Audience"] ?? "ExpressRecipe.API";
        _tokenHandler = new JwtSecurityTokenHandler();
    }

    /// <summary>
    /// Gets or generates a service-to-service JWT token.
    /// Tokens are cached and reused until they're about to expire.
    /// </summary>
    public Task<string?> GetAccessTokenAsync()
    {
        // Return cached token if still valid (with 1-minute buffer)
        if (!string.IsNullOrEmpty(_cachedToken) && DateTime.UtcNow.AddMinutes(1) < _tokenExpiry)
        {
            return Task.FromResult<string?>(_cachedToken);
        }

        // Generate new token using official JWT handler
        _cachedToken = GenerateServiceToken();
        _tokenExpiry = DateTime.UtcNow.AddHours(1);
        return Task.FromResult<string?>(_cachedToken);
    }

    public Task<string?> GetRefreshTokenAsync() => Task.FromResult<string?>(null);
    public Task SetTokensAsync(string accessToken, string refreshToken) => Task.CompletedTask;
    public Task ClearTokensAsync()
    {
        _cachedToken = null;
        _tokenExpiry = DateTime.MinValue;
        return Task.CompletedTask;
    }

    private string GenerateServiceToken()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey))
        {
            KeyId = ExpressRecipe.Shared.Security.JwtConstants.SigningKeyId
        };
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, _serviceName),
            new Claim("service", _serviceName),  // Custom claim to identify as service-to-service
            new Claim(JwtRegisteredClaimNames.Sub, _serviceName),
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddHours(1),
            Issuer = _issuer,
            Audience = _audience,
            SigningCredentials = credentials
        };

        var token = _tokenHandler.CreateToken(tokenDescriptor);
        return _tokenHandler.WriteToken(token);
    }
}
