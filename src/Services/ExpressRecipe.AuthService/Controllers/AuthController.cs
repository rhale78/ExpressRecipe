using ExpressRecipe.AuthService.Data;
using ExpressRecipe.Shared.DTOs.Auth;
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
    private readonly IHttpClientFactory _httpClientFactory;

    public AuthController(
        IAuthRepository repository,
        TokenService tokenService,
        ILogger<AuthController> logger,
        IHttpClientFactory httpClientFactory)
    {
        _repository = repository;
        _tokenService = tokenService;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        try
        {
            // Log raw request info
            _logger.LogInformation("Registration attempt - Email: {Email}, FirstName: {FirstName}, LastName: {LastName}, PasswordLength: {PasswordLength}",
                request.Email ?? "(null)",
                request.FirstName ?? "(null)",
                request.LastName ?? "(null)",
                request.Password?.Length ?? 0);

            // Check model state and log validation errors
            if (!ModelState.IsValid)
            {
                var errors = ModelState
                    .Where(x => x.Value?.Errors.Count > 0)
                    .Select(x => new
                    {
                        Field = x.Key,
                        Errors = x.Value?.Errors.Select(e => e.ErrorMessage).ToArray()
                    })
                    .ToList();

                _logger.LogWarning("Model validation failed: {Errors}", System.Text.Json.JsonSerializer.Serialize(errors));
                
                return BadRequest(new
                {
                    message = "Validation failed",
                    errors = errors
                });
            }
            
            // Check if user already exists
            var existingUser = await _repository.GetUserByEmailAsync(request.Email);
            if (existingUser != null)
            {
                _logger.LogWarning("Registration failed: User {Email} already exists", request.Email);
                return BadRequest(new { message = "User with this email already exists" });
            }

            // Hash password
            var passwordHash = _tokenService.HashPassword(request.Password);

            // Create user
            _logger.LogInformation("Creating user for email: {Email}", request.Email);
            var userId = await _repository.CreateUserAsync(
                request.Email,
                passwordHash,
                request.FirstName ?? "",
                request.LastName ?? ""
            );

            // Get created user
            var user = await _repository.GetUserByIdAsync(userId);
            if (user == null)
            {
                _logger.LogError("User creation appeared successful but user {UserId} could not be retrieved", userId);
                return StatusCode(500, new { message = "Failed to create user" });
            }

            // Generate tokens
            var accessToken = _tokenService.GenerateAccessToken(user);
            var refreshToken = _tokenService.GenerateRefreshToken();
            var refreshTokenExpiration = _tokenService.GetRefreshTokenExpiration();

            await _repository.CreateRefreshTokenAsync(userId, refreshToken, refreshTokenExpiration);

            // Create user profile in UserService
            try
            {
                var profileRequest = new ExpressRecipe.Shared.DTOs.User.CreateUserProfileForNewUserRequest
                {
                    UserId = userId,
                    FirstName = request.FirstName ?? "",
                    LastName = request.LastName ?? "",
                    Email = request.Email
                };

                var userServiceClient = _httpClientFactory.CreateClient("UserService");
                var profileResponse = await userServiceClient.PostAsJsonAsync("/api/userprofile/system/create", profileRequest);
                
                if (profileResponse.IsSuccessStatusCode)
                {
                    _logger.LogInformation("User profile created successfully for user {UserId}", userId);
                }
                else
                {
                    _logger.LogWarning("Failed to create user profile for user {UserId}: {StatusCode}", userId, profileResponse.StatusCode);
                }
            }
            catch (Exception profileEx)
            {
                _logger.LogWarning(profileEx, "Error creating user profile for user {UserId} - registration will continue", userId);
            }

            _logger.LogInformation("User {UserId} registered successfully", userId);

            return Ok(new AuthResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresAt = DateTime.UtcNow.AddMinutes(60),
                User = new UserDto
                {
                    Id = user.Id,
                    Email = user.Email,
                    EmailConfirmed = false,
                    PhoneNumber = null,
                    TwoFactorEnabled = false
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Registration failed for {Email}: {Message}", request.Email, ex.Message);
            return StatusCode(500, new { message = $"Registration failed: {ex.Message}" });
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        _logger.LogInformation("Login attempt - Email: {Email}, PasswordLength: {PasswordLength}", 
            request.Email ?? "(null)", 
            request.Password?.Length ?? 0);

        if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
        {
            _logger.LogWarning("Login failed: Email or Password is null/empty");
            return Unauthorized(new { message = "Invalid email or password" });
        }

        // Get user by email
        var user = await _repository.GetUserByEmailAsync(request.Email);
        if (user == null)
        {
            _logger.LogWarning("Login failed: User not found for email {Email}", request.Email);
            return Unauthorized(new { message = "Invalid email or password" });
        }

        // Check if user is active
        if (!user.IsActive)
        {
            _logger.LogWarning("Login failed: User {Email} is inactive", request.Email);
            return Unauthorized(new { message = "Account is inactive" });
        }

        // Verify password
        bool passwordValid = _tokenService.VerifyPassword(request.Password, user.PasswordHash);
        
        if (!passwordValid)
        {
            _logger.LogWarning("Login failed: Password mismatch for user {Email}", request.Email);       
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

        return Ok(new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(60),
            User = new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                EmailConfirmed = false,
                PhoneNumber = null,
                TwoFactorEnabled = false
            }
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

        return Ok(new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = newRefreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(60),
            User = new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                EmailConfirmed = false,
                PhoneNumber = null,
                TwoFactorEnabled = false
            }
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
