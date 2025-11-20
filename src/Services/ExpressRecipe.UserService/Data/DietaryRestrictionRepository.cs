using ExpressRecipe.Data.Common;
using ExpressRecipe.Shared.DTOs.User;

namespace ExpressRecipe.UserService.Data;

public interface IDietaryRestrictionRepository
{
    Task<List<DietaryRestrictionDto>> GetAllRestrictionsAsync();
    Task<DietaryRestrictionDto?> GetByIdAsync(Guid id);
    Task<List<DietaryRestrictionDto>> GetByTypeAsync(string type);
    Task<List<UserDietaryRestrictionDto>> GetUserRestrictionsAsync(Guid userId);
    Task<Guid> AddUserRestrictionAsync(Guid userId, AddUserDietaryRestrictionRequest request, Guid? createdBy = null);
    Task<bool> UpdateUserRestrictionAsync(Guid userRestrictionId, UpdateUserDietaryRestrictionRequest request, Guid? updatedBy = null);
    Task<bool> RemoveUserRestrictionAsync(Guid userRestrictionId, Guid? deletedBy = null);
}

public class DietaryRestrictionRepository : SqlHelper, IDietaryRestrictionRepository
{
    public DietaryRestrictionRepository(string connectionString) : base(connectionString)
    {
    }

    public async Task<List<DietaryRestrictionDto>> GetAllRestrictionsAsync()
    {
        const string sql = @"
            SELECT Id, Name, Type, Description, CommonExclusions
            FROM DietaryRestriction
            WHERE IsDeleted = 0
            ORDER BY Type, Name";

        return await ExecuteReaderAsync(
            sql,
            reader => new DietaryRestrictionDto
            {
                Id = GetGuid(reader, "Id"),
                Name = GetString(reader, "Name") ?? string.Empty,
                Type = GetString(reader, "Type") ?? string.Empty,
                Description = GetString(reader, "Description"),
                CommonExclusions = GetString(reader, "CommonExclusions")
            });
    }

    public async Task<DietaryRestrictionDto?> GetByIdAsync(Guid id)
    {
        const string sql = @"
            SELECT Id, Name, Type, Description, CommonExclusions
            FROM DietaryRestriction
            WHERE Id = @Id AND IsDeleted = 0";

        var results = await ExecuteReaderAsync(
            sql,
            reader => new DietaryRestrictionDto
            {
                Id = GetGuid(reader, "Id"),
                Name = GetString(reader, "Name") ?? string.Empty,
                Type = GetString(reader, "Type") ?? string.Empty,
                Description = GetString(reader, "Description"),
                CommonExclusions = GetString(reader, "CommonExclusions")
            },
            CreateParameter("@Id", id));

        return results.FirstOrDefault();
    }

    public async Task<List<DietaryRestrictionDto>> GetByTypeAsync(string type)
    {
        const string sql = @"
            SELECT Id, Name, Type, Description, CommonExclusions
            FROM DietaryRestriction
            WHERE Type = @Type AND IsDeleted = 0
            ORDER BY Name";

        return await ExecuteReaderAsync(
            sql,
            reader => new DietaryRestrictionDto
            {
                Id = GetGuid(reader, "Id"),
                Name = GetString(reader, "Name") ?? string.Empty,
                Type = GetString(reader, "Type") ?? string.Empty,
                Description = GetString(reader, "Description"),
                CommonExclusions = GetString(reader, "CommonExclusions")
            },
            CreateParameter("@Type", type));
    }

    public async Task<List<UserDietaryRestrictionDto>> GetUserRestrictionsAsync(Guid userId)
    {
        const string sql = @"
            SELECT udr.Id, udr.UserId, udr.DietaryRestrictionId,
                   dr.Name as RestrictionName, dr.Type as RestrictionType,
                   udr.Strictness, udr.Notes, udr.StartDate, udr.EndDate
            FROM UserDietaryRestriction udr
            INNER JOIN DietaryRestriction dr ON udr.DietaryRestrictionId = dr.Id
            WHERE udr.UserId = @UserId AND udr.IsDeleted = 0 AND dr.IsDeleted = 0
            ORDER BY udr.Strictness DESC, dr.Name";

        return await ExecuteReaderAsync(
            sql,
            reader => new UserDietaryRestrictionDto
            {
                Id = GetGuid(reader, "Id"),
                UserId = GetGuid(reader, "UserId"),
                DietaryRestrictionId = GetGuid(reader, "DietaryRestrictionId"),
                RestrictionName = GetString(reader, "RestrictionName") ?? string.Empty,
                RestrictionType = GetString(reader, "RestrictionType") ?? string.Empty,
                Strictness = GetString(reader, "Strictness") ?? string.Empty,
                Notes = GetString(reader, "Notes"),
                StartDate = GetDateTime(reader, "StartDate"),
                EndDate = GetDateTime(reader, "EndDate")
            },
            CreateParameter("@UserId", userId));
    }

    public async Task<Guid> AddUserRestrictionAsync(Guid userId, AddUserDietaryRestrictionRequest request, Guid? createdBy = null)
    {
        const string sql = @"
            INSERT INTO UserDietaryRestriction (
                Id, UserId, DietaryRestrictionId, Strictness, Notes,
                StartDate, EndDate, CreatedBy, CreatedAt
            )
            VALUES (
                @Id, @UserId, @DietaryRestrictionId, @Strictness, @Notes,
                @StartDate, @EndDate, @CreatedBy, GETUTCDATE()
            )";

        var userRestrictionId = Guid.NewGuid();

        await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", userRestrictionId),
            CreateParameter("@UserId", userId),
            CreateParameter("@DietaryRestrictionId", request.DietaryRestrictionId),
            CreateParameter("@Strictness", request.Strictness),
            CreateParameter("@Notes", request.Notes),
            CreateParameter("@StartDate", request.StartDate),
            CreateParameter("@EndDate", request.EndDate),
            CreateParameter("@CreatedBy", createdBy));

        return userRestrictionId;
    }

    public async Task<bool> UpdateUserRestrictionAsync(Guid userRestrictionId, UpdateUserDietaryRestrictionRequest request, Guid? updatedBy = null)
    {
        const string sql = @"
            UPDATE UserDietaryRestriction
            SET Strictness = @Strictness,
                Notes = @Notes,
                StartDate = @StartDate,
                EndDate = @EndDate,
                UpdatedBy = @UpdatedBy,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND IsDeleted = 0";

        var rowsAffected = await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", userRestrictionId),
            CreateParameter("@Strictness", request.Strictness),
            CreateParameter("@Notes", request.Notes),
            CreateParameter("@StartDate", request.StartDate),
            CreateParameter("@EndDate", request.EndDate),
            CreateParameter("@UpdatedBy", updatedBy));

        return rowsAffected > 0;
    }

    public async Task<bool> RemoveUserRestrictionAsync(Guid userRestrictionId, Guid? deletedBy = null)
    {
        const string sql = @"
            UPDATE UserDietaryRestriction
            SET IsDeleted = 1,
                DeletedAt = GETUTCDATE(),
                UpdatedBy = @DeletedBy,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND IsDeleted = 0";

        var rowsAffected = await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", userRestrictionId),
            CreateParameter("@DeletedBy", deletedBy));

        return rowsAffected > 0;
    }
}
