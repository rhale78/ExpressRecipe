using ExpressRecipe.Data.Common;
using ExpressRecipe.Shared.DTOs.User;
using Microsoft.Data.SqlClient;

namespace ExpressRecipe.UserService.Data;

public interface IUserProfileRepository
{
    Task<UserProfileDto?> GetByUserIdAsync(Guid userId);
    Task<Guid> CreateAsync(CreateUserProfileRequest request, Guid? createdBy = null);
    Task<bool> UpdateAsync(Guid userId, UpdateUserProfileRequest request, Guid? updatedBy = null);
    Task<bool> DeleteAsync(Guid userId, Guid? deletedBy = null);
    Task<bool> UserProfileExistsAsync(Guid userId);
}

public class UserProfileRepository : SqlHelper, IUserProfileRepository
{
    public UserProfileRepository(string connectionString) : base(connectionString)
    {
    }

    public async Task<UserProfileDto?> GetByUserIdAsync(Guid userId)
    {
        const string sql = @"
            SELECT Id, UserId, FirstName, LastName, DateOfBirth, Gender,
                   HeightCm, WeightKg, ActivityLevel, CookingSkillLevel,
                   SubscriptionTier, SubscriptionExpiresAt
            FROM UserProfile
            WHERE UserId = @UserId AND IsDeleted = 0";

        var results = await ExecuteReaderAsync(
            sql,
            reader => new UserProfileDto
            {
                Id = GetGuid(reader, "Id"),
                UserId = GetGuid(reader, "UserId"),
                FirstName = GetString(reader, "FirstName"),
                LastName = GetString(reader, "LastName"),
                DateOfBirth = GetDateTime(reader, "DateOfBirth"),
                Gender = GetString(reader, "Gender"),
                HeightCm = GetDecimal(reader, "HeightCm"),
                WeightKg = GetDecimal(reader, "WeightKg"),
                ActivityLevel = GetString(reader, "ActivityLevel"),
                CookingSkillLevel = GetString(reader, "CookingSkillLevel"),
                SubscriptionTier = GetString(reader, "SubscriptionTier") ?? "Free",
                SubscriptionExpiresAt = GetDateTime(reader, "SubscriptionExpiresAt")
            },
            CreateParameter("@UserId", userId));

        return results.FirstOrDefault();
    }

    public async Task<Guid> CreateAsync(CreateUserProfileRequest request, Guid? createdBy = null)
    {
        const string sql = @"
            INSERT INTO UserProfile (
                Id, UserId, FirstName, LastName, DateOfBirth, Gender,
                HeightCm, WeightKg, ActivityLevel, CookingSkillLevel,
                SubscriptionTier, CreatedBy, CreatedAt
            )
            VALUES (
                @Id, @UserId, @FirstName, @LastName, @DateOfBirth, @Gender,
                @HeightCm, @WeightKg, @ActivityLevel, @CookingSkillLevel,
                'Free', @CreatedBy, GETUTCDATE()
            )";

        var profileId = Guid.NewGuid();

        await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", profileId),
            CreateParameter("@UserId", request.UserId),
            CreateParameter("@FirstName", request.FirstName),
            CreateParameter("@LastName", request.LastName),
            CreateParameter("@DateOfBirth", request.DateOfBirth),
            CreateParameter("@Gender", request.Gender),
            CreateParameter("@HeightCm", request.HeightCm),
            CreateParameter("@WeightKg", request.WeightKg),
            CreateParameter("@ActivityLevel", request.ActivityLevel),
            CreateParameter("@CookingSkillLevel", request.CookingSkillLevel),
            CreateParameter("@CreatedBy", createdBy));

        return profileId;
    }

    public async Task<bool> UpdateAsync(Guid userId, UpdateUserProfileRequest request, Guid? updatedBy = null)
    {
        const string sql = @"
            UPDATE UserProfile
            SET FirstName = @FirstName,
                LastName = @LastName,
                DateOfBirth = @DateOfBirth,
                Gender = @Gender,
                HeightCm = @HeightCm,
                WeightKg = @WeightKg,
                ActivityLevel = @ActivityLevel,
                CookingSkillLevel = @CookingSkillLevel,
                UpdatedBy = @UpdatedBy,
                UpdatedAt = GETUTCDATE()
            WHERE UserId = @UserId AND IsDeleted = 0";

        var rowsAffected = await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@UserId", userId),
            CreateParameter("@FirstName", request.FirstName),
            CreateParameter("@LastName", request.LastName),
            CreateParameter("@DateOfBirth", request.DateOfBirth),
            CreateParameter("@Gender", request.Gender),
            CreateParameter("@HeightCm", request.HeightCm),
            CreateParameter("@WeightKg", request.WeightKg),
            CreateParameter("@ActivityLevel", request.ActivityLevel),
            CreateParameter("@CookingSkillLevel", request.CookingSkillLevel),
            CreateParameter("@UpdatedBy", updatedBy));

        return rowsAffected > 0;
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid? deletedBy = null)
    {
        const string sql = @"
            UPDATE UserProfile
            SET IsDeleted = 1,
                DeletedAt = GETUTCDATE(),
                UpdatedBy = @DeletedBy,
                UpdatedAt = GETUTCDATE()
            WHERE UserId = @UserId AND IsDeleted = 0";

        var rowsAffected = await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@UserId", userId),
            CreateParameter("@DeletedBy", deletedBy));

        return rowsAffected > 0;
    }

    public async Task<bool> UserProfileExistsAsync(Guid userId)
    {
        const string sql = "SELECT COUNT(*) FROM UserProfile WHERE UserId = @UserId AND IsDeleted = 0";

        var count = await ExecuteScalarAsync<int>(
            sql,
            CreateParameter("@UserId", userId));

        return count > 0;
    }
}
