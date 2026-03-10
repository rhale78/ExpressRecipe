using ExpressRecipe.Data.Common;
using ExpressRecipe.Shared.DTOs.Product;
using ExpressRecipe.Shared.Services;
using ExpressRecipe.Client.Shared.Services;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Security.Cryptography;
using System.Text;

namespace ExpressRecipe.ProductService.Data;

public interface IProductRepository
{
    Task<ProductDto?> GetByIdAsync(Guid id);
    Task<ProductDto?> GetByBarcodeAsync(string barcode);
    Task<Dictionary<string, ProductDto>> GetByBarcodesAsync(IEnumerable<string> barcodes);
    Task<ProductDto?> GetProductByBarcodeAsync(string barcode);
    Task<List<ProductDto>> SearchAsync(ProductSearchRequest request);
    Task<int> GetSearchCountAsync(ProductSearchRequest request);
    Task<Dictionary<string, int>> GetLetterCountsAsync(ProductSearchRequest request);
    Task<Guid> CreateAsync(CreateProductRequest request, Guid? createdBy = null);
    Task<Guid> CreateProductAsync(CreateProductRequest request);
    Task<bool> UpdateAsync(Guid id, UpdateProductRequest request, Guid? updatedBy = null);
    Task<bool> DeleteAsync(Guid id, Guid? deletedBy = null);
    Task<bool> ApproveAsync(Guid id, bool approve, Guid approvedBy, string? rejectionReason = null);
    Task<bool> ProductExistsAsync(Guid id);
    Task AddIngredientToProductAsync(Guid productId, string ingredient, int orderIndex = 0);
    Task AddLabelToProductAsync(Guid productId, string label);
    Task AddAllergenToProductAsync(Guid productId, string allergen);
    Task AddExternalLinkAsync(Guid productId, string source, string externalId);
    Task UpdateProductMetadataAsync(Guid productId, string key, string value);
    Task<ProductDto?> GetProductByExternalIdAsync(string source, string externalId);
    Task<int?> GetProductCountAsync();

    // High-speed bulk operations
    Task<int> BulkCreateFullProductsHighSpeedAsync(List<FullProductImportDto> products);
    Task<HashSet<string>> GetAllBarcodesAsync();

    // Admin: force-import a product, bypassing the approval queue
    Task<Guid> ForceImportAsync(ManualProductImportRequest request, Guid approvedBy);
}

public class ProductRepository : SqlHelper, IProductRepository
{
    private readonly IProductImageRepository _productImageRepository;
    private readonly IIngredientServiceClient? _ingredientClient;
    private readonly HybridCacheService? _cache;
    private readonly ILogger<ProductRepository>? _logger;

    // Constructor with cache and logger (recommended)
    public ProductRepository(
        string connectionString, 
        IProductImageRepository productImageRepository, 
        IIngredientServiceClient? ingredientClient = null,
        HybridCacheService? cache = null, 
        ILogger<ProductRepository>? logger = null) : base(connectionString)
    {
        _productImageRepository = productImageRepository;
        _ingredientClient = ingredientClient;
        _cache = cache;
        _logger = logger;
    }

    public async Task<ProductDto?> GetByIdAsync(Guid id)
    {
        if (_cache != null)
        {
            var cacheKey = CacheKeys.FormatKey("product:id:{0}", id);
            return await _cache.GetOrSetAsync<ProductDto?>(
                cacheKey,
                ct => new ValueTask<ProductDto?>(GetByIdFromDbAsync(id)),
                expiration: TimeSpan.FromHours(1));
        }

        return await GetByIdFromDbAsync(id);
    }

    private async Task<ProductDto?> GetByIdFromDbAsync(Guid id)
    {
        const string sql = @"
            SELECT Id, Name, Brand, Barcode, BarcodeType, Description, Category,
                   ServingSize, ServingUnit, ImageUrl, ApprovalStatus,
                   ApprovedBy, ApprovedAt, RejectionReason, SubmittedBy, CreatedAt
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
                SubmittedBy = GetGuidNullable(reader, "SubmittedBy"),
                CreatedAt = GetDateTime(reader, "CreatedAt")
            },
            CreateParameter("@Id", id));

        var product = results.FirstOrDefault();
        if (product != null)
        {
            await LoadImagesAsync(product);
        }
        return product;
    }

    public async Task<ProductDto?> GetProductByBarcodeAsync(string barcode)
    {
        return await GetByBarcodeAsync(barcode);
    }

    public async Task<ProductDto?> GetByBarcodeAsync(string barcode)
    {
        if (_cache != null)
        {
            var cacheKey = CacheKeys.FormatKey("product:barcode:{0}", barcode);
            return await _cache.GetOrSetAsync<ProductDto?>(
                cacheKey,
                ct => new ValueTask<ProductDto?>(GetByBarcodeFromDbAsync(barcode)),
                expiration: TimeSpan.FromMinutes(15));
        }

        return await GetByBarcodeFromDbAsync(barcode);
    }

    private async Task<ProductDto?> GetByBarcodeFromDbAsync(string barcode)
    {
        const string sql = @"
            SELECT Id, Name, Brand, Barcode, BarcodeType, Description, Category,
                   ServingSize, ServingUnit, ImageUrl, ApprovalStatus,
                   ApprovedBy, ApprovedAt, RejectionReason, SubmittedBy, CreatedAt
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
                SubmittedBy = GetGuidNullable(reader, "SubmittedBy"),
                CreatedAt = GetDateTime(reader, "CreatedAt")
            },
            CreateParameter("@Barcode", barcode));

        var product = results.FirstOrDefault();
        if (product != null)
        {
            await LoadImagesAsync(product);
        }
        return product;
    }

    public async Task<List<ProductDto>> SearchAsync(ProductSearchRequest request)
    {
        // Only cache simple queries (no dietary restrictions, no complex search terms)
        // Cache key based on main filters: category, brand, firstLetter, page
        var shouldCache = _cache != null && 
            (request.Restrictions == null || !request.Restrictions.Any()) &&
            string.IsNullOrWhiteSpace(request.SearchTerm);

        if (shouldCache)
        {
            var cacheKey = GenerateSearchCacheKey(request);
            return await _cache!.GetOrSetAsync(
                cacheKey,
                ct => new ValueTask<List<ProductDto>>(SearchFromDbAsync(request)),
                expiration: TimeSpan.FromMinutes(15));
        }

        return await SearchFromDbAsync(request);
    }

    private string GenerateSearchCacheKey(ProductSearchRequest request)
    {
        // Create deterministic cache key from request parameters
        var keyParts = new[]
        {
            "product:search",
            request.Category ?? "all",
            request.Brand ?? "all",
            request.FirstLetter ?? "all",
            request.OnlyApproved?.ToString() ?? "false",
            request.PageNumber.ToString(),
            request.PageSize.ToString(),
            request.SortBy ?? "name"
        };
        return string.Join(":", keyParts);
    }

    private async Task<List<ProductDto>> SearchFromDbAsync(ProductSearchRequest request)
    {
        var hasSearchTerm = !string.IsNullOrWhiteSpace(request.SearchTerm);

        // Build SELECT with relevance scoring when search term is provided
        var selectClause = hasSearchTerm ? @"
            SELECT p.Id, p.Name, p.Brand, p.Barcode, p.BarcodeType, p.Description, p.Category,
                   p.ServingSize, p.ServingUnit, p.ImageUrl, p.ApprovalStatus,
                   p.ApprovedBy, p.ApprovedAt, p.RejectionReason, p.SubmittedBy, p.CreatedAt,
                   -- Relevance scoring (higher = better match)
                   CASE
                       -- Exact match in Name (highest priority)
                       WHEN LOWER(p.Name) = @SearchTermLower THEN 100
                       -- Exact match in Brand
                       WHEN LOWER(p.Brand) = @SearchTermLower THEN 90
                       -- Starts with in Name
                       WHEN LOWER(p.Name) LIKE @SearchTermStart THEN 80
                       -- Starts with in Brand
                       WHEN LOWER(p.Brand) LIKE @SearchTermStart THEN 70
                       -- Contains in Name
                       WHEN LOWER(p.Name) LIKE @SearchTerm THEN 60
                       -- Contains in Brand
                       WHEN LOWER(p.Brand) LIKE @SearchTerm THEN 50
                       -- Contains in Description (lowest priority)
                       WHEN LOWER(p.Description) LIKE @SearchTerm THEN 20
                       -- No match (should not happen due to WHERE clause)
                       ELSE 0
                   END AS Relevance
            FROM Product p" : @"
            SELECT p.Id, p.Name, p.Brand, p.Barcode, p.BarcodeType, p.Description, p.Category,
                   p.ServingSize, p.ServingUnit, p.ImageUrl, p.ApprovalStatus,
                   p.ApprovedBy, p.ApprovedAt, p.RejectionReason, p.SubmittedBy, p.CreatedAt
            FROM Product p";

        var sql = selectClause + @"
            WHERE p.IsDeleted = 0";

        var parameters = new List<Microsoft.Data.SqlClient.SqlParameter>();

        if (request.OnlyApproved == true)
        {
            sql += " AND p.ApprovalStatus = 'Approved'";
        }

        if (!string.IsNullOrWhiteSpace(request.Barcode))
        {
            sql += " AND p.Barcode = @Barcode";
            parameters.Add((Microsoft.Data.SqlClient.SqlParameter)CreateParameter("@Barcode", request.Barcode));
        }

        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            sql += " AND p.Category = @Category";
            parameters.Add((Microsoft.Data.SqlClient.SqlParameter)CreateParameter("@Category", request.Category));
        }

        if (!string.IsNullOrWhiteSpace(request.Brand))
        {
            sql += " AND p.Brand = @Brand";
            parameters.Add((Microsoft.Data.SqlClient.SqlParameter)CreateParameter("@Brand", request.Brand));
        }

        if (hasSearchTerm)
        {
            // Use relevance-based search with case-insensitive matching
            sql += " AND (LOWER(p.Name) LIKE @SearchTerm OR LOWER(p.Brand) LIKE @SearchTerm OR LOWER(p.Description) LIKE @SearchTerm)";
            parameters.Add((Microsoft.Data.SqlClient.SqlParameter)CreateParameter("@SearchTerm", $"%{request.SearchTerm.ToLower()}%"));
            parameters.Add((Microsoft.Data.SqlClient.SqlParameter)CreateParameter("@SearchTermLower", request.SearchTerm.ToLower()));
            parameters.Add((Microsoft.Data.SqlClient.SqlParameter)CreateParameter("@SearchTermStart", $"{request.SearchTerm.ToLower()}%"));
        }

        // Filter by first letter (for alphabetical pagination)
        if (!string.IsNullOrWhiteSpace(request.FirstLetter))
        {
            if (char.IsDigit(request.FirstLetter[0]))
            {
                // Match any digit at start
                sql += " AND SUBSTRING(p.Name, 1, 1) BETWEEN '0' AND '9'";
            }
            else
            {
                // Match specific letter at start (case insensitive)
                sql += " AND UPPER(SUBSTRING(p.Name, 1, 1)) = @FirstLetter";
                parameters.Add((Microsoft.Data.SqlClient.SqlParameter)CreateParameter("@FirstLetter", request.FirstLetter.ToUpper()));
            }
        }

        // Filter by dietary restrictions (exclude products containing restricted ingredients)
        if (request.Restrictions != null && request.Restrictions.Any())
        {
            var restrictionConditions = new List<string>();

            for (int i = 0; i < request.Restrictions.Count; i++)
            {
                var restriction = request.Restrictions[i].ToLower().Trim();
                var paramName = $"@Restriction{i}";

                // Add condition to check ingredient name contains the restriction
                restrictionConditions.Add($"LOWER(vif.IngredientName) LIKE {paramName}");
                parameters.Add((Microsoft.Data.SqlClient.SqlParameter)CreateParameter(paramName, $"%{restriction}%"));
            }

            // Exclude products that have ANY of the restricted ingredients
            sql += $@" AND NOT EXISTS (
                SELECT 1 FROM vw_ProductIngredientFlat vif
                WHERE vif.ProductId = p.Id
                    AND ({string.Join(" OR ", restrictionConditions)})
            )";
        }

        // Dynamic sorting - prioritize relevance when searching, otherwise use requested sort
        var orderByClause = hasSearchTerm ? "Relevance DESC, p.Name" : (request.SortBy?.ToLower() switch
        {
            "brand" => "p.Brand, p.Name",
            "created" => "p.CreatedAt DESC",
            _ => "p.Name" // Default to name
        });

        sql += $" ORDER BY {orderByClause} OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";
        parameters.Add((Microsoft.Data.SqlClient.SqlParameter)CreateParameter("@Offset", (request.PageNumber - 1) * request.PageSize));
        parameters.Add((Microsoft.Data.SqlClient.SqlParameter)CreateParameter("@PageSize", request.PageSize));

        var products = await ExecuteReaderAsync(
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
                SubmittedBy = GetGuidNullable(reader, "SubmittedBy"),
                CreatedAt = GetDateTime(reader, "CreatedAt")
            },
            parameters.ToArray());

        // Load images for all products
        await LoadImagesAsync(products);

        return products;
    }

    public async Task<int> GetSearchCountAsync(ProductSearchRequest request)
    {
        var sql = @"
            SELECT COUNT(*)
            FROM Product p
            WHERE p.IsDeleted = 0";

        var parameters = new List<Microsoft.Data.SqlClient.SqlParameter>();

        if (request.OnlyApproved == true)
        {
            sql += " AND p.ApprovalStatus = 'Approved'";
        }

        if (!string.IsNullOrWhiteSpace(request.Barcode))
        {
            sql += " AND p.Barcode = @Barcode";
            parameters.Add((Microsoft.Data.SqlClient.SqlParameter)CreateParameter("@Barcode", request.Barcode));
        }

        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            sql += " AND p.Category = @Category";
            parameters.Add((Microsoft.Data.SqlClient.SqlParameter)CreateParameter("@Category", request.Category));
        }

        if (!string.IsNullOrWhiteSpace(request.Brand))
        {
            sql += " AND p.Brand = @Brand";
            parameters.Add((Microsoft.Data.SqlClient.SqlParameter)CreateParameter("@Brand", request.Brand));
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            sql += " AND (LOWER(p.Name) LIKE @SearchTerm OR LOWER(p.Brand) LIKE @SearchTerm OR LOWER(p.Description) LIKE @SearchTerm)";
            parameters.Add((Microsoft.Data.SqlClient.SqlParameter)CreateParameter("@SearchTerm", $"%{request.SearchTerm.ToLower()}%"));
        }

        // Filter by first letter (for alphabetical pagination)
        if (!string.IsNullOrWhiteSpace(request.FirstLetter))
        {
            if (char.IsDigit(request.FirstLetter[0]))
            {
                // Match any digit at start
                sql += " AND SUBSTRING(p.Name, 1, 1) BETWEEN '0' AND '9'";
            }
            else
            {
                // Match specific letter at start (case insensitive)
                sql += " AND UPPER(SUBSTRING(p.Name, 1, 1)) = @FirstLetter";
                parameters.Add((Microsoft.Data.SqlClient.SqlParameter)CreateParameter("@FirstLetter", request.FirstLetter.ToUpper()));
            }
        }

        // Filter by dietary restrictions (same logic as SearchAsync)
        if (request.Restrictions != null && request.Restrictions.Any())
        {
            var restrictionConditions = new List<string>();

            for (int i = 0; i < request.Restrictions.Count; i++)
            {
                var restriction = request.Restrictions[i].ToLower().Trim();
                var paramName = $"@Restriction{i}";

                restrictionConditions.Add($"LOWER(vif.IngredientName) LIKE {paramName}");
                parameters.Add((Microsoft.Data.SqlClient.SqlParameter)CreateParameter(paramName, $"%{restriction}%"));
            }

            sql += $@" AND NOT EXISTS (
                SELECT 1 FROM vw_ProductIngredientFlat vif
                WHERE vif.ProductId = p.Id
                    AND ({string.Join(" OR ", restrictionConditions)})
            )";
        }

        var count = await ExecuteScalarAsync<int>(sql, parameters.ToArray());
        return count;
    }

    public async Task<Dictionary<string, int>> GetLetterCountsAsync(ProductSearchRequest request)
    {
        // Build WHERE clause similar to search query but exclude FirstLetter filter
        var sql = @"
            SELECT
                UPPER(SUBSTRING(p.Name, 1, 1)) AS FirstLetter,
                COUNT(*) AS ProductCount
            FROM Product p
            WHERE p.IsDeleted = 0";

        var parameters = new List<Microsoft.Data.SqlClient.SqlParameter>();

        if (request.OnlyApproved == true)
        {
            sql += " AND p.ApprovalStatus = 'Approved'";
        }

        if (!string.IsNullOrWhiteSpace(request.Barcode))
        {
            sql += " AND p.Barcode = @Barcode";
            parameters.Add((Microsoft.Data.SqlClient.SqlParameter)CreateParameter("@Barcode", request.Barcode));
        }

        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            sql += " AND p.Category = @Category";
            parameters.Add((Microsoft.Data.SqlClient.SqlParameter)CreateParameter("@Category", request.Category));
        }

        if (!string.IsNullOrWhiteSpace(request.Brand))
        {
            sql += " AND p.Brand = @Brand";
            parameters.Add((Microsoft.Data.SqlClient.SqlParameter)CreateParameter("@Brand", request.Brand));
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            sql += " AND (p.Name LIKE @SearchTerm OR p.Description LIKE @SearchTerm)";
            parameters.Add((Microsoft.Data.SqlClient.SqlParameter)CreateParameter("@SearchTerm", $"%{request.SearchTerm}%"));
        }

        // Filter by dietary restrictions (same logic as SearchAsync)
        if (request.Restrictions != null && request.Restrictions.Any())
        {
            var restrictionConditions = new List<string>();

            for (int i = 0; i < request.Restrictions.Count; i++)
            {
                var restriction = request.Restrictions[i].ToLower().Trim();
                var paramName = $"@Restriction{i}";

                restrictionConditions.Add($"LOWER(vif.IngredientName) LIKE {paramName}");
                parameters.Add((Microsoft.Data.SqlClient.SqlParameter)CreateParameter(paramName, $"%{restriction}%"));
            }

            sql += $@" AND NOT EXISTS (
                SELECT 1 FROM vw_ProductIngredientFlat vif
                WHERE vif.ProductId = p.Id
                    AND ({string.Join(" OR ", restrictionConditions)})
            )";
        }

        sql += @"
            GROUP BY UPPER(SUBSTRING(p.Name, 1, 1))
            ORDER BY FirstLetter";

        var results = await ExecuteReaderAsync(
            sql,
            reader => new
            {
                Letter = GetString(reader, "FirstLetter") ?? string.Empty,
                Count = GetInt32(reader, "ProductCount")
            },
            parameters.ToArray());

        return results.ToDictionary(r => r.Letter, r => r.Count);
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

            // Invalidate search caches (new product may appear in search results)
            await InvalidateSearchCachesAsync();

            return productId;
    }

    public async Task<Guid> CreateProductAsync(CreateProductRequest request)
    {
        // Delegate to the existing CreateAsync implementation
        return await CreateAsync(request, null);
    }

    public async Task AddIngredientToProductAsync(Guid productId, string ingredient, int orderIndex = 0)
    {
        Guid? ingredientId = null;

        // Ensure ingredient is registered in the microservice
        if (_ingredientClient != null)
        {
            ingredientId = await _ingredientClient.GetIngredientIdByNameAsync(ingredient);
            if (ingredientId == null)
            {
                ingredientId = await _ingredientClient.CreateIngredientAsync(new CreateIngredientRequest { Name = ingredient });
            }
        }

        const string sql = @"
            INSERT INTO ProductIngredient (Id, ProductId, IngredientId, OrderIndex, Quantity, Notes, IngredientListString, CreatedBy, CreatedAt)
            VALUES (@Id, @ProductId, @IngredientId, @OrderIndex, NULL, NULL, @IngredientListString, NULL, GETUTCDATE())";

        await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", Guid.NewGuid()),
            CreateParameter("@ProductId", productId),
            CreateParameter("@IngredientId", (object?)ingredientId ?? DBNull.Value),
            CreateParameter("@OrderIndex", orderIndex),
            CreateParameter("@IngredientListString", ingredient));

        // Invalidate product ingredients cache
        if (_cache != null)
        {
            await _cache.RemoveAsync(CacheKeys.FormatKey("product:ingredients:{0}", productId));
        }
    }

    public async Task AddLabelToProductAsync(Guid productId, string label)
    {
        const string sql = @"
            INSERT INTO ProductLabel (Id, ProductId, LabelName, CreatedAt)
            VALUES (@Id, @ProductId, @LabelName, GETUTCDATE())";

        await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", Guid.NewGuid()),
            CreateParameter("@ProductId", productId),
            CreateParameter("@LabelName", label));
    }

    public async Task AddAllergenToProductAsync(Guid productId, string allergen)
    {
        const string sql = @"
            INSERT INTO ProductAllergen (Id, ProductId, AllergenName, CreatedAt)
            VALUES (@Id, @ProductId, @AllergenName, GETUTCDATE())";

        await ExecuteNonQueryAsync(
            sql,
                    CreateParameter("@Id", Guid.NewGuid()),
                    CreateParameter("@ProductId", productId),
                    CreateParameter("@AllergenName", allergen));

                // Invalidate product allergens cache
                if (_cache != null)
                {
                    await _cache.RemoveAsync(CacheKeys.FormatKey("product:allergens:{0}", productId));
                }
            }

    public async Task AddExternalLinkAsync(Guid productId, string source, string externalId)
    {
        const string sql = @"
            INSERT INTO ProductExternalLink (Id, ProductId, Source, ExternalId, CreatedAt)
            VALUES (@Id, @ProductId, @Source, @ExternalId, GETUTCDATE())";

        await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", Guid.NewGuid()),
            CreateParameter("@ProductId", productId),
            CreateParameter("@Source", source),
            CreateParameter("@ExternalId", externalId));
    }

    public async Task UpdateProductMetadataAsync(Guid productId, string key, string value)
    {
        const string sql = @"
            INSERT INTO ProductMetadata (Id, ProductId, MetaKey, MetaValue, CreatedAt)
            VALUES (@Id, @ProductId, @MetaKey, @MetaValue, GETUTCDATE())";

        await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", Guid.NewGuid()),
            CreateParameter("@ProductId", productId),
            CreateParameter("@MetaKey", key),
            CreateParameter("@MetaValue", value));
    }

    public async Task<ProductDto?> GetProductByExternalIdAsync(string source, string externalId)
    {
        // Try to locate product by external reference if table columns exist
        const string sql = @"
            SELECT Id, Name, Brand, Barcode, BarcodeType, Description, Category,
                   ServingSize, ServingUnit, ImageUrl, ApprovalStatus,
                   ApprovedBy, ApprovedAt, RejectionReason, SubmittedBy
            FROM Product
            WHERE ExternalSource = @Source AND ExternalId = @ExternalId AND IsDeleted = 0";

        try
        {
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
                CreateParameter("@Source", source),
                CreateParameter("@ExternalId", externalId));

            return results.FirstOrDefault();
        }
        catch
        {
            // If the schema doesn't include external fields, return null
            return null;
        }
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

            if (rowsAffected > 0 && _cache != null)
            {
                // Invalidate product cache
                await _cache.RemoveAsync(CacheKeys.FormatKey("product:id:{0}", id));
                await _cache.RemoveAsync(CacheKeys.FormatKey("product:ingredients:{0}", id));
                await _cache.RemoveAsync(CacheKeys.FormatKey("product:allergens:{0}", id));

                // Invalidate barcode cache if barcode changed
                if (!string.IsNullOrWhiteSpace(request.Barcode))
                {
                    await _cache.RemoveAsync(CacheKeys.FormatKey("product:barcode:{0}", request.Barcode));
                }

                // Invalidate search caches (product may appear in different result sets now)
                await InvalidateSearchCachesAsync();
            }

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

            if (rowsAffected > 0 && _cache != null)
            {
                // Invalidate all caches for this product
                await _cache.RemoveAsync(CacheKeys.FormatKey("product:id:{0}", id));
                await _cache.RemoveAsync(CacheKeys.FormatKey("product:ingredients:{0}", id));
                await _cache.RemoveAsync(CacheKeys.FormatKey("product:allergens:{0}", id));

                // Invalidate search caches (product no longer appears in results)
                await InvalidateSearchCachesAsync();
            }

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

            public async Task<int?> GetProductCountAsync()
            {
                const string sql = "SELECT COUNT(*) FROM Product WHERE IsDeleted = 0";
                return await ExecuteScalarAsync<int>(sql);
            }
        
            public async Task<HashSet<string>> GetAllBarcodesAsync()
            {
                const string sql = "SELECT Barcode FROM Product WITH (NOLOCK) WHERE Barcode IS NOT NULL AND IsDeleted = 0";
                var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                await ExecuteReaderAsync<bool>(sql, reader => 
                {
                    result.Add(reader.GetString(0));
                    return true;
                });
                return result;
            }
        
            public async Task<int> BulkCreateFullProductsHighSpeedAsync(List<FullProductImportDto> importBatch)
            {
                if (!importBatch.Any()) return 0;

                return await ExecuteWithDeadlockRetryAsync(async () =>
                {
                    using var connection = new SqlConnection(ConnectionString);
                    await connection.OpenAsync();

                    using var transaction = connection.BeginTransaction();
                    try
                    {
                        var timestamp = DateTime.UtcNow;
                        var productData = new List<object[]>();
                        var imageData = new List<object[]>();
                        var ingredientMappingData = new List<object[]>();
                        var allergenData = new List<object[]>();
                        var metadataData = new List<object[]>();
                        var externalLinkData = new List<object[]>();

                        foreach (var item in importBatch)
                        {
                            var productId = Guid.NewGuid();

                            // Product
                            productData.Add(new object[] {
                                productId, item.Product.Name, (object?)item.Product.Brand ?? DBNull.Value,
                                (object?)item.Product.Barcode ?? DBNull.Value, (object?)item.Product.BarcodeType ?? "Unknown",
                                (object?)item.Product.Description ?? DBNull.Value, (object?)item.Product.Category ?? "General",
                                (object?)item.Product.ServingSize ?? DBNull.Value, (object?)item.Product.ServingUnit ?? DBNull.Value,
                                (object?)item.Product.ImageUrl ?? DBNull.Value, "Approved", Guid.Empty, timestamp,
                                DBNull.Value, Guid.Empty, timestamp, DBNull.Value, timestamp, false, DBNull.Value
                            });

                            // Images
                            if (item.Images.Any())
                            {
                                foreach (var img in item.Images)
                                {
                                    imageData.Add(new object[] {
                                        Guid.NewGuid(), productId, img.ImageType ?? "Front", img.ImageUrl,
                                        (object?)img.LocalFilePath ?? DBNull.Value, img.IsPrimary, img.DisplayOrder,
                                        (object?)img.SourceSystem ?? "Import", timestamp
                                    });
                                }
                            }        

                            // Ingredients
                            int orderIdx = 0;
                            foreach (var ingId in item.IngredientIds)
                            {
                                ingredientMappingData.Add(new object[] { Guid.NewGuid(), productId, ingId, orderIdx++, timestamp });
                            }

                            // Allergens
                            foreach (var allergen in item.Allergens)
                            {
                                allergenData.Add(new object[] { Guid.NewGuid(), productId, allergen, timestamp });
                            }

                            // Metadata
                            foreach (var meta in item.Metadata)
                            {
                                metadataData.Add(new object[] { Guid.NewGuid(), productId, meta.Key, meta.Value, timestamp });
                            }

                            // External Links
                            if (!string.IsNullOrEmpty(item.ExternalId) && !string.IsNullOrEmpty(item.ExternalSource))
                            {
                                externalLinkData.Add(new object[] { Guid.NewGuid(), productId, item.ExternalSource, item.ExternalId, timestamp });
                            }
                        }

                        // Disable non-essential indexes for faster bulk insert
                        await DisableProductIndexesAsync(connection, transaction);

                        // Sequential bulk inserts (no lock contention, reliable)
                        await BulkInsertDataAsync(connection, transaction, "Product", productData, new[] {
                            "Id", "Name", "Brand", "Barcode", "BarcodeType", "Description", "Category", 
                            "ServingSize", "ServingUnit", "ImageUrl", "ApprovalStatus", "ApprovedBy", "ApprovedAt", 
                            "RejectionReason", "SubmittedBy", "CreatedAt", "UpdatedBy", "UpdatedAt", "IsDeleted", "DeletedAt"
                        });

                        await BulkInsertDataAsync(connection, transaction, "ProductImage", imageData, new[] {
                            "Id", "ProductId", "ImageType", "ImageUrl", "LocalFilePath", "IsPrimary", "DisplayOrder", "SourceSystem", "CreatedAt"
                        });

                        await BulkInsertDataAsync(connection, transaction, "ProductIngredient", ingredientMappingData, new[] {
                            "Id", "ProductId", "IngredientId", "OrderIndex", "CreatedAt"
                        });

                        await BulkInsertDataAsync(connection, transaction, "ProductAllergen", allergenData, new[] {
                            "Id", "ProductId", "AllergenName", "CreatedAt"
                        });

                        await BulkInsertDataAsync(connection, transaction, "ProductMetadata", metadataData, new[] {
                            "Id", "ProductId", "MetaKey", "MetaValue", "CreatedAt"
                        });

                        // External Links - handle duplicates one by one to avoid failing the whole batch
                        if (externalLinkData.Any())
                        {
                            foreach (var link in externalLinkData)
                            {
                                const string linkSql = @"
                                    IF NOT EXISTS (SELECT 1 FROM ProductExternalLink WHERE Source = @Source AND ExternalId = @ExtId)
                                    INSERT INTO ProductExternalLink (Id, ProductId, Source, ExternalId, CreatedAt)
                                    VALUES (@Id, @PId, @Source, @ExtId, @Date)";

                                using var cmd = new SqlCommand(linkSql, connection, transaction);
                                cmd.Parameters.AddWithValue("@Id", link[0]);
                                cmd.Parameters.AddWithValue("@PId", link[1]);
                                cmd.Parameters.AddWithValue("@Source", link[2]);
                                cmd.Parameters.AddWithValue("@ExtId", link[3]);
                                cmd.Parameters.AddWithValue("@Date", link[4]);

                                try { await cmd.ExecuteNonQueryAsync(); }
                                catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601) { /* Skip */ }
                            }
                        }

                        // Rebuild indexes after bulk insert
                        await RebuildProductIndexesAsync(connection, transaction);

                        await transaction.CommitAsync();
                        return importBatch.Count;
                    }
                    catch
                    {
                        if (transaction.Connection != null) await transaction.RollbackAsync();
                        throw;
                    }
                });
            }

            private async Task BulkInsertDataAsync(SqlConnection connection, SqlTransaction transaction, string tableName, List<object[]> data, string[] columns)
            {
                if (!data.Any()) return;

                var schema = GetTableSchema(tableName);
                var dt = new DataTable();

                foreach (var col in columns)
                {
                    if (schema.TryGetValue(col, out var type))
                    {
                        dt.Columns.Add(col, type);
                    }
                    else
                    {
                        dt.Columns.Add(col, typeof(string)); // Fallback
                    }
                }

                foreach (var row in data)
                {
                    var dtRow = dt.NewRow();
                    for (int i = 0; i < columns.Length; i++)
                    {
                        dtRow[i] = row[i] ?? DBNull.Value;
                    }
                    dt.Rows.Add(dtRow);
                }

                // Use SqlBulkCopy with optimal settings (no TableLock, EnableStreaming)
                using var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, transaction);
                bulkCopy.DestinationTableName = tableName;
                bulkCopy.BatchSize = 10000;
                bulkCopy.BulkCopyTimeout = 600;
                bulkCopy.EnableStreaming = true;

                foreach (var col in columns) bulkCopy.ColumnMappings.Add(col, col);

                await bulkCopy.WriteToServerAsync(dt);
                dt.Clear();
            }

            private async Task DisableProductIndexesAsync(SqlConnection connection, SqlTransaction transaction)
                            {
                                // Disable non-clustered indexes for faster bulk insert
                                const string disableSql = @"
                                    -- Product indexes
                                    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Product_Barcode' AND object_id = OBJECT_ID('Product'))
                                        ALTER INDEX IX_Product_Barcode ON Product DISABLE;
                                    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Product_Brand' AND object_id = OBJECT_ID('Product'))
                                        ALTER INDEX IX_Product_Brand ON Product DISABLE;
                                    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Product_Category' AND object_id = OBJECT_ID('Product'))
                                        ALTER INDEX IX_Product_Category ON Product DISABLE;

                                    -- ProductIngredient indexes
                                    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ProductIngredient_ProductId' AND object_id = OBJECT_ID('ProductIngredient'))
                                        ALTER INDEX IX_ProductIngredient_ProductId ON ProductIngredient DISABLE;
                                    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ProductIngredient_IngredientId' AND object_id = OBJECT_ID('ProductIngredient'))
                                        ALTER INDEX IX_ProductIngredient_IngredientId ON ProductIngredient DISABLE;

                                    -- ProductImage indexes
                                    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ProductImage_ProductId' AND object_id = OBJECT_ID('ProductImage'))
                                        ALTER INDEX IX_ProductImage_ProductId ON ProductImage DISABLE;";

                                using var cmd = new SqlCommand(disableSql, connection, transaction);
                                cmd.CommandTimeout = 60;
                                try
                                {
                                    await cmd.ExecuteNonQueryAsync();
                                }
                                catch (SqlException ex)
                                {
                                    // Log but don't fail - indexes may not exist yet
                                    _logger?.LogWarning("Failed to disable some indexes (may not exist): {Message}", ex.Message);
                                }
                            }

                            private async Task RebuildProductIndexesAsync(SqlConnection connection, SqlTransaction transaction)
                            {
                                // Rebuild indexes with optimized FILLFACTOR
                                const string rebuildSql = @"
                                    -- Product indexes
                                    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Product_Barcode' AND object_id = OBJECT_ID('Product'))
                                        ALTER INDEX IX_Product_Barcode ON Product REBUILD WITH (FILLFACTOR = 70);
                                    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Product_Brand' AND object_id = OBJECT_ID('Product'))
                                        ALTER INDEX IX_Product_Brand ON Product REBUILD WITH (FILLFACTOR = 70);
                                    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Product_Category' AND object_id = OBJECT_ID('Product'))
                                        ALTER INDEX IX_Product_Category ON Product REBUILD WITH (FILLFACTOR = 70);

                                    -- ProductIngredient indexes
                                    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ProductIngredient_ProductId' AND object_id = OBJECT_ID('ProductIngredient'))
                                        ALTER INDEX IX_ProductIngredient_ProductId ON ProductIngredient REBUILD WITH (FILLFACTOR = 70);
                                    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ProductIngredient_IngredientId' AND object_id = OBJECT_ID('ProductIngredient'))
                                        ALTER INDEX IX_ProductIngredient_IngredientId ON ProductIngredient REBUILD WITH (FILLFACTOR = 70);

                                    -- ProductImage indexes
                                    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ProductImage_ProductId' AND object_id = OBJECT_ID('ProductImage'))
                                        ALTER INDEX IX_ProductImage_ProductId ON ProductImage REBUILD WITH (FILLFACTOR = 70);";

                                using var cmd = new SqlCommand(rebuildSql, connection, transaction);
                                cmd.CommandTimeout = 300;
                                try
                                {
                                    await cmd.ExecuteNonQueryAsync();
                                }
                                catch (SqlException ex)
                                {
                                    // Log but don't fail - indexes may not exist yet
                                    _logger?.LogWarning("Failed to rebuild some indexes (may not exist): {Message}", ex.Message);
                                }
                            }

                            private Dictionary<string, Type> GetTableSchema(string tableName)
                            {
                                var schema = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
                                
                                switch (tableName)
                                {
                                    case "Product":
                                        schema["Id"] = typeof(Guid);
                                        schema["Name"] = typeof(string);
                                        schema["Brand"] = typeof(string);
                                        schema["Barcode"] = typeof(string);
                                        schema["BarcodeType"] = typeof(string);
                                        schema["Description"] = typeof(string);
                                        schema["Category"] = typeof(string);
                                        schema["ServingSize"] = typeof(string);
                                        schema["ServingUnit"] = typeof(string);
                                        schema["ImageUrl"] = typeof(string);
                                        schema["ApprovalStatus"] = typeof(string);
                                        schema["ApprovedBy"] = typeof(Guid);
                                        schema["ApprovedAt"] = typeof(DateTime);
                                        schema["RejectionReason"] = typeof(string);
                                        schema["SubmittedBy"] = typeof(Guid);
                                        schema["CreatedAt"] = typeof(DateTime);
                                        schema["UpdatedBy"] = typeof(Guid);
                                        schema["UpdatedAt"] = typeof(DateTime);
                                        schema["IsDeleted"] = typeof(bool);
                                        schema["DeletedAt"] = typeof(DateTime);
                                        break;
                                    case "ProductImage":
                                        schema["Id"] = typeof(Guid);
                                        schema["ProductId"] = typeof(Guid);
                                        schema["ImageType"] = typeof(string);
                                        schema["ImageUrl"] = typeof(string);
                                        schema["LocalFilePath"] = typeof(string);
                                        schema["IsPrimary"] = typeof(bool);
                                        schema["DisplayOrder"] = typeof(int);
                                        schema["SourceSystem"] = typeof(string);
                                        schema["CreatedAt"] = typeof(DateTime);
                                        break;
                                    case "ProductIngredient":
                                        schema["Id"] = typeof(Guid);
                                        schema["ProductId"] = typeof(Guid);
                                        schema["IngredientId"] = typeof(Guid);
                                        schema["OrderIndex"] = typeof(int);
                                        schema["CreatedAt"] = typeof(DateTime);
                                        break;
                                    case "ProductAllergen":
                                        schema["Id"] = typeof(Guid);
                                        schema["ProductId"] = typeof(Guid);
                                        schema["AllergenName"] = typeof(string);
                                        schema["CreatedAt"] = typeof(DateTime);
                                        break;
                                    case "ProductMetadata":
                                        schema["Id"] = typeof(Guid);
                                        schema["ProductId"] = typeof(Guid);
                                        schema["MetaKey"] = typeof(string);
                                        schema["MetaValue"] = typeof(string);
                                        schema["CreatedAt"] = typeof(DateTime);
                                        break;
                                    case "ProductExternalLink":
                                        schema["Id"] = typeof(Guid);
                                        schema["ProductId"] = typeof(Guid);
                                        schema["Source"] = typeof(string);
                                        schema["ExternalId"] = typeof(string);
                                        schema["CreatedAt"] = typeof(DateTime);
                                        break;
                                }
                                
                                return schema;
                            }    /// <summary>
    /// Helper method to load product images and populate the Images collection
    /// </summary>
    private async Task LoadImagesAsync(ProductDto product)
    {
        var images = await _productImageRepository.GetImagesByProductIdAsync(product.Id);
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

        // Update legacy ImageUrl property for backward compatibility
        var primaryImage = product.Images.FirstOrDefault(i => i.IsPrimary);
        if (primaryImage != null && string.IsNullOrEmpty(product.ImageUrl))
        {
                    product.ImageUrl = primaryImage.ImageUrl ?? primaryImage.LocalFilePath;
                }
            }

            /// <summary>
            /// Invalidate all search-related caches
            /// </summary>
            private async Task InvalidateSearchCachesAsync()
            {
                if (_cache == null) return;

                // Note: We can't easily invalidate specific search cache keys since a product
                // may appear in many different search result sets. In production, consider:
                // 1. Using cache tags/dependencies (if supported by your cache provider)
                // 2. Setting shorter TTL for search caches (already using 5min/15min)
                // 3. Accepting eventual consistency (caches expire naturally)
                // For now, we rely on short TTL for search caches
                _logger?.LogDebug("Product modified - search caches will expire naturally (5-15min TTL)");
            }

    /// <summary>
    /// Helper method to load images for multiple products
    /// </summary>
    private async Task LoadImagesAsync(List<ProductDto> products)
    {
        foreach (var product in products)
        {
            await LoadImagesAsync(product);
        }
    }

    /// <summary>
    /// Bulk lookup products by barcodes (optimized for batch operations)
    /// </summary>
    public async Task<Dictionary<string, ProductDto>> GetByBarcodesAsync(IEnumerable<string> barcodes)
    {
        var barcodeList = barcodes.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        if (!barcodeList.Any())
        {
            return new Dictionary<string, ProductDto>(StringComparer.OrdinalIgnoreCase);
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Create table-valued parameter for bulk lookup
        var dt = new DataTable();
        dt.Columns.Add("Barcode", typeof(string));

        foreach (var barcode in barcodeList)
        {
            dt.Rows.Add(barcode);
        }

        const string sql = @"
            SELECT p.Id, p.Name, p.Brand, p.Barcode, p.BarcodeType, p.Description, p.Category,
                   p.ServingSize, p.ServingUnit, p.ImageUrl, p.ApprovalStatus,
                   p.ApprovedBy, p.ApprovedAt, p.RejectionReason, p.SubmittedBy, p.CreatedAt
            FROM Product p
            INNER JOIN @Barcodes b ON p.Barcode = b.Barcode COLLATE SQL_Latin1_General_CP1_CI_AS
            WHERE p.IsDeleted = 0 AND p.ApprovalStatus = 'Approved'";

        var param = new SqlParameter("@Barcodes", SqlDbType.Structured)
        {
            TypeName = "dbo.BarcodeListType",
            Value = dt
        };

        List<ProductDto> products;

        try
        {
            products = await ExecuteReaderAsync(
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
                    SubmittedBy = GetGuidNullable(reader, "SubmittedBy"),
                    CreatedAt = GetDateTime(reader, "CreatedAt")
                },
                param);
        }
        catch (SqlException ex) when (ex.Message.Contains("BarcodeListType"))
        {
            // Table type doesn't exist yet, fall back to individual queries
            _logger?.LogWarning("BarcodeListType not found, falling back to individual queries. Run DatabaseMigrator to create the type.");
            return await GetByBarcodesAsyncFallback(barcodeList);
        }

        // Load images for all products in parallel
        await Task.WhenAll(products.Select(p => LoadImagesAsync(p)));

        sw.Stop();

        // Convert to dictionary keyed by barcode
        var result = products
            .Where(p => !string.IsNullOrEmpty(p.Barcode))
            .ToDictionary(p => p.Barcode!, p => p, StringComparer.OrdinalIgnoreCase);

        _logger?.LogDebug("[Products] DB bulk lookup: {Requested} barcodes -> {Found} products in {Ms}ms",
            barcodeList.Count, result.Count, sw.ElapsedMilliseconds);

        return result;
    }

    private async Task<Dictionary<string, ProductDto>> GetByBarcodesAsyncFallback(List<string> barcodes)
    {
        // Fallback: individual queries if table type doesn't exist
        var tasks = barcodes.Select(async barcode =>
        {
            var product = await GetByBarcodeAsync(barcode);
            return (Barcode: barcode, Product: product);
        });

        var results = await Task.WhenAll(tasks);

        return results
            .Where(r => r.Product != null)
            .ToDictionary(r => r.Barcode, r => r.Product!, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Force-import a product with ApprovalStatus='Approved', bypassing the moderation queue.
    /// Sets ApprovedBy and ApprovedAt immediately.
    /// </summary>
    public async Task<Guid> ForceImportAsync(ManualProductImportRequest request, Guid approvedBy)
    {
        const string sql = @"
            INSERT INTO Product
                (Id, Name, Brand, Barcode, Category, Description,
                 ApprovalStatus, ApprovedBy, ApprovedAt, SubmittedBy, CreatedAt, IsDeleted)
            VALUES
                (@Id, @Name, @Brand, @Barcode, @Category, @Description,
                 'Approved', @ApprovedBy, GETUTCDATE(), @ApprovedBy, GETUTCDATE(), 0)";

        var productId = Guid.NewGuid();
        await ExecuteNonQueryAsync(sql,
            CreateParameter("@Id", productId),
            CreateParameter("@Name", request.ProductName),
            CreateParameter("@Brand", (object?)request.Brand ?? DBNull.Value),
            CreateParameter("@Barcode", (object?)request.Barcode ?? DBNull.Value),
            CreateParameter("@Category", (object?)request.Category ?? DBNull.Value),
            CreateParameter("@Description", (object?)request.Description ?? DBNull.Value),
            CreateParameter("@ApprovedBy", approvedBy));

        return productId;
    }
    }
