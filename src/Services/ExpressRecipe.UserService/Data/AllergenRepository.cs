using ExpressRecipe.Data.Common;
using ExpressRecipe.Shared.DTOs.User;
using Microsoft.Data.SqlClient;

namespace ExpressRecipe.UserService.Data;

public interface IAllergenRepository
{
    Task<List<AllergenDto>> GetAllAllergensAsync();
    Task<AllergenDto?> GetByIdAsync(Guid id);
    Task<List<AllergenDto>> SearchByNameAsync(string searchTerm);
    Task<List<UserAllergenDto>> GetUserAllergensAsync(Guid userId);
    Task<Guid> AddUserAllergenAsync(Guid userId, AddUserAllergenRequest request, Guid? createdBy = null);
    Task<bool> UpdateUserAllergenAsync(Guid userAllergenId, UpdateUserAllergenRequest request, Guid? updatedBy = null);
    Task<bool> RemoveUserAllergenAsync(Guid userAllergenId, Guid? deletedBy = null);
}

public class AllergenRepository : SqlHelper, IAllergenRepository
{
    public AllergenRepository(string connectionString) : base(connectionString)
    {
    }

    public async Task<List<AllergenDto>> GetAllAllergensAsync()
    {
        const string sql = @"
            SELECT Id, Name, AlternativeNames, Description, Category
            FROM Allergen
            WHERE IsDeleted = 0
            ORDER BY Name";

        return await ExecuteReaderAsync(
            sql,
            reader => new AllergenDto
            {
                Id = GetGuid(reader, "Id"),
                Name = GetString(reader, "Name") ?? string.Empty,
                AlternativeNames = GetString(reader, "AlternativeNames"),
                Description = GetString(reader, "Description"),
                Category = GetString(reader, "Category")
            });
    }

    public async Task<AllergenDto?> GetByIdAsync(Guid id)
    {
        const string sql = @"
            SELECT Id, Name, AlternativeNames, Description, Category
            FROM Allergen
            WHERE Id = @Id AND IsDeleted = 0";

        var results = await ExecuteReaderAsync(
            sql,
            reader => new AllergenDto
            {
                Id = GetGuid(reader, "Id"),
                Name = GetString(reader, "Name") ?? string.Empty,
                AlternativeNames = GetString(reader, "AlternativeNames"),
                Description = GetString(reader, "Description"),
                Category = GetString(reader, "Category")
            },
            CreateParameter("@Id", id));

        return results.FirstOrDefault();
    }

    public async Task<List<AllergenDto>> SearchByNameAsync(string searchTerm)
    {
        const string sql = @"
            SELECT Id, Name, AlternativeNames, Description, Category
            FROM Allergen
            WHERE (Name LIKE @SearchTerm OR AlternativeNames LIKE @SearchTerm)
                  AND IsDeleted = 0
            ORDER BY Name";

        return await ExecuteReaderAsync(
            sql,
            reader => new AllergenDto
            {
                Id = GetGuid(reader, "Id"),
                Name = GetString(reader, "Name") ?? string.Empty,
                AlternativeNames = GetString(reader, "AlternativeNames"),
                Description = GetString(reader, "Description"),
                Category = GetString(reader, "Category")
            },
            CreateParameter("@SearchTerm", $"%{searchTerm}%"));
    }

    public async Task<List<UserAllergenDto>> GetUserAllergensAsync(Guid userId)
    {
        const string sql = @"
            SELECT ua.Id, ua.UserId, ua.AllergenId, a.Name as AllergenName,
                   ua.Severity, ua.Notes, ua.DiagnosedDate, ua.VerifiedByDoctor
            FROM UserAllergen ua
            INNER JOIN Allergen a ON ua.AllergenId = a.Id
            WHERE ua.UserId = @UserId AND ua.IsDeleted = 0 AND a.IsDeleted = 0
            ORDER BY ua.Severity DESC, a.Name";

        return await ExecuteReaderAsync(
            sql,
            reader => new UserAllergenDto
            {
                Id = GetGuid(reader, "Id"),
                UserId = GetGuid(reader, "UserId"),
                AllergenId = GetGuid(reader, "AllergenId"),
                AllergenName = GetString(reader, "AllergenName") ?? string.Empty,
                Severity = GetString(reader, "Severity") ?? string.Empty,
                Notes = GetString(reader, "Notes"),
                DiagnosedDate = GetDateTime(reader, "DiagnosedDate"),
                VerifiedByDoctor = GetBoolean(reader, "VerifiedByDoctor")
            },
            CreateParameter("@UserId", userId));
    }

    public async Task<Guid> AddUserAllergenAsync(Guid userId, AddUserAllergenRequest request, Guid? createdBy = null)
    {
        const string sql = @"
            INSERT INTO UserAllergen (
                Id, UserId, AllergenId, Severity, Notes,
                DiagnosedDate, VerifiedByDoctor, CreatedBy, CreatedAt
            )
            VALUES (
                @Id, @UserId, @AllergenId, @Severity, @Notes,
                @DiagnosedDate, @VerifiedByDoctor, @CreatedBy, GETUTCDATE()
            )";

        var userAllergenId = Guid.NewGuid();

        await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", userAllergenId),
            CreateParameter("@UserId", userId),
            CreateParameter("@AllergenId", request.AllergenId),
            CreateParameter("@Severity", request.Severity),
            CreateParameter("@Notes", request.Notes),
            CreateParameter("@DiagnosedDate", request.DiagnosedDate),
            CreateParameter("@VerifiedByDoctor", request.VerifiedByDoctor),
            CreateParameter("@CreatedBy", createdBy));

        return userAllergenId;
    }

    public async Task<bool> UpdateUserAllergenAsync(Guid userAllergenId, UpdateUserAllergenRequest request, Guid? updatedBy = null)
    {
        const string sql = @"
            UPDATE UserAllergen
            SET Severity = @Severity,
                Notes = @Notes,
                DiagnosedDate = @DiagnosedDate,
                VerifiedByDoctor = @VerifiedByDoctor,
                UpdatedBy = @UpdatedBy,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND IsDeleted = 0";

        var rowsAffected = await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", userAllergenId),
            CreateParameter("@Severity", request.Severity),
            CreateParameter("@Notes", request.Notes),
            CreateParameter("@DiagnosedDate", request.DiagnosedDate),
            CreateParameter("@VerifiedByDoctor", request.VerifiedByDoctor),
            CreateParameter("@UpdatedBy", updatedBy));

        return rowsAffected > 0;
    }

    public async Task<bool> RemoveUserAllergenAsync(Guid userAllergenId, Guid? deletedBy = null)
    {
        const string sql = @"
            UPDATE UserAllergen
            SET IsDeleted = 1,
                DeletedAt = GETUTCDATE(),
                UpdatedBy = @DeletedBy,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND IsDeleted = 0";

        var rowsAffected = await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", userAllergenId),
            CreateParameter("@DeletedBy", deletedBy));

        return rowsAffected > 0;
    }
}
