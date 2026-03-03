using ExpressRecipe.Data.Common;
using ExpressRecipe.Shared.DTOs.User;

namespace ExpressRecipe.UserService.Data;

public interface IFamilyMemberRepository
{
    Task<List<FamilyMemberDto>> GetByPrimaryUserIdAsync(Guid primaryUserId);
    Task<FamilyMemberDto?> GetByIdAsync(Guid id);
    Task<FamilyMemberDto?> GetByUserIdAsync(Guid userId);
    Task<Guid> CreateAsync(Guid primaryUserId, CreateFamilyMemberRequest request, Guid? createdBy = null);
    Task<Guid> CreateWithAccountAsync(Guid primaryUserId, CreateFamilyMemberWithAccountRequest request, Guid createdUserId, Guid? createdBy = null);
    Task<bool> UpdateAsync(Guid id, UpdateFamilyMemberRequest request, Guid? updatedBy = null);
    Task<bool> DeleteAsync(Guid id, Guid? deletedBy = null);
    Task<bool> DismissGuestAsync(Guid id, Guid? dismissedBy = null);
}

public class FamilyMemberRepository : SqlHelper, IFamilyMemberRepository
{
    public FamilyMemberRepository(string connectionString) : base(connectionString)
    {
    }

    public async Task<List<FamilyMemberDto>> GetByPrimaryUserIdAsync(Guid primaryUserId)
    {
        const string sql = @"
            SELECT Id, PrimaryUserId, Name, Relationship, DateOfBirth, Notes,
                   UserId, UserRole, HasUserAccount, IsGuest, LinkedUserId, Email
            FROM FamilyMember
            WHERE PrimaryUserId = @PrimaryUserId AND IsDeleted = 0
            ORDER BY Name";

        return await ExecuteReaderAsync(
            sql,
            reader => new FamilyMemberDto
            {
                Id = GetGuid(reader, "Id"),
                PrimaryUserId = GetGuid(reader, "PrimaryUserId"),
                Name = GetString(reader, "Name") ?? string.Empty,
                Relationship = GetString(reader, "Relationship"),
                DateOfBirth = GetDateTime(reader, "DateOfBirth"),
                Notes = GetString(reader, "Notes"),
                UserId = GetGuidNullable(reader, "UserId"),
                UserRole = GetString(reader, "UserRole") ?? "Member",
                HasUserAccount = GetBool(reader, "HasUserAccount") ?? false,
                IsGuest = GetBool(reader, "IsGuest") ?? false,
                LinkedUserId = GetGuidNullable(reader, "LinkedUserId"),
                Email = GetString(reader, "Email")
            },
            CreateParameter("@PrimaryUserId", primaryUserId));
    }

    public async Task<FamilyMemberDto?> GetByIdAsync(Guid id)
    {
        const string sql = @"
            SELECT Id, PrimaryUserId, Name, Relationship, DateOfBirth, Notes,
                   UserId, UserRole, HasUserAccount, IsGuest, LinkedUserId, Email
            FROM FamilyMember
            WHERE Id = @Id AND IsDeleted = 0";

        var results = await ExecuteReaderAsync(
            sql,
            reader => new FamilyMemberDto
            {
                Id = GetGuid(reader, "Id"),
                PrimaryUserId = GetGuid(reader, "PrimaryUserId"),
                Name = GetString(reader, "Name") ?? string.Empty,
                Relationship = GetString(reader, "Relationship"),
                DateOfBirth = GetDateTime(reader, "DateOfBirth"),
                Notes = GetString(reader, "Notes"),
                UserId = GetGuidNullable(reader, "UserId"),
                UserRole = GetString(reader, "UserRole") ?? "Member",
                HasUserAccount = GetBool(reader, "HasUserAccount") ?? false,
                IsGuest = GetBool(reader, "IsGuest") ?? false,
                LinkedUserId = GetGuidNullable(reader, "LinkedUserId"),
                Email = GetString(reader, "Email")
            },
            CreateParameter("@Id", id));

        return results.FirstOrDefault();
    }

    public async Task<FamilyMemberDto?> GetByUserIdAsync(Guid userId)
    {
        const string sql = @"
            SELECT Id, PrimaryUserId, Name, Relationship, DateOfBirth, Notes,
                   UserId, UserRole, HasUserAccount, IsGuest, LinkedUserId, Email
            FROM FamilyMember
            WHERE UserId = @UserId AND IsDeleted = 0";

        var results = await ExecuteReaderAsync(
            sql,
            reader => new FamilyMemberDto
            {
                Id = GetGuid(reader, "Id"),
                PrimaryUserId = GetGuid(reader, "PrimaryUserId"),
                Name = GetString(reader, "Name") ?? string.Empty,
                Relationship = GetString(reader, "Relationship"),
                DateOfBirth = GetDateTime(reader, "DateOfBirth"),
                Notes = GetString(reader, "Notes"),
                UserId = GetGuidNullable(reader, "UserId"),
                UserRole = GetString(reader, "UserRole") ?? "Member",
                HasUserAccount = GetBool(reader, "HasUserAccount") ?? false,
                IsGuest = GetBool(reader, "IsGuest") ?? false,
                LinkedUserId = GetGuidNullable(reader, "LinkedUserId"),
                Email = GetString(reader, "Email")
            },
            CreateParameter("@UserId", userId));

        return results.FirstOrDefault();
    }

    public async Task<Guid> CreateAsync(Guid primaryUserId, CreateFamilyMemberRequest request, Guid? createdBy = null)
    {
        const string sql = @"
            INSERT INTO FamilyMember (
                Id, PrimaryUserId, Name, Relationship, DateOfBirth, Notes,
                UserRole, IsGuest, HasUserAccount,
                CreatedBy, CreatedAt
            )
            VALUES (
                @Id, @PrimaryUserId, @Name, @Relationship, @DateOfBirth, @Notes,
                @UserRole, @IsGuest, 0,
                @CreatedBy, GETUTCDATE()
            )";

        var familyMemberId = Guid.NewGuid();

        await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", familyMemberId),
            CreateParameter("@PrimaryUserId", primaryUserId),
            CreateParameter("@Name", request.Name),
            CreateParameter("@Relationship", request.Relationship),
            CreateParameter("@DateOfBirth", request.DateOfBirth),
            CreateParameter("@Notes", request.Notes),
            CreateParameter("@UserRole", request.UserRole),
            CreateParameter("@IsGuest", request.IsGuest),
            CreateParameter("@CreatedBy", createdBy));

        return familyMemberId;
    }

    public async Task<Guid> CreateWithAccountAsync(Guid primaryUserId, CreateFamilyMemberWithAccountRequest request, Guid createdUserId, Guid? createdBy = null)
    {
        const string sql = @"
            INSERT INTO FamilyMember (
                Id, PrimaryUserId, Name, Relationship, DateOfBirth, Notes,
                UserId, Email, UserRole, IsGuest, HasUserAccount,
                CreatedBy, CreatedAt
            )
            VALUES (
                @Id, @PrimaryUserId, @Name, @Relationship, @DateOfBirth, @Notes,
                @UserId, @Email, @UserRole, @IsGuest, 1,
                @CreatedBy, GETUTCDATE()
            )";

        var familyMemberId = Guid.NewGuid();

        await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", familyMemberId),
            CreateParameter("@PrimaryUserId", primaryUserId),
            CreateParameter("@Name", request.Name),
            CreateParameter("@Relationship", request.Relationship),
            CreateParameter("@DateOfBirth", request.DateOfBirth),
            CreateParameter("@Notes", request.Notes),
            CreateParameter("@UserId", createdUserId),
            CreateParameter("@Email", request.Email),
            CreateParameter("@UserRole", request.UserRole),
            CreateParameter("@IsGuest", request.IsGuest),
            CreateParameter("@CreatedBy", createdBy));

        return familyMemberId;
    }

    public async Task<bool> UpdateAsync(Guid id, UpdateFamilyMemberRequest request, Guid? updatedBy = null)
    {
        const string sql = @"
            UPDATE FamilyMember
            SET Name = @Name,
                Relationship = @Relationship,
                DateOfBirth = @DateOfBirth,
                Notes = @Notes,
                UserRole = @UserRole,
                IsGuest = @IsGuest,
                UpdatedBy = @UpdatedBy,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND IsDeleted = 0";

        var rowsAffected = await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", id),
            CreateParameter("@Name", request.Name),
            CreateParameter("@Relationship", request.Relationship),
            CreateParameter("@DateOfBirth", request.DateOfBirth),
            CreateParameter("@Notes", request.Notes),
            CreateParameter("@UserRole", request.UserRole),
            CreateParameter("@IsGuest", request.IsGuest),
            CreateParameter("@UpdatedBy", updatedBy));

        return rowsAffected > 0;
    }

    public async Task<bool> DeleteAsync(Guid id, Guid? deletedBy = null)
    {
        const string sql = @"
            UPDATE FamilyMember
            SET IsDeleted = 1,
                DeletedAt = GETUTCDATE(),
                UpdatedBy = @DeletedBy,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND IsDeleted = 0";

        var rowsAffected = await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", id),
            CreateParameter("@DeletedBy", deletedBy));

        return rowsAffected > 0;
    }

    public async Task<bool> DismissGuestAsync(Guid id, Guid? dismissedBy = null)
    {
        const string sql = @"
            UPDATE FamilyMember
            SET IsDeleted = 1,
                DeletedAt = GETUTCDATE(),
                UpdatedBy = @DismissedBy,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND IsGuest = 1 AND IsDeleted = 0";

        var rowsAffected = await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", id),
            CreateParameter("@DismissedBy", dismissedBy));

        return rowsAffected > 0;
    }
}
