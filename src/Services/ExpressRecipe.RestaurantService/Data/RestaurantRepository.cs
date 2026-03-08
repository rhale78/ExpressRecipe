using ExpressRecipe.Data.Common;
using ExpressRecipe.Shared.DTOs.Product;
using Microsoft.Data.SqlClient;
using System.Data;

namespace ExpressRecipe.RestaurantService.Data;

public interface IRestaurantRepository
{
    Task<List<RestaurantDto>> SearchAsync(RestaurantSearchRequest request);
    Task<RestaurantDto?> GetByIdAsync(Guid id);
    Task<Guid> CreateAsync(CreateRestaurantRequest request, Guid createdBy);
    Task<bool> UpdateAsync(Guid id, UpdateRestaurantRequest request, Guid updatedBy);
    Task<bool> DeleteAsync(Guid id, Guid deletedBy);
    Task<bool> ApproveAsync(Guid id, bool approve, Guid approvedBy, string? rejectionReason = null);
    Task<bool> RestaurantExistsAsync(Guid id);

    // User Ratings
    Task<List<UserRestaurantRatingDto>> GetRestaurantRatingsAsync(Guid restaurantId);
    Task<UserRestaurantRatingDto?> GetUserRatingAsync(Guid restaurantId, Guid userId);
    Task<Guid> AddOrUpdateRatingAsync(Guid restaurantId, Guid userId, RateRestaurantRequest request);
    Task<bool> DeleteRatingAsync(Guid restaurantId, Guid userId);
}

public class RestaurantRepository : SqlHelper, IRestaurantRepository
{
    public RestaurantRepository(string connectionString) : base(connectionString)
    {
    }

    public async Task<List<RestaurantDto>> SearchAsync(RestaurantSearchRequest request)
    {
        var whereClauses = new List<string> { "IsActive = 1" };
        var parameters = new List<SqlParameter>();

        if (request.OnlyApproved == true)
        {
            whereClauses.Add("ApprovalStatus = 'Approved'");
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            whereClauses.Add("(Name LIKE @SearchTerm OR Brand LIKE @SearchTerm OR Address LIKE @SearchTerm)");
            parameters.Add((SqlParameter)CreateParameter("@SearchTerm", $"%{request.SearchTerm}%"));
        }

        if (!string.IsNullOrWhiteSpace(request.CuisineType))
        {
            whereClauses.Add("CuisineType = @CuisineType");
            parameters.Add((SqlParameter)CreateParameter("@CuisineType", request.CuisineType));
        }

        if (!string.IsNullOrWhiteSpace(request.City))
        {
            whereClauses.Add("City = @City");
            parameters.Add((SqlParameter)CreateParameter("@City", request.City));
        }

        if (!string.IsNullOrWhiteSpace(request.State))
        {
            whereClauses.Add("State = @State");
            parameters.Add((SqlParameter)CreateParameter("@State", request.State));
        }

        var sql = $@"
            SELECT Id, Name, Brand, Description, CuisineType, RestaurantType,
                   Address, City, State, ZipCode, Country, Latitude, Longitude,
                   PhoneNumber, Website, ImageUrl, PriceRange, IsChain,
                   ApprovalStatus, ApprovedBy, ApprovedAt, SubmittedBy,
                   AverageRating, RatingCount, IsActive, CreatedAt, UpdatedAt
            FROM Restaurant
            WHERE {string.Join(" AND ", whereClauses)}
            ORDER BY Name
            OFFSET {(request.PageNumber - 1) * request.PageSize} ROWS
            FETCH NEXT {request.PageSize} ROWS ONLY";

        return await ExecuteReaderAsync(
            sql,
            (SqlDataReader reader) => MapRestaurantDto(reader),
            parameters.ToArray());
    }

    public async Task<RestaurantDto?> GetByIdAsync(Guid id)
    {
        const string sql = @"
            SELECT Id, Name, Brand, Description, CuisineType, RestaurantType,
                   Address, City, State, ZipCode, Country, Latitude, Longitude,
                   PhoneNumber, Website, ImageUrl, PriceRange, IsChain,
                   ApprovalStatus, ApprovedBy, ApprovedAt, SubmittedBy,
                   AverageRating, RatingCount, IsActive, CreatedAt, UpdatedAt
            FROM Restaurant
            WHERE Id = @Id AND IsActive = 1";

        var results = await ExecuteReaderAsync(
            sql,
            (SqlDataReader reader) => MapRestaurantDto(reader),
            CreateParameter("@Id", id));

        return results.FirstOrDefault();
    }

    public async Task<Guid> CreateAsync(CreateRestaurantRequest request, Guid createdBy)
    {
        const string sql = @"
            INSERT INTO Restaurant (
                Id, Name, Brand, Description, CuisineType,
                Address, City, State, ZipCode, Country,
                Latitude, Longitude, PhoneNumber, Website, ImageUrl,
                PriceRange, IsChain, ApprovalStatus,
                SubmittedBy, CreatedBy, CreatedAt
            )
            VALUES (
                @Id, @Name, @Brand, @Description, @CuisineType,
                @Address, @City, @State, @ZipCode, @Country,
                @Latitude, @Longitude, @PhoneNumber, @Website, @ImageUrl,
                @PriceRange, @IsChain, 'Pending',
                @SubmittedBy, @CreatedBy, GETUTCDATE()
            )";

        var id = Guid.NewGuid();

        await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", id),
            CreateParameter("@Name", request.Name),
            CreateParameter("@Brand", request.Brand),
            CreateParameter("@Description", request.Description),
            CreateParameter("@CuisineType", request.CuisineType),
            CreateParameter("@Address", request.Address),
            CreateParameter("@City", request.City),
            CreateParameter("@State", request.State),
            CreateParameter("@ZipCode", request.ZipCode),
            CreateParameter("@Country", request.Country ?? "US"),
            CreateParameter("@Latitude", request.Latitude),
            CreateParameter("@Longitude", request.Longitude),
            CreateParameter("@PhoneNumber", request.PhoneNumber),
            CreateParameter("@Website", request.Website),
            CreateParameter("@ImageUrl", request.ImageUrl),
            CreateParameter("@PriceRange", request.PriceRange),
            CreateParameter("@IsChain", request.IsChain),
            CreateParameter("@SubmittedBy", createdBy),
            CreateParameter("@CreatedBy", createdBy));

        return id;
    }

    public async Task<bool> UpdateAsync(Guid id, UpdateRestaurantRequest request, Guid updatedBy)
    {
        const string sql = @"
            UPDATE Restaurant
            SET Name = @Name,
                Brand = @Brand,
                Description = @Description,
                CuisineType = @CuisineType,
                Address = @Address,
                City = @City,
                State = @State,
                ZipCode = @ZipCode,
                Country = @Country,
                Latitude = @Latitude,
                Longitude = @Longitude,
                PhoneNumber = @PhoneNumber,
                Website = @Website,
                ImageUrl = @ImageUrl,
                PriceRange = @PriceRange,
                IsChain = @IsChain,
                UpdatedBy = @UpdatedBy,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND IsActive = 1";

        var rowsAffected = await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", id),
            CreateParameter("@Name", request.Name),
            CreateParameter("@Brand", request.Brand),
            CreateParameter("@Description", request.Description),
            CreateParameter("@CuisineType", request.CuisineType),
            CreateParameter("@Address", request.Address),
            CreateParameter("@City", request.City),
            CreateParameter("@State", request.State),
            CreateParameter("@ZipCode", request.ZipCode),
            CreateParameter("@Country", request.Country ?? "US"),
            CreateParameter("@Latitude", request.Latitude),
            CreateParameter("@Longitude", request.Longitude),
            CreateParameter("@PhoneNumber", request.PhoneNumber),
            CreateParameter("@Website", request.Website),
            CreateParameter("@ImageUrl", request.ImageUrl),
            CreateParameter("@PriceRange", request.PriceRange),
            CreateParameter("@IsChain", request.IsChain),
            CreateParameter("@UpdatedBy", updatedBy));

        return rowsAffected > 0;
    }

    public async Task<bool> DeleteAsync(Guid id, Guid deletedBy)
    {
        const string sql = @"
            UPDATE Restaurant
            SET IsActive = 0,
                UpdatedBy = @DeletedBy,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND IsActive = 1";

        var rowsAffected = await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", id),
            CreateParameter("@DeletedBy", deletedBy));

        return rowsAffected > 0;
    }

    public async Task<bool> ApproveAsync(Guid id, bool approve, Guid approvedBy, string? rejectionReason = null)
    {
        const string sql = @"
            UPDATE Restaurant
            SET ApprovalStatus = @ApprovalStatus,
                ApprovedBy = @ApprovedBy,
                ApprovedAt = GETUTCDATE(),
                UpdatedBy = @ApprovedBy,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND IsActive = 1";

        var rowsAffected = await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", id),
            CreateParameter("@ApprovalStatus", approve ? "Approved" : "Rejected"),
            CreateParameter("@ApprovedBy", approvedBy));

        return rowsAffected > 0;
    }

    public async Task<bool> RestaurantExistsAsync(Guid id)
    {
        const string sql = "SELECT COUNT(*) FROM Restaurant WHERE Id = @Id AND IsActive = 1";

        var count = await ExecuteScalarAsync<int>(
            sql,
            CreateParameter("@Id", id));

        return count > 0;
    }

    #region User Ratings

    public async Task<List<UserRestaurantRatingDto>> GetRestaurantRatingsAsync(Guid restaurantId)
    {
        const string sql = @"
            SELECT UserId, RestaurantId, Rating, Review, CreatedAt, UpdatedAt
            FROM UserRestaurantRating
            WHERE RestaurantId = @RestaurantId
            ORDER BY CreatedAt DESC";

        return await ExecuteReaderAsync(
            sql,
            (SqlDataReader reader) => new UserRestaurantRatingDto
            {
                UserId = GetGuid(reader, "UserId"),
                RestaurantId = GetGuid(reader, "RestaurantId"),
                Rating = GetInt(reader, "Rating") ?? 0,
                Review = GetString(reader, "Review"),
                CreatedAt = GetDateTime(reader, "CreatedAt"),
                UpdatedAt = GetNullableDateTime(reader, "UpdatedAt")
            },
            CreateParameter("@RestaurantId", restaurantId));
    }

    public async Task<UserRestaurantRatingDto?> GetUserRatingAsync(Guid restaurantId, Guid userId)
    {
        const string sql = @"
            SELECT UserId, RestaurantId, Rating, Review, CreatedAt, UpdatedAt
            FROM UserRestaurantRating
            WHERE RestaurantId = @RestaurantId AND UserId = @UserId";

        var results = await ExecuteReaderAsync(
            sql,
            (SqlDataReader reader) => new UserRestaurantRatingDto
            {
                UserId = GetGuid(reader, "UserId"),
                RestaurantId = GetGuid(reader, "RestaurantId"),
                Rating = GetInt(reader, "Rating") ?? 0,
                Review = GetString(reader, "Review"),
                CreatedAt = GetDateTime(reader, "CreatedAt"),
                UpdatedAt = GetNullableDateTime(reader, "UpdatedAt")
            },
            CreateParameter("@RestaurantId", restaurantId),
            CreateParameter("@UserId", userId));

        return results.FirstOrDefault();
    }

    public async Task<Guid> AddOrUpdateRatingAsync(Guid restaurantId, Guid userId, RateRestaurantRequest request)
    {
        const string updateSql = @"
            UPDATE UserRestaurantRating
            SET Rating = @Rating,
                Review = @Review,
                UpdatedAt = GETUTCDATE()
            WHERE RestaurantId = @RestaurantId AND UserId = @UserId";

        var rowsAffected = await ExecuteNonQueryAsync(
            updateSql,
            CreateParameter("@RestaurantId", restaurantId),
            CreateParameter("@UserId", userId),
            CreateParameter("@Rating", request.Rating),
            CreateParameter("@Review", request.Review));

        if (rowsAffected > 0)
        {
            // Rating aggregates are updated by the insert branch only on new entries;
            // for updates we recalculate inline.
            await RecalculateAverageRatingAsync(restaurantId);
            return Guid.Empty;
        }

        const string insertSql = @"
            INSERT INTO UserRestaurantRating (Id, UserId, RestaurantId, Rating, Review, CreatedAt)
            VALUES (@Id, @UserId, @RestaurantId, @Rating, @Review, GETUTCDATE())";

        var id = Guid.NewGuid();

        await ExecuteNonQueryAsync(
            insertSql,
            CreateParameter("@Id", id),
            CreateParameter("@UserId", userId),
            CreateParameter("@RestaurantId", restaurantId),
            CreateParameter("@Rating", request.Rating),
            CreateParameter("@Review", request.Review));

        await RecalculateAverageRatingAsync(restaurantId);

        return id;
    }

    public async Task<bool> DeleteRatingAsync(Guid restaurantId, Guid userId)
    {
        const string sql = @"
            DELETE FROM UserRestaurantRating
            WHERE RestaurantId = @RestaurantId AND UserId = @UserId";

        var rowsAffected = await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@RestaurantId", restaurantId),
            CreateParameter("@UserId", userId));

        if (rowsAffected > 0)
        {
            await RecalculateAverageRatingAsync(restaurantId);
        }

        return rowsAffected > 0;
    }

    private async Task RecalculateAverageRatingAsync(Guid restaurantId)
    {
        const string sql = @"
            UPDATE Restaurant
            SET AverageRating = (
                    SELECT AVG(CAST(Rating AS DECIMAL(3,2)))
                    FROM UserRestaurantRating
                    WHERE RestaurantId = @RestaurantId
                ),
                RatingCount = (
                    SELECT COUNT(*)
                    FROM UserRestaurantRating
                    WHERE RestaurantId = @RestaurantId
                ),
                UpdatedAt = GETUTCDATE()
            WHERE Id = @RestaurantId";

        await ExecuteNonQueryAsync(sql, CreateParameter("@RestaurantId", restaurantId));
    }

    #endregion

    private RestaurantDto MapRestaurantDto(IDataRecord reader)
    {
        var approvalStatus = GetString(reader, "ApprovalStatus") ?? "Pending";
        return new RestaurantDto
        {
            Id = GetGuid(reader, "Id"),
            Name = GetString(reader, "Name") ?? string.Empty,
            Brand = GetString(reader, "Brand"),
            Description = GetString(reader, "Description"),
            CuisineType = GetString(reader, "CuisineType"),
            Address = GetString(reader, "Address"),
            City = GetString(reader, "City"),
            State = GetString(reader, "State"),
            ZipCode = GetString(reader, "ZipCode"),
            Country = GetString(reader, "Country"),
            Latitude = GetDecimalNullable(reader, "Latitude"),
            Longitude = GetDecimalNullable(reader, "Longitude"),
            PhoneNumber = GetString(reader, "PhoneNumber"),
            Website = GetString(reader, "Website"),
            ImageUrl = GetString(reader, "ImageUrl"),
            PriceRange = GetString(reader, "PriceRange"),
            IsChain = GetBool(reader, "IsChain") ?? false,
            ApprovalStatus = approvalStatus,
            IsApproved = approvalStatus == "Approved",
            ApprovedBy = GetGuidNullable(reader, "ApprovedBy"),
            ApprovedAt = GetNullableDateTime(reader, "ApprovedAt"),
            SubmittedBy = GetGuidNullable(reader, "SubmittedBy"),
            AverageRating = GetDecimalNullable(reader, "AverageRating"),
            RatingCount = GetInt(reader, "RatingCount") ?? 0,
            CreatedAt = GetDateTime(reader, "CreatedAt"),
            UpdatedAt = GetNullableDateTime(reader, "UpdatedAt")
        };
    }
}
