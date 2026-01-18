using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace ExpressRecipe.ProductService.Data;

public class ProductImageRepositoryAdapter : IProductImageRepository
{
    private readonly ProductDatabaseConnection _dbConnection;
    private readonly ILogger<ProductImageRepositoryAdapter> _logger;

    public ProductImageRepositoryAdapter(ProductDatabaseConnection dbConnection, ILogger<ProductImageRepositoryAdapter> logger)
    {
        _dbConnection = dbConnection ?? throw new ArgumentNullException(nameof(dbConnection));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Guid> AddImageAsync(Guid productId, string imageType, string? imageUrl, string? localFilePath,
        string? fileName, long? fileSize, string? mimeType, int? width, int? height,
        bool isPrimary, int displayOrder, bool isUserUploaded, string? sourceSystem, string? sourceId, Guid? userId)
    {
        await using var conn = new SqlConnection(_dbConnection.ConnectionString);
        await conn.OpenAsync();
        using var transaction = conn.BeginTransaction();

        try
        {
            if (isPrimary)
            {
                const string clearPrimarySql = "UPDATE ProductImage SET IsPrimary = 0 WHERE ProductId = @ProductId AND IsDeleted = 0";
                using var clearCommand = new SqlCommand(clearPrimarySql, conn, transaction);
                clearCommand.Parameters.AddWithValue("@ProductId", productId);
                await clearCommand.ExecuteNonQueryAsync();
            }

            const string insertSql = @"
                INSERT INTO ProductImage (
                    Id, ProductId, ImageType, ImageUrl, LocalFilePath, FileName, FileSize, MimeType,
                    Width, Height, DisplayOrder, IsPrimary, IsUserUploaded, SourceSystem, SourceId,
                    CreatedDate, CreatedBy, IsDeleted
                )
                OUTPUT INSERTED.Id
                VALUES (
                    NEWID(), @ProductId, @ImageType, @ImageUrl, @LocalFilePath, @FileName, @FileSize, @MimeType,
                    @Width, @Height, @DisplayOrder, @IsPrimary, @IsUserUploaded, @SourceSystem, @SourceId,
                    GETUTCDATE(), @UserId, 0
                )";

            Guid imageId;
            using (var cmd = new SqlCommand(insertSql, conn, transaction))
            {
                cmd.Parameters.AddWithValue("@ProductId", productId);
                cmd.Parameters.AddWithValue("@ImageType", imageType ?? string.Empty);
                cmd.Parameters.AddWithValue("@ImageUrl", (object?)imageUrl ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@LocalFilePath", (object?)localFilePath ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@FileName", (object?)fileName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@FileSize", (object?)fileSize ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@MimeType", (object?)mimeType ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Width", (object?)width ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Height", (object?)height ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@DisplayOrder", displayOrder);
                cmd.Parameters.AddWithValue("@IsPrimary", isPrimary);
                cmd.Parameters.AddWithValue("@IsUserUploaded", isUserUploaded);
                cmd.Parameters.AddWithValue("@SourceSystem", (object?)sourceSystem ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@SourceId", (object?)sourceId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@UserId", (object?)userId ?? DBNull.Value);

                var res = await cmd.ExecuteScalarAsync();
                imageId = (Guid)res!;
            }

            if (isPrimary && !string.IsNullOrWhiteSpace(imageUrl))
            {
                const string updateProductSql = "UPDATE Product SET ImageUrl = @ImageUrl, ModifiedDate = GETUTCDATE() WHERE Id = @ProductId";
                using var updateCmd = new SqlCommand(updateProductSql, conn, transaction);
                updateCmd.Parameters.AddWithValue("@ImageUrl", imageUrl);
                updateCmd.Parameters.AddWithValue("@ProductId", productId);
                await updateCmd.ExecuteNonQueryAsync();

                _logger.LogDebug("Synced primary image to Product.ImageUrl for ProductId: {ProductId}, ImageType: {ImageType}, Source: {Source}",
                    productId, imageType, sourceSystem ?? "Unknown");
            }

            await transaction.CommitAsync();

            _logger.LogDebug("Added image {ImageId} for product {ProductId}: Type={ImageType}, IsPrimary={IsPrimary}, Source={Source}",
                imageId, productId, imageType, isPrimary, sourceSystem ?? "Unknown");

            return imageId;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Failed to add image for product {ProductId}", productId);
            throw;
        }
    }

    public async Task<List<ProductImageModel>> GetImagesByProductIdAsync(Guid productId)
    {
        const string sql = @"
            SELECT
                Id, ProductId, ImageType, ImageUrl, LocalFilePath, FileName, FileSize, MimeType,
                Width, Height, DisplayOrder, IsPrimary, IsUserUploaded, SourceSystem, SourceId, CreatedDate
            FROM ProductImage
            WHERE ProductId = @ProductId AND IsDeleted = 0
            ORDER BY DisplayOrder, CreatedDate";

        var list = new List<ProductImageModel>();
        await using var conn = new SqlConnection(_dbConnection.ConnectionString);
        await conn.OpenAsync();
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ProductId", productId);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new ProductImageModel
            {
                Id = reader.GetGuid(0),
                ProductId = reader.GetGuid(1),
                ImageType = reader.GetString(2),
                ImageUrl = reader.IsDBNull(3) ? null : reader.GetString(3),
                LocalFilePath = reader.IsDBNull(4) ? null : reader.GetString(4),
                FileName = reader.IsDBNull(5) ? null : reader.GetString(5),
                FileSize = reader.IsDBNull(6) ? null : reader.GetInt64(6),
                MimeType = reader.IsDBNull(7) ? null : reader.GetString(7),
                Width = reader.IsDBNull(8) ? null : reader.GetInt32(8),
                Height = reader.IsDBNull(9) ? null : reader.GetInt32(9),
                DisplayOrder = reader.GetInt32(10),
                IsPrimary = reader.GetBoolean(11),
                IsUserUploaded = reader.GetBoolean(12),
                SourceSystem = reader.IsDBNull(13) ? null : reader.GetString(13),
                SourceId = reader.IsDBNull(14) ? null : reader.GetString(14),
                CreatedAt = reader.GetDateTime(15)
            });
        }

        return list;
    }

    public async Task<ProductImageModel?> GetPrimaryImageAsync(Guid productId)
    {
        const string sql = @"
            SELECT TOP 1
                Id, ProductId, ImageType, ImageUrl, LocalFilePath, FileName, FileSize, MimeType,
                Width, Height, DisplayOrder, IsPrimary, IsUserUploaded, SourceSystem, SourceId, CreatedDate
            FROM ProductImage
            WHERE ProductId = @ProductId AND IsDeleted = 0 AND IsPrimary = 1
            ORDER BY DisplayOrder";

        await using var conn = new SqlConnection(_dbConnection.ConnectionString);
        await conn.OpenAsync();
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ProductId", productId);
        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new ProductImageModel
            {
                Id = reader.GetGuid(0),
                ProductId = reader.GetGuid(1),
                ImageType = reader.GetString(2),
                ImageUrl = reader.IsDBNull(3) ? null : reader.GetString(3),
                LocalFilePath = reader.IsDBNull(4) ? null : reader.GetString(4),
                FileName = reader.IsDBNull(5) ? null : reader.GetString(5),
                FileSize = reader.IsDBNull(6) ? null : reader.GetInt64(6),
                MimeType = reader.IsDBNull(7) ? null : reader.GetString(7),
                Width = reader.IsDBNull(8) ? null : reader.GetInt32(8),
                Height = reader.IsDBNull(9) ? null : reader.GetInt32(9),
                DisplayOrder = reader.GetInt32(10),
                IsPrimary = reader.GetBoolean(11),
                IsUserUploaded = reader.GetBoolean(12),
                SourceSystem = reader.IsDBNull(13) ? null : reader.GetString(13),
                SourceId = reader.IsDBNull(14) ? null : reader.GetString(14),
                CreatedAt = reader.GetDateTime(15)
            };
        }

        return null;
    }

    public async Task SetPrimaryImageAsync(Guid productId, Guid imageId)
    {
        await using var conn = new SqlConnection(_dbConnection.ConnectionString);
        await conn.OpenAsync();
        using var transaction = conn.BeginTransaction();

        try
        {
            const string clearSql = "UPDATE ProductImage SET IsPrimary = 0 WHERE ProductId = @ProductId AND IsDeleted = 0";
            using (var clearCommand = new SqlCommand(clearSql, conn, transaction))
            {
                clearCommand.Parameters.AddWithValue("@ProductId", productId);
                await clearCommand.ExecuteNonQueryAsync();
            }

            const string setPrimarySql = "UPDATE ProductImage SET IsPrimary = 1, DisplayOrder = 0 WHERE Id = @ImageId AND IsDeleted = 0";
            using (var setPrimaryCommand = new SqlCommand(setPrimarySql, conn, transaction))
            {
                setPrimaryCommand.Parameters.AddWithValue("@ImageId", imageId);
                await setPrimaryCommand.ExecuteNonQueryAsync();
            }

            const string syncProductSql = @"
                UPDATE Product
                SET ImageUrl = COALESCE(pi.ImageUrl, pi.LocalFilePath),
                    ModifiedDate = GETUTCDATE()
                FROM Product p
                INNER JOIN ProductImage pi ON p.Id = pi.ProductId
                WHERE p.Id = @ProductId AND pi.Id = @ImageId AND pi.IsDeleted = 0";
            using (var syncCommand = new SqlCommand(syncProductSql, conn, transaction))
            {
                syncCommand.Parameters.AddWithValue("@ProductId", productId);
                syncCommand.Parameters.AddWithValue("@ImageId", imageId);
                await syncCommand.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();

            _logger.LogDebug("Set primary image {ImageId} for product {ProductId} and synced to Product.ImageUrl",
                imageId, productId);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Failed to set primary image for product {ProductId}", productId);
            throw;
        }
    }

    public async Task DeleteImageAsync(Guid imageId)
    {
        const string sql = "UPDATE ProductImage SET IsDeleted = 1, DeletedDate = GETUTCDATE() WHERE Id = @ImageId";

        await using var conn = new SqlConnection(_dbConnection.ConnectionString);
        await conn.OpenAsync();

        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ImageId", imageId);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteAllProductImagesAsync(Guid productId)
    {
        const string sql = "UPDATE ProductImage SET IsDeleted = 1, DeletedDate = GETUTCDATE() WHERE ProductId = @ProductId";

        await using var conn = new SqlConnection(_dbConnection.ConnectionString);
        await conn.OpenAsync();

        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ProductId", productId);

        await cmd.ExecuteNonQueryAsync();
    }
}
