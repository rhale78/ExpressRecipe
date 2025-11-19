using ExpressRecipe.Data.Common;
using ExpressRecipe.Shared.DTOs.Product;

namespace ExpressRecipe.ProductService.Data;

public interface IProductRepository
{
    Task<ProductDto?> GetByIdAsync(Guid id);
    Task<ProductDto?> GetByBarcodeAsync(string barcode);
    Task<List<ProductDto>> SearchAsync(ProductSearchRequest request);
    Task<Guid> CreateAsync(CreateProductRequest request, Guid? createdBy = null);
    Task<bool> UpdateAsync(Guid id, UpdateProductRequest request, Guid? updatedBy = null);
    Task<bool> DeleteAsync(Guid id, Guid? deletedBy = null);
    Task<bool> ApproveAsync(Guid id, bool approve, Guid approvedBy, string? rejectionReason = null);
    Task<bool> ProductExistsAsync(Guid id);
}

public class ProductRepository : SqlHelper, IProductRepository
{
    public ProductRepository(string connectionString) : base(connectionString)
    {
    }

    public async Task<ProductDto?> GetByIdAsync(Guid id)
    {
        const string sql = @"
            SELECT Id, Name, Brand, Barcode, BarcodeType, Description, Category,
                   ServingSize, ServingUnit, ImageUrl, ApprovalStatus,
                   ApprovedBy, ApprovedAt, RejectionReason, SubmittedBy
            FROM Product
            WHERE Id = @Id AND IsDeleted = 0";

        var results = await ExecuteReaderAsync(
            sql,
            reader => new ProductDto
            {
                Id = GetGuid(reader, "Id"),
                Name = GetString(reader, "Name") ?? string.Empty,
                Brand = GetString(reader, "Brand"),
                Barcode = GetString(reader, "Barcode"),
                BarcodeType = GetString(reader, "BarcodeType"),
                Description = GetString(reader, "Description"),
                Category = GetString(reader, "Category"),
                ServingSize = GetString(reader, "ServingSize"),
                ServingUnit = GetString(reader, "ServingUnit"),
                ImageUrl = GetString(reader, "ImageUrl"),
                ApprovalStatus = GetString(reader, "ApprovalStatus") ?? "Pending",
                ApprovedBy = GetGuidNullable(reader, "ApprovedBy"),
                ApprovedAt = GetDateTime(reader, "ApprovedAt"),
                RejectionReason = GetString(reader, "RejectionReason"),
                SubmittedBy = GetGuidNullable(reader, "SubmittedBy")
            },
            CreateParameter("@Id", id));

        return results.FirstOrDefault();
    }

    public async Task<ProductDto?> GetByBarcodeAsync(string barcode)
    {
        const string sql = @"
            SELECT Id, Name, Brand, Barcode, BarcodeType, Description, Category,
                   ServingSize, ServingUnit, ImageUrl, ApprovalStatus,
                   ApprovedBy, ApprovedAt, RejectionReason, SubmittedBy
            FROM Product
            WHERE Barcode = @Barcode AND IsDeleted = 0 AND ApprovalStatus = 'Approved'";

        var results = await ExecuteReaderAsync(
            sql,
            reader => new ProductDto
            {
                Id = GetGuid(reader, "Id"),
                Name = GetString(reader, "Name") ?? string.Empty,
                Brand = GetString(reader, "Brand"),
                Barcode = GetString(reader, "Barcode"),
                BarcodeType = GetString(reader, "BarcodeType"),
                Description = GetString(reader, "Description"),
                Category = GetString(reader, "Category"),
                ServingSize = GetString(reader, "ServingSize"),
                ServingUnit = GetString(reader, "ServingUnit"),
                ImageUrl = GetString(reader, "ImageUrl"),
                ApprovalStatus = GetString(reader, "ApprovalStatus") ?? "Pending",
                ApprovedBy = GetGuidNullable(reader, "ApprovedBy"),
                ApprovedAt = GetDateTime(reader, "ApprovedAt"),
                RejectionReason = GetString(reader, "RejectionReason"),
                SubmittedBy = GetGuidNullable(reader, "SubmittedBy")
            },
            CreateParameter("@Barcode", barcode));

        return results.FirstOrDefault();
    }

    public async Task<List<ProductDto>> SearchAsync(ProductSearchRequest request)
    {
        var sql = @"
            SELECT Id, Name, Brand, Barcode, BarcodeType, Description, Category,
                   ServingSize, ServingUnit, ImageUrl, ApprovalStatus,
                   ApprovedBy, ApprovedAt, RejectionReason, SubmittedBy
            FROM Product
            WHERE IsDeleted = 0";

        var parameters = new List<System.Data.SqlClient.SqlParameter>();

        if (request.OnlyApproved == true)
        {
            sql += " AND ApprovalStatus = 'Approved'";
        }

        if (!string.IsNullOrWhiteSpace(request.Barcode))
        {
            sql += " AND Barcode = @Barcode";
            parameters.Add(CreateParameter("@Barcode", request.Barcode));
        }

        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            sql += " AND Category = @Category";
            parameters.Add(CreateParameter("@Category", request.Category));
        }

        if (!string.IsNullOrWhiteSpace(request.Brand))
        {
            sql += " AND Brand = @Brand";
            parameters.Add(CreateParameter("@Brand", request.Brand));
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            sql += " AND (Name LIKE @SearchTerm OR Description LIKE @SearchTerm)";
            parameters.Add(CreateParameter("@SearchTerm", $"%{request.SearchTerm}%"));
        }

        sql += " ORDER BY Name OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";
        parameters.Add(CreateParameter("@Offset", (request.PageNumber - 1) * request.PageSize));
        parameters.Add(CreateParameter("@PageSize", request.PageSize));

        return await ExecuteReaderAsync(
            sql,
            reader => new ProductDto
            {
                Id = GetGuid(reader, "Id"),
                Name = GetString(reader, "Name") ?? string.Empty,
                Brand = GetString(reader, "Brand"),
                Barcode = GetString(reader, "Barcode"),
                BarcodeType = GetString(reader, "BarcodeType"),
                Description = GetString(reader, "Description"),
                Category = GetString(reader, "Category"),
                ServingSize = GetString(reader, "ServingSize"),
                ServingUnit = GetString(reader, "ServingUnit"),
                ImageUrl = GetString(reader, "ImageUrl"),
                ApprovalStatus = GetString(reader, "ApprovalStatus") ?? "Pending",
                ApprovedBy = GetGuidNullable(reader, "ApprovedBy"),
                ApprovedAt = GetDateTime(reader, "ApprovedAt"),
                RejectionReason = GetString(reader, "RejectionReason"),
                SubmittedBy = GetGuidNullable(reader, "SubmittedBy")
            },
            parameters.ToArray());
    }

    public async Task<Guid> CreateAsync(CreateProductRequest request, Guid? createdBy = null)
    {
        const string sql = @"
            INSERT INTO Product (
                Id, Name, Brand, Barcode, BarcodeType, Description, Category,
                ServingSize, ServingUnit, ImageUrl, ApprovalStatus,
                SubmittedBy, CreatedBy, CreatedAt
            )
            VALUES (
                @Id, @Name, @Brand, @Barcode, @BarcodeType, @Description, @Category,
                @ServingSize, @ServingUnit, @ImageUrl, 'Pending',
                @SubmittedBy, @CreatedBy, GETUTCDATE()
            )";

        var productId = Guid.NewGuid();

        await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", productId),
            CreateParameter("@Name", request.Name),
            CreateParameter("@Brand", request.Brand),
            CreateParameter("@Barcode", request.Barcode),
            CreateParameter("@BarcodeType", request.BarcodeType),
            CreateParameter("@Description", request.Description),
            CreateParameter("@Category", request.Category),
            CreateParameter("@ServingSize", request.ServingSize),
            CreateParameter("@ServingUnit", request.ServingUnit),
            CreateParameter("@ImageUrl", request.ImageUrl),
            CreateParameter("@SubmittedBy", createdBy),
            CreateParameter("@CreatedBy", createdBy));

        return productId;
    }

    public async Task<bool> UpdateAsync(Guid id, UpdateProductRequest request, Guid? updatedBy = null)
    {
        const string sql = @"
            UPDATE Product
            SET Name = @Name,
                Brand = @Brand,
                Barcode = @Barcode,
                BarcodeType = @BarcodeType,
                Description = @Description,
                Category = @Category,
                ServingSize = @ServingSize,
                ServingUnit = @ServingUnit,
                ImageUrl = @ImageUrl,
                UpdatedBy = @UpdatedBy,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND IsDeleted = 0";

        var rowsAffected = await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", id),
            CreateParameter("@Name", request.Name),
            CreateParameter("@Brand", request.Brand),
            CreateParameter("@Barcode", request.Barcode),
            CreateParameter("@BarcodeType", request.BarcodeType),
            CreateParameter("@Description", request.Description),
            CreateParameter("@Category", request.Category),
            CreateParameter("@ServingSize", request.ServingSize),
            CreateParameter("@ServingUnit", request.ServingUnit),
            CreateParameter("@ImageUrl", request.ImageUrl),
            CreateParameter("@UpdatedBy", updatedBy));

        return rowsAffected > 0;
    }

    public async Task<bool> DeleteAsync(Guid id, Guid? deletedBy = null)
    {
        const string sql = @"
            UPDATE Product
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
            UPDATE Product
            SET ApprovalStatus = @ApprovalStatus,
                ApprovedBy = @ApprovedBy,
                ApprovedAt = @ApprovedAt,
                RejectionReason = @RejectionReason,
                UpdatedBy = @ApprovedBy,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND IsDeleted = 0";

        var rowsAffected = await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", id),
            CreateParameter("@ApprovalStatus", approve ? "Approved" : "Rejected"),
            CreateParameter("@ApprovedBy", approvedBy),
            CreateParameter("@ApprovedAt", DateTime.UtcNow),
            CreateParameter("@RejectionReason", rejectionReason));

        return rowsAffected > 0;
    }

    public async Task<bool> ProductExistsAsync(Guid id)
    {
        const string sql = "SELECT COUNT(*) FROM Product WHERE Id = @Id AND IsDeleted = 0";

        var count = await ExecuteScalarAsync<int>(
            sql,
            CreateParameter("@Id", id));

        return count > 0;
    }
}
