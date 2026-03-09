using Microsoft.Data.SqlClient;

namespace ExpressRecipe.MealPlanningService.Data;

public class MealPlanningRepository : IMealPlanningRepository
{
    private readonly string _connectionString;
    private readonly ILogger<MealPlanningRepository> _logger;

    public MealPlanningRepository(string connectionString, ILogger<MealPlanningRepository> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task<Guid> CreateMealPlanAsync(Guid userId, DateTime startDate, DateTime endDate, string? name)
    {
        const string sql = @"
            INSERT INTO MealPlan (UserId, StartDate, EndDate, Name, CreatedAt)
            OUTPUT INSERTED.Id
            VALUES (@UserId, @StartDate, @EndDate, @Name, GETUTCDATE())";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@StartDate", startDate);
        command.Parameters.AddWithValue("@EndDate", endDate);
        command.Parameters.AddWithValue("@Name", name ?? (object)DBNull.Value);

        return (Guid)await command.ExecuteScalarAsync()!;
    }

    public async Task<List<MealPlanDto>> GetUserMealPlansAsync(Guid userId)
    {
        const string sql = @"
            SELECT mp.Id, mp.UserId, mp.StartDate, mp.EndDate, mp.Name, mp.CreatedAt,
                   (SELECT COUNT(*) FROM PlannedMeal WHERE MealPlanId = mp.Id AND IsDeleted = 0) AS TotalMeals,
                   (SELECT COUNT(*) FROM PlannedMeal WHERE MealPlanId = mp.Id AND IsCompleted = 1 AND IsDeleted = 0) AS CompletedMeals
            FROM MealPlan mp
            WHERE mp.UserId = @UserId AND mp.IsDeleted = 0
            ORDER BY mp.StartDate DESC";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);

        var plans = new List<MealPlanDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            plans.Add(new MealPlanDto
            {
                Id = reader.GetGuid(0),
                UserId = reader.GetGuid(1),
                StartDate = reader.GetDateTime(2),
                EndDate = reader.GetDateTime(3),
                Name = reader.IsDBNull(4) ? null : reader.GetString(4),
                CreatedAt = reader.GetDateTime(5),
                TotalMeals = reader.GetInt32(6),
                CompletedMeals = reader.GetInt32(7)
            });
        }

        return plans;
    }

    public async Task<MealPlanDto?> GetMealPlanAsync(Guid planId, Guid userId)
    {
        const string sql = @"
            SELECT mp.Id, mp.UserId, mp.StartDate, mp.EndDate, mp.Name, mp.CreatedAt,
                   (SELECT COUNT(*) FROM PlannedMeal WHERE MealPlanId = mp.Id AND IsDeleted = 0) AS TotalMeals,
                   (SELECT COUNT(*) FROM PlannedMeal WHERE MealPlanId = mp.Id AND IsCompleted = 1 AND IsDeleted = 0) AS CompletedMeals
            FROM MealPlan mp
            WHERE mp.Id = @PlanId AND mp.UserId = @UserId AND mp.IsDeleted = 0";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@PlanId", planId);
        command.Parameters.AddWithValue("@UserId", userId);

        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new MealPlanDto
            {
                Id = reader.GetGuid(0),
                UserId = reader.GetGuid(1),
                StartDate = reader.GetDateTime(2),
                EndDate = reader.GetDateTime(3),
                Name = reader.IsDBNull(4) ? null : reader.GetString(4),
                CreatedAt = reader.GetDateTime(5),
                TotalMeals = reader.GetInt32(6),
                CompletedMeals = reader.GetInt32(7)
            };
        }

        return null;
    }

    public async Task DeleteMealPlanAsync(Guid planId, Guid userId)
    {
        const string sql = "UPDATE MealPlan SET IsDeleted = 1 WHERE Id = @PlanId AND UserId = @UserId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@PlanId", planId);
        command.Parameters.AddWithValue("@UserId", userId);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<Guid> AddPlannedMealAsync(Guid mealPlanId, Guid userId, Guid recipeId, DateTime plannedFor, string mealType, int servings)
    {
        const string sql = @"
            INSERT INTO PlannedMeal (MealPlanId, RecipeId, PlannedFor, MealType, Servings, CreatedAt)
            OUTPUT INSERTED.Id
            VALUES (@MealPlanId, @RecipeId, @PlannedFor, @MealType, @Servings, GETUTCDATE())";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@MealPlanId", mealPlanId);
        command.Parameters.AddWithValue("@RecipeId", recipeId);
        command.Parameters.AddWithValue("@PlannedFor", plannedFor);
        command.Parameters.AddWithValue("@MealType", mealType);
        command.Parameters.AddWithValue("@Servings", servings);

        return (Guid)await command.ExecuteScalarAsync()!;
    }

    public async Task<List<PlannedMealDto>> GetPlannedMealsAsync(Guid mealPlanId, DateTime? startDate, DateTime? endDate)
    {
        var sql = @"
            SELECT pm.Id, pm.MealPlanId, pm.RecipeId, '' AS RecipeName, pm.PlannedFor, pm.MealType, pm.Servings, pm.IsCompleted, pm.CompletedAt
            FROM PlannedMeal pm
            WHERE pm.MealPlanId = @MealPlanId AND pm.IsDeleted = 0";

        if (startDate.HasValue)
            sql += " AND pm.PlannedFor >= @StartDate";
        if (endDate.HasValue)
            sql += " AND pm.PlannedFor <= @EndDate";

        sql += " ORDER BY pm.PlannedFor, pm.MealType";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@MealPlanId", mealPlanId);
        if (startDate.HasValue)
            command.Parameters.AddWithValue("@StartDate", startDate.Value);
        if (endDate.HasValue)
            command.Parameters.AddWithValue("@EndDate", endDate.Value);

        var meals = new List<PlannedMealDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            meals.Add(new PlannedMealDto
            {
                Id = reader.GetGuid(0),
                MealPlanId = reader.GetGuid(1),
                RecipeId = reader.GetGuid(2),
                RecipeName = reader.GetString(3),
                PlannedFor = reader.GetDateTime(4),
                MealType = reader.GetString(5),
                Servings = reader.GetInt32(6),
                IsCompleted = reader.GetBoolean(7),
                CompletedAt = reader.IsDBNull(8) ? null : reader.GetDateTime(8)
            });
        }

        return meals;
    }

    public async Task UpdatePlannedMealAsync(Guid plannedMealId, DateTime plannedFor, string mealType, int servings)
    {
        const string sql = @"
            UPDATE PlannedMeal
            SET PlannedFor = @PlannedFor, MealType = @MealType, Servings = @Servings
            WHERE Id = @PlannedMealId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@PlannedMealId", plannedMealId);
        command.Parameters.AddWithValue("@PlannedFor", plannedFor);
        command.Parameters.AddWithValue("@MealType", mealType);
        command.Parameters.AddWithValue("@Servings", servings);

        await command.ExecuteNonQueryAsync();
    }

    public async Task RemovePlannedMealAsync(Guid plannedMealId)
    {
        const string sql = "UPDATE PlannedMeal SET IsDeleted = 1 WHERE Id = @PlannedMealId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@PlannedMealId", plannedMealId);

        await command.ExecuteNonQueryAsync();
    }

    public async Task MarkMealAsCompletedAsync(Guid plannedMealId)
    {
        const string sql = "UPDATE PlannedMeal SET IsCompleted = 1, CompletedAt = GETUTCDATE() WHERE Id = @PlannedMealId";

        await using SqlConnection connection = new(_connectionString);
        await connection.OpenAsync();

        await using SqlCommand command = new(sql, connection);
        command.Parameters.AddWithValue("@PlannedMealId", plannedMealId);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<PlannedMealDto?> GetPlannedMealAsync(Guid plannedMealId)
    {
        const string sql = @"
            SELECT pm.Id, pm.MealPlanId, pm.RecipeId, '' AS RecipeName, pm.PlannedFor, pm.MealType, pm.Servings, pm.IsCompleted, pm.CompletedAt
            FROM PlannedMeal pm
            WHERE pm.Id = @PlannedMealId AND pm.IsDeleted = 0";

        await using SqlConnection connection = new(_connectionString);
        await connection.OpenAsync();

        await using SqlCommand command = new(sql, connection);
        command.Parameters.AddWithValue("@PlannedMealId", plannedMealId);

        await using SqlDataReader reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new PlannedMealDto
            {
                Id = reader.GetGuid(0),
                MealPlanId = reader.GetGuid(1),
                RecipeId = reader.GetGuid(2),
                RecipeName = reader.GetString(3),
                PlannedFor = reader.GetDateTime(4),
                MealType = reader.GetString(5),
                Servings = reader.GetInt32(6),
                IsCompleted = reader.GetBoolean(7),
                CompletedAt = reader.IsDBNull(8) ? null : reader.GetDateTime(8)
            };
        }

        return null;
    }

    public async Task<Guid> SetNutritionalGoalAsync(Guid userId, string goalType, decimal targetValue, string? unit, DateTime? startDate, DateTime? endDate)
    {
        const string sql = @"
            INSERT INTO NutritionalGoal (UserId, GoalType, TargetValue, Unit, StartDate, EndDate, IsActive, CreatedAt)
            OUTPUT INSERTED.Id
            VALUES (@UserId, @GoalType, @TargetValue, @Unit, @StartDate, @EndDate, 1, GETUTCDATE())";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@GoalType", goalType);
        command.Parameters.AddWithValue("@TargetValue", targetValue);
        command.Parameters.AddWithValue("@Unit", unit ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@StartDate", startDate ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@EndDate", endDate ?? (object)DBNull.Value);

        return (Guid)await command.ExecuteScalarAsync()!;
    }

    public async Task<List<NutritionalGoalDto>> GetUserGoalsAsync(Guid userId)
    {
        const string sql = @"
            SELECT Id, UserId, GoalType, TargetValue, Unit, StartDate, EndDate, IsActive
            FROM NutritionalGoal
            WHERE UserId = @UserId
            ORDER BY CreatedAt DESC";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);

        var goals = new List<NutritionalGoalDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            goals.Add(new NutritionalGoalDto
            {
                Id = reader.GetGuid(0),
                UserId = reader.GetGuid(1),
                GoalType = reader.GetString(2),
                TargetValue = reader.GetDecimal(3),
                Unit = reader.IsDBNull(4) ? null : reader.GetString(4),
                StartDate = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                EndDate = reader.IsDBNull(6) ? null : reader.GetDateTime(6),
                IsActive = reader.GetBoolean(7)
            });
        }

        return goals;
    }

    public async Task<NutritionSummaryDto> GetNutritionSummaryAsync(Guid userId, DateTime date)
    {
        // Stub implementation - would calculate from completed meals
        return new NutritionSummaryDto
        {
            Date = date,
            TotalCalories = 0,
            TotalProtein = 0,
            TotalCarbs = 0,
            TotalFat = 0,
            GoalProgress = new()
        };
    }

    public async Task<Guid> SavePlanTemplateAsync(Guid userId, string name, string? description, List<TemplateMealDto> meals)
    {
        // Stub implementation
        return Guid.NewGuid();
    }

    public async Task<List<PlanTemplateDto>> GetUserTemplatesAsync(Guid userId)
    {
        // Stub implementation
        return new List<PlanTemplateDto>();
    }

    public async Task<Guid> ApplyTemplateAsync(Guid templateId, Guid userId, DateTime startDate)
    {
        // Stub implementation
        return Guid.NewGuid();
    }

    // ── Cooking History ───────────────────────────────────────────────────────

    public async Task<Guid> RecordCookingHistoryAsync(CookingHistoryRecord record, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO CookingHistory
                (UserId, HouseholdId, RecipeId, RecipeName, CookedAt, Servings, MealType, Source, PlannedMealId)
            OUTPUT INSERTED.Id
            VALUES
                (@UserId, @HouseholdId, @RecipeId, @RecipeName, @CookedAt, @Servings, @MealType, @Source, @PlannedMealId)";

        await using SqlConnection connection = new(_connectionString);
        await connection.OpenAsync(ct);

        await using SqlCommand command = new(sql, connection);
        command.Parameters.AddWithValue("@UserId", record.UserId);
        command.Parameters.AddWithValue("@HouseholdId", record.HouseholdId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@RecipeId", record.RecipeId);
        command.Parameters.AddWithValue("@RecipeName", record.RecipeName);
        command.Parameters.AddWithValue("@CookedAt", record.CookedAt);
        command.Parameters.AddWithValue("@Servings", record.Servings);
        command.Parameters.AddWithValue("@MealType", record.MealType);
        command.Parameters.AddWithValue("@Source", record.Source);
        command.Parameters.AddWithValue("@PlannedMealId", record.PlannedMealId ?? (object)DBNull.Value);

        return (Guid)(await command.ExecuteScalarAsync(ct))!;
    }

    public async Task UpdateCookingRatingAsync(Guid historyId, Guid userId, byte rating, bool? wouldCookAgain, string? notes, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE CookingHistory
            SET Rating = @Rating, WouldCookAgain = @WouldCookAgain, Notes = @Notes
            WHERE Id = @HistoryId AND UserId = @UserId";

        await using SqlConnection connection = new(_connectionString);
        await connection.OpenAsync(ct);

        await using SqlCommand command = new(sql, connection);
        command.Parameters.AddWithValue("@HistoryId", historyId);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@Rating", rating);
        command.Parameters.AddWithValue("@WouldCookAgain", wouldCookAgain ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Notes", notes ?? (object)DBNull.Value);

        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<CookingHistoryDto>> GetCookingHistoryAsync(Guid userId, int daysBack, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT Id, UserId, HouseholdId, RecipeId, RecipeName, CookedAt, Servings,
                   MealType, Rating, WouldCookAgain, Notes, Source, PlannedMealId, InventoryDeductionSent
            FROM CookingHistory
            WHERE UserId = @UserId
              AND CookedAt >= DATEADD(day, -@DaysBack, GETUTCDATE())
            ORDER BY CookedAt DESC";

        await using SqlConnection connection = new(_connectionString);
        await connection.OpenAsync(ct);

        await using SqlCommand command = new(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@DaysBack", daysBack);

        List<CookingHistoryDto> history = new();
        await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            history.Add(MapCookingHistoryDto(reader));
        }

        return history;
    }

    public async Task<List<CookingHistorySummaryDto>> GetMostCookedAsync(Guid userId, int limit, int daysBack, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT RecipeId, RecipeName, COUNT(*) AS CookCount,
                   AVG(CAST(Rating AS DECIMAL(5,2))) AS AverageRating,
                   MAX(CookedAt) AS LastCookedAt
            FROM CookingHistory
            WHERE UserId = @UserId
              AND CookedAt >= DATEADD(day, -@DaysBack, GETUTCDATE())
            GROUP BY RecipeId, RecipeName
            ORDER BY CookCount DESC
            OFFSET 0 ROWS FETCH NEXT @Limit ROWS ONLY";

        await using SqlConnection connection = new(_connectionString);
        await connection.OpenAsync(ct);

        await using SqlCommand command = new(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@DaysBack", daysBack);
        command.Parameters.AddWithValue("@Limit", limit);

        List<CookingHistorySummaryDto> summaries = new();
        await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            summaries.Add(new CookingHistorySummaryDto
            {
                RecipeId = reader.GetGuid(0),
                RecipeName = reader.GetString(1),
                CookCount = reader.GetInt32(2),
                AverageRating = reader.IsDBNull(3) ? null : reader.GetDecimal(3),
                LastCookedAt = reader.IsDBNull(4) ? null : reader.GetDateTime(4)
            });
        }

        return summaries;
    }

    public async Task<List<Guid>> GetRecentlyCookedRecipeIdsAsync(Guid userId, int daysBack, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT DISTINCT RecipeId
            FROM CookingHistory
            WHERE UserId = @UserId
              AND CookedAt >= DATEADD(day, -@DaysBack, GETUTCDATE())";

        await using SqlConnection connection = new(_connectionString);
        await connection.OpenAsync(ct);

        await using SqlCommand command = new(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@DaysBack", daysBack);

        List<Guid> ids = new();
        await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            ids.Add(reader.GetGuid(0));
        }

        return ids;
    }

    public async Task<int> GetCookCountForRecipeAsync(Guid userId, Guid recipeId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT COUNT(*) FROM CookingHistory
            WHERE UserId = @UserId AND RecipeId = @RecipeId";

        await using SqlConnection connection = new(_connectionString);
        await connection.OpenAsync(ct);

        await using SqlCommand command = new(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@RecipeId", recipeId);

        return (int)(await command.ExecuteScalarAsync(ct))!;
    }

    public async Task<List<CookingHistoryDto>> GetPendingInventoryDeductionsAsync(CancellationToken ct = default)
    {
        const string sql = @"
            SELECT Id, UserId, HouseholdId, RecipeId, RecipeName, CookedAt, Servings,
                   MealType, Rating, WouldCookAgain, Notes, Source, PlannedMealId, InventoryDeductionSent
            FROM CookingHistory
            WHERE InventoryDeductionSent = 0";

        await using SqlConnection connection = new(_connectionString);
        await connection.OpenAsync(ct);

        await using SqlCommand command = new(sql, connection);

        List<CookingHistoryDto> pending = new();
        await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            pending.Add(MapCookingHistoryDto(reader));
        }

        return pending;
    }

    public async Task MarkInventoryDeductionSentAsync(Guid historyId, CancellationToken ct = default)
    {
        const string sql = "UPDATE CookingHistory SET InventoryDeductionSent = 1 WHERE Id = @HistoryId";

        await using SqlConnection connection = new(_connectionString);
        await connection.OpenAsync(ct);

        await using SqlCommand command = new(sql, connection);
        command.Parameters.AddWithValue("@HistoryId", historyId);

        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<RatingPromptRow>> GetUnratedCookingHistoryAsync(CancellationToken ct = default)
    {
        const string sql = @"
            SELECT Id, UserId, RecipeName
            FROM CookingHistory
            WHERE Rating IS NULL
              AND CookedAt < DATEADD(minute, -30, GETUTCDATE())
              AND RatingPromptSent = 0";

        await using SqlConnection connection = new(_connectionString);
        await connection.OpenAsync(ct);

        await using SqlCommand command = new(sql, connection);

        List<RatingPromptRow> rows = new();
        await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new RatingPromptRow
            {
                Id = reader.GetGuid(0),
                UserId = reader.GetGuid(1),
                RecipeName = reader.GetString(2)
            });
        }

        return rows;
    }

    public async Task MarkRatingPromptSentAsync(Guid historyId, CancellationToken ct = default)
    {
        const string sql = "UPDATE CookingHistory SET RatingPromptSent = 1 WHERE Id = @HistoryId";

        await using SqlConnection connection = new(_connectionString);
        await connection.OpenAsync(ct);

        await using SqlCommand command = new(sql, connection);
        command.Parameters.AddWithValue("@HistoryId", historyId);
        await command.ExecuteNonQueryAsync(ct);
    }

    // ── Recipe Analytics ──────────────────────────────────────────────────────

    public async Task<Dictionary<Guid, decimal>> GetUserRecipeRatingsAsync(Guid userId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT RecipeId, AVG(CAST(Rating AS DECIMAL(5,2))) AS AvgRating
            FROM CookingHistory
            WHERE UserId = @UserId AND Rating IS NOT NULL
            GROUP BY RecipeId";

        await using SqlConnection connection = new(_connectionString);
        await connection.OpenAsync(ct);

        await using SqlCommand command = new(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);

        Dictionary<Guid, decimal> ratings = new();
        await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            ratings[reader.GetGuid(0)] = reader.GetDecimal(1);
        }

        return ratings;
    }

    public async Task<Dictionary<Guid, int>> GetUserRecipeCookCountsAsync(Guid userId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT RecipeId, COUNT(*) AS CookCount
            FROM CookingHistory
            WHERE UserId = @UserId
            GROUP BY RecipeId";

        await using SqlConnection connection = new(_connectionString);
        await connection.OpenAsync(ct);

        await using SqlCommand command = new(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);

        Dictionary<Guid, int> counts = new();
        await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            counts[reader.GetGuid(0)] = reader.GetInt32(1);
        }

        return counts;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static CookingHistoryDto MapCookingHistoryDto(SqlDataReader reader)
    {
        return new CookingHistoryDto
        {
            Id = reader.GetGuid(0),
            UserId = reader.GetGuid(1),
            HouseholdId = reader.IsDBNull(2) ? null : reader.GetGuid(2),
            RecipeId = reader.GetGuid(3),
            RecipeName = reader.GetString(4),
            CookedAt = reader.GetDateTime(5),
            Servings = reader.GetInt32(6),
            MealType = reader.GetString(7),
            Rating = reader.IsDBNull(8) ? null : reader.GetByte(8),
            WouldCookAgain = reader.IsDBNull(9) ? null : reader.GetBoolean(9),
            Notes = reader.IsDBNull(10) ? null : reader.GetString(10),
            Source = reader.GetString(11),
            PlannedMealId = reader.IsDBNull(12) ? null : reader.GetGuid(12),
            InventoryDeductionSent = reader.GetBoolean(13)
        };
    }
}
