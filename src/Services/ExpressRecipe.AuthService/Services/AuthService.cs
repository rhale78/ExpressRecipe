using ExpressRecipe.AuthService.Data;
using ExpressRecipe.Shared.DTOs.Auth;
using ExpressRecipe.Shared.Models;

namespace ExpressRecipe.AuthService.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<AuthResponse> RefreshTokenAsync(string refreshToken);
}

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly ITokenService _tokenService;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository userRepository,
        ITokenService tokenService,
        ILogger<AuthService> logger)
    {
        _userRepository = userRepository;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        // Check if email already exists
        if (await _userRepository.EmailExistsAsync(request.Email))
        {
            throw new InvalidOperationException("Email already registered");
        }

        // Hash password
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

        // Create user
        var user = new User
        {
            Email = request.Email,
            EmailConfirmed = false,
            LockoutEnabled = true,
            AccessFailedCount = 0
        };

        var userId = await _userRepository.CreateAsync(user, passwordHash);
        user.Id = userId;

        _logger.LogInformation("User registered: {Email}", request.Email);

        // Generate tokens
        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshToken = _tokenService.GenerateRefreshToken();

        return new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(60),
            User = new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                EmailConfirmed = user.EmailConfirmed,
                PhoneNumber = user.PhoneNumber,
                TwoFactorEnabled = user.TwoFactorEnabled
            }
        };
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        // Get user by email
        var user = await _userRepository.GetByEmailAsync(request.Email);
        if (user == null)
        {
            _logger.LogWarning("Login attempt with non-existent email: {Email}", request.Email);
            throw new UnauthorizedAccessException("Invalid email or password");
        }

        // Check if account is locked
        if (user.LockoutEnd.HasValue && user.LockoutEnd > DateTime.UtcNow)
        {
            _logger.LogWarning("Login attempt on locked account: {Email}", request.Email);
            throw new UnauthorizedAccessException("Account is locked");
        }

        // Verify password (retrieve password hash from database)
        var passwordHash = await GetPasswordHashAsync(user.Id);
        if (!BCrypt.Net.BCrypt.Verify(request.Password, passwordHash))
        {
            await _userRepository.IncrementAccessFailedCountAsync(user.Id);
            _logger.LogWarning("Failed login attempt for user: {Email}", request.Email);
            throw new UnauthorizedAccessException("Invalid email or password");
        }

        // Reset failed access count on successful login
        if (user.AccessFailedCount > 0)
        {
            await _userRepository.ResetAccessFailedCountAsync(user.Id);
        }

        _logger.LogInformation("User logged in: {Email}", request.Email);

        // Generate tokens
        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshToken = _tokenService.GenerateRefreshToken();

        return new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(60),
            User = new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                EmailConfirmed = user.EmailConfirmed,
                PhoneNumber = user.PhoneNumber,
                TwoFactorEnabled = user.TwoFactorEnabled
            }
        };
    }

    public async Task<AuthResponse> RefreshTokenAsync(string refreshToken)
    {
        // Validate refresh token
        var userId = _tokenService.ValidateRefreshToken(refreshToken);
        if (userId == null)
        {
            throw new UnauthorizedAccessException("Invalid refresh token");
        }

        // Get user
        var user = await _userRepository.GetByIdAsync(userId.Value);
        if (user == null)
        {
            throw new UnauthorizedAccessException("User not found");
        }

        // Generate new tokens
        var accessToken = _tokenService.GenerateAccessToken(user);
        var newRefreshToken = _tokenService.GenerateRefreshToken();

        return new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = newRefreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(60),
            User = new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                EmailConfirmed = user.EmailConfirmed,
                PhoneNumber = user.PhoneNumber,
                TwoFactorEnabled = user.TwoFactorEnabled
            }
        };
    }

    private async Task<string> GetPasswordHashAsync(Guid userId)
    {
        // This is a simplified version - in production, you'd query the password hash
        // For now, we'll add this method to the repository
        const string sql = "SELECT PasswordHash FROM [User] WHERE Id = @Id";

        using var connection = new Microsoft.Data.SqlClient.SqlConnection(_userRepository.GetType()
            .GetField("ConnectionString", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
            .GetValue(_userRepository)?.ToString() ?? throw new InvalidOperationException());

        await connection.OpenAsync();
        using var command = new Microsoft.Data.SqlClient.SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", userId);

        var result = await command.ExecuteScalarAsync();
        return result?.ToString() ?? throw new InvalidOperationException("Password hash not found");
    }
}
