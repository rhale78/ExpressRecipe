using System.Data;
using Microsoft.Data.SqlClient;
using ExpressRecipe.Shared.DTOs.Product;
using Microsoft.Extensions.Logging;

namespace ExpressRecipe.ProductService.Data;

/// <summary>
/// Adapter that implements IAllergenRepository but uses HighSpeedDAL runtime components
/// (ProductDatabaseConnection) and ADO.NET for queries that rely on the vw_ProductIngredientFlat view
/// and ProductAllergenCache. Keeps the original IAllergenRepository surface for compatibility.
/// </summary>
public class AllergenRepositoryAdapter : IAllergenRepository
{
    private readonly ProductDatabaseConnection _dbConnection;
    private readonly ProductSearchAdapter _searchAdapter;
    private readonly ILogger<AllergenRepositoryAdapter> _logger;

    public AllergenRepositoryAdapter(ProductDatabaseConnection dbConnection, ProductSearchAdapter searchAdapter, ILogger<AllergenRepositoryAdapter> logger)
    {
        _dbConnection = dbConnection ?? throw new ArgumentNullException(nameof(dbConnection));
        _searchAdapter = searchAdapter ?? throw new ArgumentNullException(nameof(searchAdapter));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<List<ProductDto>> FindProductsWithoutAllergensAsync(List<string> allergens, int limit = 100)
    {
        if (allergens == null) throw new ArgumentNullException(nameof(allergens));
        if (!allergens.Any()) return new List<ProductDto>();

        var paramList = new List<SqlParameter>();
        var excludeConditions = new List<string>();

        for (int i = 0; i < allergens.Count; i++)
        {
            var a = allergens[i].ToLowerInvariant().Trim();
            var paramName = $"@Allergen{i}";
            excludeConditions.Add($"NOT EXISTS (SELECT 1 FROM vw_ProductIngredientFlat vif WHERE vif.ProductId = p.Id AND vif.IngredientName LIKE {paramName})");
            paramList.Add(new SqlParameter(paramName, System.Data.SqlDbType.NVarChar) { Value = $"%{a}%" });
        }

        var whereClause = string.Join(" AND ", excludeConditions);

        var sql = $@"
            SELECT DISTINCT TOP (@Limit)
                p.Id, p.Name, p.Brand, p.Barcode, p.BarcodeType,
                p.Description, p.Category, p.ImageUrl, p.ApprovalStatus, p.CreatedDate, p.SubmittedBy, p.ApprovedAt, p.ApprovedBy, p.RejectionReason
            FROM Product p
            WHERE p.IsDeleted = 0
                AND p.ApprovalStatus = 'Approved'
                AND {whereClause}
            ORDER BY p.Name";

        paramList.Add(new SqlParameter("@Limit", System.Data.SqlDbType.Int) { Value = limit });

        var results = new List<ProductDto>();
        await using var conn = new SqlConnection(_dbConnection.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddRange(paramList.ToArray());
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(MapReaderToProductDto(reader));
        }

        return results;
    }

    public async Task<bool> ProductContainsAllergensAsync(Guid productId, List<string> allergens)
    {
        if (allergens == null) throw new ArgumentNullException(nameof(allergens));
        if (!allergens.Any()) return false;

        var conditions = new List<string>();
        var parameters = new List<SqlParameter> { new SqlParameter("@ProductId", System.Data.SqlDbType.UniqueIdentifier) { Value = productId } };

        for (int i = 0; i < allergens.Count; i++)
        {
            var a = allergens[i].ToLowerInvariant().Trim();
            var paramName = $"@Allergen{i}";
            conditions.Add($"vif.IngredientName LIKE {paramName}");
            parameters.Add(new SqlParameter(paramName, System.Data.SqlDbType.NVarChar) { Value = $"%{a}%" });
        }

        var sql = $@"
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM vw_ProductIngredientFlat vif
                WHERE vif.ProductId = @ProductId
                    AND ({string.Join(" OR ", conditions)})
            ) THEN 1 ELSE 0 END";

        var contains = false;
        try
        {
            await using var conn = new SqlConnection(_dbConnection.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddRange(parameters.ToArray());
            var scalar = await cmd.ExecuteScalarAsync();
            contains = Convert.ToInt32(scalar) == 1;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking product allergens for product {ProductId}", productId);
            return false;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                foreach (var allergen in allergens)
                {
                    var nameLower = allergen.ToLowerInvariant().Trim();
                    var mergeSql = @"
                        MERGE ProductAllergenCache AS target
                        USING (SELECT @ProductId AS ProductId, @AllergenName AS AllergenName) AS source
                        ON (target.ProductId = source.ProductId AND target.AllergenName = source.AllergenName)
                        WHEN MATCHED THEN
                            UPDATE SET ContainsAllergen = @ContainsAllergen, LastChecked = GETUTCDATE()
                        WHEN NOT MATCHED THEN
                            INSERT (ProductId, AllergenName, ContainsAllergen, LastChecked)
                            VALUES (@ProductId, @AllergenName, @ContainsAllergen, GETUTCDATE());";

                    await using var conn = new SqlConnection(_dbConnection.ConnectionString);
                    await conn.OpenAsync();
                    await using var cmd = new SqlCommand(mergeSql, conn);
                    cmd.Parameters.AddWithValue("@ProductId", productId);
                    cmd.Parameters.AddWithValue("@AllergenName", nameLower);
                    cmd.Parameters.AddWithValue("@ContainsAllergen", contains);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Non-fatal: failed to update allergen cache for product {ProductId}", productId);
            }
        });

        return contains;
    }

    public async Task<List<string>> GetProductAllergensAsync(Guid productId)
    {
        const string sql = @"
            SELECT DISTINCT vif.IngredientName
            FROM vw_ProductIngredientFlat vif
            WHERE vif.ProductId = @ProductId
            ORDER BY vif.IngredientName";

        var ingredients = new List<string>();
        await using (var conn = new SqlConnection(_dbConnection.ConnectionString))
        {
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@ProductId", productId);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var ing = reader.IsDBNull(reader.GetOrdinal("IngredientName")) ? string.Empty : reader.GetString(reader.GetOrdinal("IngredientName"));
                if (!string.IsNullOrWhiteSpace(ing)) ingredients.Add(ing);
            }
        }

        var knownAllergens = new List<string>
        {
            "milk","dairy","whey","casein","lactose","butter","cream","cheese","yogurt",
            "egg","eggs","albumin","ovalbumin",
            "fish","salmon","tuna","cod","anchovy","tilapia","catfish","bass","trout",
            "shellfish","shrimp","crab","lobster","clam","oyster","mussel","scallop","crawfish",
            "almond","cashew","walnut","pecan","hazelnut","pistachio","macadamia","pine nut","brazil nut","chestnut",
            "peanut","peanuts","groundnut",
            "wheat","gluten","barley","rye","malt","spelt","kamut",
            "soy","soya","soybean","tofu","edamame","tempeh","miso",
            "sesame","tahini",
            "corn","sulfite","sulfites","mustard","celery","lupin","mollusc"
        };

        var detected = new List<string>();
        foreach (var ingredient in ingredients)
        {
            if (string.IsNullOrWhiteSpace(ingredient)) continue;
            var lower = ingredient.ToLowerInvariant();
            foreach (var allergen in knownAllergens)
            {
                if (lower.Contains(allergen))
                {
                    var display = char.ToUpperInvariant(ingredient[0]) + ingredient.Substring(1);
                    detected.Add(display);
                    break;
                }
            }
        }

        return detected.Distinct().OrderBy(x => x).ToList();
    }

    public Task<List<ProductDto>> FindProductsByDietaryRestrictionAsync(string restriction, int limit = 100)
    {
        if (string.IsNullOrWhiteSpace(restriction)) return Task.FromResult(new List<ProductDto>());

        var restrictionLower = restriction.ToLowerInvariant().Trim();
        var allergenRestrictions = new List<string>();

        switch (restrictionLower)
        {
            case "vegan":
                allergenRestrictions = new List<string>
                {
                    "milk","dairy","whey","casein","lactose","butter","cream","cheese",
                    "eggs","egg","albumin",
                    "fish","salmon","tuna","cod","anchovy",
                    "shellfish","shrimp","crab","lobster","clam","oyster",
                    "honey","gelatin","collagen"
                };
                break;
            case "vegetarian":
                allergenRestrictions = new List<string>
                {
                    "fish","salmon","tuna","cod","anchovy",
                    "shellfish","shrimp","crab","lobster","clam","oyster",
                    "meat","beef","pork","chicken","turkey","lamb",
                    "gelatin","collagen"
                };
                break;
            case "gluten-free":
            case "gluten free":
                allergenRestrictions = new List<string> { "gluten", "wheat", "barley", "rye", "malt", "spelt", "kamut" };
                break;
            case "dairy-free":
            case "dairy free":
            case "lactose-free":
            case "lactose free":
                allergenRestrictions = new List<string> { "milk", "dairy", "whey", "casein", "lactose", "butter", "cream", "cheese", "yogurt" };
                break;
            case "nut-free":
            case "nut free":
                allergenRestrictions = new List<string> { "peanut", "almond", "cashew", "walnut", "pecan", "hazelnut", "pistachio", "macadamia", "pine nut", "brazil nut" };
                break;
            case "soy-free":
            case "soy free":
                allergenRestrictions = new List<string> { "soy", "soya", "tofu", "edamame", "tempeh", "miso" };
                break;
            default:
                return Task.FromResult(new List<ProductDto>());
        }

        return FindProductsWithoutAllergensAsync(allergenRestrictions, limit);
    }

    private static ProductDto MapReaderToProductDto(IDataRecord reader)
    {
        return new ProductDto
        {
            Id = reader.GetGuid(reader.GetOrdinal("Id")),
            Name = reader.IsDBNull(reader.GetOrdinal("Name")) ? string.Empty : reader.GetString(reader.GetOrdinal("Name")),
            Brand = reader.IsDBNull(reader.GetOrdinal("Brand")) ? null : reader.GetString(reader.GetOrdinal("Brand")),
            Barcode = reader.IsDBNull(reader.GetOrdinal("Barcode")) ? null : reader.GetString(reader.GetOrdinal("Barcode")),
            BarcodeType = reader.IsDBNull(reader.GetOrdinal("BarcodeType")) ? null : reader.GetString(reader.GetOrdinal("BarcodeType")),
            Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
            Category = reader.IsDBNull(reader.GetOrdinal("Category")) ? null : reader.GetString(reader.GetOrdinal("Category")),
            ImageUrl = reader.IsDBNull(reader.GetOrdinal("ImageUrl")) ? null : reader.GetString(reader.GetOrdinal("ImageUrl")),
            ApprovalStatus = reader.IsDBNull(reader.GetOrdinal("ApprovalStatus")) ? "Pending" : reader.GetString(reader.GetOrdinal("ApprovalStatus")),
            ApprovedBy = reader.IsDBNull(reader.GetOrdinal("ApprovedBy")) ? null : reader.GetGuid(reader.GetOrdinal("ApprovedBy")),
            ApprovedAt = reader.IsDBNull(reader.GetOrdinal("ApprovedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("ApprovedAt")),
            RejectionReason = reader.IsDBNull(reader.GetOrdinal("RejectionReason")) ? null : reader.GetString(reader.GetOrdinal("RejectionReason")),
            SubmittedBy = reader.IsDBNull(reader.GetOrdinal("SubmittedBy")) ? null : reader.GetGuid(reader.GetOrdinal("SubmittedBy")),
            CreatedAt = reader.IsDBNull(reader.GetOrdinal("CreatedDate")) ? DateTime.UtcNow : reader.GetDateTime(reader.GetOrdinal("CreatedDate"))
        };
    }
}
