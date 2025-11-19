using ExpressRecipe.Data.Common;
using ExpressRecipe.Shared.DTOs.Product;

namespace ExpressRecipe.ProductService.Data;

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
        var whereClauses = new List<string> { "IsDeleted = 0" };
        var parameters = new List<SqlParameter>();

        if (request.OnlyApproved == true)
        {
            whereClauses.Add("IsApproved = 1");
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            whereClauses.Add("(Name LIKE @SearchTerm OR Brand LIKE @SearchTerm OR Address LIKE @SearchTerm)");
            parameters.Add(CreateParameter("@SearchTerm", $"%{request.SearchTerm}%"));
        }

        if (!string.IsNullOrWhiteSpace(request.CuisineType))
        {
            whereClauses.Add("CuisineType = @CuisineType");
            parameters.Add(CreateParameter("@CuisineType", request.CuisineType));
        }

        if (!string.IsNullOrWhiteSpace(request.City))
        {
            whereClauses.Add("City = @City");
            parameters.Add(CreateParameter("@City", request.City));
        }

        var sql = $@"
            SELECT Id, Name, Brand, CuisineType, Address, City, State, ZipCode,
                   Country, Latitude, Longitude, Phone, Website,
                   IsApproved, ApprovedBy, ApprovedAt, RejectionReason,
                   SubmittedBy, CreatedAt, UpdatedAt
            FROM Restaurant
            WHERE {string.Join(" AND ", whereClauses)}
            ORDER BY Name
            OFFSET {(request.PageNumber - 1) * request.PageSize} ROWS
            FETCH NEXT {request.PageSize} ROWS ONLY";

        return await ExecuteReaderAsync(
            sql,
            reader => MapRestaurantDto(reader),
            parameters.ToArray());
    }

    public async Task<RestaurantDto?> GetByIdAsync(Guid id)
    {
        const string sql = @"
            SELECT Id, Name, Brand, CuisineType, Address, City, State, ZipCode,
                   Country, Latitude, Longitude, Phone, Website,
                   IsApproved, ApprovedBy, ApprovedAt, RejectionReason,
                   SubmittedBy, CreatedAt, UpdatedAt
            FROM Restaurant
            WHERE Id = @Id AND IsDeleted = 0";

        var results = await ExecuteReaderAsync(
            sql,
            reader => MapRestaurantDto(reader),
            CreateParameter("@Id", id));

        return results.FirstOrDefault();
    }

    public async Task<Guid> CreateAsync(CreateRestaurantRequest request, Guid createdBy)
    {
        const string sql = @"
            INSERT INTO Restaurant (
                Id, Name, Brand, CuisineType, Address, City, State, ZipCode,
                Country, Latitude, Longitude, Phone, Website,
                SubmittedBy, CreatedBy, CreatedAt
            )
            VALUES (
                @Id, @Name, @Brand, @CuisineType, @Address, @City, @State, @ZipCode,
                @Country, @Latitude, @Longitude, @Phone, @Website,
                @SubmittedBy, @CreatedBy, GETUTCDATE()
            )";

        var id = Guid.NewGuid();

        await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", id),
            CreateParameter("@Name", request.Name),
            CreateParameter("@Brand", request.Brand),
            CreateParameter("@CuisineType", request.CuisineType),
            CreateParameter("@Address", request.Address),
            CreateParameter("@City", request.City),
            CreateParameter("@State", request.State),
            CreateParameter("@ZipCode", request.ZipCode),
            CreateParameter("@Country", request.Country),
            CreateParameter("@Latitude", request.Latitude),
            CreateParameter("@Longitude", request.Longitude),
            CreateParameter("@Phone", request.Phone),
            CreateParameter("@Website", request.Website),
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
                CuisineType = @CuisineType,
                Address = @Address,
                City = @City,
                State = @State,
                ZipCode = @ZipCode,
                Country = @Country,
                Latitude = @Latitude,
                Longitude = @Longitude,
                Phone = @Phone,
                Website = @Website,
                UpdatedBy = @UpdatedBy,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND IsDeleted = 0";

        var rowsAffected = await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", id),
            CreateParameter("@Name", request.Name),
            CreateParameter("@Brand", request.Brand),
            CreateParameter("@CuisineType", request.CuisineType),
            CreateParameter("@Address", request.Address),
            CreateParameter("@City", request.City),
            CreateParameter("@State", request.State),
            CreateParameter("@ZipCode", request.ZipCode),
            CreateParameter("@Country", request.Country),
            CreateParameter("@Latitude", request.Latitude),
            CreateParameter("@Longitude", request.Longitude),
            CreateParameter("@Phone", request.Phone),
            CreateParameter("@Website", request.Website),
            CreateParameter("@UpdatedBy", updatedBy));

        return rowsAffected > 0;
    }

    public async Task<bool> DeleteAsync(Guid id, Guid deletedBy)
    {
        const string sql = @"
            UPDATE Restaurant
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

    public async Task<bool> ApproveAsync(Guid id, bool approve, Guid approvedBy, string? rejectionReason = null)
    {
        const string sql = @"
            UPDATE Restaurant
            SET IsApproved = @IsApproved,
                ApprovedBy = @ApprovedBy,
                ApprovedAt = GETUTCDATE(),
                RejectionReason = @RejectionReason,
                UpdatedBy = @ApprovedBy,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND IsDeleted = 0";

        var rowsAffected = await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", id),
            CreateParameter("@IsApproved", approve),
            CreateParameter("@ApprovedBy", approvedBy),
            CreateParameter("@RejectionReason", approve ? null : rejectionReason));

        return rowsAffected > 0;
    }

    public async Task<bool> RestaurantExistsAsync(Guid id)
    {
        const string sql = "SELECT COUNT(*) FROM Restaurant WHERE Id = @Id AND IsDeleted = 0";

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
            reader => new UserRestaurantRatingDto
            {
                UserId = GetGuid(reader, "UserId"),
                RestaurantId = GetGuid(reader, "RestaurantId"),
                Rating = GetInt(reader, "Rating") ?? 0,
                Review = GetString(reader, "Review"),
                CreatedAt = GetDateTime(reader, "CreatedAt") ?? DateTime.UtcNow,
                UpdatedAt = GetDateTime(reader, "UpdatedAt")
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
            reader => new UserRestaurantRatingDto
            {
                UserId = GetGuid(reader, "UserId"),
                RestaurantId = GetGuid(reader, "RestaurantId"),
                Rating = GetInt(reader, "Rating") ?? 0,
                Review = GetString(reader, "Review"),
                CreatedAt = GetDateTime(reader, "CreatedAt") ?? DateTime.UtcNow,
                UpdatedAt = GetDateTime(reader, "UpdatedAt")
            },
            CreateParameter("@RestaurantId", restaurantId),
            CreateParameter("@UserId", userId));

        return results.FirstOrDefault();
    }

    public async Task<Guid> AddOrUpdateRatingAsync(Guid restaurantId, Guid userId, RateRestaurantRequest request)
    {
        // Try to update existing rating first
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
            // Updated existing rating
            return Guid.Empty; // Return value not used for updates
        }

        // Insert new rating
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

        return rowsAffected > 0;
    }

    #endregion

    private RestaurantDto MapRestaurantDto(IDataReader reader)
    {
        return new RestaurantDto
        {
            Id = GetGuid(reader, "Id"),
            Name = GetString(reader, "Name") ?? string.Empty,
            Brand = GetString(reader, "Brand"),
            CuisineType = GetString(reader, "CuisineType"),
            Address = GetString(reader, "Address"),
            City = GetString(reader, "City"),
            State = GetString(reader, "State"),
            ZipCode = GetString(reader, "ZipCode"),
            Country = GetString(reader, "Country"),
            Latitude = GetDecimal(reader, "Latitude"),
            Longitude = GetDecimal(reader, "Longitude"),
            Phone = GetString(reader, "Phone"),
            Website = GetString(reader, "Website"),
            IsApproved = GetBool(reader, "IsApproved") ?? false,
            ApprovedBy = GetGuid(reader, "ApprovedBy"),
            ApprovedAt = GetDateTime(reader, "ApprovedAt"),
            RejectionReason = GetString(reader, "RejectionReason"),
            SubmittedBy = GetGuid(reader, "SubmittedBy"),
            CreatedAt = GetDateTime(reader, "CreatedAt") ?? DateTime.UtcNow,
            UpdatedAt = GetDateTime(reader, "UpdatedAt")
        };
    }
}
