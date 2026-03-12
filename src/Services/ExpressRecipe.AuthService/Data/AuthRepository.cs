using ExpressRecipe.AuthService.Models;
using ExpressRecipe.Shared.Services;
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
    Task<bool> EnsureAdminUserExistsAsync();

    // GDPR
    Task DeleteUserDataAsync(Guid userId, CancellationToken ct = default);
}

public class AuthRepository : IAuthRepository
{
    private readonly string _connectionString;
    private readonly ILogger<AuthRepository> _logger;
    private readonly HybridCacheService? _cache;
    private const string CachePrefix = "auth:user:";

    public AuthRepository(string connectionString, ILogger<AuthRepository> logger, HybridCacheService? cache = null)
    {
        _connectionString = connectionString;
        _logger = logger;
        _cache = cache;
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

        var result = await command.ExecuteScalarAsync();
        var userId = result != null ? (Guid)result : Guid.Empty;
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
        if (_cache != null)
        {
            return await _cache.GetOrSetAsync(
                $"{CachePrefix}id:{userId}",
                async (ct) => await GetUserByIdFromDbAsync(userId),
                expiration: TimeSpan.FromMinutes(5));
        }

        return await GetUserByIdFromDbAsync(userId);
    }

    private async Task<AuthUser?> GetUserByIdFromDbAsync(Guid userId)
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

        if (_cache != null)
            await _cache.RemoveAsync($"{CachePrefix}id:{userId}");
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

        var result = await command.ExecuteScalarAsync();
        return result != null ? (Guid)result : Guid.Empty;
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

    public async Task<bool> EnsureAdminUserExistsAsync()
    {
        const string adminEmail = "admin@admin.com";
        // Use a well-known GUID for the admin user (same across all environments)
        var adminId = new Guid("00000000-0000-0000-0000-000000000001");
        
        const string checkByIdSql = "SELECT COUNT(1) FROM [User] WHERE Id = @Id AND IsDeleted = 0";
        const string checkByEmailSql = "SELECT Id FROM [User] WHERE Email = @Email AND IsDeleted = 0";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        // 1. Check if admin user exists by ID
        await using var checkIdCommand = new SqlCommand(checkByIdSql, connection);
        checkIdCommand.Parameters.AddWithValue("@Id", adminId);
        var countById = (int)(await checkIdCommand.ExecuteScalarAsync() ?? 0);

        // 2. Check if admin user exists by Email (in case it was seeded with a different ID)
        await using var checkEmailCommand = new SqlCommand(checkByEmailSql, connection);
        checkEmailCommand.Parameters.AddWithValue("@Email", adminEmail);
        var existingUserIdResult = await checkEmailCommand.ExecuteScalarAsync();
        var existingUserIdByEmail = existingUserIdResult != null ? (Guid)existingUserIdResult : Guid.Empty;

        // BCrypt hash for "admin" with work factor 11
        var adminPasswordHash = BCrypt.Net.BCrypt.HashPassword("admin", 11);

        if (countById > 0)
        {
            _logger.LogInformation("Admin user found by ID, ensuring credentials are correct...");
            const string updateSql = @"
                UPDATE [User] 
                SET Email = @Email, PasswordHash = @PasswordHash, FirstName = @FirstName, LastName = @LastName, IsActive = 1
                WHERE Id = @Id";

            await using var updateCommand = new SqlCommand(updateSql, connection);
            updateCommand.Parameters.AddWithValue("@Id", adminId);
            updateCommand.Parameters.AddWithValue("@Email", adminEmail);
            updateCommand.Parameters.AddWithValue("@PasswordHash", adminPasswordHash);
            updateCommand.Parameters.AddWithValue("@FirstName", "Admin");
            updateCommand.Parameters.AddWithValue("@LastName", "User");
            await updateCommand.ExecuteNonQueryAsync();
            return false;
        }
        else if (existingUserIdByEmail != Guid.Empty)
        {
            _logger.LogInformation("Admin user found by Email with different ID {ExistingId}, updating to standard admin ID...", existingUserIdByEmail);
            
            // Delete the old one and recreate with standard ID to avoid unique constraint issues
            // This is safer than updating the PK
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
            try
            {
                const string deleteSql = "DELETE FROM [User] WHERE Id = @Id";
                await using (var deleteCommand = new SqlCommand(deleteSql, connection, transaction))
                {
                    deleteCommand.Parameters.AddWithValue("@Id", existingUserIdByEmail);
                    await deleteCommand.ExecuteNonQueryAsync();
                }

                const string insertSql = @"
                    INSERT INTO [User] (Id, Email, PasswordHash, FirstName, LastName, EmailVerified, IsActive, CreatedAt)
                    VALUES (@Id, @Email, @PasswordHash, @FirstName, @LastName, 1, 1, GETUTCDATE())";

                await using (var insertCommand = new SqlCommand(insertSql, connection, transaction))
                {
                    insertCommand.Parameters.AddWithValue("@Id", adminId);
                    insertCommand.Parameters.AddWithValue("@Email", adminEmail);
                    insertCommand.Parameters.AddWithValue("@PasswordHash", adminPasswordHash);
                    insertCommand.Parameters.AddWithValue("@FirstName", "Admin");
                    insertCommand.Parameters.AddWithValue("@LastName", "User");
                    await insertCommand.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // Create fresh admin user
        _logger.LogInformation("Creating fresh admin user...");
        const string freshInsertSql = @"
            INSERT INTO [User] (Id, Email, PasswordHash, FirstName, LastName, EmailVerified, IsActive, CreatedAt)
            VALUES (@Id, @Email, @PasswordHash, @FirstName, @LastName, 1, 1, GETUTCDATE())";

        await using var freshInsertCommand = new SqlCommand(freshInsertSql, connection);
        freshInsertCommand.Parameters.AddWithValue("@Id", adminId);
        freshInsertCommand.Parameters.AddWithValue("@Email", adminEmail);
        freshInsertCommand.Parameters.AddWithValue("@PasswordHash", adminPasswordHash);
        freshInsertCommand.Parameters.AddWithValue("@FirstName", "Admin");
        freshInsertCommand.Parameters.AddWithValue("@LastName", "User");

        await freshInsertCommand.ExecuteNonQueryAsync();
        return true;
    }

    public async Task DeleteUserDataAsync(Guid userId, CancellationToken ct = default)
    {
        const string sql = @"
DELETE FROM ExternalCalendarToken WHERE UserId = @UserId;
DELETE FROM ExternalLogin          WHERE UserId = @UserId;
DELETE FROM RefreshToken           WHERE UserId = @UserId;";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        await command.ExecuteNonQueryAsync(ct);

        if (_cache != null)
            await _cache.RemoveAsync($"{CachePrefix}id:{userId}");
    }
}
