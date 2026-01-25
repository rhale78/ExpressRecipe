using ExpressRecipe.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace ExpressRecipe.ProductService.Data
{
    public interface IProductImageRepository
    {
        Task<Guid> AddImageAsync(Guid productId, string imageType, string? imageUrl, string? localFilePath,
            string? fileName, long? fileSize, string? mimeType, int? width, int? height,
            bool isPrimary, int displayOrder, bool isUserUploaded, string? sourceSystem, string? sourceId, Guid? userId);
        Task<List<ProductImageModel>> GetImagesByProductIdAsync(Guid productId);
        Task<Dictionary<Guid, List<ProductImageModel>>> GetImagesByProductIdsAsync(IEnumerable<Guid> productIds);
        Task<ProductImageModel?> GetPrimaryImageAsync(Guid productId);
        Task SetPrimaryImageAsync(Guid productId, Guid imageId);
        Task DeleteImageAsync(Guid imageId);
        Task DeleteAllProductImagesAsync(Guid productId);

        // Bulk operations
        Task<int> BulkAddImagesAsync(IEnumerable<ProductImageRequest> images);
    }

    public class ProductImageRequest
    {
        public Guid ProductId { get; set; }
        public string ImageType { get; set; } = "Front";
        public string? ImageUrl { get; set; }
        public string? LocalFilePath { get; set; }
        public string? FileName { get; set; }
        public long? FileSize { get; set; }
        public string? MimeType { get; set; } = "image/jpeg";
        public int? Width { get; set; }
        public int? Height { get; set; }
        public bool IsPrimary { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsUserUploaded { get; set; }
        public string? SourceSystem { get; set; }
        public string? SourceId { get; set; }
    }

    public class ProductImageRepository : IProductImageRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<ProductImageRepository>? _logger;

        public ProductImageRepository(string connectionString, ILogger<ProductImageRepository>? logger = null)
        {
            _connectionString = connectionString;
            _logger = logger;
        }

        public async Task<Guid> AddImageAsync(Guid productId, string imageType, string? imageUrl, string? localFilePath,
            string? fileName, long? fileSize, string? mimeType, int? width, int? height,
            bool isPrimary, int displayOrder, bool isUserUploaded, string? sourceSystem, string? sourceId, Guid? userId)
        {
            using SqlConnection connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            using SqlTransaction transaction = connection.BeginTransaction();

            try
            {
                // If this is a primary image, clear other primary flags first
                if (isPrimary)
                {
                    const string clearPrimarySql = "UPDATE ProductImage SET IsPrimary = 0 WHERE ProductId = @ProductId AND IsDeleted = 0";
                    using (SqlCommand clearCommand = new SqlCommand(clearPrimarySql, connection, transaction))
                    {
                        clearCommand.Parameters.AddWithValue("@ProductId", productId);
                        await clearCommand.ExecuteNonQueryAsync();
                    }
                }

                // Insert the new image
                const string sql = @"
                INSERT INTO ProductImage (
                    ProductId, ImageType, ImageUrl, LocalFilePath, FileName, FileSize, MimeType,
                    Width, Height, DisplayOrder, IsPrimary, IsUserUploaded, SourceSystem, SourceId,
                    CreatedDate, CreatedBy, IsDeleted
                )
                OUTPUT INSERTED.Id
                VALUES (
                    @ProductId, @ImageType, @ImageUrl, @LocalFilePath, @FileName, @FileSize, @MimeType,
                    @Width, @Height, @DisplayOrder, @IsPrimary, @IsUserUploaded, @SourceSystem, @SourceId,
                    GETUTCDATE(), @UserId, 0
                )";

                Guid imageId;
                using (SqlCommand command = new SqlCommand(sql, connection, transaction))
                {
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
                    imageId = (Guid)result!;
                }

                // If this is a primary image, sync Product.ImageUrl for backward compatibility
                if (isPrimary && !string.IsNullOrWhiteSpace(imageUrl))
                {
                    const string updateProductSql = "UPDATE Product SET ImageUrl = @ImageUrl, UpdatedAt = GETUTCDATE() WHERE Id = @ProductId";
                    using (SqlCommand updateCommand = new SqlCommand(updateProductSql, connection, transaction))
                    {
                        updateCommand.Parameters.AddWithValue("@ImageUrl", imageUrl);
                        updateCommand.Parameters.AddWithValue("@ProductId", productId);
                        await updateCommand.ExecuteNonQueryAsync();
                    }

                    _logger?.LogDebug("Synced primary image to Product.ImageUrl for ProductId: {ProductId}, ImageType: {ImageType}, Source: {Source}",
                        productId, imageType, sourceSystem ?? "Unknown");
                }

                await transaction.CommitAsync();

                _logger?.LogDebug("Added image {ImageId} for product {ProductId}: Type={ImageType}, IsPrimary={IsPrimary}, Source={Source}",
                    imageId, productId, imageType, isPrimary, sourceSystem ?? "Unknown");

                return imageId;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger?.LogError(ex, "Failed to add image for product {ProductId}", productId);
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

            using SqlConnection connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            using SqlCommand command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@ProductId", productId);

            List<ProductImageModel> images = [];
            using SqlDataReader reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                images.Add(new ProductImageModel
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

            return images;
        }

        public async Task<Dictionary<Guid, List<ProductImageModel>>> GetImagesByProductIdsAsync(IEnumerable<Guid> productIds)
        {
            // Legacy implementation for batch loading - delegates to individual queries
            // Note: This is superseded by ProductImageRepositoryAdapter.GetImagesByProductIdsAsync which uses
            // HighSpeedDAL's caching and in-memory tables for much better performance
            Dictionary<Guid, List<ProductImageModel>> result = [];
            foreach (Guid productId in productIds)
            {
                List<ProductImageModel> images = await GetImagesByProductIdAsync(productId);
                result[productId] = images;
            }
            return result;
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

            using SqlConnection connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            using SqlCommand command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@ProductId", productId);

            using SqlDataReader reader = await command.ExecuteReaderAsync();
            return await reader.ReadAsync()
                ? new ProductImageModel
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
                }
                : null;
        }

        public async Task SetPrimaryImageAsync(Guid productId, Guid imageId)
        {
            using SqlConnection connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            using SqlTransaction transaction = connection.BeginTransaction();

            try
            {
                // Clear current primary
                const string clearSql = "UPDATE ProductImage SET IsPrimary = 0 WHERE ProductId = @ProductId AND IsDeleted = 0";
                using (SqlCommand clearCommand = new SqlCommand(clearSql, connection, transaction))
                {
                    clearCommand.Parameters.AddWithValue("@ProductId", productId);
                    await clearCommand.ExecuteNonQueryAsync();
                }

                // Set new primary
                const string setPrimarySql = "UPDATE ProductImage SET IsPrimary = 1, DisplayOrder = 0 WHERE Id = @ImageId AND IsDeleted = 0";
                using (SqlCommand setPrimaryCommand = new SqlCommand(setPrimarySql, connection, transaction))
                {
                    setPrimaryCommand.Parameters.AddWithValue("@ImageId", imageId);
                    await setPrimaryCommand.ExecuteNonQueryAsync();
                }

                // Sync Product.ImageUrl with the new primary image
                const string syncProductSql = @"
                UPDATE Product
                SET ImageUrl = COALESCE(pi.ImageUrl, pi.LocalFilePath),
                    UpdatedAt = GETUTCDATE()
                FROM Product p
                INNER JOIN ProductImage pi ON p.Id = pi.ProductId
                WHERE p.Id = @ProductId AND pi.Id = @ImageId AND pi.IsDeleted = 0";
                using (SqlCommand syncCommand = new SqlCommand(syncProductSql, connection, transaction))
                {
                    syncCommand.Parameters.AddWithValue("@ProductId", productId);
                    syncCommand.Parameters.AddWithValue("@ImageId", imageId);
                    await syncCommand.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();

                _logger?.LogDebug("Set primary image {ImageId} for product {ProductId} and synced to Product.ImageUrl",
                    imageId, productId);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger?.LogError(ex, "Failed to set primary image for product {ProductId}", productId);
                throw;
            }
        }

        public async Task DeleteImageAsync(Guid imageId)
        {
            const string sql = "UPDATE ProductImage SET IsDeleted = 1, DeletedAt = GETUTCDATE() WHERE Id = @ImageId";

            using SqlConnection connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            using SqlCommand command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@ImageId", imageId);

            await command.ExecuteNonQueryAsync();
        }

        public async Task DeleteAllProductImagesAsync(Guid productId)
        {
            const string sql = "UPDATE ProductImage SET IsDeleted = 1, DeletedAt = GETUTCDATE() WHERE ProductId = @ProductId";

            using SqlConnection connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            using SqlCommand command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@ProductId", productId);

            await command.ExecuteNonQueryAsync();
        }

        public Task<int> BulkAddImagesAsync(IEnumerable<ProductImageRequest> images)
        {
            // Use ProductImageRepositoryAdapter with HighSpeedDAL for bulk operations
            throw new NotImplementedException("Use ProductImageRepositoryAdapter for bulk operations with HighSpeedDAL in-memory table support");
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
}
