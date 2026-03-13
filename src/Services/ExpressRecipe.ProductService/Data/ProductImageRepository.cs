using ExpressRecipe.Data.Common;
using Microsoft.Extensions.Logging;

namespace ExpressRecipe.ProductService.Data;

public interface IProductImageRepository
{
    Task<Guid> AddImageAsync(Guid productId, string imageType, string? imageUrl, string? localFilePath,
        string? fileName, long? fileSize, string? mimeType, int? width, int? height,
        bool isPrimary, int displayOrder, bool isUserUploaded, string? sourceSystem, string? sourceId, Guid? userId);
    Task<List<ProductImageModel>> GetImagesByProductIdAsync(Guid productId);
    Task<ProductImageModel?> GetPrimaryImageAsync(Guid productId);
    Task SetPrimaryImageAsync(Guid productId, Guid imageId);
    Task DeleteImageAsync(Guid imageId);
    Task DeleteAllProductImagesAsync(Guid productId);
}

public class ProductImageRepository : SqlHelper, IProductImageRepository
{
    private readonly ILogger<ProductImageRepository>? _logger;

    public ProductImageRepository(string connectionString, ILogger<ProductImageRepository>? logger = null)
        : base(connectionString)
    {
        _logger = logger;
    }

    public async Task<Guid> AddImageAsync(Guid productId, string imageType, string? imageUrl, string? localFilePath,
        string? fileName, long? fileSize, string? mimeType, int? width, int? height,
        bool isPrimary, int displayOrder, bool isUserUploaded, string? sourceSystem, string? sourceId, Guid? userId)
    {
        return await ExecuteTransactionAsync<Guid>(async (connection, transaction) =>
        {
            // If this is a primary image, clear other primary flags first
            if (isPrimary)
            {
                const string clearPrimarySql = "UPDATE ProductImage SET IsPrimary = 0 WHERE ProductId = @ProductId AND IsDeleted = 0";
                using var clearCommand = new Microsoft.Data.SqlClient.SqlCommand(clearPrimarySql, connection, transaction);
                clearCommand.Parameters.AddWithValue("@ProductId", productId);
                await clearCommand.ExecuteNonQueryAsync();
            }

            // Insert the new image
            const string sql = @"
                INSERT INTO ProductImage (
                    ProductId, ImageType, ImageUrl, LocalFilePath, FileName, FileSize, MimeType,
                    Width, Height, DisplayOrder, IsPrimary, IsUserUploaded, SourceSystem, SourceId,
                    CreatedAt, CreatedBy, IsDeleted
                )
                OUTPUT INSERTED.Id
                VALUES (
                    @ProductId, @ImageType, @ImageUrl, @LocalFilePath, @FileName, @FileSize, @MimeType,
                    @Width, @Height, @DisplayOrder, @IsPrimary, @IsUserUploaded, @SourceSystem, @SourceId,
                    GETUTCDATE(), @UserId, 0
                )";

            using var command = new Microsoft.Data.SqlClient.SqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("@ProductId", productId);
            command.Parameters.AddWithValue("@ImageType", imageType);
            command.Parameters.AddWithValue("@ImageUrl", (object?)imageUrl ?? DBNull.Value);
            command.Parameters.AddWithValue("@LocalFilePath", (object?)localFilePath ?? DBNull.Value);
            command.Parameters.AddWithValue("@FileName", (object?)fileName ?? DBNull.Value);
            command.Parameters.AddWithValue("@FileSize", (object?)fileSize ?? DBNull.Value);
            command.Parameters.AddWithValue("@MimeType", (object?)mimeType ?? DBNull.Value);
            command.Parameters.AddWithValue("@Width", (object?)width ?? DBNull.Value);
            command.Parameters.AddWithValue("@Height", (object?)height ?? DBNull.Value);
            command.Parameters.AddWithValue("@DisplayOrder", displayOrder);
            command.Parameters.AddWithValue("@IsPrimary", isPrimary);
            command.Parameters.AddWithValue("@IsUserUploaded", isUserUploaded);
            command.Parameters.AddWithValue("@SourceSystem", (object?)sourceSystem ?? DBNull.Value);
            command.Parameters.AddWithValue("@SourceId", (object?)sourceId ?? DBNull.Value);
            command.Parameters.AddWithValue("@UserId", (object?)userId ?? DBNull.Value);

            var result = await command.ExecuteScalarAsync();
            var imageId = (Guid)result!;

            // If this is a primary image, sync Product.ImageUrl for backward compatibility
            if (isPrimary && !string.IsNullOrWhiteSpace(imageUrl))
            {
                const string updateProductSql = "UPDATE Product SET ImageUrl = @ImageUrl, UpdatedAt = GETUTCDATE() WHERE Id = @ProductId";
                using var updateCommand = new Microsoft.Data.SqlClient.SqlCommand(updateProductSql, connection, transaction);
                updateCommand.Parameters.AddWithValue("@ImageUrl", imageUrl);
                updateCommand.Parameters.AddWithValue("@ProductId", productId);
                await updateCommand.ExecuteNonQueryAsync();

                _logger?.LogDebug("Synced primary image to Product.ImageUrl for ProductId: {ProductId}, ImageType: {ImageType}, Source: {Source}",
                    productId, imageType, sourceSystem ?? "Unknown");
            }

            _logger?.LogDebug("Added image {ImageId} for product {ProductId}: Type={ImageType}, IsPrimary={IsPrimary}, Source={Source}",
                imageId, productId, imageType, isPrimary, sourceSystem ?? "Unknown");

            return imageId;
        });
    }

    private static ProductImageModel MapImage(System.Data.IDataRecord reader) => new ProductImageModel
    {
        Id = SqlHelper.GetGuid(reader, "Id"),
        ProductId = SqlHelper.GetGuid(reader, "ProductId"),
        ImageType = SqlHelper.GetString(reader, "ImageType")!,
        ImageUrl = SqlHelper.GetString(reader, "ImageUrl"),
        LocalFilePath = SqlHelper.GetString(reader, "LocalFilePath"),
        FileName = SqlHelper.GetString(reader, "FileName"),
        FileSize = reader.IsDBNull(reader.GetOrdinal("FileSize")) ? null : reader.GetInt64(reader.GetOrdinal("FileSize")),
        MimeType = SqlHelper.GetString(reader, "MimeType"),
        Width = SqlHelper.GetIntNullable(reader, "Width"),
        Height = SqlHelper.GetIntNullable(reader, "Height"),
        DisplayOrder = SqlHelper.GetInt32(reader, "DisplayOrder"),
        IsPrimary = SqlHelper.GetBoolean(reader, "IsPrimary"),
        IsUserUploaded = SqlHelper.GetBoolean(reader, "IsUserUploaded"),
        SourceSystem = SqlHelper.GetString(reader, "SourceSystem"),
        SourceId = SqlHelper.GetString(reader, "SourceId"),
        CreatedAt = SqlHelper.GetDateTime(reader, "CreatedAt")
    };

    public async Task<List<ProductImageModel>> GetImagesByProductIdAsync(Guid productId)
    {
        const string sql = @"
            SELECT
                Id, ProductId, ImageType, ImageUrl, LocalFilePath, FileName, FileSize, MimeType,
                Width, Height, DisplayOrder, IsPrimary, IsUserUploaded, SourceSystem, SourceId, CreatedAt
            FROM ProductImage
            WHERE ProductId = @ProductId AND IsDeleted = 0
            ORDER BY DisplayOrder, CreatedAt";

        return await ExecuteReaderAsync<ProductImageModel>(sql, MapImage, CreateParameter("@ProductId", productId));
    }

    public async Task<ProductImageModel?> GetPrimaryImageAsync(Guid productId)
    {
        const string sql = @"
            SELECT TOP 1
                Id, ProductId, ImageType, ImageUrl, LocalFilePath, FileName, FileSize, MimeType,
                Width, Height, DisplayOrder, IsPrimary, IsUserUploaded, SourceSystem, SourceId, CreatedAt
            FROM ProductImage
            WHERE ProductId = @ProductId AND IsDeleted = 0 AND IsPrimary = 1
            ORDER BY DisplayOrder";

        var results = await ExecuteReaderAsync<ProductImageModel>(sql, MapImage, CreateParameter("@ProductId", productId));
        return results.FirstOrDefault();
    }

    public async Task SetPrimaryImageAsync(Guid productId, Guid imageId)
    {
        await ExecuteTransactionAsync(async (connection, transaction) =>
        {
            // Clear current primary
            const string clearSql = "UPDATE ProductImage SET IsPrimary = 0 WHERE ProductId = @ProductId AND IsDeleted = 0";
            using var clearCommand = new Microsoft.Data.SqlClient.SqlCommand(clearSql, connection, transaction);
            clearCommand.Parameters.AddWithValue("@ProductId", productId);
            await clearCommand.ExecuteNonQueryAsync();

            // Set new primary
            const string setPrimarySql = "UPDATE ProductImage SET IsPrimary = 1, DisplayOrder = 0 WHERE Id = @ImageId AND IsDeleted = 0";
            using var setPrimaryCommand = new Microsoft.Data.SqlClient.SqlCommand(setPrimarySql, connection, transaction);
            setPrimaryCommand.Parameters.AddWithValue("@ImageId", imageId);
            await setPrimaryCommand.ExecuteNonQueryAsync();

            // Sync Product.ImageUrl with the new primary image
            const string syncProductSql = @"
                UPDATE Product
                SET ImageUrl = COALESCE(pi.ImageUrl, pi.LocalFilePath),
                    UpdatedAt = GETUTCDATE()
                FROM Product p
                INNER JOIN ProductImage pi ON p.Id = pi.ProductId
                WHERE p.Id = @ProductId AND pi.Id = @ImageId AND pi.IsDeleted = 0";
            using var syncCommand = new Microsoft.Data.SqlClient.SqlCommand(syncProductSql, connection, transaction);
            syncCommand.Parameters.AddWithValue("@ProductId", productId);
            syncCommand.Parameters.AddWithValue("@ImageId", imageId);
            await syncCommand.ExecuteNonQueryAsync();

            _logger?.LogDebug("Set primary image {ImageId} for product {ProductId} and synced to Product.ImageUrl",
                imageId, productId);
        });
    }

    public async Task DeleteImageAsync(Guid imageId)
    {
        const string sql = "UPDATE ProductImage SET IsDeleted = 1, DeletedAt = GETUTCDATE() WHERE Id = @ImageId";
        await ExecuteNonQueryAsync(sql, CreateParameter("@ImageId", imageId));
    }

    public async Task DeleteAllProductImagesAsync(Guid productId)
    {
        const string sql = "UPDATE ProductImage SET IsDeleted = 1, DeletedAt = GETUTCDATE() WHERE ProductId = @ProductId";
        await ExecuteNonQueryAsync(sql, CreateParameter("@ProductId", productId));
    }
}

public class ProductImageModel
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ImageType { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? LocalFilePath { get; set; }
    public string? FileName { get; set; }
    public long? FileSize { get; set; }
    public string? MimeType { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsPrimary { get; set; }
    public bool IsUserUploaded { get; set; }
    public string? SourceSystem { get; set; }
    public string? SourceId { get; set; }
    public DateTime CreatedAt { get; set; }
}
