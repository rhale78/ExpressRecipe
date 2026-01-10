using ExpressRecipe.Data.Common;
using ExpressRecipe.Shared.DTOs.User;

namespace ExpressRecipe.UserService.Data;

public interface IUserProductRatingRepository
{
    Task<List<UserProductRatingDto>> GetRatingsByUserIdAsync(Guid userId);
    Task<List<UserProductRatingDto>> GetRatingsByProductIdAsync(Guid productId);
    Task<UserProductRatingDto?> GetRatingAsync(Guid userId, Guid productId);
    Task<Guid> CreateOrUpdateRatingAsync(Guid userId, CreateUserProductRatingRequest request, Guid? createdBy = null);
    Task<bool> DeleteRatingAsync(Guid userId, Guid productId);
    Task<(double averageRating, int totalRatings)> GetProductRatingStatsAsync(Guid productId);
}

public class UserProductRatingRepository : SqlHelper, IUserProductRatingRepository
{
    public UserProductRatingRepository(string connectionString) : base(connectionString)
    {
    }

    public async Task<List<UserProductRatingDto>> GetRatingsByUserIdAsync(Guid userId)
    {
        const string sql = @"
            SELECT Id, UserId, ProductId, Rating, ReviewText, CreatedAt, UpdatedAt
            FROM UserProductRating
            WHERE UserId = @UserId AND IsDeleted = 0
            ORDER BY UpdatedAt DESC, CreatedAt DESC";

        return await ExecuteReaderAsync(
            sql,
            reader => new UserProductRatingDto
            {
                Id = GetGuid(reader, "Id"),
                UserId = GetGuid(reader, "UserId"),
                ProductId = GetGuid(reader, "ProductId"),
                Rating = GetInt(reader, "Rating") ?? 0,
                ReviewText = GetString(reader, "ReviewText"),
                CreatedAt = GetDateTime(reader, "CreatedAt") ?? DateTime.UtcNow,
                UpdatedAt = GetDateTime(reader, "UpdatedAt")
            },
            CreateParameter("@UserId", userId));
    }

    public async Task<List<UserProductRatingDto>> GetRatingsByProductIdAsync(Guid productId)
    {
        const string sql = @"
            SELECT Id, UserId, ProductId, Rating, ReviewText, CreatedAt, UpdatedAt
            FROM UserProductRating
            WHERE ProductId = @ProductId AND IsDeleted = 0
            ORDER BY UpdatedAt DESC, CreatedAt DESC";

        return await ExecuteReaderAsync(
            sql,
            reader => new UserProductRatingDto
            {
                Id = GetGuid(reader, "Id"),
                UserId = GetGuid(reader, "UserId"),
                ProductId = GetGuid(reader, "ProductId"),
                Rating = GetInt(reader, "Rating") ?? 0,
                ReviewText = GetString(reader, "ReviewText"),
                CreatedAt = GetDateTime(reader, "CreatedAt") ?? DateTime.UtcNow,
                UpdatedAt = GetDateTime(reader, "UpdatedAt")
            },
            CreateParameter("@ProductId", productId));
    }

    public async Task<UserProductRatingDto?> GetRatingAsync(Guid userId, Guid productId)
    {
        const string sql = @"
            SELECT Id, UserId, ProductId, Rating, ReviewText, CreatedAt, UpdatedAt
            FROM UserProductRating
            WHERE UserId = @UserId AND ProductId = @ProductId AND IsDeleted = 0";

        var results = await ExecuteReaderAsync(
            sql,
            reader => new UserProductRatingDto
            {
                Id = GetGuid(reader, "Id"),
                UserId = GetGuid(reader, "UserId"),
                ProductId = GetGuid(reader, "ProductId"),
                Rating = GetInt(reader, "Rating") ?? 0,
                ReviewText = GetString(reader, "ReviewText"),
                CreatedAt = GetDateTime(reader, "CreatedAt") ?? DateTime.UtcNow,
                UpdatedAt = GetDateTime(reader, "UpdatedAt")
            },
            CreateParameter("@UserId", userId),
            CreateParameter("@ProductId", productId));

        return results.FirstOrDefault();
    }

    public async Task<Guid> CreateOrUpdateRatingAsync(Guid userId, CreateUserProductRatingRequest request, Guid? createdBy = null)
    {
        // Check if rating already exists
        var existing = await GetRatingAsync(userId, request.ProductId);

        if (existing != null)
        {
            // Update existing rating
            const string updateSql = @"
                UPDATE UserProductRating
                SET Rating = @Rating,
                    ReviewText = @ReviewText,
                    UpdatedBy = @UpdatedBy,
                    UpdatedAt = GETUTCDATE()
                WHERE UserId = @UserId AND ProductId = @ProductId AND IsDeleted = 0";

            await ExecuteNonQueryAsync(
                updateSql,
                CreateParameter("@Rating", request.Rating),
                CreateParameter("@ReviewText", request.ReviewText),
                CreateParameter("@UpdatedBy", createdBy ?? userId),
                CreateParameter("@UserId", userId),
                CreateParameter("@ProductId", request.ProductId));

            return existing.Id;
        }
        else
        {
            // Create new rating
            const string insertSql = @"
                INSERT INTO UserProductRating (
                    Id, UserId, ProductId, Rating, ReviewText, CreatedBy, CreatedAt
                )
                VALUES (
                    @Id, @UserId, @ProductId, @Rating, @ReviewText, @CreatedBy, GETUTCDATE()
                )";

            var ratingId = Guid.NewGuid();

            await ExecuteNonQueryAsync(
                insertSql,
                CreateParameter("@Id", ratingId),
                CreateParameter("@UserId", userId),
                CreateParameter("@ProductId", request.ProductId),
                CreateParameter("@Rating", request.Rating),
                CreateParameter("@ReviewText", request.ReviewText),
                CreateParameter("@CreatedBy", createdBy ?? userId));

            return ratingId;
        }
    }

    public async Task<bool> DeleteRatingAsync(Guid userId, Guid productId)
    {
        const string sql = @"
            UPDATE UserProductRating
            SET IsDeleted = 1, DeletedAt = GETUTCDATE()
            WHERE UserId = @UserId AND ProductId = @ProductId AND IsDeleted = 0";

        var rowsAffected = await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@UserId", userId),
            CreateParameter("@ProductId", productId));

        return rowsAffected > 0;
    }

    public async Task<(double averageRating, int totalRatings)> GetProductRatingStatsAsync(Guid productId)
    {
        const string sql = @"
            SELECT AVG(CAST(Rating AS FLOAT)) AS AverageRating, COUNT(*) AS TotalRatings
            FROM UserProductRating
            WHERE ProductId = @ProductId AND IsDeleted = 0";

        var results = await ExecuteReaderAsync(
            sql,
            reader => new
            {
                AverageRating = reader.IsDBNull(reader.GetOrdinal("AverageRating")) 
                    ? 0.0 
                    : reader.GetDouble(reader.GetOrdinal("AverageRating")),
                TotalRatings = reader.GetInt32(reader.GetOrdinal("TotalRatings"))
            },
            CreateParameter("@ProductId", productId));

        var stats = results.FirstOrDefault();
        return (stats?.AverageRating ?? 0.0, stats?.TotalRatings ?? 0);
    }
}
