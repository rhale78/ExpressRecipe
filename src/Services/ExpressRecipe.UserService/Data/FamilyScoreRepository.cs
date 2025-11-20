using ExpressRecipe.Data.Common;
using ExpressRecipe.Shared.DTOs.User;
using Microsoft.Data.SqlClient;

namespace ExpressRecipe.UserService.Data;

public interface IFamilyScoreRepository
{
    Task<FamilyScoreDto?> GetFamilyScoreAsync(Guid userId, string entityType, Guid entityId);
    Task<List<FamilyScoreDto>> GetUserFamilyScoresAsync(Guid userId, string? entityType = null, bool? favoritesOnly = null);
    Task<Guid> CreateFamilyScoreAsync(Guid userId, CreateFamilyScoreRequest request);
    Task<bool> UpdateFamilyScoreAsync(Guid familyScoreId, Guid userId, UpdateFamilyScoreRequest request);
    Task<bool> DeleteFamilyScoreAsync(Guid familyScoreId, Guid userId);

    Task<Guid> AddMemberScoreAsync(Guid familyScoreId, Guid familyMemberId, int score, string? notes = null);
    Task<bool> UpdateMemberScoreAsync(Guid memberScoreId, UpdateFamilyMemberScoreRequest request);
    Task<bool> DeleteMemberScoreAsync(Guid memberScoreId);
    Task<List<FamilyMemberScoreDto>> GetMemberScoresAsync(Guid familyScoreId);

    Task<List<FamilyScoreDto>> GetFavoritesAsync(Guid userId, string? entityType = null);
    Task<List<FamilyScoreDto>> GetBlacklistedAsync(Guid userId, string? entityType = null);
}

public class FamilyScoreRepository : SqlHelper, IFamilyScoreRepository
{
    public FamilyScoreRepository(string connectionString) : base(connectionString)
    {
    }

    public async Task<FamilyScoreDto?> GetFamilyScoreAsync(Guid userId, string entityType, Guid entityId)
    {
        const string sql = @"
            SELECT Id, UserId, EntityType, EntityId, FamilyAverageScore, Notes,
                   IsFavorite, IsBlacklisted, CreatedAt, UpdatedAt
            FROM FamilyScore
            WHERE UserId = @UserId
              AND EntityType = @EntityType
              AND EntityId = @EntityId
              AND IsDeleted = 0";

        var results = await ExecuteReaderAsync(
            sql,
            reader => MapFamilyScoreDto(reader),
            new SqlParameter("@UserId", userId),
            new SqlParameter("@EntityType", entityType),
            new SqlParameter("@EntityId", entityId));

        var score = results.FirstOrDefault();

        if (score != null)
        {
            score.MemberScores = await GetMemberScoresAsync(score.Id);
        }

        return score;
    }

    public async Task<List<FamilyScoreDto>> GetUserFamilyScoresAsync(Guid userId, string? entityType = null, bool? favoritesOnly = null)
    {
        var sql = @"
            SELECT Id, UserId, EntityType, EntityId, FamilyAverageScore, Notes,
                   IsFavorite, IsBlacklisted, CreatedAt, UpdatedAt
            FROM FamilyScore
            WHERE UserId = @UserId
              AND IsDeleted = 0";

        var parameters = new List<SqlParameter>
        {
            new SqlParameter("@UserId", userId)
        };

        if (!string.IsNullOrWhiteSpace(entityType))
        {
            sql += " AND EntityType = @EntityType";
            parameters.Add(new SqlParameter("@EntityType", entityType));
        }

        if (favoritesOnly.HasValue)
        {
            sql += " AND IsFavorite = @IsFavorite";
            parameters.Add(new SqlParameter("@IsFavorite", favoritesOnly.Value));
        }

        sql += " ORDER BY UpdatedAt DESC, CreatedAt DESC";

        var scores = await ExecuteReaderAsync(
            sql,
            reader => MapFamilyScoreDto(reader),
            parameters.ToArray());

        // Load member scores for each
        foreach (var score in scores)
        {
            score.MemberScores = await GetMemberScoresAsync(score.Id);
        }

        return scores;
    }

    public async Task<Guid> CreateFamilyScoreAsync(Guid userId, CreateFamilyScoreRequest request)
    {
        // Check if already exists
        var existing = await GetFamilyScoreAsync(userId, request.EntityType, request.EntityId);
        if (existing != null)
        {
            throw new InvalidOperationException("Family score already exists for this entity");
        }

        var id = Guid.NewGuid();

        const string sql = @"
            INSERT INTO FamilyScore (Id, UserId, EntityType, EntityId, Notes, IsFavorite, IsBlacklisted, CreatedAt)
            VALUES (@Id, @UserId, @EntityType, @EntityId, @Notes, @IsFavorite, @IsBlacklisted, GETUTCDATE())";

        await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", id),
            new SqlParameter("@UserId", userId),
            new SqlParameter("@EntityType", request.EntityType),
            new SqlParameter("@EntityId", request.EntityId),
            new SqlParameter("@Notes", (object?)request.Notes ?? DBNull.Value),
            new SqlParameter("@IsFavorite", request.IsFavorite),
            new SqlParameter("@IsBlacklisted", request.IsBlacklisted));

        // Add member scores if provided
        if (request.MemberScores != null && request.MemberScores.Any())
        {
            foreach (var memberScore in request.MemberScores)
            {
                await AddMemberScoreAsync(id, memberScore.FamilyMemberId, memberScore.IndividualScore, memberScore.Notes);
            }

            // Update average
            await UpdateFamilyAverageAsync(id);
        }

        return id;
    }

    public async Task<bool> UpdateFamilyScoreAsync(Guid familyScoreId, Guid userId, UpdateFamilyScoreRequest request)
    {
        const string sql = @"
            UPDATE FamilyScore
            SET Notes = @Notes,
                IsFavorite = @IsFavorite,
                IsBlacklisted = @IsBlacklisted,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id
              AND UserId = @UserId
              AND IsDeleted = 0";

        var rowsAffected = await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", familyScoreId),
            new SqlParameter("@UserId", userId),
            new SqlParameter("@Notes", (object?)request.Notes ?? DBNull.Value),
            new SqlParameter("@IsFavorite", request.IsFavorite),
            new SqlParameter("@IsBlacklisted", request.IsBlacklisted));

        return rowsAffected > 0;
    }

    public async Task<bool> DeleteFamilyScoreAsync(Guid familyScoreId, Guid userId)
    {
        const string sql = @"
            UPDATE FamilyScore
            SET IsDeleted = 1,
                DeletedAt = GETUTCDATE()
            WHERE Id = @Id
              AND UserId = @UserId";

        var rowsAffected = await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", familyScoreId),
            new SqlParameter("@UserId", userId));

        return rowsAffected > 0;
    }

    public async Task<Guid> AddMemberScoreAsync(Guid familyScoreId, Guid familyMemberId, int score, string? notes = null)
    {
        if (score < 1 || score > 5)
        {
            throw new ArgumentException("Score must be between 1 and 5", nameof(score));
        }

        var id = Guid.NewGuid();

        const string sql = @"
            INSERT INTO FamilyMemberScore (Id, FamilyScoreId, FamilyMemberId, IndividualScore, Notes, LastUpdated)
            VALUES (@Id, @FamilyScoreId, @FamilyMemberId, @IndividualScore, @Notes, GETUTCDATE())";

        await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", id),
            new SqlParameter("@FamilyScoreId", familyScoreId),
            new SqlParameter("@FamilyMemberId", familyMemberId),
            new SqlParameter("@IndividualScore", score),
            new SqlParameter("@Notes", (object?)notes ?? DBNull.Value));

        // Update family average
        await UpdateFamilyAverageAsync(familyScoreId);

        return id;
    }

    public async Task<bool> UpdateMemberScoreAsync(Guid memberScoreId, UpdateFamilyMemberScoreRequest request)
    {
        if (request.IndividualScore < 1 || request.IndividualScore > 5)
        {
            throw new ArgumentException("Score must be between 1 and 5");
        }

        const string sql = @"
            UPDATE FamilyMemberScore
            SET IndividualScore = @IndividualScore,
                Notes = @Notes,
                LastUpdated = GETUTCDATE()
            WHERE Id = @Id";

        var rowsAffected = await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", memberScoreId),
            new SqlParameter("@IndividualScore", request.IndividualScore),
            new SqlParameter("@Notes", (object?)request.Notes ?? DBNull.Value));

        if (rowsAffected > 0)
        {
            // Get the family score ID and update average
            const string getFamilyScoreIdSql = "SELECT FamilyScoreId FROM FamilyMemberScore WHERE Id = @Id";
            var familyScoreId = await ExecuteScalarAsync<Guid>(getFamilyScoreIdSql, new SqlParameter("@Id", memberScoreId));
            await UpdateFamilyAverageAsync(familyScoreId);
        }

        return rowsAffected > 0;
    }

    public async Task<bool> DeleteMemberScoreAsync(Guid memberScoreId)
    {
        // Get the family score ID before deleting
        const string getFamilyScoreIdSql = "SELECT FamilyScoreId FROM FamilyMemberScore WHERE Id = @Id";
        var familyScoreId = await ExecuteScalarAsync<Guid>(getFamilyScoreIdSql, new SqlParameter("@Id", memberScoreId));

        const string sql = @"
            DELETE FROM FamilyMemberScore
            WHERE Id = @Id";

        var rowsAffected = await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", memberScoreId));

        if (rowsAffected > 0)
        {
            await UpdateFamilyAverageAsync(familyScoreId);
        }

        return rowsAffected > 0;
    }

    public async Task<List<FamilyMemberScoreDto>> GetMemberScoresAsync(Guid familyScoreId)
    {
        const string sql = @"
            SELECT fms.Id, fms.FamilyScoreId, fms.FamilyMemberId, fms.IndividualScore,
                   fms.Notes, fms.LastUpdated,
                   fm.Name AS FamilyMemberName
            FROM FamilyMemberScore fms
            INNER JOIN FamilyMember fm ON fms.FamilyMemberId = fm.Id
            WHERE fms.FamilyScoreId = @FamilyScoreId
            ORDER BY fms.LastUpdated DESC";

        return await ExecuteReaderAsync(
            sql,
            reader => new FamilyMemberScoreDto
            {
                Id = GetGuid(reader, "Id"),
                FamilyScoreId = GetGuid(reader, "FamilyScoreId"),
                FamilyMemberId = GetGuid(reader, "FamilyMemberId"),
                FamilyMemberName = GetString(reader, "FamilyMemberName"),
                IndividualScore = GetInt(reader, "IndividualScore"),
                Notes = GetString(reader, "Notes"),
                LastUpdated = GetDateTime(reader, "LastUpdated") ?? DateTime.UtcNow
            },
            new SqlParameter("@FamilyScoreId", familyScoreId));
    }

    public async Task<List<FamilyScoreDto>> GetFavoritesAsync(Guid userId, string? entityType = null)
    {
        return await GetUserFamilyScoresAsync(userId, entityType, true);
    }

    public async Task<List<FamilyScoreDto>> GetBlacklistedAsync(Guid userId, string? entityType = null)
    {
        var sql = @"
            SELECT Id, UserId, EntityType, EntityId, FamilyAverageScore, Notes,
                   IsFavorite, IsBlacklisted, CreatedAt, UpdatedAt
            FROM FamilyScore
            WHERE UserId = @UserId
              AND IsBlacklisted = 1
              AND IsDeleted = 0";

        var parameters = new List<SqlParameter>
        {
            new SqlParameter("@UserId", userId)
        };

        if (!string.IsNullOrWhiteSpace(entityType))
        {
            sql += " AND EntityType = @EntityType";
            parameters.Add(new SqlParameter("@EntityType", entityType));
        }

        sql += " ORDER BY UpdatedAt DESC, CreatedAt DESC";

        var scores = await ExecuteReaderAsync(
            sql,
            reader => MapFamilyScoreDto(reader),
            parameters.ToArray());

        foreach (var score in scores)
        {
            score.MemberScores = await GetMemberScoresAsync(score.Id);
        }

        return scores;
    }

    private async Task UpdateFamilyAverageAsync(Guid familyScoreId)
    {
        const string sql = @"
            UPDATE FamilyScore
            SET FamilyAverageScore = (
                SELECT AVG(CAST(IndividualScore AS DECIMAL(5,2)))
                FROM FamilyMemberScore
                WHERE FamilyScoreId = @FamilyScoreId
            ),
            UpdatedAt = GETUTCDATE()
            WHERE Id = @FamilyScoreId";

        await ExecuteNonQueryAsync(sql,
            new SqlParameter("@FamilyScoreId", familyScoreId));
    }

    private FamilyScoreDto MapFamilyScoreDto(SqlDataReader reader)
    {
        return new FamilyScoreDto
        {
            Id = GetGuid(reader, "Id"),
            UserId = GetGuid(reader, "UserId"),
            EntityType = GetString(reader, "EntityType") ?? string.Empty,
            EntityId = GetGuid(reader, "EntityId"),
            FamilyAverageScore = GetDecimalNullable(reader, "FamilyAverageScore"),
            Notes = GetString(reader, "Notes"),
            IsFavorite = GetBoolean(reader, "IsFavorite"),
            IsBlacklisted = GetBoolean(reader, "IsBlacklisted"),
            CreatedAt = GetDateTime(reader, "CreatedAt") ?? DateTime.UtcNow,
            UpdatedAt = GetDateTime(reader, "UpdatedAt")
        };
    }
}
