using ExpressRecipe.Data.Common;
using Microsoft.Data.SqlClient;
using System.Data;

namespace ExpressRecipe.MealPlanningService.Data;

public sealed record CookingHistoryRow
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public Guid RecipeId { get; init; }
    public Guid? PlannedMealId { get; init; }
    public DateTime CookedAt { get; init; }
    public decimal ServingsCooked { get; init; } = 1m;
    public decimal? ServingsEaten { get; init; }
    public byte? UserRating { get; init; }
    public string? Notes { get; init; }
    public bool WasSubstituted { get; init; }
    public bool InventoryUpdated { get; init; }
    public bool NutritionLogged { get; init; }
}

public interface ICookingHistoryRepository
{
    Task<Guid> CreateAsync(CookingHistoryRow row, CancellationToken ct = default);
}

public sealed class CookingHistoryRepository : SqlHelper, ICookingHistoryRepository
{
    public CookingHistoryRepository(string connectionString) : base(connectionString) { }

    public async Task<Guid> CreateAsync(CookingHistoryRow row, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO CookingHistory
                (Id, UserId, RecipeId, PlannedMealId, CookedAt, ServingsCooked, ServingsEaten,
                 UserRating, Notes, WasSubstituted, InventoryUpdated, NutritionLogged, CreatedAt)
            VALUES
                (@Id, @UserId, @RecipeId, @PlannedMealId, @CookedAt, @ServingsCooked, @ServingsEaten,
                 @UserRating, @Notes, @WasSubstituted, @InventoryUpdated, @NutritionLogged, GETUTCDATE())";

        await ExecuteNonQueryAsync(sql, ct,
            new SqlParameter("@Id",               row.Id),
            new SqlParameter("@UserId",           row.UserId),
            new SqlParameter("@RecipeId",         row.RecipeId),
            new SqlParameter("@PlannedMealId",    (object?)row.PlannedMealId ?? DBNull.Value),
            new SqlParameter("@CookedAt",         row.CookedAt),
            new SqlParameter("@ServingsCooked",   row.ServingsCooked),
            new SqlParameter("@ServingsEaten",    (object?)row.ServingsEaten ?? DBNull.Value),
            new SqlParameter("@UserRating",       (object?)row.UserRating ?? DBNull.Value),
            new SqlParameter("@Notes",            (object?)row.Notes ?? DBNull.Value),
            new SqlParameter("@WasSubstituted",   row.WasSubstituted),
            new SqlParameter("@InventoryUpdated", row.InventoryUpdated),
            new SqlParameter("@NutritionLogged",  row.NutritionLogged));

        return row.Id;
    }
}
