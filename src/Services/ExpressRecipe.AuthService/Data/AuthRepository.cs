using ExpressRecipe.AuthService.Models;
using Microsoft.Data.SqlClient;

namespace ExpressRecipe.AuthService.Data;

public interface IAuthRepository
{
    Task<Guid> CreateUserAsync(string email, string passwordHash, string firstName, string lastName);
    Task<AuthUser?> GetUserByEmailAsync(string email);
    Task<AuthUser?> GetUserByIdAsync(Guid userId);
    Task UpdateLastLoginAsync(Guid userId);
    Task<Guid> CreateRefreshTokenAsync(Guid userId, string token, DateTime expiresAt);
    Task<(Guid userId, bool isValid)> ValidateRefreshTokenAsync(string token);
    Task RevokeRefreshTokenAsync(string token);
    Task RevokeAllUserTokensAsync(Guid userId);
}

public class AuthRepository : IAuthRepository
{
    private readonly string _connectionString;
    private readonly ILogger<AuthRepository> _logger;

    public AuthRepository(string connectionString, ILogger<AuthRepository> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task<Guid> CreateUserAsync(string email, string passwordHash, string firstName, string lastName)
    {
        const string sql = @"
            INSERT INTO [User] (Email, PasswordHash, FirstName, LastName, EmailVerified, IsActive, CreatedAt)
            OUTPUT INSERTED.Id
            VALUES (@Email, @PasswordHash, @FirstName, @LastName, 0, 1, GETUTCDATE())";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Email", email);
        command.Parameters.AddWithValue("@PasswordHash", passwordHash);
        command.Parameters.AddWithValue("@FirstName", firstName);
        command.Parameters.AddWithValue("@LastName", lastName);

        var userId = (Guid)await command.ExecuteScalarAsync()!;
        _logger.LogInformation("Created user {UserId} with email {Email}", userId, email);
        return userId;
    }

    public async Task<AuthUser?> GetUserByEmailAsync(string email)
    {
        const string sql = @"
            SELECT Id, Email, PasswordHash, FirstName, LastName, EmailVerified, IsActive, CreatedAt, LastLoginAt
            FROM [User]
            WHERE Email = @Email AND IsDeleted = 0";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Email", email);

        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new AuthUser
            {
                Id = reader.GetGuid(0),
                Email = reader.GetString(1),
                PasswordHash = reader.GetString(2),
                FirstName = reader.GetString(3),
                LastName = reader.GetString(4),
                EmailVerified = reader.GetBoolean(5),
                IsActive = reader.GetBoolean(6),
                CreatedAt = reader.GetDateTime(7),
                LastLoginAt = reader.IsDBNull(8) ? null : reader.GetDateTime(8)
            };
        }

        return null;
    }

    public async Task<AuthUser?> GetUserByIdAsync(Guid userId)
    {
        const string sql = @"
            SELECT Id, Email, PasswordHash, FirstName, LastName, EmailVerified, IsActive, CreatedAt, LastLoginAt
            FROM [User]
            WHERE Id = @UserId AND IsDeleted = 0";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);

        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new AuthUser
            {
                Id = reader.GetGuid(0),
                Email = reader.GetString(1),
                PasswordHash = reader.GetString(2),
                FirstName = reader.GetString(3),
                LastName = reader.GetString(4),
                EmailVerified = reader.GetBoolean(5),
                IsActive = reader.GetBoolean(6),
                CreatedAt = reader.GetDateTime(7),
                LastLoginAt = reader.IsDBNull(8) ? null : reader.GetDateTime(8)
            };
        }

        return null;
    }

    public async Task UpdateLastLoginAsync(Guid userId)
    {
        const string sql = "UPDATE [User] SET LastLoginAt = GETUTCDATE() WHERE Id = @UserId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<Guid> CreateRefreshTokenAsync(Guid userId, string token, DateTime expiresAt)
    {
        const string sql = @"
            INSERT INTO RefreshToken (UserId, Token, ExpiresAt, CreatedAt)
            OUTPUT INSERTED.Id
            VALUES (@UserId, @Token, @ExpiresAt, GETUTCDATE())";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@Token", token);
        command.Parameters.AddWithValue("@ExpiresAt", expiresAt);

        return (Guid)await command.ExecuteScalarAsync()!;
    }

    public async Task<(Guid userId, bool isValid)> ValidateRefreshTokenAsync(string token)
    {
        const string sql = @"
            SELECT UserId, ExpiresAt, IsRevoked
            FROM RefreshToken
            WHERE Token = @Token";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Token", token);

        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var userId = reader.GetGuid(0);
            var expiresAt = reader.GetDateTime(1);
            var isRevoked = reader.GetBoolean(2);

            var isValid = !isRevoked && expiresAt > DateTime.UtcNow;
            return (userId, isValid);
        }

        return (Guid.Empty, false);
    }

    public async Task RevokeRefreshTokenAsync(string token)
    {
        const string sql = "UPDATE RefreshToken SET IsRevoked = 1, RevokedAt = GETUTCDATE() WHERE Token = @Token";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Token", token);

        await command.ExecuteNonQueryAsync();
    }

    public async Task RevokeAllUserTokensAsync(Guid userId)
    {
        const string sql = "UPDATE RefreshToken SET IsRevoked = 1, RevokedAt = GETUTCDATE() WHERE UserId = @UserId AND IsRevoked = 0";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);

        await command.ExecuteNonQueryAsync();
        _logger.LogInformation("Revoked all refresh tokens for user {UserId}", userId);
    }
}
