using ExpressRecipe.Data.Common;
using ExpressRecipe.Shared.DTOs.Product;
using Microsoft.Data.SqlClient;

namespace ExpressRecipe.ProductService.Data
{
    public interface IAllergenRepository
    {
        /// <summary>
        /// Find products that are safe for users with specific allergen restrictions
        /// </summary>
        Task<List<ProductDto>> FindProductsWithoutAllergensAsync(List<string> allergens, int limit = 100);

        /// <summary>
        /// Check if a specific product contains any of the given allergens
        /// </summary>
        Task<bool> ProductContainsAllergensAsync(Guid productId, List<string> allergens);

        /// <summary>
        /// Get all allergens for a specific product (from both ingredients and allergen table)
        /// </summary>
        Task<List<string>> GetProductAllergensAsync(Guid productId);

        /// <summary>
        /// Find products by dietary restriction (vegan, vegetarian, gluten-free, etc.)
        /// </summary>
        Task<List<ProductDto>> FindProductsByDietaryRestrictionAsync(string restriction, int limit = 100);
    }

    public class AllergenRepository : SqlHelper, IAllergenRepository
    {
        public AllergenRepository(string connectionString) : base(connectionString)
        {
        }

        /// <summary>
        /// Find products safe for specific allergen restrictions (FAST - uses indexed view with free-form text search)
        /// Supports ANY ingredient name: almonds, annatto, whey, shrimp, pickles, etc.
        /// </summary>
        public async Task<List<ProductDto>> FindProductsWithoutAllergensAsync(List<string> allergens, int limit = 100)
        {
            if (allergens.Count == 0)
            {
                return [];
            }

            // Build query to exclude products containing ANY of the specified allergens
            // Uses indexed vw_ProductIngredientFlat for fast case-insensitive partial matching
            List<SqlParameter> allergenParams = [];
            List<string> excludeConditions = [];

            for (int i = 0; i < allergens.Count; i++)
            {
                var allergenName = allergens[i].ToLower().Trim();
                var paramName = $"@Allergen{i}";

                // Exclude products that have this ingredient (case-insensitive partial match)
                excludeConditions.Add($@"
                NOT EXISTS (
                    SELECT 1 FROM vw_ProductIngredientFlat vif
                    WHERE vif.ProductId = p.Id
                        AND vif.IngredientName LIKE {paramName}
                )");

                allergenParams.Add(new SqlParameter(paramName, $"%{allergenName}%"));
            }

            var whereClause = string.Join(" AND ", excludeConditions);

            var sql = $@"
            SELECT DISTINCT TOP (@Limit)
                p.Id, p.Name, p.Brand, p.Barcode, p.BarcodeType,
                p.Description, p.Category, p.ImageUrl, p.ApprovalStatus
            FROM Product p
            WHERE p.IsDeleted = 0
                AND p.ApprovalStatus = 'Approved'
                AND {whereClause}
            ORDER BY p.Name";

            allergenParams.Add(new SqlParameter("@Limit", limit));

            return await ExecuteReaderAsync(sql, reader => new ProductDto
            {
                Id = GetGuid(reader, "Id"),
                Name = GetString(reader, "Name") ?? string.Empty,
                Brand = GetString(reader, "Brand"),
                Barcode = GetString(reader, "Barcode"),
                BarcodeType = GetString(reader, "BarcodeType"),
                Description = GetString(reader, "Description"),
                Category = GetString(reader, "Category"),
                ImageUrl = GetString(reader, "ImageUrl"),
                ApprovalStatus = GetString(reader, "ApprovalStatus") ?? string.Empty
            }, allergenParams.ToArray());
        }

        /// <summary>
        /// Check if a product contains specific allergens (FAST - supports free-form text search)
        /// Supports ANY ingredient name: almonds, annatto, whey, shrimp, pickles, etc.
        /// </summary>
        public async Task<bool> ProductContainsAllergensAsync(Guid productId, List<string> allergens)
        {
            if (allergens.Count == 0)
            {
                return false;
            }

            // Check cache first for frequently searched allergens
            var cachedResults = await CheckAllergenCacheAsync(productId, allergens);
            if (cachedResults != null)
            {
                return cachedResults.Value;
            }

            // Build query to check if product contains ANY of the allergens
            List<SqlParameter> allergenParams = [new SqlParameter("@ProductId", productId)];
            List<string> conditions = [];

            for (int i = 0; i < allergens.Count; i++)
            {
                var allergenName = allergens[i].ToLower().Trim();
                var paramName = $"@Allergen{i}";

                conditions.Add($"vif.IngredientName LIKE {paramName}");
                allergenParams.Add(new SqlParameter(paramName, $"%{allergenName}%"));
            }

            var sql = $@"
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM vw_ProductIngredientFlat vif
                WHERE vif.ProductId = @ProductId
                    AND ({string.Join(" OR ", conditions)})
            ) THEN 1 ELSE 0 END";

            var result = await ExecuteScalarAsync<int>(sql, allergenParams.ToArray()) == 1;

            // Cache the result for future queries
            await CacheAllergenCheckAsync(productId, allergens, result);

            return result;
        }

        /// <summary>
        /// Check allergen cache for previous results (performance optimization)
        /// </summary>
        private async Task<bool?> CheckAllergenCacheAsync(Guid productId, List<string> allergens)
        {
            try
            {
                // Check if ALL allergens have cached results (within last 7 days)
                List<SqlParameter> allergenParams = [new SqlParameter("@ProductId", productId)];
                List<string> conditions = [];

                for (int i = 0; i < allergens.Count; i++)
                {
                    var allergenName = allergens[i].ToLower().Trim();
                    var paramName = $"@Allergen{i}";
                    conditions.Add($"AllergenName = {paramName}");
                    allergenParams.Add(new SqlParameter(paramName, allergenName));
                }

                var sql = $@"
                SELECT ContainsAllergen
                FROM ProductAllergenCache
                WHERE ProductId = @ProductId
                    AND ({string.Join(" OR ", conditions)})
                    AND LastChecked > DATEADD(DAY, -7, GETUTCDATE())
                    AND ContainsAllergen = 1"; // Only use cache if allergen was found

                List<bool> cachedResults = await ExecuteReaderAsync(sql, reader => GetBoolean(reader, "ContainsAllergen"), allergenParams.ToArray());

                // If any allergen was cached as present, return true immediately
                if (cachedResults.Any(r => r))
                {
                    return true;
                }

                return null; // Cache miss or inconclusive
            }
            catch
            {
                return null; // On error, skip cache
            }
        }

        /// <summary>
        /// Cache allergen check results for future queries
        /// </summary>
        private async Task CacheAllergenCheckAsync(Guid productId, List<string> allergens, bool containsAllergen)
        {
            try
            {
                foreach (var allergen in allergens)
                {
                    var allergenName = allergen.ToLower().Trim();

                    var sql = @"
                    MERGE ProductAllergenCache AS target
                    USING (SELECT @ProductId AS ProductId, @AllergenName AS AllergenName) AS source
                    ON (target.ProductId = source.ProductId AND target.AllergenName = source.AllergenName)
                    WHEN MATCHED THEN
                        UPDATE SET ContainsAllergen = @ContainsAllergen, LastChecked = GETUTCDATE()
                    WHEN NOT MATCHED THEN
                        INSERT (ProductId, AllergenName, ContainsAllergen, LastChecked)
                        VALUES (@ProductId, @AllergenName, @ContainsAllergen, GETUTCDATE());";

                    await ExecuteNonQueryAsync(sql,
                        new SqlParameter("@ProductId", productId),
                        new SqlParameter("@AllergenName", allergenName),
                        new SqlParameter("@ContainsAllergen", containsAllergen));
                }
            }
            catch
            {
                // Cache write failure is not critical, just skip
            }
        }

        /// <summary>
        /// Get only actual allergens for a product (filters ingredients against known allergen list)
        /// Returns only ingredients that match FDA-required allergens and common allergens
        /// </summary>
        public async Task<List<string>> GetProductAllergensAsync(Guid productId)
        {
            // Define known allergens (FDA FALCPA + common additional allergens)
            List<string> knownAllergens =
            [
                // Milk/Dairy
                "milk", "dairy", "whey", "casein", "lactose", "butter", "cream", "cheese", "yogurt",
                // Eggs
                "egg", "eggs", "albumin", "ovalbumin",
                // Fish
                "fish", "salmon", "tuna", "cod", "anchovy", "tilapia", "catfish", "bass", "trout",
                // Shellfish
                "shellfish", "shrimp", "crab", "lobster", "clam", "oyster", "mussel", "scallop", "crawfish",
                // Tree nuts
                "almond", "cashew", "walnut", "pecan", "hazelnut", "pistachio", "macadamia", "pine nut", "brazil nut", "chestnut",
                // Peanuts
                "peanut", "peanuts", "groundnut",
                // Wheat/Gluten
                "wheat", "gluten", "barley", "rye", "malt", "spelt", "kamut",
                // Soy
                "soy", "soya", "soybean", "tofu", "edamame", "tempeh", "miso",
                // Sesame (FDA required as of 2023)
                "sesame", "tahini",
                // Common additional allergens
                "corn", "sulfite", "sulfites", "mustard", "celery", "lupin", "mollusc"
            ];

            const string sql = @"
            SELECT DISTINCT vif.IngredientName
            FROM vw_ProductIngredientFlat vif
            WHERE vif.ProductId = @ProductId
            ORDER BY vif.IngredientName";

            List<string> allIngredients = await ExecuteReaderAsync(sql, reader =>
            {
                var ingredientName = GetString(reader, "IngredientName");
                return ingredientName ?? string.Empty;
            }, new SqlParameter("@ProductId", productId));

            // Filter to only return ingredients that match known allergens
            List<string> detectedAllergens = [];

            foreach (var ingredient in allIngredients)
            {
                if (string.IsNullOrWhiteSpace(ingredient))
                {
                    continue;
                }

                var ingredientLower = ingredient.ToLowerInvariant();

                // Check if this ingredient contains any known allergen
                foreach (var allergen in knownAllergens)
                {
                    if (ingredientLower.Contains(allergen))
                    {
                        // Capitalize first letter for display
                        var displayName = ingredient.Length > 0
                            ? char.ToUpper(ingredient[0]) + ingredient.Substring(1)
                            : ingredient;

                        detectedAllergens.Add(displayName);
                        break; // Don't check other allergens for this ingredient
                    }
                }
            }

            return detectedAllergens.Distinct().OrderBy(a => a).ToList();
        }

        /// <summary>
        /// Find products by dietary restriction (maps common restrictions to ingredient searches)
        /// Supports vegan, vegetarian, gluten-free, dairy-free, nut-free, etc.
        /// </summary>
        public async Task<List<ProductDto>> FindProductsByDietaryRestrictionAsync(string restriction, int limit = 100)
        {
            var restrictionLower = restriction.ToLower().Trim();
            List<string> allergenRestrictions = [];

            switch (restrictionLower)
            {
                case "vegan":
                    // Exclude all animal products
                    allergenRestrictions =
                    [
                        "milk", "dairy", "whey", "casein", "lactose", "butter", "cream", "cheese",
                        "eggs", "egg", "albumin",
                        "fish", "salmon", "tuna", "cod", "anchovy",
                        "shellfish", "shrimp", "crab", "lobster", "clam", "oyster",
                        "honey",
                        "gelatin", "collagen"
                    ];
                    break;
                case "vegetarian":
                    // Exclude meat and seafood but allow dairy and eggs
                    allergenRestrictions =
                    [
                        "fish", "salmon", "tuna", "cod", "anchovy",
                        "shellfish", "shrimp", "crab", "lobster", "clam", "oyster",
                        "meat", "beef", "pork", "chicken", "turkey", "lamb",
                        "gelatin", "collagen"
                    ];
                    break;
                case "gluten-free":
                case "gluten free":
                    allergenRestrictions =
                    [
                        "gluten", "wheat", "barley", "rye", "malt", "spelt", "kamut"
                    ];
                    break;
                case "dairy-free":
                case "dairy free":
                case "lactose-free":
                case "lactose free":
                    allergenRestrictions =
                    [
                        "milk", "dairy", "whey", "casein", "lactose", "butter", "cream", "cheese", "yogurt"
                    ];
                    break;
                case "nut-free":
                case "nut free":
                    allergenRestrictions =
                    [
                        "peanut", "almond", "cashew", "walnut", "pecan", "hazelnut",
                        "pistachio", "macadamia", "pine nut", "brazil nut"
                    ];
                    break;
                case "soy-free":
                case "soy free":
                    allergenRestrictions =
                    [
                        "soy", "soya", "tofu", "edamame", "tempeh", "miso"
                    ];
                    break;
                default:
                    return [];
            }

            return await FindProductsWithoutAllergensAsync(allergenRestrictions, limit);
        }
    }
}
