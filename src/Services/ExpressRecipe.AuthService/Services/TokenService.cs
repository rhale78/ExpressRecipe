using ExpressRecipe.AuthService.Models;
using ExpressRecipe.Shared.Models;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace ExpressRecipe.AuthService.Services
{
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
            IConfigurationSection jwtSettings = _configuration.GetSection("JwtSettings");
            var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured");
            var issuer = jwtSettings["Issuer"] ?? "ExpressRecipe";
            var audience = jwtSettings["Audience"] ?? "ExpressRecipe.API";
            var expirationMinutes = int.Parse(jwtSettings["ExpirationMinutes"] ?? "60");

            SymmetricSecurityKey key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            SigningCredentials credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            Claim[] claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())
            };

            JwtSecurityToken token = new JwtSecurityToken(
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
            IConfigurationSection jwtSettings = _configuration.GetSection("JwtSettings");
            var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured");
            var issuer = jwtSettings["Issuer"] ?? "ExpressRecipe";
            var audience = jwtSettings["Audience"] ?? "ExpressRecipe.API";
            var expirationMinutes = int.Parse(jwtSettings["ExpirationMinutes"] ?? "60");

            SymmetricSecurityKey key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            SigningCredentials credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            Claim[] claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.GivenName, user.FirstName),
                new Claim(JwtRegisteredClaimNames.FamilyName, user.LastName),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())
            };

            JwtSecurityToken token = new JwtSecurityToken(
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
            using RandomNumberGenerator rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);
            return Convert.ToBase64String(randomBytes);
        }

        public DateTime GetRefreshTokenExpiration()
        {
            IConfigurationSection jwtSettings = _configuration.GetSection("JwtSettings");
            var refreshTokenDays = int.Parse(jwtSettings["RefreshTokenDays"] ?? "7");
            return DateTime.UtcNow.AddDays(refreshTokenDays);
        }

        public string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
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
}
