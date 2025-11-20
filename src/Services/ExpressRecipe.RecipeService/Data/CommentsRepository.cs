using ExpressRecipe.Data.Common;
using ExpressRecipe.Shared.DTOs.User;
using Microsoft.Data.SqlClient;

namespace ExpressRecipe.RecipeService.Data;

public class CommentsRepository : SqlHelper, ICommentsRepository
{
    public CommentsRepository(string connectionString) : base(connectionString) { }

    public async Task<List<RecipeCommentDto>> GetRecipeCommentsAsync(Guid recipeId, string sortBy = "CreatedAt", int pageNumber = 1, int pageSize = 50)
    {
        var validSortColumns = new[] { "CreatedAt", "LikesCount", "Rating" };
        var sortColumn = validSortColumns.Contains(sortBy) ? sortBy : "CreatedAt";
        var offset = (pageNumber - 1) * pageSize;

        var sql = $@"
            SELECT Id, RecipeId, UserId, ParentCommentId, CommentText, Rating,
                   LikesCount, DislikesCount, IsEdited, IsFlagged, FlagReason,
                   CreatedAt, UpdatedAt
            FROM RecipeComment
            WHERE RecipeId = @RecipeId
              AND ParentCommentId IS NULL
              AND IsDeleted = 0
            ORDER BY {sortColumn} DESC
            OFFSET @Offset ROWS
            FETCH NEXT @PageSize ROWS ONLY";

        return await ExecuteReaderAsync(sql, MapToDto,
            new SqlParameter("@RecipeId", recipeId),
            new SqlParameter("@Offset", offset),
            new SqlParameter("@PageSize", pageSize));
    }

    public async Task<RecipeCommentDto?> GetCommentByIdAsync(Guid id)
    {
        const string sql = @"
            SELECT Id, RecipeId, UserId, ParentCommentId, CommentText, Rating,
                   LikesCount, DislikesCount, IsEdited, IsFlagged, FlagReason,
                   CreatedAt, UpdatedAt
            FROM RecipeComment
            WHERE Id = @Id AND IsDeleted = 0";

        var comments = await ExecuteReaderAsync(sql, MapToDto,
            new SqlParameter("@Id", id));

        return comments.FirstOrDefault();
    }

    public async Task<List<RecipeCommentDto>> GetCommentRepliesAsync(Guid parentCommentId)
    {
        const string sql = @"
            SELECT Id, RecipeId, UserId, ParentCommentId, CommentText, Rating,
                   LikesCount, DislikesCount, IsEdited, IsFlagged, FlagReason,
                   CreatedAt, UpdatedAt
            FROM RecipeComment
            WHERE ParentCommentId = @ParentCommentId AND IsDeleted = 0
            ORDER BY CreatedAt ASC";

        return await ExecuteReaderAsync(sql, MapToDto,
            new SqlParameter("@ParentCommentId", parentCommentId));
    }

    public async Task<Guid> CreateCommentAsync(Guid userId, CreateRecipeCommentRequest request)
    {
        var id = Guid.NewGuid();

        const string sql = @"
            INSERT INTO RecipeComment
                (Id, RecipeId, UserId, ParentCommentId, CommentText, Rating,
                 LikesCount, DislikesCount, IsEdited, IsFlagged, CreatedAt, IsDeleted)
            VALUES
                (@Id, @RecipeId, @UserId, @ParentCommentId, @CommentText, @Rating,
                 0, 0, 0, 0, GETUTCDATE(), 0)";

        await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", id),
            new SqlParameter("@RecipeId", request.RecipeId),
            new SqlParameter("@UserId", userId),
            new SqlParameter("@ParentCommentId", (object?)request.ParentCommentId ?? DBNull.Value),
            new SqlParameter("@CommentText", request.CommentText),
            new SqlParameter("@Rating", (object?)request.Rating ?? DBNull.Value));

        return id;
    }

    public async Task<bool> UpdateCommentAsync(Guid id, Guid userId, UpdateRecipeCommentRequest request)
    {
        const string sql = @"
            UPDATE RecipeComment
            SET CommentText = @CommentText,
                Rating = @Rating,
                IsEdited = 1,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND UserId = @UserId AND IsDeleted = 0";

        var rowsAffected = await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", id),
            new SqlParameter("@UserId", userId),
            new SqlParameter("@CommentText", request.CommentText),
            new SqlParameter("@Rating", (object?)request.Rating ?? DBNull.Value));

        return rowsAffected > 0;
    }

    public async Task<bool> DeleteCommentAsync(Guid id, Guid userId)
    {
        const string sql = @"
            UPDATE RecipeComment
            SET IsDeleted = 1,
                DeletedAt = GETUTCDATE(),
                DeletedBy = @UserId
            WHERE Id = @Id AND UserId = @UserId AND IsDeleted = 0";

        var rowsAffected = await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", id),
            new SqlParameter("@UserId", userId));

        return rowsAffected > 0;
    }

    public async Task<bool> LikeCommentAsync(Guid commentId, Guid userId)
    {
        return await ExecuteTransactionAsync(async (conn, txn) =>
        {
            // Check if already liked
            const string checkSql = @"
                SELECT COUNT(1) FROM CommentLike
                WHERE CommentId = @CommentId AND UserId = @UserId AND IsLike = 1";

            var cmd = new SqlCommand(checkSql, conn, txn);
            cmd.Parameters.AddWithValue("@CommentId", commentId);
            cmd.Parameters.AddWithValue("@UserId", userId);
            var exists = (int)(await cmd.ExecuteScalarAsync() ?? 0) > 0;

            if (exists) return false;

            // Remove dislike if exists
            const string removeDislikeSql = @"
                DELETE FROM CommentLike
                WHERE CommentId = @CommentId AND UserId = @UserId AND IsLike = 0";

            cmd = new SqlCommand(removeDislikeSql, conn, txn);
            cmd.Parameters.AddWithValue("@CommentId", commentId);
            cmd.Parameters.AddWithValue("@UserId", userId);
            var dislikeRemoved = await cmd.ExecuteNonQueryAsync() > 0;

            // Add like
            const string insertSql = @"
                INSERT INTO CommentLike (Id, CommentId, UserId, IsLike, CreatedAt)
                VALUES (@Id, @CommentId, @UserId, 1, GETUTCDATE())";

            cmd = new SqlCommand(insertSql, conn, txn);
            cmd.Parameters.AddWithValue("@Id", Guid.NewGuid());
            cmd.Parameters.AddWithValue("@CommentId", commentId);
            cmd.Parameters.AddWithValue("@UserId", userId);
            await cmd.ExecuteNonQueryAsync();

            // Update counts
            await UpdateCommentCountsAsync(commentId, conn, txn);

            return true;
        });
    }

    public async Task<bool> UnlikeCommentAsync(Guid commentId, Guid userId)
    {
        return await ExecuteTransactionAsync(async (conn, txn) =>
        {
            const string sql = @"
                DELETE FROM CommentLike
                WHERE CommentId = @CommentId AND UserId = @UserId AND IsLike = 1";

            var cmd = new SqlCommand(sql, conn, txn);
            cmd.Parameters.AddWithValue("@CommentId", commentId);
            cmd.Parameters.AddWithValue("@UserId", userId);
            var rowsAffected = await cmd.ExecuteNonQueryAsync();

            if (rowsAffected > 0)
            {
                await UpdateCommentCountsAsync(commentId, conn, txn);
                return true;
            }

            return false;
        });
    }

    public async Task<bool> DislikeCommentAsync(Guid commentId, Guid userId)
    {
        return await ExecuteTransactionAsync(async (conn, txn) =>
        {
            // Check if already disliked
            const string checkSql = @"
                SELECT COUNT(1) FROM CommentLike
                WHERE CommentId = @CommentId AND UserId = @UserId AND IsLike = 0";

            var cmd = new SqlCommand(checkSql, conn, txn);
            cmd.Parameters.AddWithValue("@CommentId", commentId);
            cmd.Parameters.AddWithValue("@UserId", userId);
            var exists = (int)(await cmd.ExecuteScalarAsync() ?? 0) > 0;

            if (exists) return false;

            // Remove like if exists
            const string removeLikeSql = @"
                DELETE FROM CommentLike
                WHERE CommentId = @CommentId AND UserId = @UserId AND IsLike = 1";

            cmd = new SqlCommand(removeLikeSql, conn, txn);
            cmd.Parameters.AddWithValue("@CommentId", commentId);
            cmd.Parameters.AddWithValue("@UserId", userId);
            await cmd.ExecuteNonQueryAsync();

            // Add dislike
            const string insertSql = @"
                INSERT INTO CommentLike (Id, CommentId, UserId, IsLike, CreatedAt)
                VALUES (@Id, @CommentId, @UserId, 0, GETUTCDATE())";

            cmd = new SqlCommand(insertSql, conn, txn);
            cmd.Parameters.AddWithValue("@Id", Guid.NewGuid());
            cmd.Parameters.AddWithValue("@CommentId", commentId);
            cmd.Parameters.AddWithValue("@UserId", userId);
            await cmd.ExecuteNonQueryAsync();

            // Update counts
            await UpdateCommentCountsAsync(commentId, conn, txn);

            return true;
        });
    }

    public async Task<bool> UndislikeCommentAsync(Guid commentId, Guid userId)
    {
        return await ExecuteTransactionAsync(async (conn, txn) =>
        {
            const string sql = @"
                DELETE FROM CommentLike
                WHERE CommentId = @CommentId AND UserId = @UserId AND IsLike = 0";

            var cmd = new SqlCommand(sql, conn, txn);
            cmd.Parameters.AddWithValue("@CommentId", commentId);
            cmd.Parameters.AddWithValue("@UserId", userId);
            var rowsAffected = await cmd.ExecuteNonQueryAsync();

            if (rowsAffected > 0)
            {
                await UpdateCommentCountsAsync(commentId, conn, txn);
                return true;
            }

            return false;
        });
    }

    public async Task<bool> FlagCommentAsync(Guid commentId, Guid userId, string reason)
    {
        const string sql = @"
            UPDATE RecipeComment
            SET IsFlagged = 1,
                FlagReason = @Reason,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @CommentId AND IsDeleted = 0";

        var rowsAffected = await ExecuteNonQueryAsync(sql,
            new SqlParameter("@CommentId", commentId),
            new SqlParameter("@Reason", reason));

        return rowsAffected > 0;
    }

    public async Task<bool> UnflagCommentAsync(Guid commentId, Guid userId)
    {
        const string sql = @"
            UPDATE RecipeComment
            SET IsFlagged = 0,
                FlagReason = NULL,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @CommentId AND IsDeleted = 0";

        var rowsAffected = await ExecuteNonQueryAsync(sql,
            new SqlParameter("@CommentId", commentId));

        return rowsAffected > 0;
    }

    public async Task<List<RecipeCommentDto>> GetUserCommentsAsync(Guid userId)
    {
        const string sql = @"
            SELECT Id, RecipeId, UserId, ParentCommentId, CommentText, Rating,
                   LikesCount, DislikesCount, IsEdited, IsFlagged, FlagReason,
                   CreatedAt, UpdatedAt
            FROM RecipeComment
            WHERE UserId = @UserId AND IsDeleted = 0
            ORDER BY CreatedAt DESC";

        return await ExecuteReaderAsync(sql, MapToDto,
            new SqlParameter("@UserId", userId));
    }

    public async Task<List<RecipeCommentDto>> GetFlaggedCommentsAsync()
    {
        const string sql = @"
            SELECT Id, RecipeId, UserId, ParentCommentId, CommentText, Rating,
                   LikesCount, DislikesCount, IsEdited, IsFlagged, FlagReason,
                   CreatedAt, UpdatedAt
            FROM RecipeComment
            WHERE IsFlagged = 1 AND IsDeleted = 0
            ORDER BY UpdatedAt DESC";

        return await ExecuteReaderAsync(sql, MapToDto);
    }

    private async Task UpdateCommentCountsAsync(Guid commentId, SqlConnection conn, SqlTransaction txn)
    {
        const string sql = @"
            UPDATE RecipeComment
            SET LikesCount = (SELECT COUNT(1) FROM CommentLike WHERE CommentId = @CommentId AND IsLike = 1),
                DislikesCount = (SELECT COUNT(1) FROM CommentLike WHERE CommentId = @CommentId AND IsLike = 0),
                UpdatedAt = GETUTCDATE()
            WHERE Id = @CommentId";

        var cmd = new SqlCommand(sql, conn, txn);
        cmd.Parameters.AddWithValue("@CommentId", commentId);
        await cmd.ExecuteNonQueryAsync();
    }

    private static RecipeCommentDto MapToDto(SqlDataReader reader)
    {
        return new RecipeCommentDto
        {
            Id = reader.GetGuid(reader.GetOrdinal("Id")),
            RecipeId = reader.GetGuid(reader.GetOrdinal("RecipeId")),
            UserId = reader.GetGuid(reader.GetOrdinal("UserId")),
            ParentCommentId = reader.IsDBNull(reader.GetOrdinal("ParentCommentId"))
                ? null
                : reader.GetGuid(reader.GetOrdinal("ParentCommentId")),
            CommentText = reader.GetString(reader.GetOrdinal("CommentText")),
            Rating = reader.IsDBNull(reader.GetOrdinal("Rating"))
                ? null
                : reader.GetInt32(reader.GetOrdinal("Rating")),
            LikesCount = reader.GetInt32(reader.GetOrdinal("LikesCount")),
            DislikesCount = reader.GetInt32(reader.GetOrdinal("DislikesCount")),
            IsEdited = reader.GetBoolean(reader.GetOrdinal("IsEdited")),
            IsFlagged = reader.GetBoolean(reader.GetOrdinal("IsFlagged")),
            FlagReason = reader.IsDBNull(reader.GetOrdinal("FlagReason"))
                ? null
                : reader.GetString(reader.GetOrdinal("FlagReason")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
            UpdatedAt = reader.IsDBNull(reader.GetOrdinal("UpdatedAt"))
                ? null
                : reader.GetDateTime(reader.GetOrdinal("UpdatedAt"))
        };
    }
}
