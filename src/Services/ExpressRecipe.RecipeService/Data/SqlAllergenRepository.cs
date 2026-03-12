using ExpressRecipe.Data.Common;

namespace ExpressRecipe.RecipeService.Data;

/// <summary>
/// SQL Server implementation of IAllergenRepository.
/// Queries the Allergen and BaseIngredient tables in the recipe database.
/// </summary>
public class SqlAllergenRepository : SqlHelper, IAllergenRepository
{
    private readonly ILogger<SqlAllergenRepository> _logger;

    public SqlAllergenRepository(string connectionString, ILogger<SqlAllergenRepository> logger)
        : base(connectionString)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<(Guid AllergenId, string AllergenName)>> FindAllergensByIngredientNameAsync(string ingredientName)
    {
        try
        {
            const string sql = @"
                SELECT DISTINCT a.Id, a.Name
                FROM Allergen a
                WHERE a.IsDeleted = 0
                  AND (
                    LOWER(a.Name) IN (
                        SELECT value
                        FROM STRING_SPLIT(LOWER(@IngredientName), ' ')
                    )
                    OR LOWER(@IngredientName) LIKE '%' + LOWER(a.Name) + '%'
                  )";

            return await ExecuteReaderAsync(
                sql,
                reader => (reader.GetGuid(0), reader.GetString(1)),
                CreateParameter("@IngredientName", ingredientName));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to query allergen database for ingredient: {Ingredient}", ingredientName);
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<List<(Guid Id, string Name)>> GetAllKnownAllergensAsync()
    {
        try
        {
            const string sql = "SELECT Id, Name FROM Allergen WHERE IsDeleted = 0 ORDER BY Name";

            return await ExecuteReaderAsync(
                sql,
                reader => (reader.GetGuid(0), reader.GetString(1)));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve allergen list from database");
            return [];
        }
    }
}
