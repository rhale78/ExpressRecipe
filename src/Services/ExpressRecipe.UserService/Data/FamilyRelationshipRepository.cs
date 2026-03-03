using ExpressRecipe.Data.Common;
using ExpressRecipe.Shared.DTOs.User;

namespace ExpressRecipe.UserService.Data;

public interface IFamilyRelationshipRepository
{
    Task<List<FamilyRelationshipDto>> GetByFamilyMemberIdAsync(Guid familyMemberId);
    Task<FamilyRelationshipDto?> GetByIdAsync(Guid id);
    Task<Guid> CreateAsync(Guid familyMemberId1, CreateFamilyRelationshipRequest request, Guid? createdBy = null);
    Task<bool> DeleteAsync(Guid id, Guid? deletedBy = null);
}

public class FamilyRelationshipRepository : SqlHelper, IFamilyRelationshipRepository
{
    public FamilyRelationshipRepository(string connectionString) : base(connectionString)
    {
    }

    public async Task<List<FamilyRelationshipDto>> GetByFamilyMemberIdAsync(Guid familyMemberId)
    {
        const string sql = @"
            SELECT 
                fr.Id, 
                fr.FamilyMemberId1 AS FamilyMemberId,
                fr.FamilyMemberId2 AS RelatedMemberId,
                fm.Name AS RelatedMemberName,
                fr.RelationshipType,
                fr.Notes
            FROM FamilyRelationship fr
            INNER JOIN FamilyMember fm ON fr.FamilyMemberId2 = fm.Id
            WHERE fr.FamilyMemberId1 = @FamilyMemberId AND fr.IsDeleted = 0 AND fm.IsDeleted = 0
            UNION
            SELECT 
                fr.Id,
                fr.FamilyMemberId2 AS FamilyMemberId,
                fr.FamilyMemberId1 AS RelatedMemberId,
                fm.Name AS RelatedMemberName,
                fr.RelationshipType,
                fr.Notes
            FROM FamilyRelationship fr
            INNER JOIN FamilyMember fm ON fr.FamilyMemberId1 = fm.Id
            WHERE fr.FamilyMemberId2 = @FamilyMemberId AND fr.IsDeleted = 0 AND fm.IsDeleted = 0";

        return await ExecuteReaderAsync(
            sql,
            reader => new FamilyRelationshipDto
            {
                Id = GetGuid(reader, "Id"),
                FamilyMemberId = GetGuid(reader, "FamilyMemberId"),
                RelatedMemberId = GetGuid(reader, "RelatedMemberId"),
                RelatedMemberName = GetString(reader, "RelatedMemberName") ?? string.Empty,
                RelationshipType = GetString(reader, "RelationshipType") ?? string.Empty,
                Notes = GetString(reader, "Notes")
            },
            CreateParameter("@FamilyMemberId", familyMemberId));
    }

    public async Task<FamilyRelationshipDto?> GetByIdAsync(Guid id)
    {
        const string sql = @"
            SELECT 
                fr.Id, 
                fr.FamilyMemberId1 AS FamilyMemberId,
                fr.FamilyMemberId2 AS RelatedMemberId,
                fm.Name AS RelatedMemberName,
                fr.RelationshipType,
                fr.Notes
            FROM FamilyRelationship fr
            INNER JOIN FamilyMember fm ON fr.FamilyMemberId2 = fm.Id
            WHERE fr.Id = @Id AND fr.IsDeleted = 0 AND fm.IsDeleted = 0";

        var results = await ExecuteReaderAsync(
            sql,
            reader => new FamilyRelationshipDto
            {
                Id = GetGuid(reader, "Id"),
                FamilyMemberId = GetGuid(reader, "FamilyMemberId"),
                RelatedMemberId = GetGuid(reader, "RelatedMemberId"),
                RelatedMemberName = GetString(reader, "RelatedMemberName") ?? string.Empty,
                RelationshipType = GetString(reader, "RelationshipType") ?? string.Empty,
                Notes = GetString(reader, "Notes")
            },
            CreateParameter("@Id", id));

        return results.FirstOrDefault();
    }

    public async Task<Guid> CreateAsync(Guid familyMemberId1, CreateFamilyRelationshipRequest request, Guid? createdBy = null)
    {
        const string sql = @"
            INSERT INTO FamilyRelationship (
                Id, FamilyMemberId1, FamilyMemberId2, RelationshipType, Notes,
                CreatedBy, CreatedAt
            )
            VALUES (
                @Id, @FamilyMemberId1, @FamilyMemberId2, @RelationshipType, @Notes,
                @CreatedBy, GETUTCDATE()
            )";

        var relationshipId = Guid.NewGuid();

        await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", relationshipId),
            CreateParameter("@FamilyMemberId1", familyMemberId1),
            CreateParameter("@FamilyMemberId2", request.FamilyMemberId2),
            CreateParameter("@RelationshipType", request.RelationshipType),
            CreateParameter("@Notes", request.Notes),
            CreateParameter("@CreatedBy", createdBy));

        return relationshipId;
    }

    public async Task<bool> DeleteAsync(Guid id, Guid? deletedBy = null)
    {
        const string sql = @"
            UPDATE FamilyRelationship
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
