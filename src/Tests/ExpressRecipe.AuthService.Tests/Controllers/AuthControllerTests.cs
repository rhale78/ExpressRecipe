using ExpressRecipe.AuthService.Controllers;
using ExpressRecipe.AuthService.Data;
using ExpressRecipe.AuthService.Services;
using ExpressRecipe.AuthService.Tests.Helpers;
using ExpressRecipe.Shared.DTOs.Auth;
using AuthUser = ExpressRecipe.AuthService.Models.AuthUser;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;

namespace ExpressRecipe.AuthService.Tests.Controllers;

public class AuthControllerTests
{
    private readonly Mock<IAuthRepository> _mockRepository;
    private readonly TokenService _tokenService;
    private readonly Mock<ILogger<AuthController>> _mockLogger;
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly AuthController _controller;
    private readonly Guid _testUserId;

    public AuthControllerTests()
    {
        _mockRepository = new Mock<IAuthRepository>();
        _mockLogger = new Mock<ILogger<AuthController>>();
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();

        var inMemorySettings = new Dictionary<string, string?>
        {
            ["JwtSettings:SecretKey"] = "test-secret-key-minimum-32-characters-long!",
            ["JwtSettings:Issuer"] = "TestIssuer",
            ["JwtSettings:Audience"] = "TestAudience",
            ["JwtSettings:ExpirationMinutes"] = "60",
            ["JwtSettings:RefreshTokenDays"] = "7"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var mockTokenLogger = new Mock<ILogger<TokenService>>();
        _tokenService = new TokenService(configuration, mockTokenLogger.Object);

        _controller = new AuthController(
            _mockRepository.Object,
            _tokenService,
            _mockLogger.Object,
            _mockHttpClientFactory.Object
        );

        _testUserId = Guid.NewGuid();
        ControllerTestHelpers.SetupControllerContext(_controller, _testUserId);

        // Default UserService HTTP client mock (succeeds silently)
        SetupUserServiceHttpClient(HttpStatusCode.OK);
    }

    private void SetupUserServiceHttpClient(HttpStatusCode statusCode)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = statusCode });

        var client = new HttpClient(handler.Object) { BaseAddress = new Uri("http://localhost") };
        _mockHttpClientFactory.Setup(f => f.CreateClient("UserService")).Returns(client);
    }

    private AuthUser CreateActiveUser(string email = "user@example.com", string? passwordHash = null) => new AuthUser
    {
        Id = _testUserId,
        Email = email,
        PasswordHash = passwordHash ?? _tokenService.HashPassword("Password123!"),
        FirstName = "Test",
        LastName = "User",
        IsActive = true,
        EmailVerified = true,
        CreatedAt = DateTime.UtcNow
    };

    #region Register Tests

    [Fact]
    public async Task Register_WithNewEmail_CreatesUserAndReturnsToken()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "new@example.com",
            Password = "Password123!",
            FirstName = "New",
            LastName = "User"
        };

        _mockRepository.Setup(r => r.GetUserByEmailAsync(request.Email))
            .ReturnsAsync((AuthUser?)null);

        _mockRepository.Setup(r => r.CreateUserAsync(request.Email, It.IsAny<string>(), request.FirstName, request.LastName))
            .ReturnsAsync(_testUserId);

        var createdUser = CreateActiveUser(request.Email);
        _mockRepository.Setup(r => r.GetUserByIdAsync(_testUserId))
            .ReturnsAsync(createdUser);

        _mockRepository.Setup(r => r.CreateRefreshTokenAsync(_testUserId, It.IsAny<string>(), It.IsAny<DateTime>()))
            .ReturnsAsync(Guid.NewGuid());

        // Act
        var result = await _controller.Register(request);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<AuthResponse>().Subject;
        response.AccessToken.Should().NotBeNullOrEmpty();
        response.RefreshToken.Should().NotBeNullOrEmpty();
        response.User.Email.Should().Be(request.Email);
    }

    [Fact]
    public async Task Register_WithExistingEmail_ReturnsBadRequest()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "existing@example.com",
            Password = "Password123!",
            FirstName = "Existing",
            LastName = "User"
        };

        _mockRepository.Setup(r => r.GetUserByEmailAsync(request.Email))
            .ReturnsAsync(CreateActiveUser(request.Email));

        // Act
        var result = await _controller.Register(request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion

    #region Login Tests

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsTokens()
    {
        // Arrange
        const string password = "Password123!";
        var user = CreateActiveUser(passwordHash: _tokenService.HashPassword(password));

        var request = new LoginRequest { Email = user.Email, Password = password };

        _mockRepository.Setup(r => r.GetUserByEmailAsync(user.Email))
            .ReturnsAsync(user);
        _mockRepository.Setup(r => r.UpdateLastLoginAsync(user.Id))
            .Returns(Task.CompletedTask);
        _mockRepository.Setup(r => r.CreateRefreshTokenAsync(user.Id, It.IsAny<string>(), It.IsAny<DateTime>()))
            .ReturnsAsync(Guid.NewGuid());

        // Act
        var result = await _controller.Login(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<AuthResponse>().Subject;
        response.AccessToken.Should().NotBeNullOrEmpty();
        response.RefreshToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_WithInvalidEmail_ReturnsUnauthorized()
    {
        // Arrange
        var request = new LoginRequest { Email = "notfound@example.com", Password = "Password123!" };

        _mockRepository.Setup(r => r.GetUserByEmailAsync(request.Email))
            .ReturnsAsync((AuthUser?)null);

        // Act
        var result = await _controller.Login(request);

        // Assert
        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Login_WithInactiveUser_ReturnsUnauthorized()
    {
        // Arrange
        const string password = "Password123!";
        var user = CreateActiveUser(passwordHash: _tokenService.HashPassword(password));
        user.IsActive = false;

        var request = new LoginRequest { Email = user.Email, Password = password };

        _mockRepository.Setup(r => r.GetUserByEmailAsync(user.Email))
            .ReturnsAsync(user);

        // Act
        var result = await _controller.Login(request);

        // Assert
        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        // Arrange
        var user = CreateActiveUser(passwordHash: _tokenService.HashPassword("CorrectPassword123!"));
        var request = new LoginRequest { Email = user.Email, Password = "WrongPassword456!" };

        _mockRepository.Setup(r => r.GetUserByEmailAsync(user.Email))
            .ReturnsAsync(user);

        // Act
        var result = await _controller.Login(request);

        // Assert
        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Login_WithEmptyCredentials_ReturnsUnauthorized()
    {
        // Arrange
        var request = new LoginRequest { Email = string.Empty, Password = string.Empty };

        // Act
        var result = await _controller.Login(request);

        // Assert
        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    #endregion

    #region RefreshToken Tests

    [Fact]
    public async Task RefreshToken_WithValidToken_ReturnsNewTokens()
    {
        // Arrange
        const string oldRefreshToken = "old-refresh-token";
        var user = CreateActiveUser();

        var request = new RefreshTokenRequest { RefreshToken = oldRefreshToken };

        _mockRepository.Setup(r => r.ValidateRefreshTokenAsync(oldRefreshToken))
            .ReturnsAsync((_testUserId, true));
        _mockRepository.Setup(r => r.GetUserByIdAsync(_testUserId))
            .ReturnsAsync(user);
        _mockRepository.Setup(r => r.RevokeRefreshTokenAsync(oldRefreshToken))
            .Returns(Task.CompletedTask);
        _mockRepository.Setup(r => r.CreateRefreshTokenAsync(_testUserId, It.IsAny<string>(), It.IsAny<DateTime>()))
            .ReturnsAsync(Guid.NewGuid());

        // Act
        var result = await _controller.RefreshToken(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<AuthResponse>().Subject;
        response.AccessToken.Should().NotBeNullOrEmpty();
        response.RefreshToken.Should().NotBeNullOrEmpty();
        response.RefreshToken.Should().NotBe(oldRefreshToken);
    }

    [Fact]
    public async Task RefreshToken_WithInvalidToken_ReturnsUnauthorized()
    {
        // Arrange
        var request = new RefreshTokenRequest { RefreshToken = "invalid-token" };

        _mockRepository.Setup(r => r.ValidateRefreshTokenAsync("invalid-token"))
            .ReturnsAsync((Guid.Empty, false));

        // Act
        var result = await _controller.RefreshToken(request);

        // Assert
        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    #endregion

    #region Logout Tests

    [Fact]
    public async Task Logout_WhenAuthenticated_RevokesTokens()
    {
        // Arrange
        ControllerTestHelpers.SetupControllerContext(_controller, _testUserId);

        _mockRepository.Setup(r => r.RevokeAllUserTokensAsync(_testUserId))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Logout();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockRepository.Verify(r => r.RevokeAllUserTokensAsync(_testUserId), Times.Once);
    }

    #endregion

    #region RegisterInternal Tests

    [Fact]
    public async Task RegisterInternal_WithNewEmail_ReturnsUserId()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "internal@example.com",
            Password = "Password123!",
            FirstName = "Internal",
            LastName = "User"
        };

        _mockRepository.Setup(r => r.GetUserByEmailAsync(request.Email))
            .ReturnsAsync((AuthUser?)null);

        _mockRepository.Setup(r => r.CreateUserAsync(request.Email, It.IsAny<string>(), request.FirstName, request.LastName))
            .ReturnsAsync(_testUserId);

        // Act
        var result = await _controller.RegisterInternal(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().NotBeNull();

        // Verify the response contains userId
        var value = okResult.Value!;
        var userIdProp = value.GetType().GetProperty("userId");
        userIdProp.Should().NotBeNull();
        userIdProp!.GetValue(value).Should().Be(_testUserId);
    }

    #endregion

    #region End-to-End Flow Tests

    [Fact]
    public async Task Register_Login_Refresh_Logout_EndToEnd_Flow()
    {
        // --- REGISTER ---
        const string password = "Password123!";
        var registerRequest = new RegisterRequest
        {
            Email = "e2e@example.com",
            Password = password,
            FirstName = "E2E",
            LastName = "Test"
        };

        _mockRepository.Setup(r => r.GetUserByEmailAsync(registerRequest.Email))
            .ReturnsAsync((AuthUser?)null);

        string capturedPasswordHash = string.Empty;
        _mockRepository.Setup(r => r.CreateUserAsync(registerRequest.Email, It.IsAny<string>(), "E2E", "Test"))
            .Callback<string, string, string, string>((_, hash, _, _) => capturedPasswordHash = hash)
            .ReturnsAsync(_testUserId);

        var registeredUser = new AuthUser
        {
            Id = _testUserId,
            Email = registerRequest.Email,
            FirstName = "E2E",
            LastName = "Test",
            PasswordHash = string.Empty, // will be set after registration
            IsActive = true,
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow
        };

        _mockRepository.Setup(r => r.GetUserByIdAsync(_testUserId))
            .ReturnsAsync(registeredUser);
        _mockRepository.Setup(r => r.CreateRefreshTokenAsync(_testUserId, It.IsAny<string>(), It.IsAny<DateTime>()))
            .ReturnsAsync(Guid.NewGuid());

        var registerResult = await _controller.Register(registerRequest);
        registerResult.Should().BeOfType<OkObjectResult>();

        // --- LOGIN ---
        // Update the user's PasswordHash to the captured hash for login verification
        registeredUser.PasswordHash = capturedPasswordHash.Length > 0
            ? capturedPasswordHash
            : _tokenService.HashPassword(password);

        _mockRepository.Setup(r => r.GetUserByEmailAsync(registerRequest.Email))
            .ReturnsAsync(registeredUser);
        _mockRepository.Setup(r => r.UpdateLastLoginAsync(_testUserId))
            .Returns(Task.CompletedTask);

        var loginResult = await _controller.Login(new LoginRequest
        {
            Email = registerRequest.Email,
            Password = password
        });

        var loginOk = loginResult.Should().BeOfType<OkObjectResult>().Subject;
        var loginResponse = loginOk.Value.Should().BeAssignableTo<AuthResponse>().Subject;
        var refreshToken = loginResponse.RefreshToken;
        refreshToken.Should().NotBeNullOrEmpty();

        // --- REFRESH ---
        _mockRepository.Setup(r => r.ValidateRefreshTokenAsync(refreshToken))
            .ReturnsAsync((_testUserId, true));
        _mockRepository.Setup(r => r.RevokeRefreshTokenAsync(refreshToken))
            .Returns(Task.CompletedTask);

        var refreshResult = await _controller.RefreshToken(new RefreshTokenRequest { RefreshToken = refreshToken });
        var refreshOk = refreshResult.Should().BeOfType<OkObjectResult>().Subject;
        var refreshResponse = refreshOk.Value.Should().BeAssignableTo<AuthResponse>().Subject;
        refreshResponse.AccessToken.Should().NotBeNullOrEmpty();

        // --- LOGOUT ---
        _mockRepository.Setup(r => r.RevokeAllUserTokensAsync(_testUserId))
            .Returns(Task.CompletedTask);

        var logoutResult = await _controller.Logout();
        logoutResult.Should().BeOfType<OkObjectResult>();

        _mockRepository.Verify(r => r.RevokeAllUserTokensAsync(_testUserId), Times.Once);
    }

    #endregion
}
