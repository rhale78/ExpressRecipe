using ExpressRecipe.Data.Common;
using System.Data;
using Microsoft.Data.SqlClient;

namespace ExpressRecipe.ProductService.Data;

public interface IProductStagingRepository
{
    Task<Guid> InsertStagingProductAsync(StagedProduct product);
    Task<int> BulkInsertStagingProductsAsync(IEnumerable<StagedProduct> products);
    Task<int> BulkAugmentStagingProductsAsync(IEnumerable<StagedProduct> products, string sourceLabel);
    Task<List<StagedProduct>> GetPendingProductsAsync(int limit = 100);
    Task UpdateProcessingStatusAsync(Guid id, string status, string? error = null);
    Task BulkUpdateProcessingStatusAsync(IEnumerable<Guid> ids, string status, string? error = null);
    Task<int> GetPendingCountAsync();
}

public class ProductStagingRepository : SqlHelper, IProductStagingRepository
{
    public ProductStagingRepository(string connectionString) : base(connectionString)
    {
    }

    public async Task<Guid> InsertStagingProductAsync(StagedProduct product)
    {
        const string sql = @"
            INSERT INTO ProductStaging (
                ExternalId, Barcode, ProductName, GenericName, Brands,
                IngredientsText, IngredientsTextEn, Allergens, AllergensHierarchy,
                Categories, CategoriesHierarchy, NutritionData,
                ImageUrl, ImageSmallUrl, Lang, Countries,
                NutriScore, NovaGroup, EcoScore, RawJson
            )
            VALUES (
                @ExternalId, @Barcode, @ProductName, @GenericName, @Brands,
                @IngredientsText, @IngredientsTextEn, @Allergens, @AllergensHierarchy,
                @Categories, @CategoriesHierarchy, @NutritionData,
                @ImageUrl, @ImageSmallUrl, @Lang, @Countries,
                @NutriScore, @NovaGroup, @EcoScore, @RawJson
            );
            SELECT CAST(SCOPE_IDENTITY() AS UNIQUEIDENTIFIER);";

        return await ExecuteScalarAsync<Guid>(sql,
            new SqlParameter("@ExternalId", product.ExternalId),
            new SqlParameter("@Barcode", (object?)product.Barcode ?? DBNull.Value),
            new SqlParameter("@ProductName", (object?)product.ProductName ?? DBNull.Value),
            new SqlParameter("@GenericName", (object?)product.GenericName ?? DBNull.Value),
            new SqlParameter("@Brands", (object?)product.Brands ?? DBNull.Value),
            new SqlParameter("@IngredientsText", (object?)product.IngredientsText ?? DBNull.Value),
            new SqlParameter("@IngredientsTextEn", (object?)product.IngredientsTextEn ?? DBNull.Value),
            new SqlParameter("@Allergens", (object?)product.Allergens ?? DBNull.Value),
            new SqlParameter("@AllergensHierarchy", (object?)product.AllergensHierarchy ?? DBNull.Value),
            new SqlParameter("@Categories", (object?)product.Categories ?? DBNull.Value),
            new SqlParameter("@CategoriesHierarchy", (object?)product.CategoriesHierarchy ?? DBNull.Value),
            new SqlParameter("@NutritionData", (object?)product.NutritionData ?? DBNull.Value),
            new SqlParameter("@ImageUrl", (object?)product.ImageUrl ?? DBNull.Value),
            new SqlParameter("@ImageSmallUrl", (object?)product.ImageSmallUrl ?? DBNull.Value),
            new SqlParameter("@Lang", (object?)product.Lang ?? DBNull.Value),
            new SqlParameter("@Countries", (object?)product.Countries ?? DBNull.Value),
            new SqlParameter("@NutriScore", (object?)product.NutriScore ?? DBNull.Value),
            new SqlParameter("@NovaGroup", (object?)product.NovaGroup ?? DBNull.Value),
            new SqlParameter("@EcoScore", (object?)product.EcoScore ?? DBNull.Value),
            new SqlParameter("@RawJson", (object?)product.RawJson ?? DBNull.Value)
        );
    }

    public async Task<int> BulkInsertStagingProductsAsync(IEnumerable<StagedProduct> products)
    {
        var productList = products.ToList();
        if (!productList.Any()) return 0;

        using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        using var transaction = connection.BeginTransaction();
        try
        {
            int insertedCount = 0;

            const string sql = @"
                INSERT INTO ProductStaging (
                    ExternalId, Barcode, ProductName, GenericName, Brands,
                    IngredientsText, IngredientsTextEn, Allergens, AllergensHierarchy,
                    Categories, CategoriesHierarchy, NutritionData,
                    ImageUrl, ImageSmallUrl, Lang, Countries,
                    NutriScore, NovaGroup, EcoScore, RawJson
                )
                VALUES (
                    @ExternalId, @Barcode, @ProductName, @GenericName, @Brands,
                    @IngredientsText, @IngredientsTextEn, @Allergens, @AllergensHierarchy,
                    @Categories, @CategoriesHierarchy, @NutritionData,
                    @ImageUrl, @ImageSmallUrl, @Lang, @Countries,
                    @NutriScore, @NovaGroup, @EcoScore, @RawJson
                )";

            using var command = new SqlCommand(sql, connection, transaction);
            command.Parameters.Add("@ExternalId", SqlDbType.NVarChar, 100);
            command.Parameters.Add("@Barcode", SqlDbType.NVarChar, 50);
            command.Parameters.Add("@ProductName", SqlDbType.NVarChar, 500);
            command.Parameters.Add("@GenericName", SqlDbType.NVarChar, 500);
            command.Parameters.Add("@Brands", SqlDbType.NVarChar, 500);
            command.Parameters.Add("@IngredientsText", SqlDbType.NVarChar, -1);
            command.Parameters.Add("@IngredientsTextEn", SqlDbType.NVarChar, -1);
            command.Parameters.Add("@Allergens", SqlDbType.NVarChar, -1);
            command.Parameters.Add("@AllergensHierarchy", SqlDbType.NVarChar, -1);
            command.Parameters.Add("@Categories", SqlDbType.NVarChar, -1);
            command.Parameters.Add("@CategoriesHierarchy", SqlDbType.NVarChar, -1);
            command.Parameters.Add("@NutritionData", SqlDbType.NVarChar, -1);
            command.Parameters.Add("@ImageUrl", SqlDbType.NVarChar, 500);
            command.Parameters.Add("@ImageSmallUrl", SqlDbType.NVarChar, 500);
            command.Parameters.Add("@Lang", SqlDbType.NVarChar, 10);
            command.Parameters.Add("@Countries", SqlDbType.NVarChar, 200);
            command.Parameters.Add("@NutriScore", SqlDbType.NVarChar, 10);
            command.Parameters.Add("@NovaGroup", SqlDbType.Int);
            command.Parameters.Add("@EcoScore", SqlDbType.NVarChar, 10);
            command.Parameters.Add("@RawJson", SqlDbType.NVarChar, -1);

            foreach (var product in productList)
            {
                command.Parameters["@ExternalId"].Value = product.ExternalId;
                command.Parameters["@Barcode"].Value = (object?)product.Barcode ?? DBNull.Value;
                command.Parameters["@ProductName"].Value = (object?)product.ProductName ?? DBNull.Value;
                command.Parameters["@GenericName"].Value = (object?)product.GenericName ?? DBNull.Value;
                command.Parameters["@Brands"].Value = (object?)product.Brands ?? DBNull.Value;
                command.Parameters["@IngredientsText"].Value = (object?)product.IngredientsText ?? DBNull.Value;
                command.Parameters["@IngredientsTextEn"].Value = (object?)product.IngredientsTextEn ?? DBNull.Value;
                command.Parameters["@Allergens"].Value = (object?)product.Allergens ?? DBNull.Value;
                command.Parameters["@AllergensHierarchy"].Value = (object?)product.AllergensHierarchy ?? DBNull.Value;
                command.Parameters["@Categories"].Value = (object?)product.Categories ?? DBNull.Value;
                command.Parameters["@CategoriesHierarchy"].Value = (object?)product.CategoriesHierarchy ?? DBNull.Value;
                command.Parameters["@NutritionData"].Value = (object?)product.NutritionData ?? DBNull.Value;
                command.Parameters["@ImageUrl"].Value = (object?)product.ImageUrl ?? DBNull.Value;
                command.Parameters["@ImageSmallUrl"].Value = (object?)product.ImageSmallUrl ?? DBNull.Value;
                command.Parameters["@Lang"].Value = (object?)product.Lang ?? DBNull.Value;
                command.Parameters["@Countries"].Value = (object?)product.Countries ?? DBNull.Value;
                command.Parameters["@NutriScore"].Value = (object?)product.NutriScore ?? DBNull.Value;
                command.Parameters["@NovaGroup"].Value = (object?)product.NovaGroup ?? DBNull.Value;
                command.Parameters["@EcoScore"].Value = (object?)product.EcoScore ?? DBNull.Value;
                command.Parameters["@RawJson"].Value = (object?)product.RawJson ?? DBNull.Value;

                try
                {
                    await command.ExecuteNonQueryAsync();
                    insertedCount++;
                }
                catch (SqlException ex) when (ex.Number == 2627) // Unique constraint violation
                {
                    // Skip duplicates
                    continue;
                }
            }

                    await transaction.CommitAsync();
                    return insertedCount;
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }

            public async Task<int> BulkAugmentStagingProductsAsync(IEnumerable<StagedProduct> products, string sourceLabel)
            {
                var productList = products.ToList();
                if (!productList.Any()) return 0;

                using var connection = new SqlConnection(ConnectionString);
                await connection.OpenAsync();

                using var transaction = connection.BeginTransaction();
                try
                {
                    int augmentedCount = 0;

                    const string sql = @"
                        UPDATE ProductStaging
                        SET 
                            ProductName = COALESCE(ProductName, @ProductName),
                            GenericName = COALESCE(GenericName, @GenericName),
                            Brands = COALESCE(Brands, @Brands),
                            IngredientsText = COALESCE(IngredientsText, @IngredientsText),
                            IngredientsTextEn = COALESCE(IngredientsTextEn, @IngredientsTextEn),
                            Allergens = COALESCE(Allergens, @Allergens),
                            AllergensHierarchy = COALESCE(AllergensHierarchy, @AllergensHierarchy),
                            Categories = COALESCE(Categories, @Categories),
                            CategoriesHierarchy = COALESCE(CategoriesHierarchy, @CategoriesHierarchy),
                            NutritionData = COALESCE(NutritionData, @NutritionData),
                            ImageUrl = COALESCE(ImageUrl, @ImageUrl),
                            ImageSmallUrl = COALESCE(ImageSmallUrl, @ImageSmallUrl),
                            Countries = COALESCE(Countries, @Countries),
                            NutriScore = COALESCE(NutriScore, @NutriScore),
                            NovaGroup = COALESCE(NovaGroup, @NovaGroup),
                            EcoScore = COALESCE(EcoScore, @EcoScore),
                            ModifiedDate = GETUTCDATE()
                        WHERE Barcode = @Barcode AND IsDeleted = 0";

                    using var command = new SqlCommand(sql, connection, transaction);
                    command.Parameters.Add("@Barcode", SqlDbType.NVarChar, 50);
                    command.Parameters.Add("@ProductName", SqlDbType.NVarChar, 500);
                    command.Parameters.Add("@GenericName", SqlDbType.NVarChar, 500);
                    command.Parameters.Add("@Brands", SqlDbType.NVarChar, 500);
                    command.Parameters.Add("@IngredientsText", SqlDbType.NVarChar, -1);
                    command.Parameters.Add("@IngredientsTextEn", SqlDbType.NVarChar, -1);
                    command.Parameters.Add("@Allergens", SqlDbType.NVarChar, -1);
                    command.Parameters.Add("@AllergensHierarchy", SqlDbType.NVarChar, -1);
                    command.Parameters.Add("@Categories", SqlDbType.NVarChar, -1);
                    command.Parameters.Add("@CategoriesHierarchy", SqlDbType.NVarChar, -1);
                    command.Parameters.Add("@NutritionData", SqlDbType.NVarChar, -1);
                    command.Parameters.Add("@ImageUrl", SqlDbType.NVarChar, 500);
                    command.Parameters.Add("@ImageSmallUrl", SqlDbType.NVarChar, 500);
                    command.Parameters.Add("@Countries", SqlDbType.NVarChar, 200);
                    command.Parameters.Add("@NutriScore", SqlDbType.NVarChar, 10);
                    command.Parameters.Add("@NovaGroup", SqlDbType.Int);
                    command.Parameters.Add("@EcoScore", SqlDbType.NVarChar, 10);

                    foreach (var product in productList)
                    {
                        if (string.IsNullOrWhiteSpace(product.Barcode))
                            continue;

                        command.Parameters["@Barcode"].Value = product.Barcode;
                        command.Parameters["@ProductName"].Value = (object?)product.ProductName ?? DBNull.Value;
                        command.Parameters["@GenericName"].Value = (object?)product.GenericName ?? DBNull.Value;
                        command.Parameters["@Brands"].Value = (object?)product.Brands ?? DBNull.Value;
                        command.Parameters["@IngredientsText"].Value = (object?)product.IngredientsText ?? DBNull.Value;
                        command.Parameters["@IngredientsTextEn"].Value = (object?)product.IngredientsTextEn ?? DBNull.Value;
                        command.Parameters["@Allergens"].Value = (object?)product.Allergens ?? DBNull.Value;
                        command.Parameters["@AllergensHierarchy"].Value = (object?)product.AllergensHierarchy ?? DBNull.Value;
                        command.Parameters["@Categories"].Value = (object?)product.Categories ?? DBNull.Value;
                        command.Parameters["@CategoriesHierarchy"].Value = (object?)product.CategoriesHierarchy ?? DBNull.Value;
                        command.Parameters["@NutritionData"].Value = (object?)product.NutritionData ?? DBNull.Value;
                        command.Parameters["@ImageUrl"].Value = (object?)product.ImageUrl ?? DBNull.Value;
                        command.Parameters["@ImageSmallUrl"].Value = (object?)product.ImageSmallUrl ?? DBNull.Value;
                        command.Parameters["@Countries"].Value = (object?)product.Countries ?? DBNull.Value;
                        command.Parameters["@NutriScore"].Value = (object?)product.NutriScore ?? DBNull.Value;
                        command.Parameters["@NovaGroup"].Value = (object?)product.NovaGroup ?? DBNull.Value;
                        command.Parameters["@EcoScore"].Value = (object?)product.EcoScore ?? DBNull.Value;

                        var rowsAffected = await command.ExecuteNonQueryAsync();
                        if (rowsAffected > 0)
                            augmentedCount++;
                    }

                    await transaction.CommitAsync();
                    return augmentedCount;
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }

            public async Task<List<StagedProduct>> GetPendingProductsAsync(int limit = 100)
    {
        const string sql = @"
            SELECT TOP (@Limit)
                Id, ExternalId, Barcode, ProductName, GenericName, Brands,
                IngredientsText, IngredientsTextEn, Allergens, AllergensHierarchy,
                Categories, CategoriesHierarchy, NutritionData,
                ImageUrl, ImageSmallUrl, Lang, Countries,
                NutriScore, NovaGroup, EcoScore, RawJson,
                ProcessingStatus, ProcessedAt, ProcessingError, ProcessingAttempts,
                CreatedDate, UpdatedAt
            FROM ProductStaging WITH (UPDLOCK, READPAST)
            WHERE ProcessingStatus = 'Pending'
                AND IsDeleted = 0
                AND ProcessingAttempts < 3
            ORDER BY CreatedDate ASC";

        // Use 120 second timeout during high-volume processing
        return await ExecuteReaderAsync(sql, MapStagedProduct, timeoutSeconds: 120, new SqlParameter("@Limit", limit));
    }

    public async Task UpdateProcessingStatusAsync(Guid id, string status, string? error = null)
    {
        const string sql = @"
            UPDATE ProductStaging
            SET ProcessingStatus = @Status,
                ProcessedAt = CASE WHEN @Status = 'Completed' THEN GETUTCDATE() ELSE ProcessedAt END,
                ProcessingError = @Error,
                ProcessingAttempts = ProcessingAttempts + 1,
                ModifiedDate = GETUTCDATE()
            WHERE Id = @Id";

        await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", id),
            new SqlParameter("@Status", status),
            new SqlParameter("@Error", (object?)error ?? DBNull.Value)
        );
    }

    public async Task<int> GetPendingCountAsync()
    {
        const string sql = @"
            SELECT COUNT(*)
            FROM ProductStaging WITH (NOLOCK)
            WHERE ProcessingStatus = 'Pending'
                AND IsDeleted = 0
                AND ProcessingAttempts < 3";

        // Use 120 second timeout during high-volume processing
        return await ExecuteScalarAsync<int>(sql, timeoutSeconds: 120);
    }

    private static StagedProduct MapStagedProduct(SqlDataReader reader)
    {
        return new StagedProduct
        {
            Id = reader.GetGuid("Id"),
            ExternalId = reader.GetString("ExternalId"),
            Barcode = reader.IsDBNull("Barcode") ? null : reader.GetString("Barcode"),
            ProductName = reader.IsDBNull("ProductName") ? null : reader.GetString("ProductName"),
            GenericName = reader.IsDBNull("GenericName") ? null : reader.GetString("GenericName"),
            Brands = reader.IsDBNull("Brands") ? null : reader.GetString("Brands"),
            IngredientsText = reader.IsDBNull("IngredientsText") ? null : reader.GetString("IngredientsText"),
            IngredientsTextEn = reader.IsDBNull("IngredientsTextEn") ? null : reader.GetString("IngredientsTextEn"),
            Allergens = reader.IsDBNull("Allergens") ? null : reader.GetString("Allergens"),
            AllergensHierarchy = reader.IsDBNull("AllergensHierarchy") ? null : reader.GetString("AllergensHierarchy"),
            Categories = reader.IsDBNull("Categories") ? null : reader.GetString("Categories"),
            CategoriesHierarchy = reader.IsDBNull("CategoriesHierarchy") ? null : reader.GetString("CategoriesHierarchy"),
            NutritionData = reader.IsDBNull("NutritionData") ? null : reader.GetString("NutritionData"),
            ImageUrl = reader.IsDBNull("ImageUrl") ? null : reader.GetString("ImageUrl"),
            ImageSmallUrl = reader.IsDBNull("ImageSmallUrl") ? null : reader.GetString("ImageSmallUrl"),
            Lang = reader.IsDBNull("Lang") ? null : reader.GetString("Lang"),
            Countries = reader.IsDBNull("Countries") ? null : reader.GetString("Countries"),
            NutriScore = reader.IsDBNull("NutriScore") ? null : reader.GetString("NutriScore"),
            NovaGroup = reader.IsDBNull("NovaGroup") ? null : reader.GetInt32("NovaGroup"),
            EcoScore = reader.IsDBNull("EcoScore") ? null : reader.GetString("EcoScore"),
            RawJson = reader.IsDBNull("RawJson") ? null : reader.GetString("RawJson"),
            ProcessingStatus = reader.GetString("ProcessingStatus"),
            ProcessedAt = reader.IsDBNull("ProcessedAt") ? null : reader.GetDateTime("ProcessedAt"),
            ProcessingError = reader.IsDBNull("ProcessingError") ? null : reader.GetString("ProcessingError"),
            ProcessingAttempts = reader.GetInt32("ProcessingAttempts"),
            CreatedDate = reader.GetDateTime("CreatedDate"),
            ModifiedDate = reader.IsDBNull("UpdatedAt") ? null : reader.GetDateTime("UpdatedAt")
        };
    }

    public Task BulkUpdateProcessingStatusAsync(IEnumerable<Guid> ids, string status, string? error = null)
    {
        throw new NotImplementedException("Use ProductStagingRepositoryAdapter for bulk operations with HighSpeedDAL");
    }
}

public class StagedProduct
{
    public Guid Id { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string? ProductName { get; set; }
    public string? GenericName { get; set; }
    public string? Brands { get; set; }
    public string? IngredientsText { get; set; }
    public string? IngredientsTextEn { get; set; }
    public string? Allergens { get; set; }
    public string? AllergensHierarchy { get; set; }
    public string? Categories { get; set; }
    public string? CategoriesHierarchy { get; set; }
    public string? NutritionData { get; set; }
    public string? ImageUrl { get; set; }
    public string? ImageSmallUrl { get; set; }
    public string? Lang { get; set; }
    public string? Countries { get; set; }
    public string? NutriScore { get; set; }
    public int? NovaGroup { get; set; }
    public string? EcoScore { get; set; }
    public string? RawJson { get; set; }
    public string ProcessingStatus { get; set; } = "Pending";
    public DateTime? ProcessedAt { get; set; }
    public string? ProcessingError { get; set; }
    public int ProcessingAttempts { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
}
