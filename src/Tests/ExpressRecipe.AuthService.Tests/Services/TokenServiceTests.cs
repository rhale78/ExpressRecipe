using ExpressRecipe.AuthService.Models;
using ExpressRecipe.AuthService.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System.IdentityModel.Tokens.Jwt;

namespace ExpressRecipe.AuthService.Tests.Services;

public class TokenServiceTests
{
    private readonly TokenService _tokenService;
    private readonly IConfiguration _configuration;

    public TokenServiceTests()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            ["JwtSettings:SecretKey"] = "test-secret-key-minimum-32-characters-long!",
            ["JwtSettings:Issuer"] = "TestIssuer",
            ["JwtSettings:Audience"] = "TestAudience",
            ["JwtSettings:ExpirationMinutes"] = "60",
            ["JwtSettings:RefreshTokenDays"] = "7"
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var mockLogger = new Mock<ILogger<TokenService>>();
        _tokenService = new TokenService(_configuration, mockLogger.Object);
    }

    private AuthUser CreateTestAuthUser() => new AuthUser
    {
        Id = Guid.NewGuid(),
        Email = "test@example.com",
        FirstName = "Test",
        LastName = "User",
        PasswordHash = string.Empty,
        IsActive = true,
        EmailVerified = true,
        CreatedAt = DateTime.UtcNow
    };

    [Fact]
    public void GenerateAccessToken_WithAuthUser_ProducesValidJwtWithCorrectClaims()
    {
        // Arrange
        var user = CreateTestAuthUser();

        // Act
        var token = _tokenService.GenerateAccessToken(user);

        // Assert
        token.Should().NotBeNullOrEmpty();

        var handler = new JwtSecurityTokenHandler();
        handler.CanReadToken(token).Should().BeTrue();

        var jwt = handler.ReadJwtToken(token);
        jwt.Subject.Should().Be(user.Id.ToString());
        jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Email)?.Value
            .Should().Be(user.Email);
        jwt.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            .Should().Be(user.Id.ToString());
        jwt.Issuer.Should().Be("TestIssuer");
        jwt.Audiences.Should().Contain("TestAudience");
    }

    [Fact]
    public void GenerateAccessToken_WithAuthUser_TokenExpiresInFuture()
    {
        // Arrange
        var user = CreateTestAuthUser();
        var before = DateTime.UtcNow;

        // Act
        var token = _tokenService.GenerateAccessToken(user);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);
        jwt.ValidTo.Should().BeAfter(before);
    }

    [Fact]
    public void GenerateRefreshToken_ProducesBase64String_Of64Bytes()
    {
        // Act
        var refreshToken = _tokenService.GenerateRefreshToken();

        // Assert
        refreshToken.Should().NotBeNullOrEmpty();
        refreshToken.Should().HaveLength(88); // base64 of 64 bytes = 88 chars
        var bytes = Convert.FromBase64String(refreshToken);
        bytes.Should().HaveCount(64);
    }

    [Fact]
    public void HashPassword_AndVerify_RoundTripsCorrectly()
    {
        // Arrange
        const string password = "SecurePassword123!";

        // Act
        var hash = _tokenService.HashPassword(password);
        var isValid = _tokenService.VerifyPassword(password, hash);

        // Assert
        hash.Should().NotBeNullOrEmpty();
        hash.Should().NotBe(password);
        isValid.Should().BeTrue();
    }

    [Fact]
    public void VerifyPassword_WrongPassword_ReturnsFalse()
    {
        // Arrange
        const string password = "CorrectPassword123!";
        const string wrongPassword = "WrongPassword456!";

        // Act
        var hash = _tokenService.HashPassword(password);
        var isValid = _tokenService.VerifyPassword(wrongPassword, hash);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void GetRefreshTokenExpiration_Returns7DaysFromNow()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var expiration = _tokenService.GetRefreshTokenExpiration();

        // Assert
        var expected = before.AddDays(7);
        expiration.Should().BeCloseTo(expected, TimeSpan.FromSeconds(5));
    }
}
