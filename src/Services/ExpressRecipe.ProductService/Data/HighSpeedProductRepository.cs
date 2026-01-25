using ExpressRecipe.Data.Common;
using ExpressRecipe.Shared.DTOs.Product;
using ExpressRecipe.Shared.Services;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System.Data;

namespace ExpressRecipe.ProductService.Data
{
    /// <summary>
    /// High-speed repository for bulk product operations.
    /// Optimized for batch inserts, updates, and parallel queries.
    /// </summary>
    public interface IHighSpeedProductRepository
    {
        // Bulk Write Operations
        Task<int> BulkInsertProductsAsync(IEnumerable<CreateProductRequest> products, CancellationToken ct = default);
        Task<int> BulkUpdateProductsAsync(IEnumerable<UpdateProductWithIdRequest> products, CancellationToken ct = default);
        Task<int> BulkUpsertProductsAsync(IEnumerable<CreateProductRequest> products, CancellationToken ct = default);

        // Batch Read Operations
        Task<Dictionary<Guid, ProductDto>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
        Task<Dictionary<string, ProductDto>> GetByBarcodesAsync(IEnumerable<string> barcodes, CancellationToken ct = default);

        // Smart Caching Operations
        Task<Dictionary<Guid, ProductDto>> GetOrSetBatchAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
        Task InvalidateCacheAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
        Task InvalidateCacheByBarcodeAsync(IEnumerable<string> barcodes, CancellationToken ct = default);
    }
    // NOTE: HighSpeedProductRepository retained for reference but not registered in DI when using HighSpeedDAL adapters.
    // The implementation remains available for reference and can be re-activated by registering IHighSpeedProductRepository in DI.

    public class HighSpeedProductRepository : SqlHelper, IHighSpeedProductRepository
    {
        private readonly IProductImageRepository _productImageRepository;
        private readonly HybridCacheService? _cache;
        private readonly ILogger<HighSpeedProductRepository>? _logger;

        public HighSpeedProductRepository(
            string connectionString,
            IProductImageRepository productImageRepository,
            HybridCacheService? cache = null,
            ILogger<HighSpeedProductRepository>? logger = null) : base(connectionString)
        {
            _productImageRepository = productImageRepository;
            _cache = cache;
            _logger = logger;
        }

        #region Bulk Write Operations

        /// <summary>
        /// Bulk insert products using SqlBulkCopy for maximum performance.
        /// </summary>
        public async Task<int> BulkInsertProductsAsync(
            IEnumerable<CreateProductRequest> products,
            CancellationToken ct = default)
        {
            List<CreateProductRequest> productsList = products.ToList();
            if (productsList.Count == 0)
            {
                return 0;
            }

            _logger?.LogInformation("Bulk inserting {Count} products", productsList.Count);

            await using SqlConnection connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync(ct);

            await using SqlTransaction transaction = connection.BeginTransaction();
            try
            {
                // Create DataTable
                DataTable dataTable = CreateProductDataTable();

                foreach (CreateProductRequest product in productsList)
                {
                    DataRow row = dataTable.NewRow();
                    MapProductToDataRow(product, row);
                    dataTable.Rows.Add(row);
                }

                // Bulk insert
                using SqlBulkCopy bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, transaction);
                bulkCopy.DestinationTableName = "Product";
                bulkCopy.BatchSize = 1000;
                bulkCopy.BulkCopyTimeout = 300;

                MapBulkCopyColumns(bulkCopy, dataTable);

                await bulkCopy.WriteToServerAsync(dataTable, ct);
                await transaction.CommitAsync(ct);

                _logger?.LogInformation("Successfully bulk inserted {Count} products", dataTable.Rows.Count);
                return dataTable.Rows.Count;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during bulk insert of products");
                await transaction.RollbackAsync(ct);
                throw;
            }
        }

        /// <summary>
        /// Bulk update products using MERGE statement.
        /// </summary>
        public async Task<int> BulkUpdateProductsAsync(
            IEnumerable<UpdateProductWithIdRequest> products,
            CancellationToken ct = default)
        {
            List<UpdateProductWithIdRequest> productsList = products.ToList();
            if (productsList.Count == 0)
            {
                return 0;
            }

            _logger?.LogInformation("Bulk updating {Count} products", productsList.Count);

            DataTable dataTable = CreateProductUpdateDataTable();
            foreach (UpdateProductWithIdRequest product in productsList)
            {
                DataRow row = dataTable.NewRow();
                MapProductUpdateToDataRow(product, row);
                dataTable.Rows.Add(row);
            }

            var affectedRows = await BulkOperationsHelper.BulkUpsertAsync(
                ConnectionString,
                productsList,
                "Product",
                "#TempProductUpdates",
                new[] { "Id" },
                (product, row) =>
                {
                    MapProductUpdateToDataRow(product, row);
                    return row;
                },
                dataTable,
                ct);

            _logger?.LogInformation("Successfully bulk updated {Count} products", affectedRows);

            // Invalidate cache for updated products
            if (_cache != null)
            {
                await InvalidateCacheAsync(productsList.Select(p => p.Id), ct);
            }

            return affectedRows;
        }

        /// <summary>
        /// Bulk upsert products (insert new, update existing) using MERGE.
        /// </summary>
        public async Task<int> BulkUpsertProductsAsync(
            IEnumerable<CreateProductRequest> products,
            CancellationToken ct = default)
        {
            List<CreateProductRequest> productsList = products.ToList();
            if (productsList.Count == 0)
            {
                return 0;
            }

            _logger?.LogInformation("Bulk upserting {Count} products", productsList.Count);

            DataTable dataTable = CreateProductDataTable();

            var affectedRows = await BulkOperationsHelper.BulkUpsertAsync(
                ConnectionString,
                productsList,
                "Product",
                "#TempProducts",
                new[] { "Barcode" }, // Use barcode as natural key
                (product, row) =>
                {
                    MapProductToDataRow(product, row);
                    return row;
                },
                dataTable,
                ct);

            _logger?.LogInformation("Successfully bulk upserted {Count} products", affectedRows);

            return affectedRows;
        }

        #endregion

        #region Batch Read Operations

        /// <summary>
        /// Get multiple products by IDs in a single optimized query.
        /// </summary>
        public async Task<Dictionary<Guid, ProductDto>> GetByIdsAsync(
            IEnumerable<Guid> ids,
            CancellationToken ct = default)
        {
            List<Guid> idsList = ids.Distinct().ToList();
            if (idsList.Count == 0)
            {
                return [];
            }

            _logger?.LogDebug("Batch fetching {Count} products by ID", idsList.Count);

            Dictionary<Guid, ProductDto> result = await ExecuteBatchLookupAsync<Guid, ProductDto>(
                "Product",
                "Id",
                idsList,
                reader => new KeyValuePair<Guid, ProductDto>(
                    GetGuid(reader, "Id"),
                    MapReaderToProductDto(reader)
                ),
                ct);

            // Load images for all products
            foreach (ProductDto product in result.Values)
            {
                await LoadImagesAsync(product);
            }

            _logger?.LogDebug("Retrieved {Count} products by ID", result.Count);

            return result;
        }

        /// <summary>
        /// Get multiple products by barcodes in a single optimized query.
        /// </summary>
        public async Task<Dictionary<string, ProductDto>> GetByBarcodesAsync(
            IEnumerable<string> barcodes,
            CancellationToken ct = default)
        {
            List<string> barcodesList = barcodes.Distinct().ToList();
            if (barcodesList.Count == 0)
            {
                return [];
            }

            _logger?.LogDebug("Batch fetching {Count} products by barcode", barcodesList.Count);

            await using SqlConnection connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync(ct);

            var tempTableName = $"#TempBarcodes_{Guid.NewGuid():N}";

            // Create temp table
            await using (SqlCommand createCmd = new SqlCommand($"CREATE TABLE {tempTableName} ([Barcode] NVARCHAR(50) PRIMARY KEY)", connection))
            {
                await createCmd.ExecuteNonQueryAsync(ct);
            }

            // Bulk insert barcodes
            DataTable dataTable = new DataTable();
            dataTable.Columns.Add("Barcode", typeof(string));
            foreach (var barcode in barcodesList)
            {
                dataTable.Rows.Add(barcode);
            }

            using (SqlBulkCopy bulkCopy = new SqlBulkCopy(connection))
            {
                bulkCopy.DestinationTableName = tempTableName;
                bulkCopy.BatchSize = 1000;
                bulkCopy.ColumnMappings.Add("Barcode", "Barcode");
                await bulkCopy.WriteToServerAsync(dataTable, ct);
            }

            // Query using temp table join
            var querySql = $@"
            SELECT p.Id, p.Name, p.Brand, p.Barcode, p.BarcodeType, p.Description, 
                   p.Category, p.ServingSize, p.ServingUnit, p.ImageUrl, 
                   p.ApprovalStatus, p.ApprovedBy, p.ApprovedAt, p.RejectionReason,
                   p.SubmittedBy, p.CreatedAt
            FROM Product p
            INNER JOIN {tempTableName} tmp ON p.Barcode = tmp.Barcode
            WHERE p.IsDeleted = 0";

            Dictionary<string, ProductDto> result = new Dictionary<string, ProductDto>(StringComparer.OrdinalIgnoreCase);
            await using (SqlCommand queryCmd = new SqlCommand(querySql, connection))
            {
                queryCmd.CommandTimeout = 300;
                await using SqlDataReader reader = await queryCmd.ExecuteReaderAsync(ct);

                while (await reader.ReadAsync(ct))
                {
                    ProductDto product = MapReaderToProductDto(reader);
                    if (product.Barcode != null)
                    {
                        result[product.Barcode] = product;
                    }
                }
            }

            // Load images for all products
            foreach (ProductDto product in result.Values)
            {
                await LoadImagesAsync(product);
            }

            _logger?.LogDebug("Retrieved {Count} products by barcode", result.Count);

            return result;
        }

        #endregion

        #region Smart Caching Operations

        /// <summary>
        /// Get products from cache first, query DB only for cache misses.
        /// </summary>
        public async Task<Dictionary<Guid, ProductDto>> GetOrSetBatchAsync(
            IEnumerable<Guid> ids,
            CancellationToken ct = default)
        {
            List<Guid> idsList = ids.Distinct().ToList();
            if (idsList.Count == 0)
            {
                return [];
            }

            if (_cache == null)
            {
                // No cache available, fetch directly from DB
                return await GetByIdsAsync(idsList, ct);
            }

            Dictionary<Guid, ProductDto> result = [];
            List<Guid> uncachedIds = [];

            // Check cache for each product
            foreach (Guid id in idsList)
            {
                var cacheKey = CacheKeys.FormatKey("product:id:{0}", id);
                ProductDto? cachedProduct = await _cache.GetAsync<ProductDto>(cacheKey);

                if (cachedProduct != null)
                {
                    result[id] = cachedProduct;
                }
                else
                {
                    uncachedIds.Add(id);
                }
            }

            _logger?.LogDebug("Product cache: {CacheHits} hits, {CacheMisses} misses", 
                result.Count, uncachedIds.Count);

            // Fetch uncached products from DB
            if (uncachedIds.Count != 0)
            {
                Dictionary<Guid, ProductDto> dbProducts = await GetByIdsAsync(uncachedIds, ct);

                // Cache each product individually
                foreach (KeyValuePair<Guid, ProductDto> kvp in dbProducts)
                {
                    var cacheKey = CacheKeys.FormatKey("product:id:{0}", kvp.Key);
                    await _cache.SetAsync(
                        cacheKey,
                        kvp.Value,
                        memoryExpiry: TimeSpan.FromMinutes(15),
                        distributedExpiry: TimeSpan.FromHours(1));

                    result[kvp.Key] = kvp.Value;
                }
            }

            return result;
        }

        /// <summary>
        /// Invalidate cache entries for multiple products.
        /// </summary>
        public async Task InvalidateCacheAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
        {
            if (_cache == null)
            {
                return;
            }

            IEnumerable<Task> tasks = ids.Select(id =>
            {
                var cacheKey = CacheKeys.FormatKey("product:id:{0}", id);
                return _cache.RemoveAsync(cacheKey);
            });

            await Task.WhenAll(tasks);

            _logger?.LogDebug("Invalidated cache for {Count} products", ids.Count());
        }

        /// <summary>
        /// Invalidate cache entries by barcodes.
        /// </summary>
        public async Task InvalidateCacheByBarcodeAsync(IEnumerable<string> barcodes, CancellationToken ct = default)
        {
            if (_cache == null)
            {
                return;
            }

            IEnumerable<Task> tasks = barcodes.Select(barcode =>
            {
                var cacheKey = CacheKeys.FormatKey("product:barcode:{0}", barcode);
                return _cache.RemoveAsync(cacheKey);
            });

            await Task.WhenAll(tasks);

            _logger?.LogDebug("Invalidated cache for {Count} product barcodes", barcodes.Count());
        }

        #endregion

        #region Helper Methods

        private static DataTable CreateProductDataTable()
        {
            DataTable dataTable = new DataTable();
            dataTable.Columns.Add("Id", typeof(Guid));
            dataTable.Columns.Add("Name", typeof(string));
            dataTable.Columns.Add("Brand", typeof(string));
            dataTable.Columns.Add("Barcode", typeof(string));
            dataTable.Columns.Add("BarcodeType", typeof(string));
            dataTable.Columns.Add("Description", typeof(string));
            dataTable.Columns.Add("Category", typeof(string));
            dataTable.Columns.Add("ServingSize", typeof(string));
            dataTable.Columns.Add("ServingUnit", typeof(string));
            dataTable.Columns.Add("ImageUrl", typeof(string));
            dataTable.Columns.Add("ApprovalStatus", typeof(string));
            dataTable.Columns.Add("SubmittedBy", typeof(Guid));
            dataTable.Columns.Add("CreatedAt", typeof(DateTime));
            dataTable.Columns.Add("CreatedBy", typeof(Guid));
            dataTable.Columns.Add("IsDeleted", typeof(bool));

            return dataTable;
        }

        private static DataTable CreateProductUpdateDataTable()
        {
            DataTable dataTable = new DataTable();
            dataTable.Columns.Add("Id", typeof(Guid));
            dataTable.Columns.Add("Name", typeof(string));
            dataTable.Columns.Add("Brand", typeof(string));
            dataTable.Columns.Add("Description", typeof(string));
            dataTable.Columns.Add("Category", typeof(string));
            dataTable.Columns.Add("ServingSize", typeof(string));
            dataTable.Columns.Add("ServingUnit", typeof(string));
            dataTable.Columns.Add("UpdatedAt", typeof(DateTime));
            dataTable.Columns.Add("UpdatedBy", typeof(Guid));

            return dataTable;
        }

        private static void MapProductToDataRow(CreateProductRequest product, DataRow row)
        {
            row["Id"] = Guid.NewGuid();
            row["Name"] = product.Name;
            row["Brand"] = (object?)product.Brand ?? DBNull.Value;
            row["Barcode"] = (object?)product.Barcode ?? DBNull.Value;
            row["BarcodeType"] = (object?)product.BarcodeType ?? DBNull.Value;
            row["Description"] = (object?)product.Description ?? DBNull.Value;
            row["Category"] = (object?)product.Category ?? DBNull.Value;
            row["ServingSize"] = (object?)product.ServingSize ?? DBNull.Value;
            row["ServingUnit"] = (object?)product.ServingUnit ?? DBNull.Value;
            row["ImageUrl"] = (object?)product.ImageUrl ?? DBNull.Value;
            row["ApprovalStatus"] = "Pending";
            row["SubmittedBy"] = DBNull.Value;
            row["CreatedAt"] = DateTime.UtcNow;
            row["CreatedBy"] = DBNull.Value;
            row["IsDeleted"] = false;
        }

        private static void MapProductUpdateToDataRow(UpdateProductWithIdRequest product, DataRow row)
        {
            row["Id"] = product.Id;
            row["Name"] = product.Name;
            row["Brand"] = (object?)product.Brand ?? DBNull.Value;
            row["Description"] = (object?)product.Description ?? DBNull.Value;
            row["Category"] = (object?)product.Category ?? DBNull.Value;
            row["ServingSize"] = (object?)product.ServingSize ?? DBNull.Value;
            row["ServingUnit"] = (object?)product.ServingUnit ?? DBNull.Value;
            row["UpdatedAt"] = DateTime.UtcNow;
            row["UpdatedBy"] = DBNull.Value;
        }

        private static void MapBulkCopyColumns(SqlBulkCopy bulkCopy, DataTable dataTable)
        {
            foreach (DataColumn column in dataTable.Columns)
            {
                bulkCopy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
            }
        }

        private static ProductDto MapReaderToProductDto(IDataRecord reader)
        {
            return new ProductDto
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
                ApprovedAt = GetDateTimeNullable(reader, "ApprovedAt"),
                RejectionReason = GetString(reader, "RejectionReason"),
                SubmittedBy = GetGuidNullable(reader, "SubmittedBy"),
                CreatedAt = GetDateTimeNullable(reader, "CreatedAt") ?? DateTime.UtcNow
            };
        }

        private async Task LoadImagesAsync(ProductDto product)
        {
            if (product?.Id == null)
            {
                return;
            }

            try
            {
                List<ProductImageModel> images = await _productImageRepository.GetImagesByProductIdAsync(product.Id);
                product.Images = images.Select(img => new ProductImageDto
                {
                    Id = img.Id,
                    ProductId = img.ProductId,
                    ImageType = img.ImageType,
                    ImageUrl = img.ImageUrl,
                    LocalFilePath = img.LocalFilePath,
                    FileName = img.FileName,
                    FileSize = img.FileSize,
                    MimeType = img.MimeType,
                    Width = img.Width,
                    Height = img.Height,
                    DisplayOrder = img.DisplayOrder,
                    IsPrimary = img.IsPrimary,
                    IsUserUploaded = img.IsUserUploaded,
                    SourceSystem = img.SourceSystem,
                    SourceId = img.SourceId,
                    CreatedAt = img.CreatedAt
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to load images for product {ProductId}", product.Id);
                product.Images = [];
            }
        }

        #endregion
    }

    /// <summary>
    /// Update request with Id for bulk updates.
    /// </summary>
    public class UpdateProductWithIdRequest : UpdateProductRequest
    {
        public Guid Id { get; set; }
    }
}
