using ExpressRecipe.Data.Common;
using ExpressRecipe.Shared.DTOs.Recipe;
using Microsoft.Data.SqlClient;
using System.Data;

namespace ExpressRecipe.RecipeService.Data;

/// <summary>
/// Repository for recipe rating and family member data access
/// </summary>
public interface IRatingRepository
{
    // Family Member operations
    Task<Guid> CreateFamilyMemberAsync(Guid userId, CreateFamilyMemberRequest request);
    Task UpdateFamilyMemberAsync(Guid id, Guid userId, CreateFamilyMemberRequest request);
    Task DeleteFamilyMemberAsync(Guid id, Guid userId);
    Task<FamilyMemberDto?> GetFamilyMemberAsync(Guid id, Guid userId);
    Task<List<FamilyMemberDto>> GetUserFamilyMembersAsync(Guid userId);

    // Recipe Rating operations
    Task<Guid> CreateOrUpdateRatingAsync(Guid userId, CreateRecipeRatingRequest request);
    Task DeleteRatingAsync(Guid id, Guid userId);
    Task<UserRecipeFamilyRatingDto?> GetRatingAsync(Guid id, Guid userId);
    Task<UserRecipeFamilyRatingDto?> GetUserRecipeRatingAsync(Guid userId, Guid recipeId, Guid? familyMemberId);
    Task<List<UserRecipeFamilyRatingDto>> GetRecipeRatingsAsync(Guid recipeId, Guid? userId = null);
    Task<RecipeRatingSummaryDto> GetRecipeRatingSummaryAsync(Guid recipeId, Guid? userId = null);
    Task<RecipeRatingDto?> GetAggregatedRatingAsync(Guid recipeId);
}

/// <summary>
/// Implementation of rating repository using ADO.NET
/// </summary>
public class RatingRepository : SqlHelper, IRatingRepository
{
    public RatingRepository(string connectionString) : base(connectionString)
    {
    }

    #region Family Member Operations

    public async Task<Guid> CreateFamilyMemberAsync(Guid userId, CreateFamilyMemberRequest request)
    {
        var id = Guid.NewGuid();

        const string sql = @"
            INSERT INTO FamilyMember (Id, UserId, Name, Nickname, BirthDate, IsActive, DisplayOrder, CreatedBy, CreatedAt)
            VALUES (@Id, @UserId, @Name, @Nickname, @BirthDate, @IsActive, @DisplayOrder, @UserId, GETUTCDATE())";

        await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", id),
            new SqlParameter("@UserId", userId),
            new SqlParameter("@Name", request.Name),
            new SqlParameter("@Nickname", (object?)request.Nickname ?? DBNull.Value),
            new SqlParameter("@BirthDate", (object?)request.BirthDate ?? DBNull.Value),
            new SqlParameter("@IsActive", request.IsActive),
            new SqlParameter("@DisplayOrder", request.DisplayOrder)
        );

        return id;
    }

    public async Task UpdateFamilyMemberAsync(Guid id, Guid userId, CreateFamilyMemberRequest request)
    {
        const string sql = @"
            UPDATE FamilyMember
            SET Name = @Name,
                Nickname = @Nickname,
                BirthDate = @BirthDate,
                IsActive = @IsActive,
                DisplayOrder = @DisplayOrder,
                UpdatedBy = @UserId,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND UserId = @UserId AND IsDeleted = 0";

        await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", id),
            new SqlParameter("@UserId", userId),
            new SqlParameter("@Name", request.Name),
            new SqlParameter("@Nickname", (object?)request.Nickname ?? DBNull.Value),
            new SqlParameter("@BirthDate", (object?)request.BirthDate ?? DBNull.Value),
            new SqlParameter("@IsActive", request.IsActive),
            new SqlParameter("@DisplayOrder", request.DisplayOrder)
        );
    }

    public async Task DeleteFamilyMemberAsync(Guid id, Guid userId)
    {
        const string sql = @"
            UPDATE FamilyMember
            SET IsDeleted = 1,
                DeletedAt = GETUTCDATE()
            WHERE Id = @Id AND UserId = @UserId";

        await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", id),
            new SqlParameter("@UserId", userId)
        );
    }

    public async Task<FamilyMemberDto?> GetFamilyMemberAsync(Guid id, Guid userId)
    {
        const string sql = @"
            SELECT Id, UserId, Name, Nickname, BirthDate, IsActive, DisplayOrder, CreatedAt, UpdatedAt
            FROM FamilyMember
            WHERE Id = @Id AND UserId = @UserId AND IsDeleted = 0";

        var results = await ExecuteReaderAsync(sql, reader => new FamilyMemberDto
        {
            Id = reader.GetGuid(reader.GetOrdinal("Id")),
            UserId = reader.GetGuid(reader.GetOrdinal("UserId")),
            Name = reader.GetString(reader.GetOrdinal("Name")),
            Nickname = reader.IsDBNull(reader.GetOrdinal("Nickname")) ? null : reader.GetString(reader.GetOrdinal("Nickname")),
            BirthDate = reader.IsDBNull(reader.GetOrdinal("BirthDate")) ? null : reader.GetDateTime(reader.GetOrdinal("BirthDate")),
            IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
            DisplayOrder = reader.GetInt32(reader.GetOrdinal("DisplayOrder")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
            UpdatedAt = reader.IsDBNull(reader.GetOrdinal("UpdatedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("UpdatedAt"))
        },
        new SqlParameter("@Id", id),
        new SqlParameter("@UserId", userId));

        return results.FirstOrDefault();
    }

    public async Task<List<FamilyMemberDto>> GetUserFamilyMembersAsync(Guid userId)
    {
        const string sql = @"
            SELECT Id, UserId, Name, Nickname, BirthDate, IsActive, DisplayOrder, CreatedAt, UpdatedAt
            FROM FamilyMember
            WHERE UserId = @UserId AND IsDeleted = 0
            ORDER BY DisplayOrder, Name";

        return await ExecuteReaderAsync(sql, reader => new FamilyMemberDto
        {
            Id = reader.GetGuid(reader.GetOrdinal("Id")),
            UserId = reader.GetGuid(reader.GetOrdinal("UserId")),
            Name = reader.GetString(reader.GetOrdinal("Name")),
            Nickname = reader.IsDBNull(reader.GetOrdinal("Nickname")) ? null : reader.GetString(reader.GetOrdinal("Nickname")),
            BirthDate = reader.IsDBNull(reader.GetOrdinal("BirthDate")) ? null : reader.GetDateTime(reader.GetOrdinal("BirthDate")),
            IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
            DisplayOrder = reader.GetInt32(reader.GetOrdinal("DisplayOrder")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
            UpdatedAt = reader.IsDBNull(reader.GetOrdinal("UpdatedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("UpdatedAt"))
        },
        new SqlParameter("@UserId", userId));
    }

    #endregion

    #region Recipe Rating Operations

    public async Task<Guid> CreateOrUpdateRatingAsync(Guid userId, CreateRecipeRatingRequest request)
    {
        // Validate rating is in 0.5 increments
        if (request.Rating % 0.5m != 0)
        {
            throw new ArgumentException("Rating must be in 0.5 increments (e.g., 0, 0.5, 1.0, ..., 5.0)");
        }

        // Check if rating already exists
        var existing = await GetUserRecipeRatingAsync(userId, request.RecipeId, request.FamilyMemberId);

        if (existing != null)
        {
            // Update existing
            const string updateSql = @"
                UPDATE UserRecipeFamilyRating
                SET Rating = @Rating,
                    Review = @Review,
                    WouldMakeAgain = @WouldMakeAgain,
                    MadeItDate = @MadeItDate,
                    MadeItCount = @MadeItCount,
                    UpdatedAt = GETUTCDATE()
                WHERE Id = @Id";

            await ExecuteNonQueryAsync(updateSql,
                new SqlParameter("@Id", existing.Id),
                new SqlParameter("@Rating", request.Rating),
                new SqlParameter("@Review", (object?)request.Review ?? DBNull.Value),
                new SqlParameter("@WouldMakeAgain", (object?)request.WouldMakeAgain ?? DBNull.Value),
                new SqlParameter("@MadeItDate", (object?)request.MadeItDate ?? DBNull.Value),
                new SqlParameter("@MadeItCount", request.MadeItCount)
            );

            return existing.Id;
        }
        else
        {
            // Create new
            var id = Guid.NewGuid();

            const string insertSql = @"
                INSERT INTO UserRecipeFamilyRating 
                    (Id, UserId, RecipeId, FamilyMemberId, Rating, Review, WouldMakeAgain, MadeItDate, MadeItCount, CreatedAt)
                VALUES 
                    (@Id, @UserId, @RecipeId, @FamilyMemberId, @Rating, @Review, @WouldMakeAgain, @MadeItDate, @MadeItCount, GETUTCDATE())";

            await ExecuteNonQueryAsync(insertSql,
                new SqlParameter("@Id", id),
                new SqlParameter("@UserId", userId),
                new SqlParameter("@RecipeId", request.RecipeId),
                new SqlParameter("@FamilyMemberId", (object?)request.FamilyMemberId ?? DBNull.Value),
                new SqlParameter("@Rating", request.Rating),
                new SqlParameter("@Review", (object?)request.Review ?? DBNull.Value),
                new SqlParameter("@WouldMakeAgain", (object?)request.WouldMakeAgain ?? DBNull.Value),
                new SqlParameter("@MadeItDate", (object?)request.MadeItDate ?? DBNull.Value),
                new SqlParameter("@MadeItCount", request.MadeItCount)
            );

            return id;
        }
    }

    public async Task DeleteRatingAsync(Guid id, Guid userId)
    {
        const string sql = @"
            DELETE FROM UserRecipeFamilyRating
            WHERE Id = @Id AND UserId = @UserId";

        await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", id),
            new SqlParameter("@UserId", userId)
        );
    }

    public async Task<UserRecipeFamilyRatingDto?> GetRatingAsync(Guid id, Guid userId)
    {
        const string sql = @"
            SELECT r.Id, r.UserId, r.RecipeId, r.FamilyMemberId, r.Rating, r.Review, 
                   r.WouldMakeAgain, r.MadeItDate, r.MadeItCount, r.CreatedAt, r.UpdatedAt,
                   fm.Name as FamilyMemberName
            FROM UserRecipeFamilyRating r
            LEFT JOIN FamilyMember fm ON r.FamilyMemberId = fm.Id
            WHERE r.Id = @Id AND r.UserId = @UserId";

        var results = await ExecuteReaderAsync(sql, MapRating,
            new SqlParameter("@Id", id),
            new SqlParameter("@UserId", userId));

        return results.FirstOrDefault();
    }

    public async Task<UserRecipeFamilyRatingDto?> GetUserRecipeRatingAsync(Guid userId, Guid recipeId, Guid? familyMemberId)
    {
        const string sql = @"
            SELECT r.Id, r.UserId, r.RecipeId, r.FamilyMemberId, r.Rating, r.Review, 
                   r.WouldMakeAgain, r.MadeItDate, r.MadeItCount, r.CreatedAt, r.UpdatedAt,
                   fm.Name as FamilyMemberName
            FROM UserRecipeFamilyRating r
            LEFT JOIN FamilyMember fm ON r.FamilyMemberId = fm.Id
            WHERE r.UserId = @UserId AND r.RecipeId = @RecipeId 
                AND (@FamilyMemberId IS NULL AND r.FamilyMemberId IS NULL OR r.FamilyMemberId = @FamilyMemberId)";

        var results = await ExecuteReaderAsync(sql, MapRating,
            new SqlParameter("@UserId", userId),
            new SqlParameter("@RecipeId", recipeId),
            new SqlParameter("@FamilyMemberId", (object?)familyMemberId ?? DBNull.Value));

        return results.FirstOrDefault();
    }

    public async Task<List<UserRecipeFamilyRatingDto>> GetRecipeRatingsAsync(Guid recipeId, Guid? userId = null)
    {
        string sql;
        List<SqlParameter> parameters = new() { new SqlParameter("@RecipeId", recipeId) };

        if (userId.HasValue)
        {
            sql = @"
                SELECT r.Id, r.UserId, r.RecipeId, r.FamilyMemberId, r.Rating, r.Review, 
                       r.WouldMakeAgain, r.MadeItDate, r.MadeItCount, r.CreatedAt, r.UpdatedAt,
                       fm.Name as FamilyMemberName
                FROM UserRecipeFamilyRating r
                LEFT JOIN FamilyMember fm ON r.FamilyMemberId = fm.Id
                WHERE r.RecipeId = @RecipeId AND r.UserId = @UserId
                ORDER BY r.CreatedAt DESC";

            parameters.Add(new SqlParameter("@UserId", userId.Value));
        }
        else
        {
            sql = @"
                SELECT r.Id, r.UserId, r.RecipeId, r.FamilyMemberId, r.Rating, r.Review, 
                       r.WouldMakeAgain, r.MadeItDate, r.MadeItCount, r.CreatedAt, r.UpdatedAt,
                       fm.Name as FamilyMemberName
                FROM UserRecipeFamilyRating r
                LEFT JOIN FamilyMember fm ON r.FamilyMemberId = fm.Id
                WHERE r.RecipeId = @RecipeId
                ORDER BY r.CreatedAt DESC";
        }

        return await ExecuteReaderAsync(sql, MapRating, parameters.ToArray());
    }

    public async Task<RecipeRatingSummaryDto> GetRecipeRatingSummaryAsync(Guid recipeId, Guid? userId = null)
    {
        var ratings = await GetRecipeRatingsAsync(recipeId, userId);
        var aggregated = await GetAggregatedRatingAsync(recipeId);

        return new RecipeRatingSummaryDto
        {
            RecipeId = recipeId,
            OverallAverageRating = aggregated?.AverageRating ?? 0,
            TotalRatings = ratings.Count,
            FamilyRatings = ratings,
            AggregatedRating = aggregated
        };
    }

    public async Task<RecipeRatingDto?> GetAggregatedRatingAsync(Guid recipeId)
    {
        const string sql = @"
            SELECT Id, RecipeId, AverageRating, TotalRatings, FiveStarCount, FourStarCount, 
                   ThreeStarCount, TwoStarCount, OneStarCount, UpdatedAt
            FROM RecipeRating
            WHERE RecipeId = @RecipeId";

        var results = await ExecuteReaderAsync(sql, reader => new RecipeRatingDto
        {
            Id = reader.GetGuid(reader.GetOrdinal("Id")),
            RecipeId = reader.GetGuid(reader.GetOrdinal("RecipeId")),
            AverageRating = reader.GetDecimal(reader.GetOrdinal("AverageRating")),
            TotalRatings = reader.GetInt32(reader.GetOrdinal("TotalRatings")),
            FiveStarCount = reader.GetInt32(reader.GetOrdinal("FiveStarCount")),
            FourStarCount = reader.GetInt32(reader.GetOrdinal("FourStarCount")),
            ThreeStarCount = reader.GetInt32(reader.GetOrdinal("ThreeStarCount")),
            TwoStarCount = reader.GetInt32(reader.GetOrdinal("TwoStarCount")),
            OneStarCount = reader.GetInt32(reader.GetOrdinal("OneStarCount")),
            UpdatedAt = reader.GetDateTime(reader.GetOrdinal("UpdatedAt"))
        },
        new SqlParameter("@RecipeId", recipeId));

        return results.FirstOrDefault();
    }

    #endregion

    #region Helper Methods

    private UserRecipeFamilyRatingDto MapRating(SqlDataReader reader)
    {
        return new UserRecipeFamilyRatingDto
        {
            Id = reader.GetGuid(reader.GetOrdinal("Id")),
            UserId = reader.GetGuid(reader.GetOrdinal("UserId")),
            RecipeId = reader.GetGuid(reader.GetOrdinal("RecipeId")),
            FamilyMemberId = reader.IsDBNull(reader.GetOrdinal("FamilyMemberId")) ? null : reader.GetGuid(reader.GetOrdinal("FamilyMemberId")),
            FamilyMemberName = reader.IsDBNull(reader.GetOrdinal("FamilyMemberName")) ? null : reader.GetString(reader.GetOrdinal("FamilyMemberName")),
            Rating = reader.GetDecimal(reader.GetOrdinal("Rating")),
            Review = reader.IsDBNull(reader.GetOrdinal("Review")) ? null : reader.GetString(reader.GetOrdinal("Review")),
            WouldMakeAgain = reader.IsDBNull(reader.GetOrdinal("WouldMakeAgain")) ? null : reader.GetBoolean(reader.GetOrdinal("WouldMakeAgain")),
            MadeItDate = reader.IsDBNull(reader.GetOrdinal("MadeItDate")) ? null : reader.GetDateTime(reader.GetOrdinal("MadeItDate")),
            MadeItCount = reader.GetInt32(reader.GetOrdinal("MadeItCount")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
            UpdatedAt = reader.IsDBNull(reader.GetOrdinal("UpdatedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("UpdatedAt"))
        };
    }

    #endregion
}
