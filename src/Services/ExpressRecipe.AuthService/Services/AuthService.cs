using ExpressRecipe.AuthService.Data;
using ExpressRecipe.Shared.DTOs.Auth;
using ExpressRecipe.Shared.Models;

using Microsoft.Data.SqlClient;

namespace ExpressRecipe.AuthService.Services
{
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
            if (await _userRepository.EmailExistsAsync(request.Email))
            {
                throw new InvalidOperationException("Email already registered");
            }

            var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

            User user = new User
            {
                Email = request.Email,
                EmailConfirmed = false,
                FirstName = request.FirstName,
                LastName = request.LastName,
                AccessFailedCount = 0
            };

            Guid userId = await _userRepository.CreateAsync(user, passwordHash);
            user.Id = userId;

            _logger.LogInformation("User registered: {Email}", request.Email);

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
            User? user = await _userRepository.GetByEmailAsync(request.Email);
            if (user == null)
            {
                _logger.LogWarning("Login attempt with non-existent email: {Email}", request.Email);
                throw new UnauthorizedAccessException("Invalid email or password");
            }

            // Verify password (retrieve password hash from database)
            var passwordHash = await GetPasswordHashAsync(user.Id);
            if (!BCrypt.Net.BCrypt.Verify(request.Password, passwordHash))
            {
                await _userRepository.IncrementAccessFailedCountAsync(user.Id);
                _logger.LogWarning("Failed login attempt for user: {Email}", request.Email);
                throw new UnauthorizedAccessException("Invalid email or password");
            }

            if (user.AccessFailedCount > 0)
            {
                await _userRepository.ResetAccessFailedCountAsync(user.Id);
            }

            _logger.LogInformation("User logged in: {Email}", request.Email);

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
            Guid? userId = _tokenService.ValidateRefreshToken(refreshToken);
            if (userId == null)
            {
                throw new UnauthorizedAccessException("Invalid refresh token");
            }

            User? user = await _userRepository.GetByIdAsync(userId.Value);
            if (user == null)
            {
                throw new UnauthorizedAccessException("User not found");
            }

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
            const string sql = "SELECT PasswordHash FROM [User] WHERE Id = @Id";

            using SqlConnection connection = new Microsoft.Data.SqlClient.SqlConnection(_userRepository.GetType()
                .GetField("ConnectionString", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
                .GetValue(_userRepository)?.ToString() ?? throw new InvalidOperationException());

            await connection.OpenAsync();
            using SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@Id", userId);

            var result = await command.ExecuteScalarAsync();
            return result?.ToString() ?? throw new InvalidOperationException("Password hash not found");
        }
    }
}
