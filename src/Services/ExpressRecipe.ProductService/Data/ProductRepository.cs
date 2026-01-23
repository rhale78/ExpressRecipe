using ExpressRecipe.Data.Common;
using ExpressRecipe.Shared.DTOs.Product;
using ExpressRecipe.Shared.Services;
using System.Security.Cryptography;
using System.Text;

namespace ExpressRecipe.ProductService.Data;

public class ProductRepository : SqlHelper, IProductRepository
{
    private readonly IProductImageRepository _productImageRepository;
    private readonly HybridCacheService? _cache;
    private readonly ILogger<ProductRepository>? _logger;

    // Constructor with cache and logger (recommended)
    public ProductRepository(string connectionString, IProductImageRepository productImageRepository, 
        HybridCacheService? cache = null, ILogger<ProductRepository>? logger = null) : base(connectionString)
    {
        _productImageRepository = productImageRepository;
        _cache = cache;
        _logger = logger;
    }

    public async Task<ProductDto?> GetByIdAsync(Guid id)
    {
        if (_cache != null)
        {
            var cacheKey = CacheKeys.FormatKey("product:id:{0}", id);
            return await _cache.GetOrSetAsync(
                cacheKey,
                async () => await GetByIdFromDbAsync(id),
                memoryExpiry: TimeSpan.FromMinutes(15),
                distributedExpiry: TimeSpan.FromHours(1));
        }

        return await GetByIdFromDbAsync(id);
    }

    private async Task<ProductDto?> GetByIdFromDbAsync(Guid id)
    {
        const string sql = @"
            SELECT Id, Name, Brand, Barcode, BarcodeType, Description, Category,
                   ServingSize, ServingUnit, ImageUrl, ApprovalStatus,
                   ApprovedBy, ApprovedAt, RejectionReason, SubmittedBy, CreatedDate
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
                CreatedAt = GetDateTime(reader, "CreatedDate")
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
        const string sql = @"
            SELECT Id, Name, Brand, Barcode, BarcodeType, Description, Category,
                   ServingSize, ServingUnit, ImageUrl, ApprovalStatus,
                   ApprovedBy, ApprovedAt, RejectionReason, SubmittedBy, CreatedDate
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
                CreatedAt = GetDateTime(reader, "CreatedDate")
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
                async () => await SearchFromDbAsync(request),
                memoryExpiry: TimeSpan.FromMinutes(5),
                distributedExpiry: TimeSpan.FromMinutes(15));
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
                   p.ApprovedBy, p.ApprovedAt, p.RejectionReason, p.SubmittedBy, p.CreatedDate,
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
                   p.ApprovedBy, p.ApprovedAt, p.RejectionReason, p.SubmittedBy, p.CreatedDate
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
            "created" => "p.CreatedDate DESC",
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
                CreatedAt = GetDateTime(reader, "CreatedDate")
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

    public Task<int> BulkCreateAsync(IEnumerable<CreateProductRequest> requests, Guid? createdBy = null)
    {
        throw new NotImplementedException("Use ProductRepositoryAdapter for bulk operations with HighSpeedDAL");
    }

    public async Task<Guid> CreateAsync(CreateProductRequest request, Guid? createdBy = null)
    {
        const string sql = @"
            INSERT INTO Product (
                Id, Name, Brand, Barcode, BarcodeType, Description, Category,
                ServingSize, ServingUnit, ImageUrl, ApprovalStatus,
                SubmittedBy, CreatedBy, CreatedDate
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
        const string sql = @"
            INSERT INTO ProductIngredient (Id, ProductId, IngredientId, OrderIndex, Quantity, Notes, IngredientListString, CreatedBy, CreatedDate)
            VALUES (@Id, @ProductId, NULL, @OrderIndex, NULL, NULL, @IngredientListString, NULL, GETUTCDATE())";

        await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", Guid.NewGuid()),
                    CreateParameter("@ProductId", productId),
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
            INSERT INTO ProductLabel (Id, ProductId, LabelName, CreatedDate)
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
            INSERT INTO ProductAllergen (Id, ProductId, AllergenName, CreatedDate)
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
            INSERT INTO ProductExternalLink (Id, ProductId, Source, ExternalId, CreatedDate)
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
            INSERT INTO ProductMetadata (Id, ProductId, MetaKey, MetaValue, CreatedDate)
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

    /// <summary>
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

    public Task<IEnumerable<string>> GetExistingBarcodesAsync(IEnumerable<string> barcodes)
    {
        throw new NotImplementedException("Use ProductRepositoryAdapter for bulk operations with HighSpeedDAL");
    }

    public Task<Dictionary<string, Guid>> GetProductIdsByBarcodesAsync(IEnumerable<string> barcodes)
    {
        throw new NotImplementedException("Use ProductRepositoryAdapter for bulk operations with HighSpeedDAL");
    }

    public Task BulkAddExternalLinksAsync(IEnumerable<(Guid ProductId, string Source, string ExternalId)> links)
    {
        throw new NotImplementedException("Use ProductRepositoryAdapter for bulk operations with HighSpeedDAL");
    }
    }
