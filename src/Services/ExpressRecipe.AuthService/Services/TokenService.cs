using ExpressRecipe.AuthService.Models;
using ExpressRecipe.Shared.Models;
using ExpressRecipe.Shared.Security;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace ExpressRecipe.AuthService.Services;

public interface ITokenService
{
    string GenerateAccessToken(User user);
    string GenerateAccessToken(AuthUser user);
    string GenerateRefreshToken();
    DateTime GetRefreshTokenExpiration();
    string HashPassword(string password);
    bool VerifyPassword(string password, string passwordHash);
    Guid? ValidateRefreshToken(string refreshToken);
}

public class TokenService : ITokenService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<TokenService> _logger;

    public TokenService(IConfiguration configuration, ILogger<TokenService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public string GenerateAccessToken(User user)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var secretKey = jwtSettings["SecretKey"] ?? _configuration["JWT_SECRET_KEY"] ?? "development-secret-key-change-in-production-min-32-chars-required!";
        if (secretKey == "development-secret-key-change-in-production-min-32-chars-required!")
            _logger.LogWarning("JWT secret key is using development fallback. Configure JWT_SECRET_KEY for production use.");
        var issuer = jwtSettings["Issuer"] ?? "ExpressRecipe.AuthService";
        var audience = jwtSettings["Audience"] ?? "ExpressRecipe.API";
        var expirationMinutes = int.Parse(jwtSettings["ExpirationMinutes"] ?? "60");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
        {
            KeyId = ExpressRecipe.Shared.Security.JwtConstants.SigningKeyId
        };
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expirationMinutes),
            signingCredentials: credentials
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        _logger.LogInformation("Generated access token for user {UserId}", user.Id);
        return tokenString;
    }

    public string GenerateAccessToken(AuthUser user)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var secretKey = jwtSettings["SecretKey"] ?? _configuration["JWT_SECRET_KEY"] ?? "development-secret-key-change-in-production-min-32-chars-required!";
        if (secretKey == "development-secret-key-change-in-production-min-32-chars-required!")
            _logger.LogWarning("JWT secret key is using development fallback. Configure JWT_SECRET_KEY for production use.");
        var issuer = jwtSettings["Issuer"] ?? "ExpressRecipe.AuthService";
        var audience = jwtSettings["Audience"] ?? "ExpressRecipe.API";
        var expirationMinutes = int.Parse(jwtSettings["ExpirationMinutes"] ?? "60");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
        {
            KeyId = ExpressRecipe.Shared.Security.JwtConstants.SigningKeyId
        };
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.GivenName, user.FirstName),
            new Claim(JwtRegisteredClaimNames.FamilyName, user.LastName),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim("subscription_tier", user.SubscriptionTier)
        };

        // Only emit household_id when set — consumers parse this as a GUID
        if (user.HouseholdId.HasValue)
            claims.Add(new Claim("household_id", user.HouseholdId.Value.ToString()));

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expirationMinutes),
            signingCredentials: credentials
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        _logger.LogInformation("Generated access token for user {UserId}", user.Id);
        return tokenString;
    }

    public string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    public DateTime GetRefreshTokenExpiration()
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var refreshTokenDays = int.Parse(jwtSettings["RefreshTokenDays"] ?? "7");
        return DateTime.UtcNow.AddDays(refreshTokenDays);
    }

    public string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 11);
    }

    public bool VerifyPassword(string password, string passwordHash)
    {
        return BCrypt.Net.BCrypt.Verify(password, passwordHash);
    }

    public Guid? ValidateRefreshToken(string refreshToken)
    {
        _logger.LogWarning("ValidateRefreshToken not fully implemented");
        return null;
    }
}
