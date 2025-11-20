using ExpressRecipe.Data.Common;
using ExpressRecipe.Shared.Models;
using Microsoft.Data.SqlClient;

namespace ExpressRecipe.AuthService.Data;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id);
    Task<User?> GetByEmailAsync(string email);
    Task<Guid> CreateAsync(User user, string passwordHash);
    Task UpdateAsync(User user);
    Task<bool> EmailExistsAsync(string email);
    Task IncrementAccessFailedCountAsync(Guid userId);
    Task ResetAccessFailedCountAsync(Guid userId);
}

public class UserRepository : SqlHelper, IUserRepository
{
    public UserRepository(string connectionString) : base(connectionString)
    {
    }

    public async Task<User?> GetByIdAsync(Guid id)
    {
        const string sql = @"
            SELECT Id, Email, EmailConfirmed, PhoneNumber, PhoneNumberConfirmed,
                   TwoFactorEnabled, LockoutEnd, LockoutEnabled, AccessFailedCount,
                   CreatedAt, UpdatedAt, IsDeleted
            FROM [User]
            WHERE Id = @Id AND IsDeleted = 0";

        return await ExecuteReaderSingleAsync(
            sql,
            reader => MapUser(reader),
            CreateParameter("@Id", id));
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        const string sql = @"
            SELECT Id, Email, EmailConfirmed, PhoneNumber, PhoneNumberConfirmed,
                   TwoFactorEnabled, LockoutEnd, LockoutEnabled, AccessFailedCount,
                   CreatedAt, UpdatedAt, IsDeleted
            FROM [User]
            WHERE Email = @Email AND IsDeleted = 0";

        return await ExecuteReaderSingleAsync(
            sql,
            reader => MapUser(reader),
            CreateParameter("@Email", email));
    }

    public async Task<Guid> CreateAsync(User user, string passwordHash)
    {
        const string sql = @"
            INSERT INTO [User] (Id, Email, EmailConfirmed, PasswordHash, SecurityStamp,
                               PhoneNumber, PhoneNumberConfirmed, TwoFactorEnabled,
                               LockoutEnabled, AccessFailedCount, CreatedAt, IsDeleted)
            VALUES (@Id, @Email, @EmailConfirmed, @PasswordHash, @SecurityStamp,
                   @PhoneNumber, @PhoneNumberConfirmed, @TwoFactorEnabled,
                   @LockoutEnabled, @AccessFailedCount, @CreatedAt, 0);
            SELECT @Id";

        var userId = Guid.NewGuid();

        await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", userId),
            CreateParameter("@Email", user.Email),
            CreateParameter("@EmailConfirmed", user.EmailConfirmed),
            CreateParameter("@PasswordHash", passwordHash),
            CreateParameter("@SecurityStamp", Guid.NewGuid().ToString()),
            CreateParameter("@PhoneNumber", user.PhoneNumber),
            CreateParameter("@PhoneNumberConfirmed", user.PhoneNumberConfirmed),
            CreateParameter("@TwoFactorEnabled", user.TwoFactorEnabled),
            CreateParameter("@LockoutEnabled", user.LockoutEnabled),
            CreateParameter("@AccessFailedCount", user.AccessFailedCount),
            CreateParameter("@CreatedAt", DateTime.UtcNow));

        return userId;
    }

    public async Task UpdateAsync(User user)
    {
        const string sql = @"
            UPDATE [User]
            SET Email = @Email,
                EmailConfirmed = @EmailConfirmed,
                PhoneNumber = @PhoneNumber,
                PhoneNumberConfirmed = @PhoneNumberConfirmed,
                TwoFactorEnabled = @TwoFactorEnabled,
                LockoutEnd = @LockoutEnd,
                LockoutEnabled = @LockoutEnabled,
                AccessFailedCount = @AccessFailedCount,
                UpdatedAt = @UpdatedAt
            WHERE Id = @Id";

        await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", user.Id),
            CreateParameter("@Email", user.Email),
            CreateParameter("@EmailConfirmed", user.EmailConfirmed),
            CreateParameter("@PhoneNumber", user.PhoneNumber),
            CreateParameter("@PhoneNumberConfirmed", user.PhoneNumberConfirmed),
            CreateParameter("@TwoFactorEnabled", user.TwoFactorEnabled),
            CreateParameter("@LockoutEnd", user.LockoutEnd),
            CreateParameter("@LockoutEnabled", user.LockoutEnabled),
            CreateParameter("@AccessFailedCount", user.AccessFailedCount),
            CreateParameter("@UpdatedAt", DateTime.UtcNow));
    }

    public async Task<bool> EmailExistsAsync(string email)
    {
        const string sql = @"
            SELECT COUNT(1)
            FROM [User]
            WHERE Email = @Email AND IsDeleted = 0";

        var count = await ExecuteScalarAsync<int>(
            sql,
            CreateParameter("@Email", email));

        return count > 0;
    }

    public async Task IncrementAccessFailedCountAsync(Guid userId)
    {
        const string sql = @"
            UPDATE [User]
            SET AccessFailedCount = AccessFailedCount + 1,
                UpdatedAt = @UpdatedAt
            WHERE Id = @Id";

        await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", userId),
            CreateParameter("@UpdatedAt", DateTime.UtcNow));
    }

    public async Task ResetAccessFailedCountAsync(Guid userId)
    {
        const string sql = @"
            UPDATE [User]
            SET AccessFailedCount = 0,
                UpdatedAt = @UpdatedAt
            WHERE Id = @Id";

        await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", userId),
            CreateParameter("@UpdatedAt", DateTime.UtcNow));
    }

    private static User MapUser(SqlDataReader reader)
    {
        return new User
        {
            Id = GetGuid(reader, "Id"),
            Email = GetString(reader, "Email"),
            EmailConfirmed = GetBoolean(reader, "EmailConfirmed"),
            PhoneNumber = GetNullableString(reader, "PhoneNumber"),
            PhoneNumberConfirmed = GetBoolean(reader, "PhoneNumberConfirmed"),
            TwoFactorEnabled = GetBoolean(reader, "TwoFactorEnabled"),
            LockoutEnd = GetNullableDateTime(reader, "LockoutEnd"),
            LockoutEnabled = GetBoolean(reader, "LockoutEnabled"),
            AccessFailedCount = GetInt32(reader, "AccessFailedCount"),
            CreatedAt = GetDateTime(reader, "CreatedAt"),
            UpdatedAt = GetNullableDateTime(reader, "UpdatedAt"),
            IsDeleted = GetBoolean(reader, "IsDeleted")
        };
    }
}
