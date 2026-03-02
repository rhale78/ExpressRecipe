using ExpressRecipe.Data.Common;
using ExpressRecipe.ProductService.Logging;
using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace ExpressRecipe.ProductService.Data;

public interface IProductStagingRepository
{
    Task<Guid> InsertStagingProductAsync(StagedProduct product);
    Task<int> BulkInsertStagingProductsAsync(IEnumerable<StagedProduct> products);
    Task<int> BulkAugmentStagingProductsAsync(IEnumerable<StagedProduct> products, string sourceLabel);
    Task<List<StagedProduct>> GetPendingProductsAsync(int limit = 100);
    Task UpdateProcessingStatusAsync(Guid id, string status, string? error = null);
    Task BulkUpdateStatusAsync(IEnumerable<Guid> ids, string status, string? error = null);
    Task<int> GetPendingCountAsync();
}

public class ProductStagingRepository : SqlHelper, IProductStagingRepository
{
    private readonly ILogger<ProductStagingRepository>? _logger;

    public ProductStagingRepository(string connectionString, ILogger<ProductStagingRepository>? logger = null) : base(connectionString)
    {
        _logger = logger;
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

        var sw = System.Diagnostics.Stopwatch.StartNew();

        using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        using var transaction = connection.BeginTransaction();
        try
        {
            // 1. Create temp table for staging
            const string createTempSql = @"
                CREATE TABLE #TempProductStaging (
                    ExternalId NVARCHAR(100), Barcode NVARCHAR(50), ProductName NVARCHAR(500), 
                    GenericName NVARCHAR(MAX), Brands NVARCHAR(500), IngredientsText NVARCHAR(MAX), 
                    IngredientsTextEn NVARCHAR(MAX), Allergens NVARCHAR(MAX), AllergensHierarchy NVARCHAR(MAX),
                    Categories NVARCHAR(MAX), CategoriesHierarchy NVARCHAR(MAX), NutritionData NVARCHAR(MAX),
                    ImageUrl NVARCHAR(500), ImageSmallUrl NVARCHAR(500), Lang NVARCHAR(10), 
                    Countries NVARCHAR(200), NutriScore NVARCHAR(20), NovaGroup INT, 
                    EcoScore NVARCHAR(20), RawJson NVARCHAR(MAX)
                )";

            using (var cmd = new SqlCommand(createTempSql, connection, transaction))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            // 2. Bulk Copy into temp table
            var dt = new DataTable();
            dt.Columns.Add("ExternalId", typeof(string));
            dt.Columns.Add("Barcode", typeof(string));
            dt.Columns.Add("ProductName", typeof(string));
            dt.Columns.Add("GenericName", typeof(string));
            dt.Columns.Add("Brands", typeof(string));
            dt.Columns.Add("IngredientsText", typeof(string));
            dt.Columns.Add("IngredientsTextEn", typeof(string));
            dt.Columns.Add("Allergens", typeof(string));
            dt.Columns.Add("AllergensHierarchy", typeof(string));
            dt.Columns.Add("Categories", typeof(string));
            dt.Columns.Add("CategoriesHierarchy", typeof(string));
            dt.Columns.Add("NutritionData", typeof(string));
            dt.Columns.Add("ImageUrl", typeof(string));
            dt.Columns.Add("ImageSmallUrl", typeof(string));
            dt.Columns.Add("Lang", typeof(string));
            dt.Columns.Add("Countries", typeof(string));
            dt.Columns.Add("NutriScore", typeof(string));
            dt.Columns.Add("NovaGroup", typeof(int));
            dt.Columns.Add("EcoScore", typeof(string));
            dt.Columns.Add("RawJson", typeof(string));

            foreach (var p in productList)
            {
                dt.Rows.Add(
                    p.ExternalId,
                    (object?)p.Barcode ?? DBNull.Value,
                    (object?)p.ProductName ?? DBNull.Value,
                    (object?)p.GenericName ?? DBNull.Value,
                    (object?)p.Brands ?? DBNull.Value,
                    (object?)p.IngredientsText ?? DBNull.Value,
                    (object?)p.IngredientsTextEn ?? DBNull.Value,
                    (object?)p.Allergens ?? DBNull.Value,
                    (object?)p.AllergensHierarchy ?? DBNull.Value,
                    (object?)p.Categories ?? DBNull.Value,
                    (object?)p.CategoriesHierarchy ?? DBNull.Value,
                    (object?)p.NutritionData ?? DBNull.Value,
                    (object?)p.ImageUrl ?? DBNull.Value,
                    (object?)p.ImageSmallUrl ?? DBNull.Value,
                    (object?)p.Lang ?? DBNull.Value,
                    (object?)p.Countries ?? DBNull.Value,
                    (object?)p.NutriScore ?? DBNull.Value,
                    (object?)p.NovaGroup ?? DBNull.Value,
                    (object?)p.EcoScore ?? DBNull.Value,
                    (object?)p.RawJson ?? DBNull.Value
                );
            }

            using (var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, transaction))
            {
                bulkCopy.DestinationTableName = "#TempProductStaging";
                bulkCopy.BatchSize = 5000;
                foreach (DataColumn col in dt.Columns) bulkCopy.ColumnMappings.Add(col.ColumnName, col.ColumnName);
                await bulkCopy.WriteToServerAsync(dt);
            }

            // 3. MERGE into real table
            const string mergeSql = @"
                MERGE ProductStaging AS target
                USING (
                    SELECT 
                        ExternalId, 
                        MAX(Barcode) as Barcode, 
                        MAX(ProductName) as ProductName, 
                        MAX(GenericName) as GenericName, 
                        MAX(Brands) as Brands,
                        MAX(IngredientsText) as IngredientsText, 
                        MAX(IngredientsTextEn) as IngredientsTextEn, 
                        MAX(Allergens) as Allergens, 
                        MAX(AllergensHierarchy) as AllergensHierarchy,
                        MAX(Categories) as Categories, 
                        MAX(CategoriesHierarchy) as CategoriesHierarchy, 
                        MAX(NutritionData) as NutritionData,
                        MAX(ImageUrl) as ImageUrl, 
                        MAX(ImageSmallUrl) as ImageSmallUrl, 
                        MAX(Lang) as Lang, 
                        MAX(Countries) as Countries,
                        MAX(NutriScore) as NutriScore, 
                        MAX(NovaGroup) as NovaGroup, 
                        MAX(EcoScore) as EcoScore, 
                        MAX(RawJson) as RawJson
                    FROM #TempProductStaging
                    GROUP BY ExternalId
                ) AS source
                ON (target.ExternalId = source.ExternalId)
                WHEN MATCHED THEN
                    UPDATE SET 
                        Barcode = COALESCE(source.Barcode, target.Barcode),
                        ProductName = COALESCE(source.ProductName, target.ProductName),
                        GenericName = COALESCE(source.GenericName, target.GenericName),
                        Brands = COALESCE(source.Brands, target.Brands),
                        IngredientsText = COALESCE(source.IngredientsText, target.IngredientsText),
                        IngredientsTextEn = COALESCE(source.IngredientsTextEn, target.IngredientsTextEn),
                        NutritionData = COALESCE(source.NutritionData, target.NutritionData),
                        ImageUrl = COALESCE(source.ImageUrl, target.ImageUrl),
                        UpdatedAt = GETUTCDATE()
                WHEN NOT MATCHED THEN
                    INSERT (
                        ExternalId, Barcode, ProductName, GenericName, Brands,
                        IngredientsText, IngredientsTextEn, Allergens, AllergensHierarchy,
                        Categories, CategoriesHierarchy, NutritionData,
                        ImageUrl, ImageSmallUrl, Lang, Countries,
                        NutriScore, NovaGroup, EcoScore, RawJson,
                        CreatedAt, UpdatedAt, ProcessingStatus, ProcessingAttempts, IsDeleted
                    )
                    VALUES (
                        source.ExternalId, source.Barcode, source.ProductName, source.GenericName, source.Brands,
                        source.IngredientsText, source.IngredientsTextEn, source.Allergens, source.AllergensHierarchy,
                        source.Categories, source.CategoriesHierarchy, source.NutritionData,
                        source.ImageUrl, source.ImageSmallUrl, source.Lang, source.Countries,
                        source.NutriScore, source.NovaGroup, source.EcoScore, source.RawJson,
                        GETUTCDATE(), GETUTCDATE(), 'Pending', 0, 0
                    );";

            using (var cmd = new SqlCommand(mergeSql, connection, transaction))
            {
                cmd.CommandTimeout = 300;
                var rows = await cmd.ExecuteNonQueryAsync();
                await transaction.CommitAsync();

                sw.Stop();
                var recordsPerSec = productList.Count / (sw.ElapsedMilliseconds / 1000.0);
                _logger?.LogBulkInsert(productList.Count, rows, sw.ElapsedMilliseconds, recordsPerSec);

                return rows;
            }
        }
        catch
        {
            if (transaction.Connection != null) await transaction.RollbackAsync();
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
                                                // 1. Create temp table for augmenting
                                                const string createTempSql = @"
                                                    CREATE TABLE #TempAugment (
                                                        Barcode NVARCHAR(50), ProductName NVARCHAR(500), GenericName NVARCHAR(MAX), 
                                                        Brands NVARCHAR(500), IngredientsText NVARCHAR(MAX), IngredientsTextEn NVARCHAR(MAX), 
                                                        Allergens NVARCHAR(MAX), AllergensHierarchy NVARCHAR(MAX), Categories NVARCHAR(MAX), 
                                                        CategoriesHierarchy NVARCHAR(MAX), NutritionData NVARCHAR(MAX), ImageUrl NVARCHAR(500), 
                                                        ImageSmallUrl NVARCHAR(500), Countries NVARCHAR(200), NutriScore NVARCHAR(20), 
                                                        NovaGroup INT, EcoScore NVARCHAR(20)
                                                    )";                        using (var cmd = new SqlCommand(createTempSql, connection, transaction))
                        {
                            await cmd.ExecuteNonQueryAsync();
                        }
            
                        // 2. Bulk Copy into temp table
                        var dt = new DataTable();
                        dt.Columns.Add("Barcode", typeof(string));
                        dt.Columns.Add("ProductName", typeof(string));
                        dt.Columns.Add("GenericName", typeof(string));
                        dt.Columns.Add("Brands", typeof(string));
                        dt.Columns.Add("IngredientsText", typeof(string));
                        dt.Columns.Add("IngredientsTextEn", typeof(string));
                        dt.Columns.Add("Allergens", typeof(string));
                        dt.Columns.Add("AllergensHierarchy", typeof(string));
                        dt.Columns.Add("Categories", typeof(string));
                        dt.Columns.Add("CategoriesHierarchy", typeof(string));
                        dt.Columns.Add("NutritionData", typeof(string));
                        dt.Columns.Add("ImageUrl", typeof(string));
                        dt.Columns.Add("ImageSmallUrl", typeof(string));
                        dt.Columns.Add("Countries", typeof(string));
                        dt.Columns.Add("NutriScore", typeof(string));
                        dt.Columns.Add("NovaGroup", typeof(int));
                        dt.Columns.Add("EcoScore", typeof(string));
            
                        foreach (var p in productList)
                        {
                            if (string.IsNullOrWhiteSpace(p.Barcode)) continue;
                            dt.Rows.Add(
                                p.Barcode,
                                (object?)p.ProductName ?? DBNull.Value,
                                (object?)p.GenericName ?? DBNull.Value,
                                (object?)p.Brands ?? DBNull.Value,
                                (object?)p.IngredientsText ?? DBNull.Value,
                                (object?)p.IngredientsTextEn ?? DBNull.Value,
                                (object?)p.Allergens ?? DBNull.Value,
                                (object?)p.AllergensHierarchy ?? DBNull.Value,
                                (object?)p.Categories ?? DBNull.Value,
                                (object?)p.CategoriesHierarchy ?? DBNull.Value,
                                (object?)p.NutritionData ?? DBNull.Value,
                                (object?)p.ImageUrl ?? DBNull.Value,
                                (object?)p.ImageSmallUrl ?? DBNull.Value,
                                (object?)p.Countries ?? DBNull.Value,
                                (object?)p.NutriScore ?? DBNull.Value,
                                (object?)p.NovaGroup ?? DBNull.Value,
                                (object?)p.EcoScore ?? DBNull.Value
                            );
                        }
            
                        using (var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, transaction))
                        {
                            bulkCopy.DestinationTableName = "#TempAugment";
                            bulkCopy.BatchSize = 5000;
                            foreach (DataColumn col in dt.Columns) bulkCopy.ColumnMappings.Add(col.ColumnName, col.ColumnName);
                            await bulkCopy.WriteToServerAsync(dt);
                        }
            
                                    // 3. MERGE (UPDATE existing)
                                    const string mergeSql = @"
                                        UPDATE target
                                        SET 
                                            ProductName = COALESCE(target.ProductName, source.ProductName),
                                            GenericName = COALESCE(target.GenericName, source.GenericName),
                                            Brands = COALESCE(target.Brands, source.Brands),
                                            IngredientsText = COALESCE(target.IngredientsText, source.IngredientsText),
                                            IngredientsTextEn = COALESCE(target.IngredientsTextEn, source.IngredientsTextEn),
                                            Allergens = COALESCE(target.Allergens, source.Allergens),
                                            AllergensHierarchy = COALESCE(target.AllergensHierarchy, source.AllergensHierarchy),
                                            Categories = COALESCE(target.Categories, source.Categories),
                                            CategoriesHierarchy = COALESCE(target.CategoriesHierarchy, source.CategoriesHierarchy),
                                            NutritionData = COALESCE(target.NutritionData, source.NutritionData),
                                            ImageUrl = COALESCE(target.ImageUrl, source.ImageUrl),
                                            ImageSmallUrl = COALESCE(target.ImageSmallUrl, source.ImageSmallUrl),
                                            Countries = COALESCE(target.Countries, source.Countries),
                                            NutriScore = COALESCE(target.NutriScore, source.NutriScore),
                                            NovaGroup = COALESCE(target.NovaGroup, source.NovaGroup),
                                            EcoScore = COALESCE(target.EcoScore, source.EcoScore),
                                            UpdatedAt = GETUTCDATE()
                                        FROM ProductStaging target
                                        INNER JOIN (
                                            SELECT 
                                                Barcode, 
                                                MAX(ProductName) as ProductName, MAX(GenericName) as GenericName, 
                                                MAX(Brands) as Brands, MAX(IngredientsText) as IngredientsText, 
                                                MAX(IngredientsTextEn) as IngredientsTextEn, MAX(Allergens) as Allergens, 
                                                MAX(AllergensHierarchy) as AllergensHierarchy, MAX(Categories) as Categories, 
                                                MAX(CategoriesHierarchy) as CategoriesHierarchy, MAX(NutritionData) as NutritionData, 
                                                MAX(ImageUrl) as ImageUrl, MAX(ImageSmallUrl) as ImageSmallUrl, 
                                                MAX(Countries) as Countries, MAX(NutriScore) as NutriScore, 
                                                MAX(NovaGroup) as NovaGroup, MAX(EcoScore) as EcoScore
                                            FROM #TempAugment
                                            GROUP BY Barcode
                                        ) source ON target.Barcode = source.Barcode
                                        WHERE target.IsDeleted = 0";            
                        using (var cmd = new SqlCommand(mergeSql, connection, transaction))
                        {
                            cmd.CommandTimeout = 300;
                            var rows = await cmd.ExecuteNonQueryAsync();
                            await transaction.CommitAsync();
                            return rows;
                        }
                    }
                    catch
                    {
                        if (transaction.Connection != null) await transaction.RollbackAsync();
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
                CreatedAt, UpdatedAt
            FROM ProductStaging WITH (UPDLOCK, READPAST)
            WHERE ProcessingStatus = 'Pending'
                AND IsDeleted = 0
                AND ProcessingAttempts < 3
            ORDER BY CreatedAt ASC";

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
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id";

        await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", id),
            new SqlParameter("@Status", status),
            new SqlParameter("@Error", (object?)error ?? DBNull.Value)
        );
    }

    public async Task BulkUpdateStatusAsync(IEnumerable<Guid> ids, string status, string? error = null)
    {
        var idList = ids.Distinct().ToList(); // Remove duplicates to prevent PK violation
        if (!idList.Any()) return;

        // OPTIMIZATION: Use batched updates with IN clause instead of temp tables
        // SQL Server has a ~2100 parameter limit, so batch if needed
        const int batchSize = 2000;

        for (int i = 0; i < idList.Count; i += batchSize)
        {
            var batch = idList.Skip(i).Take(batchSize).ToList();
            await ExecuteBatchUpdateAsync(batch, status, error);
        }
    }

    private async Task ExecuteBatchUpdateAsync(List<Guid> ids, string status, string? error)
    {
        // Build parameterized IN clause (more efficient than temp table for small-medium batches)
        var parameters = new List<SqlParameter>
        {
            new SqlParameter("@Status", status),
            new SqlParameter("@Error", (object?)error ?? DBNull.Value)
        };

        // Create parameter placeholders for IN clause
        var paramNames = new List<string>();
        for (int i = 0; i < ids.Count; i++)
        {
            var paramName = $"@Id{i}";
            paramNames.Add(paramName);
            parameters.Add(new SqlParameter(paramName, ids[i]));
        }

        var sql = $@"
            UPDATE ProductStaging
            SET ProcessingStatus = @Status,
                ProcessedAt = CASE WHEN @Status = 'Completed' THEN GETUTCDATE() ELSE ProcessedAt END,
                ProcessingError = @Error,
                ProcessingAttempts = ProcessingAttempts + 1,
                UpdatedAt = GETUTCDATE()
            WHERE Id IN ({string.Join(", ", paramNames)})";

        await ExecuteNonQueryAsync(sql, parameters.ToArray());
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
            CreatedAt = reader.GetDateTime("CreatedAt"),
            UpdatedAt = reader.IsDBNull("UpdatedAt") ? null : reader.GetDateTime("UpdatedAt")
        };
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
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
