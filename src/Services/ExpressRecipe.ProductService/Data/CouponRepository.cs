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
                   c.DiscountPercentage, c.MinimumPurchaseAmount, c.MaximumDiscount,
                   c.BuyQuantity, c.GetQuantity, c.RequiredProductId, c.FreeProductId,
                   c.CategoryRestriction, c.BrandRestriction, c.StoreId, c.ManufacturerId,
                   c.CanBeDoubled, c.MaxItemsAllowed, c.UseLimitPerUser, c.TotalUseLimit,
                   c.UsedCount, c.StartDate, c.ExpirationDate, c.ImageUrl, c.BarcodeValue,
                   c.CouponUrl, c.IsActive, c.CreatedAt,
                   s.Name AS StoreName
            FROM Coupon c
            LEFT JOIN Store s ON c.StoreId = s.Id
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
                   c.DiscountPercentage, c.MinimumPurchaseAmount, c.MaximumDiscount,
                   c.BuyQuantity, c.GetQuantity, c.RequiredProductId, c.FreeProductId,
                   c.CategoryRestriction, c.BrandRestriction, c.StoreId, c.ManufacturerId,
                   c.CanBeDoubled, c.MaxItemsAllowed, c.UseLimitPerUser, c.TotalUseLimit,
                   c.UsedCount, c.StartDate, c.ExpirationDate, c.ImageUrl, c.BarcodeValue,
                   c.CouponUrl, c.IsActive, c.CreatedAt,
                   s.Name AS StoreName
            FROM Coupon c
            LEFT JOIN Store s ON c.StoreId = s.Id
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
            sql += " AND (c.RequiredProductId = @ProductId OR c.FreeProductId = @ProductId OR c.RequiredProductId IS NULL)";
            parameters.Add(new SqlParameter("@ProductId", request.ProductId.Value));
        }

        if (request.ActiveOnly)
        {
            sql += " AND c.IsActive = 1 AND (c.ExpirationDate IS NULL OR c.ExpirationDate >= GETUTCDATE())";
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
                              DiscountPercentage, MinimumPurchaseAmount, MaximumDiscount,
                              BuyQuantity, GetQuantity, RequiredProductId, FreeProductId,
                              CategoryRestriction, BrandRestriction, StoreId, ManufacturerId,
                              CanBeDoubled, MaxItemsAllowed, UseLimitPerUser, TotalUseLimit,
                              UsedCount, StartDate, ExpirationDate, ImageUrl, BarcodeValue,
                              CouponUrl, IsActive, CreatedAt, CreatedBy)
            VALUES (@Id, @Code, @Description, @CouponType, @DiscountType, @DiscountAmount,
                    @DiscountPercentage, @MinimumPurchaseAmount, @MaximumDiscount,
                    @BuyQuantity, @GetQuantity, @RequiredProductId, @FreeProductId,
                    @CategoryRestriction, @BrandRestriction, @StoreId, @ManufacturerId,
                    @CanBeDoubled, @MaxItemsAllowed, @UseLimitPerUser, @TotalUseLimit,
                    0, @StartDate, @ExpirationDate, @ImageUrl, @BarcodeValue,
                    @CouponUrl, @IsActive, GETUTCDATE(), @CreatedBy)";

        await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", id),
            new SqlParameter("@Code", (object?)request.Code ?? DBNull.Value),
            new SqlParameter("@Description", request.Description),
            new SqlParameter("@CouponType", request.CouponType),
            new SqlParameter("@DiscountType", request.DiscountType),
            new SqlParameter("@DiscountAmount", (object?)request.DiscountAmount ?? DBNull.Value),
            new SqlParameter("@DiscountPercentage", (object?)request.DiscountPercentage ?? DBNull.Value),
            new SqlParameter("@MinimumPurchaseAmount", (object?)request.MinimumPurchaseAmount ?? DBNull.Value),
            new SqlParameter("@MaximumDiscount", (object?)request.MaximumDiscount ?? DBNull.Value),
            new SqlParameter("@BuyQuantity", (object?)request.BuyQuantity ?? DBNull.Value),
            new SqlParameter("@GetQuantity", (object?)request.GetQuantity ?? DBNull.Value),
            new SqlParameter("@RequiredProductId", (object?)request.RequiredProductId ?? DBNull.Value),
            new SqlParameter("@FreeProductId", (object?)request.FreeProductId ?? DBNull.Value),
            new SqlParameter("@CategoryRestriction", (object?)request.CategoryRestriction ?? DBNull.Value),
            new SqlParameter("@BrandRestriction", (object?)request.BrandRestriction ?? DBNull.Value),
            new SqlParameter("@StoreId", (object?)request.StoreId ?? DBNull.Value),
            new SqlParameter("@ManufacturerId", (object?)request.ManufacturerId ?? DBNull.Value),
            new SqlParameter("@CanBeDoubled", request.CanBeDoubled),
            new SqlParameter("@MaxItemsAllowed", (object?)request.MaxItemsAllowed ?? DBNull.Value),
            new SqlParameter("@UseLimitPerUser", (object?)request.UseLimitPerUser ?? DBNull.Value),
            new SqlParameter("@TotalUseLimit", (object?)request.TotalUseLimit ?? DBNull.Value),
            new SqlParameter("@StartDate", (object?)request.StartDate ?? DBNull.Value),
            new SqlParameter("@ExpirationDate", (object?)request.ExpirationDate ?? DBNull.Value),
            new SqlParameter("@ImageUrl", (object?)request.ImageUrl ?? DBNull.Value),
            new SqlParameter("@BarcodeValue", (object?)request.BarcodeValue ?? DBNull.Value),
            new SqlParameter("@CouponUrl", (object?)request.CouponUrl ?? DBNull.Value),
            new SqlParameter("@IsActive", request.IsActive),
            new SqlParameter("@CreatedBy", (object?)createdBy ?? DBNull.Value));

        return id;
    }

    public async Task<bool> UpdateAsync(Guid id, UpdateCouponRequest request, Guid? updatedBy = null)
    {
        const string sql = @"
            UPDATE Coupon
            SET Description = @Description,
                DiscountAmount = @DiscountAmount,
                DiscountPercentage = @DiscountPercentage,
                MinimumPurchaseAmount = @MinimumPurchaseAmount,
                MaximumDiscount = @MaximumDiscount,
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
            new SqlParameter("@DiscountPercentage", (object?)request.DiscountPercentage ?? DBNull.Value),
            new SqlParameter("@MinimumPurchaseAmount", (object?)request.MinimumPurchaseAmount ?? DBNull.Value),
            new SqlParameter("@MaximumDiscount", (object?)request.MaximumDiscount ?? DBNull.Value),
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
                   uc.PurchaseId, uc.IsUsed, uc.ExpiresAt,
                   c.Description AS CouponDescription,
                   c.DiscountType, c.DiscountAmount, c.DiscountPercentage
            FROM UserCoupon uc
            INNER JOIN Coupon c ON uc.CouponId = c.Id
            WHERE uc.UserId = @UserId";

        if (activeOnly)
        {
            sql += " AND uc.IsUsed = 0 AND (uc.ExpiresAt IS NULL OR uc.ExpiresAt >= GETUTCDATE())";
        }

        sql += " ORDER BY uc.ExpiresAt, uc.ClippedAt DESC";

        return await ExecuteReaderAsync(
            sql,
            reader => new UserCouponDto
            {
                Id = GetGuid(reader, "Id"),
                UserId = GetGuid(reader, "UserId"),
                CouponId = GetGuid(reader, "CouponId"),
                CouponDescription = GetString(reader, "CouponDescription"),
                DiscountType = GetString(reader, "DiscountType"),
                DiscountAmount = GetDecimalNullable(reader, "DiscountAmount"),
                DiscountPercentage = GetDecimalNullable(reader, "DiscountPercentage"),
                ClippedAt = GetDateTime(reader, "ClippedAt") ?? DateTime.UtcNow,
                UsedAt = GetDateTime(reader, "UsedAt"),
                PurchaseId = GetGuidNullable(reader, "PurchaseId"),
                IsUsed = GetBoolean(reader, "IsUsed"),
                ExpiresAt = GetDateTime(reader, "ExpiresAt")
            },
            new SqlParameter("@UserId", userId));
    }

    public async Task<Guid> ClipCouponAsync(Guid userId, ClipCouponRequest request)
    {
        var id = Guid.NewGuid();

        const string sql = @"
            INSERT INTO UserCoupon (Id, UserId, CouponId, ClippedAt, IsUsed, ExpiresAt)
            VALUES (@Id, @UserId, @CouponId, GETUTCDATE(), 0, @ExpiresAt)";

        await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", id),
            new SqlParameter("@UserId", userId),
            new SqlParameter("@CouponId", request.CouponId),
            new SqlParameter("@ExpiresAt", (object?)request.ExpiresAt ?? DBNull.Value));

        return id;
    }

    public async Task<bool> UseCouponAsync(Guid userId, UseCouponRequest request)
    {
        const string sql = @"
            UPDATE UserCoupon
            SET IsUsed = 1,
                UsedAt = GETUTCDATE(),
                PurchaseId = @PurchaseId
            WHERE Id = @UserCouponId
              AND UserId = @UserId
              AND IsUsed = 0";

        var rowsAffected = await ExecuteNonQueryAsync(sql,
            new SqlParameter("@UserCouponId", request.UserCouponId),
            new SqlParameter("@UserId", userId),
            new SqlParameter("@PurchaseId", (object?)request.PurchaseId ?? DBNull.Value));

        // Increment usage count on the coupon
        if (rowsAffected > 0)
        {
            const string updateCouponSql = @"
                UPDATE Coupon
                SET UsedCount = UsedCount + 1
                FROM Coupon c
                INNER JOIN UserCoupon uc ON c.Id = uc.CouponId
                WHERE uc.Id = @UserCouponId";

            await ExecuteNonQueryAsync(updateCouponSql,
                new SqlParameter("@UserCouponId", request.UserCouponId));
        }

        return rowsAffected > 0;
    }

    public async Task<List<CouponDto>> GetAvailableCouponsForProductAsync(Guid productId, Guid? storeId = null)
    {
        var sql = @"
            SELECT c.Id, c.Code, c.Description, c.CouponType, c.DiscountType, c.DiscountAmount,
                   c.DiscountPercentage, c.MinimumPurchaseAmount, c.MaximumDiscount,
                   c.BuyQuantity, c.GetQuantity, c.RequiredProductId, c.FreeProductId,
                   c.CategoryRestriction, c.BrandRestriction, c.StoreId, c.ManufacturerId,
                   c.CanBeDoubled, c.MaxItemsAllowed, c.UseLimitPerUser, c.TotalUseLimit,
                   c.UsedCount, c.StartDate, c.ExpirationDate, c.ImageUrl, c.BarcodeValue,
                   c.CouponUrl, c.IsActive, c.CreatedAt,
                   s.Name AS StoreName
            FROM Coupon c
            LEFT JOIN Store s ON c.StoreId = s.Id
            WHERE c.IsDeleted = 0
              AND c.IsActive = 1
              AND (c.ExpirationDate IS NULL OR c.ExpirationDate >= GETUTCDATE())
              AND (c.RequiredProductId = @ProductId OR c.FreeProductId = @ProductId OR c.RequiredProductId IS NULL)";

        var parameters = new List<SqlParameter>
        {
            new SqlParameter("@ProductId", productId)
        };

        if (storeId.HasValue)
        {
            sql += " AND (c.StoreId = @StoreId OR c.StoreId IS NULL)";
            parameters.Add(new SqlParameter("@StoreId", storeId.Value));
        }

        sql += " ORDER BY c.DiscountAmount DESC, c.DiscountPercentage DESC";

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
            DiscountPercentage = GetDecimalNullable(reader, "DiscountPercentage"),
            MinimumPurchaseAmount = GetDecimalNullable(reader, "MinimumPurchaseAmount"),
            MaximumDiscount = GetDecimalNullable(reader, "MaximumDiscount"),
            BuyQuantity = GetIntNullable(reader, "BuyQuantity"),
            GetQuantity = GetIntNullable(reader, "GetQuantity"),
            RequiredProductId = GetGuidNullable(reader, "RequiredProductId"),
            FreeProductId = GetGuidNullable(reader, "FreeProductId"),
            CategoryRestriction = GetString(reader, "CategoryRestriction"),
            BrandRestriction = GetString(reader, "BrandRestriction"),
            StoreId = GetGuidNullable(reader, "StoreId"),
            StoreName = GetString(reader, "StoreName"),
            ManufacturerId = GetGuidNullable(reader, "ManufacturerId"),
            CanBeDoubled = GetBoolean(reader, "CanBeDoubled"),
            MaxItemsAllowed = GetIntNullable(reader, "MaxItemsAllowed"),
            UseLimitPerUser = GetIntNullable(reader, "UseLimitPerUser"),
            TotalUseLimit = GetIntNullable(reader, "TotalUseLimit"),
            UsedCount = GetInt(reader, "UsedCount"),
            StartDate = GetDateTime(reader, "StartDate"),
            ExpirationDate = GetDateTime(reader, "ExpirationDate"),
            ImageUrl = GetString(reader, "ImageUrl"),
            BarcodeValue = GetString(reader, "BarcodeValue"),
            CouponUrl = GetString(reader, "CouponUrl"),
            IsActive = GetBoolean(reader, "IsActive"),
            CreatedAt = GetDateTime(reader, "CreatedAt") ?? DateTime.UtcNow
        };
    }
}
