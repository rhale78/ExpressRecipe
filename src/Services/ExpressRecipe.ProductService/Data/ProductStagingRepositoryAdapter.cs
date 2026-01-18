using System;
using System.Data;
using System.Linq;
using Microsoft.Data.SqlClient;
using ExpressRecipe.ProductService.Entities;

namespace ExpressRecipe.ProductService.Data;

public class ProductStagingRepositoryAdapter : IProductStagingRepository
{
    private readonly ProductDatabaseConnection _dbConnection;
    private readonly ProductStagingEntityDal _dal;

    public ProductStagingRepositoryAdapter(ProductDatabaseConnection dbConnection, ProductStagingEntityDal dal)
    {
        _dbConnection = dbConnection ?? throw new ArgumentNullException(nameof(dbConnection));
        _dal = dal ?? throw new ArgumentNullException(nameof(dal));
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
            OUTPUT INSERTED.Id
            VALUES (
                @ExternalId, @Barcode, @ProductName, @GenericName, @Brands,
                @IngredientsText, @IngredientsTextEn, @Allergens, @AllergensHierarchy,
                @Categories, @CategoriesHierarchy, @NutritionData,
                @ImageUrl, @ImageSmallUrl, @Lang, @Countries,
                @NutriScore, @NovaGroup, @EcoScore, @RawJson
            );";

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

        // 1. Identify which products already exist to avoid Unique Key violations
        var externalIds = productList.Select(p => p.ExternalId).Distinct().ToList();
        var existingIds = await GetExistingExternalIdsAsync(externalIds);

        // 2. Filter out duplicates
        var newProducts = productList
            .Where(p => !existingIds.Contains(p.ExternalId))
            .DistinctBy(p => p.ExternalId) // Ensure no internal duplicates in the batch either
            .ToList();

        if (!newProducts.Any()) return 0;

        // 3. Map StagedProduct POCOs to ProductStagingEntity for HighSpeedDAL
        var entities = newProducts.Select(p => new ProductStagingEntity
        {
            Id = p.Id != Guid.Empty ? p.Id : Guid.NewGuid(),
            ExternalId = p.ExternalId,
            Barcode = p.Barcode,
            ProductName = p.ProductName,
            GenericName = p.GenericName,
            Brands = p.Brands,
            IngredientsText = p.IngredientsText,
            IngredientsTextEn = p.IngredientsTextEn,
            Allergens = p.Allergens,
            AllergensHierarchy = p.AllergensHierarchy,
            Categories = p.Categories,
            CategoriesHierarchy = p.CategoriesHierarchy,
            NutritionData = p.NutritionData,
            ImageUrl = p.ImageUrl,
            ImageSmallUrl = p.ImageSmallUrl,
            Lang = p.Lang,
            Countries = p.Countries,
            NutriScore = SanitizeScore(p.NutriScore),
            NovaGroup = p.NovaGroup,
            EcoScore = SanitizeScore(p.EcoScore),
            RawJson = p.RawJson,
            ProcessingStatus = "Pending",
            CreatedDate = DateTime.UtcNow,
            IsDeleted = false
        }).ToList();

        // 4. Use HighSpeedDAL's bulk insert
        return await _dal.BulkInsertAsync(entities, "System", System.Threading.CancellationToken.None);
    }

    private async Task<System.Collections.Generic.HashSet<string>> GetExistingExternalIdsAsync(System.Collections.Generic.List<string> externalIds)
    {
        var existing = new System.Collections.Generic.HashSet<string>();
        if (!externalIds.Any()) return existing;

        await using var conn = new SqlConnection(_dbConnection.ConnectionString);
        await conn.OpenAsync();

        // Create a temp table to hold the IDs we want to check
        using (var cmd = new SqlCommand("CREATE TABLE #CheckIds (ExternalId NVARCHAR(100) COLLATE DATABASE_DEFAULT PRIMARY KEY)", conn))
        {
            await cmd.ExecuteNonQueryAsync();
        }

        // Bulk insert IDs into temp table
        using (var bulk = new SqlBulkCopy(conn))
        {
            bulk.DestinationTableName = "#CheckIds";
            var table = new DataTable();
            table.Columns.Add("ExternalId", typeof(string));
            foreach (var id in externalIds) table.Rows.Add(id);
            await bulk.WriteToServerAsync(table);
        }

        // Query for matches
        using (var cmd = new SqlCommand("SELECT p.ExternalId FROM ProductStaging p JOIN #CheckIds c ON p.ExternalId = c.ExternalId WHERE p.IsDeleted = 0", conn))
        using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                existing.Add(reader.GetString(0));
            }
        }

        return existing;
    }

    private static string? SanitizeScore(string? score)
    {
        if (string.IsNullOrWhiteSpace(score)) return null;
        if (score.Length > 10) return score.Substring(0, 10);
        return score;
    }

    public async Task<int> BulkAugmentStagingProductsAsync(IEnumerable<StagedProduct> products, string sourceLabel)
    {
        var productList = products.ToList();
        if (!productList.Any()) return 0;

        await using var connection = new SqlConnection(_dbConnection.ConnectionString);
        await connection.OpenAsync();

        using var transaction = (SqlTransaction)connection.BeginTransaction();
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
            transaction.Commit();
            return augmentedCount;
        }
        catch
        {
            transaction.Rollback();
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
                CreatedDate, ModifiedDate
            FROM ProductStaging WITH (UPDLOCK, READPAST)
            WHERE ProcessingStatus = 'Pending'
                AND IsDeleted = 0
                AND ProcessingAttempts < 3
            ORDER BY CreatedDate ASC";

        // Use 120 second timeout during high-volume processing
        return await ExecuteReaderAsync(sql, MapStagedProduct, 120, new SqlParameter("@Limit", limit));
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
        return await ExecuteScalarAsync<int>(sql);
    }

    public async Task<int> ExecuteNonQueryAsync(string sql, params SqlParameter[] parameters)
    {
        await using var conn = new SqlConnection(_dbConnection.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        if (parameters?.Any() == true) cmd.Parameters.AddRange(parameters);
        return await cmd.ExecuteNonQueryAsync();
    }

    private async Task<TResult> ExecuteScalarAsync<TResult>(string sql, params SqlParameter[] parameters)
    {
        await using var conn = new SqlConnection(_dbConnection.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn)
        {
            CommandTimeout = 30
        };
        if (parameters?.Any() == true) cmd.Parameters.AddRange(parameters);
        var result = await cmd.ExecuteScalarAsync();
        if (result == null || result == DBNull.Value)
            return default!;

        // Convert.ChangeType does not support Guid; handle common conversions explicitly
        if (typeof(TResult) == typeof(Guid))
        {
            return (TResult)(object) (result is Guid g ? g : Guid.Parse(result.ToString()!));
        }

        return (TResult)Convert.ChangeType(result, typeof(TResult));
    }

    private async Task<List<T>> ExecuteReaderAsync<T>(string sql, Func<SqlDataReader, T> map, int timeoutSeconds = 30, params SqlParameter[] parameters)
    {
        var list = new List<T>();
        await using var conn = new SqlConnection(_dbConnection.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn)
        {
            CommandTimeout = timeoutSeconds
        };
        if (parameters?.Any() == true) cmd.Parameters.AddRange(parameters);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(map(reader));
        }
        return list;
    }

    // timeout overload removed to avoid ambiguous overload resolution with params overload

    private static StagedProduct MapStagedProduct(SqlDataReader reader)
    {
        return new StagedProduct
        {
            Id = reader.GetGuid(reader.GetOrdinal("Id")),
            ExternalId = reader.GetString(reader.GetOrdinal("ExternalId")),
            Barcode = reader.IsDBNull(reader.GetOrdinal("Barcode")) ? null : reader.GetString(reader.GetOrdinal("Barcode")),
            ProductName = reader.IsDBNull(reader.GetOrdinal("ProductName")) ? null : reader.GetString(reader.GetOrdinal("ProductName")),
            GenericName = reader.IsDBNull(reader.GetOrdinal("GenericName")) ? null : reader.GetString(reader.GetOrdinal("GenericName")),
            Brands = reader.IsDBNull(reader.GetOrdinal("Brands")) ? null : reader.GetString(reader.GetOrdinal("Brands")),
            IngredientsText = reader.IsDBNull(reader.GetOrdinal("IngredientsText")) ? null : reader.GetString(reader.GetOrdinal("IngredientsText")),
            IngredientsTextEn = reader.IsDBNull(reader.GetOrdinal("IngredientsTextEn")) ? null : reader.GetString(reader.GetOrdinal("IngredientsTextEn")),
            Allergens = reader.IsDBNull(reader.GetOrdinal("Allergens")) ? null : reader.GetString(reader.GetOrdinal("Allergens")),
            AllergensHierarchy = reader.IsDBNull(reader.GetOrdinal("AllergensHierarchy")) ? null : reader.GetString(reader.GetOrdinal("AllergensHierarchy")),
            Categories = reader.IsDBNull(reader.GetOrdinal("Categories")) ? null : reader.GetString(reader.GetOrdinal("Categories")),
            CategoriesHierarchy = reader.IsDBNull(reader.GetOrdinal("CategoriesHierarchy")) ? null : reader.GetString(reader.GetOrdinal("CategoriesHierarchy")),
            NutritionData = reader.IsDBNull(reader.GetOrdinal("NutritionData")) ? null : reader.GetString(reader.GetOrdinal("NutritionData")),
            ImageUrl = reader.IsDBNull(reader.GetOrdinal("ImageUrl")) ? null : reader.GetString(reader.GetOrdinal("ImageUrl")),
            ImageSmallUrl = reader.IsDBNull(reader.GetOrdinal("ImageSmallUrl")) ? null : reader.GetString(reader.GetOrdinal("ImageSmallUrl")),
            Lang = reader.IsDBNull(reader.GetOrdinal("Lang")) ? null : reader.GetString(reader.GetOrdinal("Lang")),
            Countries = reader.IsDBNull(reader.GetOrdinal("Countries")) ? null : reader.GetString(reader.GetOrdinal("Countries")),
            NutriScore = reader.IsDBNull(reader.GetOrdinal("NutriScore")) ? null : reader.GetString(reader.GetOrdinal("NutriScore")),
            NovaGroup = reader.IsDBNull(reader.GetOrdinal("NovaGroup")) ? null : reader.GetInt32(reader.GetOrdinal("NovaGroup")),
            EcoScore = reader.IsDBNull(reader.GetOrdinal("EcoScore")) ? null : reader.GetString(reader.GetOrdinal("EcoScore")),
            RawJson = reader.IsDBNull(reader.GetOrdinal("RawJson")) ? null : reader.GetString(reader.GetOrdinal("RawJson")),
            ProcessingStatus = reader.GetString(reader.GetOrdinal("ProcessingStatus")),
            ProcessedAt = reader.IsDBNull(reader.GetOrdinal("ProcessedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("ProcessedAt")),
            ProcessingError = reader.IsDBNull(reader.GetOrdinal("ProcessingError")) ? null : reader.GetString(reader.GetOrdinal("ProcessingError")),
            ProcessingAttempts = reader.GetInt32(reader.GetOrdinal("ProcessingAttempts")),
            CreatedDate = reader.GetDateTime(reader.GetOrdinal("CreatedDate")),
            ModifiedDate = reader.IsDBNull(reader.GetOrdinal("ModifiedDate")) ? null : reader.GetDateTime(reader.GetOrdinal("ModifiedDate"))
        };
    }
}
