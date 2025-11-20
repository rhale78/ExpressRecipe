using ExpressRecipe.Data.Common;
using ExpressRecipe.Shared.DTOs.User;

namespace ExpressRecipe.UserService.Data;

public interface IFamilyMemberRepository
{
    Task<List<FamilyMemberDto>> GetByPrimaryUserIdAsync(Guid primaryUserId);
    Task<FamilyMemberDto?> GetByIdAsync(Guid id);
    Task<Guid> CreateAsync(Guid primaryUserId, CreateFamilyMemberRequest request, Guid? createdBy = null);
    Task<bool> UpdateAsync(Guid id, UpdateFamilyMemberRequest request, Guid? updatedBy = null);
    Task<bool> DeleteAsync(Guid id, Guid? deletedBy = null);
}

public class FamilyMemberRepository : SqlHelper, IFamilyMemberRepository
{
    public FamilyMemberRepository(string connectionString) : base(connectionString)
    {
    }

    public async Task<List<FamilyMemberDto>> GetByPrimaryUserIdAsync(Guid primaryUserId)
    {
        const string sql = @"
            SELECT Id, PrimaryUserId, Name, Relationship, DateOfBirth, Notes
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
                Notes = GetString(reader, "Notes")
            },
            CreateParameter("@PrimaryUserId", primaryUserId));
    }

    public async Task<FamilyMemberDto?> GetByIdAsync(Guid id)
    {
        const string sql = @"
            SELECT Id, PrimaryUserId, Name, Relationship, DateOfBirth, Notes
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
                Notes = GetString(reader, "Notes")
            },
            CreateParameter("@Id", id));

        return results.FirstOrDefault();
    }

    public async Task<Guid> CreateAsync(Guid primaryUserId, CreateFamilyMemberRequest request, Guid? createdBy = null)
    {
        const string sql = @"
            INSERT INTO FamilyMember (
                Id, PrimaryUserId, Name, Relationship, DateOfBirth, Notes,
                CreatedBy, CreatedAt
            )
            VALUES (
                @Id, @PrimaryUserId, @Name, @Relationship, @DateOfBirth, @Notes,
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
}
