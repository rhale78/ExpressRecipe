using ExpressRecipe.Data.Common;
using ExpressRecipe.Shared.DTOs.Product;
using System.Data;
using Microsoft.Data.SqlClient;

namespace ExpressRecipe.ProductService.Data;

public interface IStoreRepository
{
    Task<StoreDto?> GetByIdAsync(Guid id);
    Task<List<StoreDto>> SearchAsync(StoreSearchRequest request);
    Task<Guid> CreateAsync(CreateStoreRequest request, Guid? createdBy = null);
    Task<bool> UpdateAsync(Guid id, UpdateStoreRequest request, Guid? updatedBy = null);
    Task<bool> DeleteAsync(Guid id, Guid? deletedBy = null);
    Task<bool> StoreExistsAsync(Guid id);
    Task<List<StoreDto>> GetNearbyStoresAsync(decimal latitude, decimal longitude, double radiusMiles);
}

public class StoreRepository : SqlHelper, IStoreRepository
{
    public StoreRepository(string connectionString) : base(connectionString)
    {
    }

    public async Task<StoreDto?> GetByIdAsync(Guid id)
    {
        const string sql = @"
            SELECT Id, ChainName, StoreNumber, Name, Address, City, State, ZipCode,
                   Country, Phone, Email, Website, Latitude, Longitude,
                   AcceptsManufacturerCoupons, AcceptsStoreCoupons, AcceptsDigitalCoupons,
                   DoublesManufacturerCoupons, MaxCouponDoubleValue,
                   CouponPolicy, Hours, IsActive
            FROM Store
            WHERE Id = @Id AND IsDeleted = 0";

        var results = await ExecuteReaderAsync(
            sql,
            reader => new StoreDto
            {
                Id = GetGuid(reader, "Id"),
                ChainName = GetString(reader, "ChainName") ?? string.Empty,
                StoreNumber = GetString(reader, "StoreNumber"),
                Name = GetString(reader, "Name") ?? string.Empty,
                Address = GetString(reader, "Address") ?? string.Empty,
                City = GetString(reader, "City") ?? string.Empty,
                State = GetString(reader, "State") ?? string.Empty,
                ZipCode = GetString(reader, "ZipCode") ?? string.Empty,
                Country = GetString(reader, "Country") ?? "USA",
                Phone = GetString(reader, "Phone"),
                Email = GetString(reader, "Email"),
                Website = GetString(reader, "Website"),
                Latitude = GetDecimalNullable(reader, "Latitude"),
                Longitude = GetDecimalNullable(reader, "Longitude"),
                AcceptsManufacturerCoupons = GetBoolean(reader, "AcceptsManufacturerCoupons"),
                AcceptsStoreCoupons = GetBoolean(reader, "AcceptsStoreCoupons"),
                AcceptsDigitalCoupons = GetBoolean(reader, "AcceptsDigitalCoupons"),
                DoublesManufacturerCoupons = GetBoolean(reader, "DoublesManufacturerCoupons"),
                MaxCouponDoubleValue = GetDecimalNullable(reader, "MaxCouponDoubleValue"),
                CouponPolicy = GetString(reader, "CouponPolicy"),
                Hours = GetString(reader, "Hours"),
                IsActive = GetBoolean(reader, "IsActive")
            },
            new SqlParameter("@Id", id));

        return results.FirstOrDefault();
    }

    public async Task<List<StoreDto>> SearchAsync(StoreSearchRequest request)
    {
        var sql = @"
            SELECT Id, ChainName, StoreNumber, Name, Address, City, State, ZipCode,
                   Country, Phone, Email, Website, Latitude, Longitude,
                   AcceptsManufacturerCoupons, AcceptsStoreCoupons, AcceptsDigitalCoupons,
                   DoublesManufacturerCoupons, MaxCouponDoubleValue,
                   CouponPolicy, Hours, IsActive
            FROM Store
            WHERE IsDeleted = 0";

        var parameters = new List<SqlParameter>();

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            sql += " AND (ChainName LIKE @SearchTerm OR Name LIKE @SearchTerm OR City LIKE @SearchTerm)";
            parameters.Add(new SqlParameter("@SearchTerm", $"%{request.SearchTerm}%"));
        }

        if (!string.IsNullOrWhiteSpace(request.ChainName))
        {
            sql += " AND ChainName = @ChainName";
            parameters.Add(new SqlParameter("@ChainName", request.ChainName));
        }

        if (!string.IsNullOrWhiteSpace(request.City))
        {
            sql += " AND City = @City";
            parameters.Add(new SqlParameter("@City", request.City));
        }

        if (!string.IsNullOrWhiteSpace(request.State))
        {
            sql += " AND State = @State";
            parameters.Add(new SqlParameter("@State", request.State));
        }

        if (!string.IsNullOrWhiteSpace(request.ZipCode))
        {
            sql += " AND ZipCode = @ZipCode";
            parameters.Add(new SqlParameter("@ZipCode", request.ZipCode));
        }

        if (request.ActiveOnly)
        {
            sql += " AND IsActive = 1";
        }

        sql += " ORDER BY ChainName, StoreNumber";

        return await ExecuteReaderAsync(
            sql,
            reader => new StoreDto
            {
                Id = GetGuid(reader, "Id"),
                ChainName = GetString(reader, "ChainName") ?? string.Empty,
                StoreNumber = GetString(reader, "StoreNumber"),
                Name = GetString(reader, "Name") ?? string.Empty,
                Address = GetString(reader, "Address") ?? string.Empty,
                City = GetString(reader, "City") ?? string.Empty,
                State = GetString(reader, "State") ?? string.Empty,
                ZipCode = GetString(reader, "ZipCode") ?? string.Empty,
                Country = GetString(reader, "Country") ?? "USA",
                Phone = GetString(reader, "Phone"),
                Email = GetString(reader, "Email"),
                Website = GetString(reader, "Website"),
                Latitude = GetDecimalNullable(reader, "Latitude"),
                Longitude = GetDecimalNullable(reader, "Longitude"),
                AcceptsManufacturerCoupons = GetBoolean(reader, "AcceptsManufacturerCoupons"),
                AcceptsStoreCoupons = GetBoolean(reader, "AcceptsStoreCoupons"),
                AcceptsDigitalCoupons = GetBoolean(reader, "AcceptsDigitalCoupons"),
                DoublesManufacturerCoupons = GetBoolean(reader, "DoublesManufacturerCoupons"),
                MaxCouponDoubleValue = GetDecimalNullable(reader, "MaxCouponDoubleValue"),
                CouponPolicy = GetString(reader, "CouponPolicy"),
                Hours = GetString(reader, "Hours"),
                IsActive = GetBoolean(reader, "IsActive")
            },
            parameters.ToArray());
    }

    public async Task<Guid> CreateAsync(CreateStoreRequest request, Guid? createdBy = null)
    {
        var id = Guid.NewGuid();

        const string sql = @"
            INSERT INTO Store (Id, ChainName, StoreNumber, Name, Address, City, State, ZipCode,
                             Country, Phone, Email, Website, Latitude, Longitude,
                             AcceptsManufacturerCoupons, AcceptsStoreCoupons, AcceptsDigitalCoupons,
                             DoublesManufacturerCoupons, MaxCouponDoubleValue, CouponPolicy, Hours,
                             IsActive, CreatedAt, CreatedBy)
            VALUES (@Id, @ChainName, @StoreNumber, @Name, @Address, @City, @State, @ZipCode,
                    @Country, @Phone, @Email, @Website, @Latitude, @Longitude,
                    @AcceptsManufacturerCoupons, @AcceptsStoreCoupons, @AcceptsDigitalCoupons,
                    @DoublesManufacturerCoupons, @MaxCouponDoubleValue, @CouponPolicy, @Hours,
                    @IsActive, GETUTCDATE(), @CreatedBy)";

        await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", id),
            new SqlParameter("@ChainName", request.ChainName),
            new SqlParameter("@StoreNumber", (object?)request.StoreNumber ?? DBNull.Value),
            new SqlParameter("@Name", request.Name),
            new SqlParameter("@Address", request.Address),
            new SqlParameter("@City", request.City),
            new SqlParameter("@State", request.State),
            new SqlParameter("@ZipCode", request.ZipCode),
            new SqlParameter("@Country", request.Country ?? "USA"),
            new SqlParameter("@Phone", (object?)request.Phone ?? DBNull.Value),
            new SqlParameter("@Email", (object?)request.Email ?? DBNull.Value),
            new SqlParameter("@Website", (object?)request.Website ?? DBNull.Value),
            new SqlParameter("@Latitude", (object?)request.Latitude ?? DBNull.Value),
            new SqlParameter("@Longitude", (object?)request.Longitude ?? DBNull.Value),
            new SqlParameter("@AcceptsManufacturerCoupons", request.AcceptsManufacturerCoupons),
            new SqlParameter("@AcceptsStoreCoupons", request.AcceptsStoreCoupons),
            new SqlParameter("@AcceptsDigitalCoupons", request.AcceptsDigitalCoupons),
            new SqlParameter("@DoublesManufacturerCoupons", request.DoublesManufacturerCoupons),
            new SqlParameter("@MaxCouponDoubleValue", (object?)request.MaxCouponDoubleValue ?? DBNull.Value),
            new SqlParameter("@CouponPolicy", (object?)request.CouponPolicy ?? DBNull.Value),
            new SqlParameter("@Hours", (object?)request.Hours ?? DBNull.Value),
            new SqlParameter("@IsActive", request.IsActive),
            new SqlParameter("@CreatedBy", (object?)createdBy ?? DBNull.Value));

        return id;
    }

    public async Task<bool> UpdateAsync(Guid id, UpdateStoreRequest request, Guid? updatedBy = null)
    {
        const string sql = @"
            UPDATE Store
            SET ChainName = @ChainName,
                StoreNumber = @StoreNumber,
                Name = @Name,
                Address = @Address,
                City = @City,
                State = @State,
                ZipCode = @ZipCode,
                Country = @Country,
                Phone = @Phone,
                Email = @Email,
                Website = @Website,
                Latitude = @Latitude,
                Longitude = @Longitude,
                AcceptsManufacturerCoupons = @AcceptsManufacturerCoupons,
                AcceptsStoreCoupons = @AcceptsStoreCoupons,
                AcceptsDigitalCoupons = @AcceptsDigitalCoupons,
                DoublesManufacturerCoupons = @DoublesManufacturerCoupons,
                MaxCouponDoubleValue = @MaxCouponDoubleValue,
                CouponPolicy = @CouponPolicy,
                Hours = @Hours,
                IsActive = @IsActive,
                UpdatedAt = GETUTCDATE(),
                UpdatedBy = @UpdatedBy
            WHERE Id = @Id AND IsDeleted = 0";

        var rowsAffected = await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", id),
            new SqlParameter("@ChainName", request.ChainName),
            new SqlParameter("@StoreNumber", (object?)request.StoreNumber ?? DBNull.Value),
            new SqlParameter("@Name", request.Name),
            new SqlParameter("@Address", request.Address),
            new SqlParameter("@City", request.City),
            new SqlParameter("@State", request.State),
            new SqlParameter("@ZipCode", request.ZipCode),
            new SqlParameter("@Country", request.Country ?? "USA"),
            new SqlParameter("@Phone", (object?)request.Phone ?? DBNull.Value),
            new SqlParameter("@Email", (object?)request.Email ?? DBNull.Value),
            new SqlParameter("@Website", (object?)request.Website ?? DBNull.Value),
            new SqlParameter("@Latitude", (object?)request.Latitude ?? DBNull.Value),
            new SqlParameter("@Longitude", (object?)request.Longitude ?? DBNull.Value),
            new SqlParameter("@AcceptsManufacturerCoupons", request.AcceptsManufacturerCoupons),
            new SqlParameter("@AcceptsStoreCoupons", request.AcceptsStoreCoupons),
            new SqlParameter("@AcceptsDigitalCoupons", request.AcceptsDigitalCoupons),
            new SqlParameter("@DoublesManufacturerCoupons", request.DoublesManufacturerCoupons),
            new SqlParameter("@MaxCouponDoubleValue", (object?)request.MaxCouponDoubleValue ?? DBNull.Value),
            new SqlParameter("@CouponPolicy", (object?)request.CouponPolicy ?? DBNull.Value),
            new SqlParameter("@Hours", (object?)request.Hours ?? DBNull.Value),
            new SqlParameter("@IsActive", request.IsActive),
            new SqlParameter("@UpdatedBy", (object?)updatedBy ?? DBNull.Value));

        return rowsAffected > 0;
    }

    public async Task<bool> DeleteAsync(Guid id, Guid? deletedBy = null)
    {
        const string sql = @"
            UPDATE Store
            SET IsDeleted = 1,
                DeletedAt = GETUTCDATE(),
                DeletedBy = @DeletedBy
            WHERE Id = @Id";

        var rowsAffected = await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", id),
            new SqlParameter("@DeletedBy", (object?)deletedBy ?? DBNull.Value));

        return rowsAffected > 0;
    }

    public async Task<bool> StoreExistsAsync(Guid id)
    {
        const string sql = "SELECT COUNT(1) FROM Store WHERE Id = @Id AND IsDeleted = 0";

        var count = await ExecuteScalarAsync<int>(sql, new SqlParameter("@Id", id));

        return count > 0;
    }

    public async Task<List<StoreDto>> GetNearbyStoresAsync(decimal latitude, decimal longitude, double radiusMiles)
    {
        // Using Haversine formula for distance calculation
        const string sql = @"
            SELECT TOP 50
                   Id, ChainName, StoreNumber, Name, Address, City, State, ZipCode,
                   Country, Phone, Email, Website, Latitude, Longitude,
                   AcceptsManufacturerCoupons, AcceptsStoreCoupons, AcceptsDigitalCoupons,
                   DoublesManufacturerCoupons, MaxCouponDoubleValue,
                   CouponPolicy, Hours, IsActive,
                   (3959 * ACOS(
                       COS(RADIANS(@Latitude)) * COS(RADIANS(Latitude)) *
                       COS(RADIANS(Longitude) - RADIANS(@Longitude)) +
                       SIN(RADIANS(@Latitude)) * SIN(RADIANS(Latitude))
                   )) AS Distance
            FROM Store
            WHERE IsDeleted = 0
                  AND IsActive = 1
                  AND Latitude IS NOT NULL
                  AND Longitude IS NOT NULL
            HAVING Distance <= @RadiusMiles
            ORDER BY Distance";

        return await ExecuteReaderAsync(
            sql,
            reader => new StoreDto
            {
                Id = GetGuid(reader, "Id"),
                ChainName = GetString(reader, "ChainName") ?? string.Empty,
                StoreNumber = GetString(reader, "StoreNumber"),
                Name = GetString(reader, "Name") ?? string.Empty,
                Address = GetString(reader, "Address") ?? string.Empty,
                City = GetString(reader, "City") ?? string.Empty,
                State = GetString(reader, "State") ?? string.Empty,
                ZipCode = GetString(reader, "ZipCode") ?? string.Empty,
                Country = GetString(reader, "Country") ?? "USA",
                Phone = GetString(reader, "Phone"),
                Email = GetString(reader, "Email"),
                Website = GetString(reader, "Website"),
                Latitude = GetDecimalNullable(reader, "Latitude"),
                Longitude = GetDecimalNullable(reader, "Longitude"),
                AcceptsManufacturerCoupons = GetBoolean(reader, "AcceptsManufacturerCoupons"),
                AcceptsStoreCoupons = GetBoolean(reader, "AcceptsStoreCoupons"),
                AcceptsDigitalCoupons = GetBoolean(reader, "AcceptsDigitalCoupons"),
                DoublesManufacturerCoupons = GetBoolean(reader, "DoublesManufacturerCoupons"),
                MaxCouponDoubleValue = GetDecimalNullable(reader, "MaxCouponDoubleValue"),
                CouponPolicy = GetString(reader, "CouponPolicy"),
                Hours = GetString(reader, "Hours"),
                IsActive = GetBoolean(reader, "IsActive")
            },
            new SqlParameter("@Latitude", latitude),
            new SqlParameter("@Longitude", longitude),
            new SqlParameter("@RadiusMiles", radiusMiles));
    }
}
