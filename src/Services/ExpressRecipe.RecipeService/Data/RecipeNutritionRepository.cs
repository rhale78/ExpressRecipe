using ExpressRecipe.Data.Common;
using Microsoft.Data.SqlClient;

namespace ExpressRecipe.RecipeService.Data;

public sealed record RecipeNutritionRow(
    Guid RecipeId,
    decimal BaseServings,
    decimal Calories,
    decimal Protein,
    decimal TotalCarbohydrates,
    decimal TotalFat,
    decimal DietaryFiber,
    decimal Sodium);

public interface IRecipeNutritionRepository
{
    Task<RecipeNutritionRow?> GetByRecipeIdAsync(Guid recipeId, CancellationToken ct);
}

public sealed class RecipeNutritionRepository : SqlHelper, IRecipeNutritionRepository
{
    public RecipeNutritionRepository(string connectionString) : base(connectionString) { }

    public async Task<RecipeNutritionRow?> GetByRecipeIdAsync(Guid recipeId, CancellationToken ct)
    {
        const string sql = @"
            SELECT rn.Calories, rn.Protein, rn.TotalCarbohydrates, rn.TotalFat,
                   rn.DietaryFiber, rn.Sodium, r.Servings AS BaseServings
            FROM RecipeNutrition rn
            JOIN Recipe r ON r.Id = rn.RecipeId
            WHERE rn.RecipeId = @RecipeId AND r.IsDeleted = 0";

        List<RecipeNutritionRow> rows = await ExecuteReaderAsync(
            sql,
            reader =>
            {
                decimal baseServings = (!reader.IsDBNull(6) && (int)reader[6] > 0) ? (decimal)(int)reader[6] : 1m;
                return new RecipeNutritionRow(
                    RecipeId: recipeId,
                    BaseServings: baseServings,
                    Calories: reader.IsDBNull(0) ? 0m : (decimal)reader[0],
                    Protein: reader.IsDBNull(1) ? 0m : (decimal)reader[1],
                    TotalCarbohydrates: reader.IsDBNull(2) ? 0m : (decimal)reader[2],
                    TotalFat: reader.IsDBNull(3) ? 0m : (decimal)reader[3],
                    DietaryFiber: reader.IsDBNull(4) ? 0m : (decimal)reader[4],
                    Sodium: reader.IsDBNull(5) ? 0m : (decimal)reader[5]);
            },
            ct,
            new SqlParameter("@RecipeId", recipeId));

        return rows.FirstOrDefault();
    }
}
