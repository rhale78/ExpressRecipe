using ExpressRecipe.AuthService.Data;
using ExpressRecipe.AuthService.Models;
using ExpressRecipe.AuthService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpressRecipe.AuthService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthRepository _repository;
    private readonly TokenService _tokenService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthRepository repository,
        TokenService tokenService,
        ILogger<AuthController> logger)
    {
        _repository = repository;
        _tokenService = tokenService;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        // Check if user already exists
        var existingUser = await _repository.GetUserByEmailAsync(request.Email);
        if (existingUser != null)
        {
            return BadRequest(new { message = "User with this email already exists" });
        }

        // Hash password
        var passwordHash = _tokenService.HashPassword(request.Password);

        // Create user
        var userId = await _repository.CreateUserAsync(
            request.Email,
            passwordHash,
            request.FirstName,
            request.LastName
        );

        // Get created user
        var user = await _repository.GetUserByIdAsync(userId);
        if (user == null)
        {
            return StatusCode(500, new { message = "Failed to create user" });
        }

        // Generate tokens
        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshToken = _tokenService.GenerateRefreshToken();
        var refreshTokenExpiration = _tokenService.GetRefreshTokenExpiration();

        await _repository.CreateRefreshTokenAsync(userId, refreshToken, refreshTokenExpiration);

        _logger.LogInformation("User {UserId} registered successfully", userId);

        return Ok(new TokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(60),
            UserId = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName
        });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        // Get user by email
        var user = await _repository.GetUserByEmailAsync(request.Email);
        if (user == null)
        {
            return Unauthorized(new { message = "Invalid email or password" });
        }

        // Check if user is active
        if (!user.IsActive)
        {
            return Unauthorized(new { message = "Account is inactive" });
        }

        // Verify password
        if (!_tokenService.VerifyPassword(request.Password, user.PasswordHash))
        {
            return Unauthorized(new { message = "Invalid email or password" });
        }

        // Update last login
        await _repository.UpdateLastLoginAsync(user.Id);

        // Generate tokens
        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshToken = _tokenService.GenerateRefreshToken();
        var refreshTokenExpiration = _tokenService.GetRefreshTokenExpiration();

        await _repository.CreateRefreshTokenAsync(user.Id, refreshToken, refreshTokenExpiration);

        _logger.LogInformation("User {UserId} logged in successfully", user.Id);

        return Ok(new TokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(60),
            UserId = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName
        });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        // Validate refresh token
        var (userId, isValid) = await _repository.ValidateRefreshTokenAsync(request.RefreshToken);
        
        if (!isValid)
        {
            return Unauthorized(new { message = "Invalid or expired refresh token" });
        }

        // Get user
        var user = await _repository.GetUserByIdAsync(userId);
        if (user == null || !user.IsActive)
        {
            return Unauthorized(new { message = "User not found or inactive" });
        }

        // Revoke old refresh token
        await _repository.RevokeRefreshTokenAsync(request.RefreshToken);

        // Generate new tokens
        var accessToken = _tokenService.GenerateAccessToken(user);
        var newRefreshToken = _tokenService.GenerateRefreshToken();
        var refreshTokenExpiration = _tokenService.GetRefreshTokenExpiration();

        await _repository.CreateRefreshTokenAsync(userId, newRefreshToken, refreshTokenExpiration);

        _logger.LogInformation("User {UserId} refreshed token successfully", userId);

        return Ok(new TokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = newRefreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(60),
            UserId = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName
        });
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        // Revoke all refresh tokens for this user
        await _repository.RevokeAllUserTokensAsync(userId);

        _logger.LogInformation("User {UserId} logged out successfully", userId);

        return Ok(new { message = "Logged out successfully" });
    }
}
