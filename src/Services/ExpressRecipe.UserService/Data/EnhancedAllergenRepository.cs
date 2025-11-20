using ExpressRecipe.Data.Common;
using ExpressRecipe.Shared.DTOs.User;
using Microsoft.Data.SqlClient;

namespace ExpressRecipe.UserService.Data;

public interface IEnhancedAllergenRepository
{
    // Allergen Reaction Types
    Task<List<AllergenReactionTypeDto>> GetReactionTypesAsync(bool activeOnly = true);
    Task<AllergenReactionTypeDto?> GetReactionTypeByIdAsync(Guid id);

    // User Allergens (Enhanced)
    Task<List<UserAllergenDto>> GetUserAllergensAsync(Guid userId, bool includeReactions = true);
    Task<UserAllergenDto?> GetUserAllergenByIdAsync(Guid id, bool includeReactions = true);
    Task<Guid> CreateUserAllergenAsync(Guid userId, AddUserAllergenRequest request);
    Task<bool> UpdateUserAllergenAsync(Guid id, Guid userId, UpdateUserAllergenRequest request);
    Task<bool> DeleteUserAllergenAsync(Guid id, Guid userId);

    // User Ingredient Allergies
    Task<List<UserIngredientAllergyDto>> GetUserIngredientAllergiesAsync(Guid userId, bool includeReactions = true);
    Task<UserIngredientAllergyDto?> GetIngredientAllergyByIdAsync(Guid id, bool includeReactions = true);
    Task<Guid> CreateIngredientAllergyAsync(Guid userId, CreateUserIngredientAllergyRequest request);
    Task<bool> UpdateIngredientAllergyAsync(Guid id, Guid userId, UpdateUserIngredientAllergyRequest request);
    Task<bool> DeleteIngredientAllergyAsync(Guid id, Guid userId);

    // Allergy Incidents
    Task<List<AllergyIncidentDto>> GetUserIncidentsAsync(Guid userId, int limit = 50);
    Task<AllergyIncidentDto?> GetIncidentByIdAsync(Guid id);
    Task<Guid> CreateIncidentAsync(Guid userId, CreateAllergyIncidentRequest request);
    Task<UserAllergenSummaryDto> GetAllergenSummaryAsync(Guid userId);
}

public class EnhancedAllergenRepository : SqlHelper, IEnhancedAllergenRepository
{
    public EnhancedAllergenRepository(string connectionString) : base(connectionString)
    {
    }

    // Allergen Reaction Types

    public async Task<List<AllergenReactionTypeDto>> GetReactionTypesAsync(bool activeOnly = true)
    {
        var sql = @"
            SELECT Id, Name, Description, Severity, RequiresMedicalAttention, IsCommon
            FROM AllergenReactionType";

        if (activeOnly)
        {
            sql += " WHERE IsActive = 1";
        }

        sql += " ORDER BY Severity DESC, Name";

        return await ExecuteReaderAsync(
            sql,
            reader => new AllergenReactionTypeDto
            {
                Id = GetGuid(reader, "Id"),
                Name = GetString(reader, "Name") ?? string.Empty,
                Description = GetString(reader, "Description"),
                Severity = GetString(reader, "Severity") ?? string.Empty,
                RequiresMedicalAttention = GetBoolean(reader, "RequiresMedicalAttention"),
                IsCommon = GetBoolean(reader, "IsCommon")
            });
    }

    public async Task<AllergenReactionTypeDto?> GetReactionTypeByIdAsync(Guid id)
    {
        const string sql = @"
            SELECT Id, Name, Description, Severity, RequiresMedicalAttention, IsCommon
            FROM AllergenReactionType
            WHERE Id = @Id";

        var results = await ExecuteReaderAsync(
            sql,
            reader => new AllergenReactionTypeDto
            {
                Id = GetGuid(reader, "Id"),
                Name = GetString(reader, "Name") ?? string.Empty,
                Description = GetString(reader, "Description"),
                Severity = GetString(reader, "Severity") ?? string.Empty,
                RequiresMedicalAttention = GetBoolean(reader, "RequiresMedicalAttention"),
                IsCommon = GetBoolean(reader, "IsCommon")
            },
            new SqlParameter("@Id", id));

        return results.FirstOrDefault();
    }

    // User Allergens

    public async Task<List<UserAllergenDto>> GetUserAllergensAsync(Guid userId, bool includeReactions = true)
    {
        const string sql = @"
            SELECT ua.Id, ua.UserId, ua.AllergenId, a.Name AS AllergenName,
                   ua.SeverityLevel, ua.RequiresEpiPen, ua.OnsetTimeMinutes,
                   ua.LastReactionDate, ua.DiagnosedBy, ua.DiagnosisDate,
                   ua.Notes, ua.CreatedAt, ua.UpdatedAt
            FROM UserAllergen ua
            INNER JOIN Allergen a ON ua.AllergenId = a.Id
            WHERE ua.UserId = @UserId
            ORDER BY ua.SeverityLevel DESC, a.Name";

        var allergens = await ExecuteReaderAsync(
            sql,
            reader => new UserAllergenDto
            {
                Id = GetGuid(reader, "Id"),
                UserId = GetGuid(reader, "UserId"),
                AllergenId = GetGuid(reader, "AllergenId"),
                AllergenName = GetString(reader, "AllergenName") ?? string.Empty,
                SeverityLevel = GetString(reader, "SeverityLevel") ?? string.Empty,
                RequiresEpiPen = GetBoolean(reader, "RequiresEpiPen"),
                OnsetTimeMinutes = GetIntNullable(reader, "OnsetTimeMinutes"),
                LastReactionDate = GetDateTime(reader, "LastReactionDate"),
                DiagnosedBy = GetString(reader, "DiagnosedBy"),
                DiagnosisDate = GetDateTime(reader, "DiagnosisDate"),
                Notes = GetString(reader, "Notes"),
                CreatedAt = GetDateTime(reader, "CreatedAt") ?? DateTime.UtcNow,
                UpdatedAt = GetDateTime(reader, "UpdatedAt")
            },
            new SqlParameter("@UserId", userId));

        if (includeReactions)
        {
            foreach (var allergen in allergens)
            {
                allergen.ReactionTypes = await GetUserAllergenReactionsAsync(allergen.Id);
            }
        }

        return allergens;
    }

    public async Task<UserAllergenDto?> GetUserAllergenByIdAsync(Guid id, bool includeReactions = true)
    {
        const string sql = @"
            SELECT ua.Id, ua.UserId, ua.AllergenId, a.Name AS AllergenName,
                   ua.SeverityLevel, ua.RequiresEpiPen, ua.OnsetTimeMinutes,
                   ua.LastReactionDate, ua.DiagnosedBy, ua.DiagnosisDate,
                   ua.Notes, ua.CreatedAt, ua.UpdatedAt
            FROM UserAllergen ua
            INNER JOIN Allergen a ON ua.AllergenId = a.Id
            WHERE ua.Id = @Id";

        var results = await ExecuteReaderAsync(
            sql,
            reader => new UserAllergenDto
            {
                Id = GetGuid(reader, "Id"),
                UserId = GetGuid(reader, "UserId"),
                AllergenId = GetGuid(reader, "AllergenId"),
                AllergenName = GetString(reader, "AllergenName") ?? string.Empty,
                SeverityLevel = GetString(reader, "SeverityLevel") ?? string.Empty,
                RequiresEpiPen = GetBoolean(reader, "RequiresEpiPen"),
                OnsetTimeMinutes = GetIntNullable(reader, "OnsetTimeMinutes"),
                LastReactionDate = GetDateTime(reader, "LastReactionDate"),
                DiagnosedBy = GetString(reader, "DiagnosedBy"),
                DiagnosisDate = GetDateTime(reader, "DiagnosisDate"),
                Notes = GetString(reader, "Notes"),
                CreatedAt = GetDateTime(reader, "CreatedAt") ?? DateTime.UtcNow,
                UpdatedAt = GetDateTime(reader, "UpdatedAt")
            },
            new SqlParameter("@Id", id));

        var allergen = results.FirstOrDefault();

        if (allergen != null && includeReactions)
        {
            allergen.ReactionTypes = await GetUserAllergenReactionsAsync(allergen.Id);
        }

        return allergen;
    }

    public async Task<Guid> CreateUserAllergenAsync(Guid userId, AddUserAllergenRequest request)
    {
        var id = Guid.NewGuid();

        const string sql = @"
            INSERT INTO UserAllergen (Id, UserId, AllergenId, SeverityLevel, RequiresEpiPen,
                                     OnsetTimeMinutes, LastReactionDate, DiagnosedBy, DiagnosisDate,
                                     Notes, CreatedAt)
            VALUES (@Id, @UserId, @AllergenId, @SeverityLevel, @RequiresEpiPen,
                    @OnsetTimeMinutes, @LastReactionDate, @DiagnosedBy, @DiagnosisDate,
                    @Notes, GETUTCDATE())";

        await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", id),
            new SqlParameter("@UserId", userId),
            new SqlParameter("@AllergenId", request.AllergenId),
            new SqlParameter("@SeverityLevel", request.SeverityLevel),
            new SqlParameter("@RequiresEpiPen", request.RequiresEpiPen),
            new SqlParameter("@OnsetTimeMinutes", (object?)request.OnsetTimeMinutes ?? DBNull.Value),
            new SqlParameter("@LastReactionDate", (object?)request.LastReactionDate ?? DBNull.Value),
            new SqlParameter("@DiagnosedBy", (object?)request.DiagnosedBy ?? DBNull.Value),
            new SqlParameter("@DiagnosisDate", (object?)request.DiagnosisDate ?? DBNull.Value),
            new SqlParameter("@Notes", (object?)request.Notes ?? DBNull.Value));

        // Add reaction types
        if (request.ReactionTypeIds != null && request.ReactionTypeIds.Any())
        {
            await AddUserAllergenReactionsAsync(id, request.ReactionTypeIds);
        }

        return id;
    }

    public async Task<bool> UpdateUserAllergenAsync(Guid id, Guid userId, UpdateUserAllergenRequest request)
    {
        const string sql = @"
            UPDATE UserAllergen
            SET SeverityLevel = @SeverityLevel,
                RequiresEpiPen = @RequiresEpiPen,
                OnsetTimeMinutes = @OnsetTimeMinutes,
                LastReactionDate = @LastReactionDate,
                DiagnosedBy = @DiagnosedBy,
                DiagnosisDate = @DiagnosisDate,
                Notes = @Notes,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND UserId = @UserId";

        var rowsAffected = await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", id),
            new SqlParameter("@UserId", userId),
            new SqlParameter("@SeverityLevel", request.SeverityLevel),
            new SqlParameter("@RequiresEpiPen", request.RequiresEpiPen),
            new SqlParameter("@OnsetTimeMinutes", (object?)request.OnsetTimeMinutes ?? DBNull.Value),
            new SqlParameter("@LastReactionDate", (object?)request.LastReactionDate ?? DBNull.Value),
            new SqlParameter("@DiagnosedBy", (object?)request.DiagnosedBy ?? DBNull.Value),
            new SqlParameter("@DiagnosisDate", (object?)request.DiagnosisDate ?? DBNull.Value),
            new SqlParameter("@Notes", (object?)request.Notes ?? DBNull.Value));

        // Update reaction types if provided
        if (rowsAffected > 0 && request.ReactionTypeIds != null)
        {
            await DeleteUserAllergenReactionsAsync(id);
            await AddUserAllergenReactionsAsync(id, request.ReactionTypeIds);
        }

        return rowsAffected > 0;
    }

    public async Task<bool> DeleteUserAllergenAsync(Guid id, Guid userId)
    {
        const string sql = @"
            DELETE FROM UserAllergen
            WHERE Id = @Id AND UserId = @UserId";

        var rowsAffected = await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", id),
            new SqlParameter("@UserId", userId));

        return rowsAffected > 0;
    }

    // User Ingredient Allergies

    public async Task<List<UserIngredientAllergyDto>> GetUserIngredientAllergiesAsync(Guid userId, bool includeReactions = true)
    {
        const string sql = @"
            SELECT uia.Id, uia.UserId, uia.IngredientId, uia.BaseIngredientId, uia.IngredientName,
                   uia.SeverityLevel, uia.RequiresEpiPen, uia.OnsetTimeMinutes,
                   uia.LastReactionDate, uia.DiagnosedBy, uia.DiagnosisDate,
                   uia.Notes, uia.CreatedAt, uia.UpdatedAt
            FROM UserIngredientAllergy uia
            WHERE uia.UserId = @UserId
            ORDER BY uia.SeverityLevel DESC, uia.IngredientName";

        var allergies = await ExecuteReaderAsync(
            sql,
            reader => new UserIngredientAllergyDto
            {
                Id = GetGuid(reader, "Id"),
                UserId = GetGuid(reader, "UserId"),
                IngredientId = GetGuidNullable(reader, "IngredientId"),
                BaseIngredientId = GetGuidNullable(reader, "BaseIngredientId"),
                IngredientName = GetString(reader, "IngredientName"),
                SeverityLevel = GetString(reader, "SeverityLevel") ?? string.Empty,
                RequiresEpiPen = GetBoolean(reader, "RequiresEpiPen"),
                OnsetTimeMinutes = GetIntNullable(reader, "OnsetTimeMinutes"),
                LastReactionDate = GetDateTime(reader, "LastReactionDate"),
                DiagnosedBy = GetString(reader, "DiagnosedBy"),
                DiagnosisDate = GetDateTime(reader, "DiagnosisDate"),
                Notes = GetString(reader, "Notes"),
                CreatedAt = GetDateTime(reader, "CreatedAt") ?? DateTime.UtcNow,
                UpdatedAt = GetDateTime(reader, "UpdatedAt")
            },
            new SqlParameter("@UserId", userId));

        if (includeReactions)
        {
            foreach (var allergy in allergies)
            {
                allergy.ReactionTypes = await GetIngredientAllergyReactionsAsync(allergy.Id);
            }
        }

        return allergies;
    }

    public async Task<UserIngredientAllergyDto?> GetIngredientAllergyByIdAsync(Guid id, bool includeReactions = true)
    {
        const string sql = @"
            SELECT uia.Id, uia.UserId, uia.IngredientId, uia.BaseIngredientId, uia.IngredientName,
                   uia.SeverityLevel, uia.RequiresEpiPen, uia.OnsetTimeMinutes,
                   uia.LastReactionDate, uia.DiagnosedBy, uia.DiagnosisDate,
                   uia.Notes, uia.CreatedAt, uia.UpdatedAt
            FROM UserIngredientAllergy uia
            WHERE uia.Id = @Id";

        var results = await ExecuteReaderAsync(
            sql,
            reader => new UserIngredientAllergyDto
            {
                Id = GetGuid(reader, "Id"),
                UserId = GetGuid(reader, "UserId"),
                IngredientId = GetGuidNullable(reader, "IngredientId"),
                BaseIngredientId = GetGuidNullable(reader, "BaseIngredientId"),
                IngredientName = GetString(reader, "IngredientName"),
                SeverityLevel = GetString(reader, "SeverityLevel") ?? string.Empty,
                RequiresEpiPen = GetBoolean(reader, "RequiresEpiPen"),
                OnsetTimeMinutes = GetIntNullable(reader, "OnsetTimeMinutes"),
                LastReactionDate = GetDateTime(reader, "LastReactionDate"),
                DiagnosedBy = GetString(reader, "DiagnosedBy"),
                DiagnosisDate = GetDateTime(reader, "DiagnosisDate"),
                Notes = GetString(reader, "Notes"),
                CreatedAt = GetDateTime(reader, "CreatedAt") ?? DateTime.UtcNow,
                UpdatedAt = GetDateTime(reader, "UpdatedAt")
            },
            new SqlParameter("@Id", id));

        var allergy = results.FirstOrDefault();

        if (allergy != null && includeReactions)
        {
            allergy.ReactionTypes = await GetIngredientAllergyReactionsAsync(allergy.Id);
        }

        return allergy;
    }

    public async Task<Guid> CreateIngredientAllergyAsync(Guid userId, CreateUserIngredientAllergyRequest request)
    {
        var id = Guid.NewGuid();

        const string sql = @"
            INSERT INTO UserIngredientAllergy (Id, UserId, IngredientId, BaseIngredientId, IngredientName,
                                              SeverityLevel, RequiresEpiPen, OnsetTimeMinutes,
                                              LastReactionDate, DiagnosedBy, DiagnosisDate,
                                              Notes, CreatedAt)
            VALUES (@Id, @UserId, @IngredientId, @BaseIngredientId, @IngredientName,
                    @SeverityLevel, @RequiresEpiPen, @OnsetTimeMinutes,
                    @LastReactionDate, @DiagnosedBy, @DiagnosisDate,
                    @Notes, GETUTCDATE())";

        await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", id),
            new SqlParameter("@UserId", userId),
            new SqlParameter("@IngredientId", (object?)request.IngredientId ?? DBNull.Value),
            new SqlParameter("@BaseIngredientId", (object?)request.BaseIngredientId ?? DBNull.Value),
            new SqlParameter("@IngredientName", (object?)request.IngredientName ?? DBNull.Value),
            new SqlParameter("@SeverityLevel", request.SeverityLevel),
            new SqlParameter("@RequiresEpiPen", request.RequiresEpiPen),
            new SqlParameter("@OnsetTimeMinutes", (object?)request.OnsetTimeMinutes ?? DBNull.Value),
            new SqlParameter("@LastReactionDate", (object?)request.LastReactionDate ?? DBNull.Value),
            new SqlParameter("@DiagnosedBy", (object?)request.DiagnosedBy ?? DBNull.Value),
            new SqlParameter("@DiagnosisDate", (object?)request.DiagnosisDate ?? DBNull.Value),
            new SqlParameter("@Notes", (object?)request.Notes ?? DBNull.Value));

        // Add reaction types
        if (request.ReactionTypeIds != null && request.ReactionTypeIds.Any())
        {
            await AddIngredientAllergyReactionsAsync(id, request.ReactionTypeIds);
        }

        return id;
    }

    public async Task<bool> UpdateIngredientAllergyAsync(Guid id, Guid userId, UpdateUserIngredientAllergyRequest request)
    {
        const string sql = @"
            UPDATE UserIngredientAllergy
            SET SeverityLevel = @SeverityLevel,
                RequiresEpiPen = @RequiresEpiPen,
                OnsetTimeMinutes = @OnsetTimeMinutes,
                LastReactionDate = @LastReactionDate,
                DiagnosedBy = @DiagnosedBy,
                DiagnosisDate = @DiagnosisDate,
                Notes = @Notes,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND UserId = @UserId";

        var rowsAffected = await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", id),
            new SqlParameter("@UserId", userId),
            new SqlParameter("@SeverityLevel", request.SeverityLevel),
            new SqlParameter("@RequiresEpiPen", request.RequiresEpiPen),
            new SqlParameter("@OnsetTimeMinutes", (object?)request.OnsetTimeMinutes ?? DBNull.Value),
            new SqlParameter("@LastReactionDate", (object?)request.LastReactionDate ?? DBNull.Value),
            new SqlParameter("@DiagnosedBy", (object?)request.DiagnosedBy ?? DBNull.Value),
            new SqlParameter("@DiagnosisDate", (object?)request.DiagnosisDate ?? DBNull.Value),
            new SqlParameter("@Notes", (object?)request.Notes ?? DBNull.Value));

        if (rowsAffected > 0 && request.ReactionTypeIds != null)
        {
            await DeleteIngredientAllergyReactionsAsync(id);
            await AddIngredientAllergyReactionsAsync(id, request.ReactionTypeIds);
        }

        return rowsAffected > 0;
    }

    public async Task<bool> DeleteIngredientAllergyAsync(Guid id, Guid userId)
    {
        const string sql = @"
            DELETE FROM UserIngredientAllergy
            WHERE Id = @Id AND UserId = @UserId";

        var rowsAffected = await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", id),
            new SqlParameter("@UserId", userId));

        return rowsAffected > 0;
    }

    // Allergy Incidents

    public async Task<List<AllergyIncidentDto>> GetUserIncidentsAsync(Guid userId, int limit = 50)
    {
        const string sql = @"
            SELECT TOP (@Limit)
                   ai.Id, ai.UserId, ai.UserAllergenId, ai.UserIngredientAllergyId,
                   ai.IncidentDate, ai.TriggerSource, ai.TriggerProductId,
                   ai.TriggerRecipeId, ai.TriggerMenuItemId, ai.Symptoms,
                   ai.SeverityLevel, ai.EpiPenUsed, ai.HospitalVisit,
                   ai.Treatment, ai.Notes, ai.CreatedAt
            FROM AllergyIncident ai
            WHERE ai.UserId = @UserId
            ORDER BY ai.IncidentDate DESC";

        var incidents = await ExecuteReaderAsync(
            sql,
            reader => new AllergyIncidentDto
            {
                Id = GetGuid(reader, "Id"),
                UserId = GetGuid(reader, "UserId"),
                UserAllergenId = GetGuidNullable(reader, "UserAllergenId"),
                UserIngredientAllergyId = GetGuidNullable(reader, "UserIngredientAllergyId"),
                IncidentDate = GetDateTime(reader, "IncidentDate") ?? DateTime.UtcNow,
                TriggerSource = GetString(reader, "TriggerSource"),
                TriggerProductId = GetGuidNullable(reader, "TriggerProductId"),
                TriggerRecipeId = GetGuidNullable(reader, "TriggerRecipeId"),
                TriggerMenuItemId = GetGuidNullable(reader, "TriggerMenuItemId"),
                Symptoms = GetString(reader, "Symptoms"),
                SeverityLevel = GetString(reader, "SeverityLevel") ?? string.Empty,
                EpiPenUsed = GetBoolean(reader, "EpiPenUsed"),
                HospitalVisit = GetBoolean(reader, "HospitalVisit"),
                Treatment = GetString(reader, "Treatment"),
                Notes = GetString(reader, "Notes"),
                CreatedAt = GetDateTime(reader, "CreatedAt") ?? DateTime.UtcNow
            },
            new SqlParameter("@UserId", userId),
            new SqlParameter("@Limit", limit));

        foreach (var incident in incidents)
        {
            incident.ReactionTypes = await GetIncidentReactionsAsync(incident.Id);
        }

        return incidents;
    }

    public async Task<AllergyIncidentDto?> GetIncidentByIdAsync(Guid id)
    {
        const string sql = @"
            SELECT ai.Id, ai.UserId, ai.UserAllergenId, ai.UserIngredientAllergyId,
                   ai.IncidentDate, ai.TriggerSource, ai.TriggerProductId,
                   ai.TriggerRecipeId, ai.TriggerMenuItemId, ai.Symptoms,
                   ai.SeverityLevel, ai.EpiPenUsed, ai.HospitalVisit,
                   ai.Treatment, ai.Notes, ai.CreatedAt
            FROM AllergyIncident ai
            WHERE ai.Id = @Id";

        var results = await ExecuteReaderAsync(
            sql,
            reader => new AllergyIncidentDto
            {
                Id = GetGuid(reader, "Id"),
                UserId = GetGuid(reader, "UserId"),
                UserAllergenId = GetGuidNullable(reader, "UserAllergenId"),
                UserIngredientAllergyId = GetGuidNullable(reader, "UserIngredientAllergyId"),
                IncidentDate = GetDateTime(reader, "IncidentDate") ?? DateTime.UtcNow,
                TriggerSource = GetString(reader, "TriggerSource"),
                TriggerProductId = GetGuidNullable(reader, "TriggerProductId"),
                TriggerRecipeId = GetGuidNullable(reader, "TriggerRecipeId"),
                TriggerMenuItemId = GetGuidNullable(reader, "TriggerMenuItemId"),
                Symptoms = GetString(reader, "Symptoms"),
                SeverityLevel = GetString(reader, "SeverityLevel") ?? string.Empty,
                EpiPenUsed = GetBoolean(reader, "EpiPenUsed"),
                HospitalVisit = GetBoolean(reader, "HospitalVisit"),
                Treatment = GetString(reader, "Treatment"),
                Notes = GetString(reader, "Notes"),
                CreatedAt = GetDateTime(reader, "CreatedAt") ?? DateTime.UtcNow
            },
            new SqlParameter("@Id", id));

        var incident = results.FirstOrDefault();

        if (incident != null)
        {
            incident.ReactionTypes = await GetIncidentReactionsAsync(incident.Id);
        }

        return incident;
    }

    public async Task<Guid> CreateIncidentAsync(Guid userId, CreateAllergyIncidentRequest request)
    {
        var id = Guid.NewGuid();

        const string sql = @"
            INSERT INTO AllergyIncident (Id, UserId, UserAllergenId, UserIngredientAllergyId,
                                        IncidentDate, TriggerSource, TriggerProductId,
                                        TriggerRecipeId, TriggerMenuItemId, Symptoms,
                                        SeverityLevel, EpiPenUsed, HospitalVisit,
                                        Treatment, Notes, CreatedAt)
            VALUES (@Id, @UserId, @UserAllergenId, @UserIngredientAllergyId,
                    @IncidentDate, @TriggerSource, @TriggerProductId,
                    @TriggerRecipeId, @TriggerMenuItemId, @Symptoms,
                    @SeverityLevel, @EpiPenUsed, @HospitalVisit,
                    @Treatment, @Notes, GETUTCDATE())";

        await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", id),
            new SqlParameter("@UserId", userId),
            new SqlParameter("@UserAllergenId", (object?)request.UserAllergenId ?? DBNull.Value),
            new SqlParameter("@UserIngredientAllergyId", (object?)request.UserIngredientAllergyId ?? DBNull.Value),
            new SqlParameter("@IncidentDate", request.IncidentDate),
            new SqlParameter("@TriggerSource", (object?)request.TriggerSource ?? DBNull.Value),
            new SqlParameter("@TriggerProductId", (object?)request.TriggerProductId ?? DBNull.Value),
            new SqlParameter("@TriggerRecipeId", (object?)request.TriggerRecipeId ?? DBNull.Value),
            new SqlParameter("@TriggerMenuItemId", (object?)request.TriggerMenuItemId ?? DBNull.Value),
            new SqlParameter("@Symptoms", (object?)request.Symptoms ?? DBNull.Value),
            new SqlParameter("@SeverityLevel", request.SeverityLevel),
            new SqlParameter("@EpiPenUsed", request.EpiPenUsed),
            new SqlParameter("@HospitalVisit", request.HospitalVisit),
            new SqlParameter("@Treatment", (object?)request.Treatment ?? DBNull.Value),
            new SqlParameter("@Notes", (object?)request.Notes ?? DBNull.Value));

        // Add reaction types
        if (request.ReactionTypeIds != null && request.ReactionTypeIds.Any())
        {
            await AddIncidentReactionsAsync(id, request.ReactionTypeIds);
        }

        // Update last reaction date on the allergen/ingredient allergy
        if (request.UserAllergenId.HasValue)
        {
            await ExecuteNonQueryAsync(
                "UPDATE UserAllergen SET LastReactionDate = @Date WHERE Id = @Id",
                new SqlParameter("@Date", request.IncidentDate),
                new SqlParameter("@Id", request.UserAllergenId.Value));
        }
        else if (request.UserIngredientAllergyId.HasValue)
        {
            await ExecuteNonQueryAsync(
                "UPDATE UserIngredientAllergy SET LastReactionDate = @Date WHERE Id = @Id",
                new SqlParameter("@Date", request.IncidentDate),
                new SqlParameter("@Id", request.UserIngredientAllergyId.Value));
        }

        return id;
    }

    public async Task<UserAllergenSummaryDto> GetAllergenSummaryAsync(Guid userId)
    {
        const string allergenCountsSql = @"
            SELECT
                COUNT(*) AS TotalAllergens,
                COUNT(CASE WHEN SeverityLevel IN ('Severe', 'Life-Threatening') THEN 1 END) AS SevereAllergens,
                COUNT(CASE WHEN RequiresEpiPen = 1 THEN 1 END) AS RequiringEpiPen
            FROM UserAllergen
            WHERE UserId = @UserId";

        var allergenCounts = await ExecuteReaderAsync(
            allergenCountsSql,
            reader => new
            {
                TotalAllergens = GetInt(reader, "TotalAllergens"),
                SevereAllergens = GetInt(reader, "SevereAllergens"),
                RequiringEpiPen = GetInt(reader, "RequiringEpiPen")
            },
            new SqlParameter("@UserId", userId));

        var counts = allergenCounts.FirstOrDefault();

        const string ingredientCountSql = @"
            SELECT COUNT(*) AS IngredientAllergies
            FROM UserIngredientAllergy
            WHERE UserId = @UserId";

        var ingredientCount = await ExecuteScalarAsync<int>(ingredientCountSql, new SqlParameter("@UserId", userId));

        const string incidentsSql = @"
            SELECT COUNT(*) AS TotalIncidents,
                   MAX(IncidentDate) AS LastIncidentDate
            FROM AllergyIncident
            WHERE UserId = @UserId";

        var incidentData = await ExecuteReaderAsync(
            incidentsSql,
            reader => new
            {
                TotalIncidents = GetInt(reader, "TotalIncidents"),
                LastIncidentDate = GetDateTime(reader, "LastIncidentDate")
            },
            new SqlParameter("@UserId", userId));

        var incidents = incidentData.FirstOrDefault();

        return new UserAllergenSummaryDto
        {
            TotalAllergens = counts?.TotalAllergens ?? 0,
            SevereAllergens = counts?.SevereAllergens ?? 0,
            RequiringEpiPen = counts?.RequiringEpiPen ?? 0,
            IngredientAllergies = ingredientCount,
            TotalIncidents = incidents?.TotalIncidents ?? 0,
            LastIncidentDate = incidents?.LastIncidentDate,
            RecentAllergens = await GetUserAllergensAsync(userId, true),
            RecentIngredientAllergies = await GetUserIngredientAllergiesAsync(userId, true),
            RecentIncidents = await GetUserIncidentsAsync(userId, 10)
        };
    }

    // Helper methods for reaction types

    private async Task<List<AllergenReactionTypeDto>> GetUserAllergenReactionsAsync(Guid userAllergenId)
    {
        const string sql = @"
            SELECT art.Id, art.Name, art.Description, art.Severity, art.RequiresMedicalAttention, art.IsCommon
            FROM UserAllergenReaction uar
            INNER JOIN AllergenReactionType art ON uar.ReactionTypeId = art.Id
            WHERE uar.UserAllergenId = @UserAllergenId";

        return await ExecuteReaderAsync(
            sql,
            reader => new AllergenReactionTypeDto
            {
                Id = GetGuid(reader, "Id"),
                Name = GetString(reader, "Name") ?? string.Empty,
                Description = GetString(reader, "Description"),
                Severity = GetString(reader, "Severity") ?? string.Empty,
                RequiresMedicalAttention = GetBoolean(reader, "RequiresMedicalAttention"),
                IsCommon = GetBoolean(reader, "IsCommon")
            },
            new SqlParameter("@UserAllergenId", userAllergenId));
    }

    private async Task AddUserAllergenReactionsAsync(Guid userAllergenId, List<Guid> reactionTypeIds)
    {
        foreach (var reactionTypeId in reactionTypeIds)
        {
            await ExecuteNonQueryAsync(
                "INSERT INTO UserAllergenReaction (Id, UserAllergenId, ReactionTypeId) VALUES (@Id, @UserAllergenId, @ReactionTypeId)",
                new SqlParameter("@Id", Guid.NewGuid()),
                new SqlParameter("@UserAllergenId", userAllergenId),
                new SqlParameter("@ReactionTypeId", reactionTypeId));
        }
    }

    private async Task DeleteUserAllergenReactionsAsync(Guid userAllergenId)
    {
        await ExecuteNonQueryAsync(
            "DELETE FROM UserAllergenReaction WHERE UserAllergenId = @UserAllergenId",
            new SqlParameter("@UserAllergenId", userAllergenId));
    }

    private async Task<List<AllergenReactionTypeDto>> GetIngredientAllergyReactionsAsync(Guid ingredientAllergyId)
    {
        const string sql = @"
            SELECT art.Id, art.Name, art.Description, art.Severity, art.RequiresMedicalAttention, art.IsCommon
            FROM UserIngredientAllergyReaction uiar
            INNER JOIN AllergenReactionType art ON uiar.ReactionTypeId = art.Id
            WHERE uiar.UserIngredientAllergyId = @IngredientAllergyId";

        return await ExecuteReaderAsync(
            sql,
            reader => new AllergenReactionTypeDto
            {
                Id = GetGuid(reader, "Id"),
                Name = GetString(reader, "Name") ?? string.Empty,
                Description = GetString(reader, "Description"),
                Severity = GetString(reader, "Severity") ?? string.Empty,
                RequiresMedicalAttention = GetBoolean(reader, "RequiresMedicalAttention"),
                IsCommon = GetBoolean(reader, "IsCommon")
            },
            new SqlParameter("@IngredientAllergyId", ingredientAllergyId));
    }

    private async Task AddIngredientAllergyReactionsAsync(Guid ingredientAllergyId, List<Guid> reactionTypeIds)
    {
        foreach (var reactionTypeId in reactionTypeIds)
        {
            await ExecuteNonQueryAsync(
                "INSERT INTO UserIngredientAllergyReaction (Id, UserIngredientAllergyId, ReactionTypeId) VALUES (@Id, @IngredientAllergyId, @ReactionTypeId)",
                new SqlParameter("@Id", Guid.NewGuid()),
                new SqlParameter("@IngredientAllergyId", ingredientAllergyId),
                new SqlParameter("@ReactionTypeId", reactionTypeId));
        }
    }

    private async Task DeleteIngredientAllergyReactionsAsync(Guid ingredientAllergyId)
    {
        await ExecuteNonQueryAsync(
            "DELETE FROM UserIngredientAllergyReaction WHERE UserIngredientAllergyId = @IngredientAllergyId",
            new SqlParameter("@IngredientAllergyId", ingredientAllergyId));
    }

    private async Task<List<AllergenReactionTypeDto>> GetIncidentReactionsAsync(Guid incidentId)
    {
        const string sql = @"
            SELECT art.Id, art.Name, art.Description, art.Severity, art.RequiresMedicalAttention, art.IsCommon
            FROM AllergyIncidentReaction air
            INNER JOIN AllergenReactionType art ON air.ReactionTypeId = art.Id
            WHERE air.AllergyIncidentId = @IncidentId";

        return await ExecuteReaderAsync(
            sql,
            reader => new AllergenReactionTypeDto
            {
                Id = GetGuid(reader, "Id"),
                Name = GetString(reader, "Name") ?? string.Empty,
                Description = GetString(reader, "Description"),
                Severity = GetString(reader, "Severity") ?? string.Empty,
                RequiresMedicalAttention = GetBoolean(reader, "RequiresMedicalAttention"),
                IsCommon = GetBoolean(reader, "IsCommon")
            },
            new SqlParameter("@IncidentId", incidentId));
    }

    private async Task AddIncidentReactionsAsync(Guid incidentId, List<Guid> reactionTypeIds)
    {
        foreach (var reactionTypeId in reactionTypeIds)
        {
            await ExecuteNonQueryAsync(
                "INSERT INTO AllergyIncidentReaction (Id, AllergyIncidentId, ReactionTypeId) VALUES (@Id, @IncidentId, @ReactionTypeId)",
                new SqlParameter("@Id", Guid.NewGuid()),
                new SqlParameter("@IncidentId", incidentId),
                new SqlParameter("@ReactionTypeId", reactionTypeId));
        }
    }
}
