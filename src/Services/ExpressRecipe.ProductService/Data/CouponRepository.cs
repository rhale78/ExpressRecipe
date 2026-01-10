using ExpressRecipe.Data.Common;
using ExpressRecipe.Shared.DTOs.Product;
using System.Data;
using Microsoft.Data.SqlClient;

namespace ExpressRecipe.ProductService.Data;

public interface ICouponRepository
{
    Task<CouponDto?> GetByIdAsync(Guid id);
    Task<List<CouponDto>> SearchAsync(CouponSearchRequest request);
    Task<Guid> CreateAsync(CreateCouponRequest request, Guid? createdBy = null);
    Task<bool> UpdateAsync(Guid id, UpdateCouponRequest request, Guid? updatedBy = null);
    Task<bool> DeleteAsync(Guid id, Guid? deletedBy = null);
    Task<List<UserCouponDto>> GetUserCouponsAsync(Guid userId, bool activeOnly = true);
    Task<Guid> ClipCouponAsync(Guid userId, ClipCouponRequest request);
    Task<bool> UseCouponAsync(Guid userId, UseCouponRequest request);
    Task<List<CouponDto>> GetAvailableCouponsForProductAsync(Guid productId, Guid? storeId = null);
}

public class CouponRepository : SqlHelper, ICouponRepository
{
    public CouponRepository(string connectionString) : base(connectionString)
    {
    }

    public async Task<CouponDto?> GetByIdAsync(Guid id)
    {
        const string sql = @"
            SELECT c.Id, c.Code, c.Description, c.CouponType, c.DiscountType, c.DiscountAmount,
                   c.MinimumPurchaseAmount, c.MinimumQuantity, c.MaximumQuantity,
                   c.MaxUsesPerUser, c.ProductId, c.StoreId, c.ManufacturerName,
                   c.ImageUrl, c.SourceUrl, c.CanBeDoubled, c.CanBeCombined,
                   c.RequiresLoyaltyCard, c.StartDate, c.ExpirationDate, c.IsActive,
                   c.SubmittedBy, c.IsApproved, c.ApprovedBy, c.ApprovedAt,
                   c.RejectionReason, c.CreatedAt, c.UpdatedAt,
                   s.ChainName AS StoreName,
                   p.Name AS ProductName
            FROM Coupon c
            LEFT JOIN Store s ON c.StoreId = s.Id
            LEFT JOIN Product p ON c.ProductId = p.Id
            WHERE c.Id = @Id AND c.IsDeleted = 0";

        var results = await ExecuteReaderAsync(
            sql,
            reader => MapCouponDto(reader),
            new SqlParameter("@Id", id));

        return results.FirstOrDefault();
    }

    public async Task<List<CouponDto>> SearchAsync(CouponSearchRequest request)
    {
        var sql = @"
            SELECT c.Id, c.Code, c.Description, c.CouponType, c.DiscountType, c.DiscountAmount,
                   c.MinimumPurchaseAmount, c.MinimumQuantity, c.MaximumQuantity,
                   c.MaxUsesPerUser, c.ProductId, c.StoreId, c.ManufacturerName,
                   c.ImageUrl, c.SourceUrl, c.CanBeDoubled, c.CanBeCombined,
                   c.RequiresLoyaltyCard, c.StartDate, c.ExpirationDate, c.IsActive,
                   c.SubmittedBy, c.IsApproved, c.ApprovedBy, c.ApprovedAt,
                   c.RejectionReason, c.CreatedAt, c.UpdatedAt,
                   s.ChainName AS StoreName,
                   p.Name AS ProductName
            FROM Coupon c
            LEFT JOIN Store s ON c.StoreId = s.Id
            LEFT JOIN Product p ON c.ProductId = p.Id
            WHERE c.IsDeleted = 0";

        var parameters = new List<SqlParameter>();

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            sql += " AND (c.Description LIKE @SearchTerm OR c.Code LIKE @SearchTerm)";
            parameters.Add(new SqlParameter("@SearchTerm", $"%{request.SearchTerm}%"));
        }

        if (!string.IsNullOrWhiteSpace(request.CouponType))
        {
            sql += " AND c.CouponType = @CouponType";
            parameters.Add(new SqlParameter("@CouponType", request.CouponType));
        }

        if (request.StoreId.HasValue)
        {
            sql += " AND (c.StoreId = @StoreId OR c.StoreId IS NULL)";
            parameters.Add(new SqlParameter("@StoreId", request.StoreId.Value));
        }

        if (request.ProductId.HasValue)
        {
            sql += " AND (c.ProductId = @ProductId OR c.ProductId IS NULL)";
            parameters.Add(new SqlParameter("@ProductId", request.ProductId.Value));
        }

        if (!string.IsNullOrWhiteSpace(request.ManufacturerName))
        {
            sql += " AND c.ManufacturerName LIKE @ManufacturerName";
            parameters.Add(new SqlParameter("@ManufacturerName", $"%{request.ManufacturerName}%"));
        }

        if (request.CanBeDoubled.HasValue)
        {
            sql += " AND c.CanBeDoubled = @CanBeDoubled";
            parameters.Add(new SqlParameter("@CanBeDoubled", request.CanBeDoubled.Value));
        }

        if (request.RequiresLoyaltyCard.HasValue)
        {
            sql += " AND c.RequiresLoyaltyCard = @RequiresLoyaltyCard";
            parameters.Add(new SqlParameter("@RequiresLoyaltyCard", request.RequiresLoyaltyCard.Value));
        }

        if (request.OnlyActive)
        {
            sql += " AND c.IsActive = 1";
        }

        if (request.OnlyApproved)
        {
            sql += " AND c.IsApproved = 1";
        }

        if (request.OnlyNotExpired)
        {
            sql += " AND (c.ExpirationDate IS NULL OR c.ExpirationDate >= GETUTCDATE())";
        }

        sql += " ORDER BY c.ExpirationDate, c.Description";

        return await ExecuteReaderAsync(
            sql,
            reader => MapCouponDto(reader),
            parameters.ToArray());
    }

    public async Task<Guid> CreateAsync(CreateCouponRequest request, Guid? createdBy = null)
    {
        var id = Guid.NewGuid();

        const string sql = @"
            INSERT INTO Coupon (Id, Code, Description, CouponType, DiscountType, DiscountAmount,
                              MinimumPurchaseAmount, MinimumQuantity, MaximumQuantity,
                              MaxUsesPerUser, ProductId, StoreId, ManufacturerName,
                              ImageUrl, SourceUrl, CanBeDoubled, CanBeCombined,
                              RequiresLoyaltyCard, StartDate, ExpirationDate, IsActive,
                              SubmittedBy, IsApproved, CreatedAt, IsDeleted)
            VALUES (@Id, @Code, @Description, @CouponType, @DiscountType, @DiscountAmount,
                    @MinimumPurchaseAmount, @MinimumQuantity, @MaximumQuantity,
                    @MaxUsesPerUser, @ProductId, @StoreId, @ManufacturerName,
                    @ImageUrl, @SourceUrl, @CanBeDoubled, @CanBeCombined,
                    @RequiresLoyaltyCard, @StartDate, @ExpirationDate, 1,
                    @CreatedBy, 0, GETUTCDATE(), 0)";

        await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", id),
            new SqlParameter("@Code", (object?)request.Code ?? DBNull.Value),
            new SqlParameter("@Description", request.Description),
            new SqlParameter("@CouponType", request.CouponType),
            new SqlParameter("@DiscountType", request.DiscountType),
            new SqlParameter("@DiscountAmount", (object?)request.DiscountAmount ?? DBNull.Value),
            new SqlParameter("@MinimumPurchaseAmount", (object?)request.MinimumPurchaseAmount ?? DBNull.Value),
            new SqlParameter("@MinimumQuantity", (object?)request.MinimumQuantity ?? DBNull.Value),
            new SqlParameter("@MaximumQuantity", (object?)request.MaximumQuantity ?? DBNull.Value),
            new SqlParameter("@MaxUsesPerUser", (object?)request.MaxUsesPerUser ?? DBNull.Value),
            new SqlParameter("@ProductId", (object?)request.ProductId ?? DBNull.Value),
            new SqlParameter("@StoreId", (object?)request.StoreId ?? DBNull.Value),
            new SqlParameter("@ManufacturerName", (object?)request.ManufacturerName ?? DBNull.Value),
            new SqlParameter("@ImageUrl", (object?)request.ImageUrl ?? DBNull.Value),
            new SqlParameter("@SourceUrl", (object?)request.SourceUrl ?? DBNull.Value),
            new SqlParameter("@CanBeDoubled", request.CanBeDoubled),
            new SqlParameter("@CanBeCombined", request.CanBeCombined),
            new SqlParameter("@RequiresLoyaltyCard", request.RequiresLoyaltyCard),
            new SqlParameter("@StartDate", (object?)request.StartDate ?? DBNull.Value),
            new SqlParameter("@ExpirationDate", (object?)request.ExpirationDate ?? DBNull.Value),
            new SqlParameter("@CreatedBy", (object?)createdBy ?? DBNull.Value));

        return id;
    }

    public async Task<bool> UpdateAsync(Guid id, UpdateCouponRequest request, Guid? updatedBy = null)
    {
        const string sql = @"
            UPDATE Coupon
            SET Description = @Description,
                DiscountAmount = @DiscountAmount,
                MinimumPurchaseAmount = @MinimumPurchaseAmount,
                ExpirationDate = @ExpirationDate,
                ImageUrl = @ImageUrl,
                IsActive = @IsActive,
                UpdatedAt = GETUTCDATE(),
                UpdatedBy = @UpdatedBy
            WHERE Id = @Id AND IsDeleted = 0";

        var rowsAffected = await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", id),
            new SqlParameter("@Description", request.Description),
            new SqlParameter("@DiscountAmount", (object?)request.DiscountAmount ?? DBNull.Value),
            new SqlParameter("@MinimumPurchaseAmount", (object?)request.MinimumPurchaseAmount ?? DBNull.Value),
            new SqlParameter("@ExpirationDate", (object?)request.ExpirationDate ?? DBNull.Value),
            new SqlParameter("@ImageUrl", (object?)request.ImageUrl ?? DBNull.Value),
            new SqlParameter("@IsActive", request.IsActive),
            new SqlParameter("@UpdatedBy", (object?)updatedBy ?? DBNull.Value));

        return rowsAffected > 0;
    }

    public async Task<bool> DeleteAsync(Guid id, Guid? deletedBy = null)
    {
        const string sql = @"
            UPDATE Coupon
            SET IsDeleted = 1,
                DeletedAt = GETUTCDATE(),
                DeletedBy = @DeletedBy
            WHERE Id = @Id";

        var rowsAffected = await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", id),
            new SqlParameter("@DeletedBy", (object?)deletedBy ?? DBNull.Value));

        return rowsAffected > 0;
    }

    public async Task<List<UserCouponDto>> GetUserCouponsAsync(Guid userId, bool activeOnly = true)
    {
        var sql = @"
            SELECT uc.Id, uc.UserId, uc.CouponId, uc.ClippedAt, uc.UsedAt,
                   uc.UsedAtStoreId, uc.SavedAmount, uc.Notes,
                   s.ChainName AS UsedAtStoreName
            FROM UserCoupon uc
            LEFT JOIN Store s ON uc.UsedAtStoreId = s.Id
            WHERE uc.UserId = @UserId";

        if (activeOnly)
        {
            sql += " AND uc.UsedAt IS NULL";
        }

        sql += " ORDER BY uc.ClippedAt DESC";

        return await ExecuteReaderAsync(
            sql,
            reader => new UserCouponDto
            {
                Id = GetGuid(reader, "Id"),
                UserId = GetGuid(reader, "UserId"),
                CouponId = GetGuid(reader, "CouponId"),
                ClippedAt = GetNullableDateTime(reader, "ClippedAt") ?? DateTime.UtcNow,
                UsedAt = GetDateTime(reader, "UsedAt"),
                UsedAtStoreId = GetGuidNullable(reader, "UsedAtStoreId"),
                UsedAtStoreName = GetString(reader, "UsedAtStoreName"),
                SavedAmount = GetDecimalNullable(reader, "SavedAmount"),
                Notes = GetString(reader, "Notes")
            },
            new SqlParameter("@UserId", userId));
    }

    public async Task<Guid> ClipCouponAsync(Guid userId, ClipCouponRequest request)
    {
        var id = Guid.NewGuid();

        const string sql = @"
            INSERT INTO UserCoupon (Id, UserId, CouponId, ClippedAt, Notes)
            VALUES (@Id, @UserId, @CouponId, GETUTCDATE(), @Notes)";

        await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", id),
            new SqlParameter("@UserId", userId),
            new SqlParameter("@CouponId", request.CouponId),
            new SqlParameter("@Notes", (object?)request.Notes ?? DBNull.Value));

        return id;
    }

    public async Task<bool> UseCouponAsync(Guid userId, UseCouponRequest request)
    {
        const string sql = @"
            UPDATE UserCoupon
            SET UsedAt = GETUTCDATE(),
                UsedAtStoreId = @StoreId,
                SavedAmount = @SavedAmount,
                Notes = @Notes
            WHERE CouponId = @CouponId
              AND UserId = @UserId
              AND UsedAt IS NULL";

        var rowsAffected = await ExecuteNonQueryAsync(sql,
            new SqlParameter("@CouponId", request.CouponId),
            new SqlParameter("@UserId", userId),
            new SqlParameter("@StoreId", request.StoreId),
            new SqlParameter("@SavedAmount", request.SavedAmount),
            new SqlParameter("@Notes", (object?)request.Notes ?? DBNull.Value));

        return rowsAffected > 0;
    }

    public async Task<List<CouponDto>> GetAvailableCouponsForProductAsync(Guid productId, Guid? storeId = null)
    {
        var sql = @"
            SELECT c.Id, c.Code, c.Description, c.CouponType, c.DiscountType, c.DiscountAmount,
                   c.MinimumPurchaseAmount, c.MinimumQuantity, c.MaximumQuantity,
                   c.MaxUsesPerUser, c.ProductId, c.StoreId, c.ManufacturerName,
                   c.ImageUrl, c.SourceUrl, c.CanBeDoubled, c.CanBeCombined,
                   c.RequiresLoyaltyCard, c.StartDate, c.ExpirationDate, c.IsActive,
                   c.SubmittedBy, c.IsApproved, c.ApprovedBy, c.ApprovedAt,
                   c.RejectionReason, c.CreatedAt, c.UpdatedAt,
                   s.ChainName AS StoreName,
                   p.Name AS ProductName
            FROM Coupon c
            LEFT JOIN Store s ON c.StoreId = s.Id
            LEFT JOIN Product p ON c.ProductId = p.Id
            WHERE c.IsDeleted = 0
              AND c.IsActive = 1
              AND c.IsApproved = 1
              AND (c.ExpirationDate IS NULL OR c.ExpirationDate >= GETUTCDATE())
              AND (c.ProductId = @ProductId OR c.ProductId IS NULL)";

        var parameters = new List<SqlParameter>
        {
            new SqlParameter("@ProductId", productId)
        };

        if (storeId.HasValue)
        {
            sql += " AND (c.StoreId = @StoreId OR c.StoreId IS NULL)";
            parameters.Add(new SqlParameter("@StoreId", storeId.Value));
        }

        sql += " ORDER BY c.DiscountAmount DESC";

        return await ExecuteReaderAsync(
            sql,
            reader => MapCouponDto(reader),
            parameters.ToArray());
    }

    private CouponDto MapCouponDto(SqlDataReader reader)
    {
        return new CouponDto
        {
            Id = GetGuid(reader, "Id"),
            Code = GetString(reader, "Code"),
            Description = GetString(reader, "Description") ?? string.Empty,
            CouponType = GetString(reader, "CouponType") ?? string.Empty,
            DiscountType = GetString(reader, "DiscountType") ?? string.Empty,
            DiscountAmount = GetDecimalNullable(reader, "DiscountAmount"),
            MinimumPurchaseAmount = GetDecimalNullable(reader, "MinimumPurchaseAmount"),
            MinimumQuantity = GetIntNullable(reader, "MinimumQuantity"),
            MaximumQuantity = GetIntNullable(reader, "MaximumQuantity"),
            MaxUsesPerUser = GetIntNullable(reader, "MaxUsesPerUser"),
            ProductId = GetGuidNullable(reader, "ProductId"),
            ProductName = GetString(reader, "ProductName"),
            StoreId = GetGuidNullable(reader, "StoreId"),
            StoreName = GetString(reader, "StoreName"),
            ManufacturerName = GetString(reader, "ManufacturerName"),
            ImageUrl = GetString(reader, "ImageUrl"),
            SourceUrl = GetString(reader, "SourceUrl"),
            CanBeDoubled = GetBoolean(reader, "CanBeDoubled"),
            CanBeCombined = GetBoolean(reader, "CanBeCombined"),
            RequiresLoyaltyCard = GetBoolean(reader, "RequiresLoyaltyCard"),
            StartDate = GetDateTime(reader, "StartDate"),
            ExpirationDate = GetDateTime(reader, "ExpirationDate"),
            IsActive = GetBoolean(reader, "IsActive"),
            SubmittedBy = GetGuid(reader, "SubmittedBy"),
            IsApproved = GetBoolean(reader, "IsApproved"),
            ApprovedBy = GetGuidNullable(reader, "ApprovedBy"),
            ApprovedAt = GetDateTime(reader, "ApprovedAt"),
            RejectionReason = GetString(reader, "RejectionReason"),
            CreatedAt = GetNullableDateTime(reader, "CreatedAt") ?? DateTime.UtcNow,
            UpdatedAt = GetDateTime(reader, "UpdatedAt")
        };
    }
}

