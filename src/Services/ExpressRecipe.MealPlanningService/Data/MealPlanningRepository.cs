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

    // ──────────────────────── Meal Plans ────────────────────────

    public async Task<Guid> CreateMealPlanAsync(Guid userId, DateTime startDate, DateTime endDate, string? name)
    {
        const string sql = @"
            INSERT INTO MealPlan (UserId, StartDate, EndDate, Name, IsActive, CreatedAt)
            OUTPUT INSERTED.Id
            VALUES (@UserId, @StartDate, @EndDate, @Name, 1, GETUTCDATE())";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@StartDate", startDate.Date);
        command.Parameters.AddWithValue("@EndDate", endDate.Date);
        command.Parameters.AddWithValue("@Name", (object?)name ?? DBNull.Value);

        var result = await command.ExecuteScalarAsync();
        return (Guid)result!;
    }

    public async Task<List<MealPlanDto>> GetUserMealPlansAsync(Guid userId, string? status = null)
    {
        var sql = @"
            SELECT mp.Id, mp.UserId, mp.StartDate, mp.EndDate, mp.Name, mp.IsActive, mp.CreatedAt,
                   (SELECT COUNT(*) FROM PlannedMeal WHERE MealPlanId = mp.Id AND IsDeleted = 0) AS TotalMeals,
                   (SELECT COUNT(*) FROM PlannedMeal WHERE MealPlanId = mp.Id AND IsCompleted = 1 AND IsDeleted = 0) AS CompletedMeals
            FROM MealPlan mp
            WHERE mp.UserId = @UserId AND mp.IsDeleted = 0";

        if (!string.IsNullOrEmpty(status))
        {
            if (string.Equals(status, "active", StringComparison.OrdinalIgnoreCase))
                sql += " AND mp.IsActive = 1";
            else if (string.Equals(status, "archived", StringComparison.OrdinalIgnoreCase))
                sql += " AND mp.IsActive = 0";
        }

        sql += " ORDER BY mp.StartDate DESC";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);

        var plans = new List<MealPlanDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var isActive = reader.GetBoolean(5);
            plans.Add(new MealPlanDto
            {
                Id = reader.GetGuid(0),
                UserId = reader.GetGuid(1),
                StartDate = reader.GetDateTime(2),
                EndDate = reader.GetDateTime(3),
                Name = reader.IsDBNull(4) ? null : reader.GetString(4),
                Status = isActive ? "Active" : "Archived",
                CreatedAt = reader.GetDateTime(6),
                TotalMeals = reader.GetInt32(7),
                CompletedMeals = reader.GetInt32(8)
            });
        }

        return plans;
    }

    public async Task<MealPlanDto?> GetMealPlanAsync(Guid planId, Guid userId)
    {
        const string sql = @"
            SELECT mp.Id, mp.UserId, mp.StartDate, mp.EndDate, mp.Name, mp.IsActive, mp.CreatedAt,
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
            var isActive = reader.GetBoolean(5);
            return new MealPlanDto
            {
                Id = reader.GetGuid(0),
                UserId = reader.GetGuid(1),
                StartDate = reader.GetDateTime(2),
                EndDate = reader.GetDateTime(3),
                Name = reader.IsDBNull(4) ? null : reader.GetString(4),
                Status = isActive ? "Active" : "Archived",
                CreatedAt = reader.GetDateTime(6),
                TotalMeals = reader.GetInt32(7),
                CompletedMeals = reader.GetInt32(8)
            };
        }

        return null;
    }

    public async Task UpdateMealPlanAsync(Guid planId, Guid userId, string name, DateTime startDate, DateTime endDate, string status)
    {
        var isActive = !string.Equals(status, "Archived", StringComparison.OrdinalIgnoreCase);
        const string sql = @"
            UPDATE MealPlan
            SET Name = @Name, StartDate = @StartDate, EndDate = @EndDate, IsActive = @IsActive, UpdatedAt = GETUTCDATE()
            WHERE Id = @PlanId AND UserId = @UserId AND IsDeleted = 0";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@PlanId", planId);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@Name", name);
        command.Parameters.AddWithValue("@StartDate", startDate.Date);
        command.Parameters.AddWithValue("@EndDate", endDate.Date);
        command.Parameters.AddWithValue("@IsActive", isActive);

        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteMealPlanAsync(Guid planId, Guid userId)
    {
        const string sql = "UPDATE MealPlan SET IsDeleted = 1, UpdatedAt = GETUTCDATE() WHERE Id = @PlanId AND UserId = @UserId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@PlanId", planId);
        command.Parameters.AddWithValue("@UserId", userId);

        await command.ExecuteNonQueryAsync();
    }

    public async Task SetMealPlanStatusAsync(Guid planId, Guid userId, string status)
    {
        var isActive = !string.Equals(status, "Archived", StringComparison.OrdinalIgnoreCase);
        const string sql = @"
            UPDATE MealPlan SET IsActive = @IsActive, UpdatedAt = GETUTCDATE()
            WHERE Id = @PlanId AND UserId = @UserId AND IsDeleted = 0";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@PlanId", planId);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@IsActive", isActive);

        await command.ExecuteNonQueryAsync();
    }

    // ──────────────────────── Planned Meals ────────────────────────

    public async Task<Guid> AddPlannedMealAsync(Guid mealPlanId, Guid userId, Guid? recipeId, DateTime plannedFor, string mealType, int servings, string? customMealName = null, string? notes = null)
    {
        const string sql = @"
            INSERT INTO PlannedMeal (MealPlanId, RecipeId, CustomMealName, PlannedDate, MealType, Servings, Notes)
            OUTPUT INSERTED.Id
            VALUES (@MealPlanId, @RecipeId, @CustomMealName, @PlannedDate, @MealType, @Servings, @Notes)";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@MealPlanId", mealPlanId);
        command.Parameters.AddWithValue("@RecipeId", (object?)recipeId ?? DBNull.Value);
        command.Parameters.AddWithValue("@CustomMealName", (object?)customMealName ?? DBNull.Value);
        command.Parameters.AddWithValue("@PlannedDate", plannedFor.Date);
        command.Parameters.AddWithValue("@MealType", mealType);
        command.Parameters.AddWithValue("@Servings", servings);
        command.Parameters.AddWithValue("@Notes", (object?)notes ?? DBNull.Value);

        var result = await command.ExecuteScalarAsync();
        return (Guid)result!;
    }

    public async Task<List<PlannedMealDto>> GetPlannedMealsAsync(Guid mealPlanId, DateTime? startDate, DateTime? endDate)
    {
        var sql = @"
            SELECT pm.Id, pm.MealPlanId, pm.RecipeId, pm.CustomMealName, pm.PlannedDate, pm.MealType, pm.Servings, pm.IsCompleted, pm.CompletedAt, pm.Notes
            FROM PlannedMeal pm
            WHERE pm.MealPlanId = @MealPlanId AND pm.IsDeleted = 0";

        if (startDate.HasValue) sql += " AND pm.PlannedDate >= @StartDate";
        if (endDate.HasValue) sql += " AND pm.PlannedDate <= @EndDate";
        sql += " ORDER BY pm.PlannedDate, pm.MealType";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@MealPlanId", mealPlanId);
        if (startDate.HasValue) command.Parameters.AddWithValue("@StartDate", startDate.Value.Date);
        if (endDate.HasValue) command.Parameters.AddWithValue("@EndDate", endDate.Value.Date);

        return await ReadPlannedMealsAsync(command);
    }

    public async Task<List<PlannedMealDto>> GetPlannedMealsByDateRangeAsync(Guid userId, DateTime startDate, DateTime endDate)
    {
        const string sql = @"
            SELECT pm.Id, pm.MealPlanId, pm.RecipeId, pm.CustomMealName, pm.PlannedDate, pm.MealType, pm.Servings, pm.IsCompleted, pm.CompletedAt, pm.Notes
            FROM PlannedMeal pm
            INNER JOIN MealPlan mp ON mp.Id = pm.MealPlanId
            WHERE mp.UserId = @UserId AND pm.IsDeleted = 0 AND mp.IsDeleted = 0
              AND pm.PlannedDate >= @StartDate AND pm.PlannedDate <= @EndDate
            ORDER BY pm.PlannedDate, pm.MealType";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@StartDate", startDate.Date);
        command.Parameters.AddWithValue("@EndDate", endDate.Date);

        return await ReadPlannedMealsAsync(command);
    }

    public async Task<PlannedMealDto?> GetPlannedMealAsync(Guid plannedMealId)
    {
        const string sql = @"
            SELECT pm.Id, pm.MealPlanId, pm.RecipeId, pm.CustomMealName, pm.PlannedDate, pm.MealType, pm.Servings, pm.IsCompleted, pm.CompletedAt, pm.Notes
            FROM PlannedMeal pm
            WHERE pm.Id = @Id AND pm.IsDeleted = 0";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", plannedMealId);

        var meals = await ReadPlannedMealsAsync(command);
        return meals.FirstOrDefault();
    }

    private static async Task<List<PlannedMealDto>> ReadPlannedMealsAsync(SqlCommand command)
    {
        var meals = new List<PlannedMealDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            meals.Add(new PlannedMealDto
            {
                Id = reader.GetGuid(0),
                MealPlanId = reader.GetGuid(1),
                RecipeId = reader.IsDBNull(2) ? null : reader.GetGuid(2),
                CustomMealName = reader.IsDBNull(3) ? null : reader.GetString(3),
                PlannedFor = reader.GetDateTime(4),
                MealType = reader.GetString(5),
                Servings = reader.GetInt32(6),
                IsCompleted = reader.GetBoolean(7),
                CompletedAt = reader.IsDBNull(8) ? null : reader.GetDateTime(8),
                Notes = reader.IsDBNull(9) ? null : reader.GetString(9)
            });
        }
        return meals;
    }

    public async Task UpdatePlannedMealAsync(Guid plannedMealId, DateTime plannedFor, string mealType, int servings, Guid? recipeId = null, string? customMealName = null, string? notes = null)
    {
        const string sql = @"
            UPDATE PlannedMeal
            SET PlannedDate = @PlannedDate, MealType = @MealType, Servings = @Servings,
                RecipeId = @RecipeId, CustomMealName = @CustomMealName, Notes = @Notes
            WHERE Id = @PlannedMealId AND IsDeleted = 0";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@PlannedMealId", plannedMealId);
        command.Parameters.AddWithValue("@PlannedDate", plannedFor.Date);
        command.Parameters.AddWithValue("@MealType", mealType);
        command.Parameters.AddWithValue("@Servings", servings);
        command.Parameters.AddWithValue("@RecipeId", (object?)recipeId ?? DBNull.Value);
        command.Parameters.AddWithValue("@CustomMealName", (object?)customMealName ?? DBNull.Value);
        command.Parameters.AddWithValue("@Notes", (object?)notes ?? DBNull.Value);

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

    public async Task MarkMealAsPreparedAsync(Guid plannedMealId, bool isPrepared)
    {
        const string sql = @"
            UPDATE PlannedMeal
            SET IsCompleted = @IsPrepared, CompletedAt = CASE WHEN @IsPrepared = 1 THEN GETUTCDATE() ELSE NULL END
            WHERE Id = @PlannedMealId AND IsDeleted = 0";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@PlannedMealId", plannedMealId);
        command.Parameters.AddWithValue("@IsPrepared", isPrepared);

        await command.ExecuteNonQueryAsync();
    }

    // ──────────────────────── Summary ────────────────────────

    public async Task<MealPlanSummaryData> GetMealPlanSummaryAsync(Guid userId)
    {
        var today = DateTime.UtcNow.Date;
        var weekStart = today.AddDays(-(int)today.DayOfWeek);
        var weekEnd = weekStart.AddDays(6);

        const string sql = @"
            SELECT
                (SELECT COUNT(*) FROM MealPlan WHERE UserId = @UserId AND IsActive = 1 AND IsDeleted = 0) AS TotalActivePlans,
                (SELECT COUNT(*) FROM PlannedMeal pm
                 INNER JOIN MealPlan mp ON mp.Id = pm.MealPlanId
                 WHERE mp.UserId = @UserId AND pm.IsDeleted = 0 AND mp.IsDeleted = 0 AND pm.PlannedDate >= @Today) AS TotalUpcomingMeals,
                (SELECT COUNT(*) FROM PlannedMeal pm
                 INNER JOIN MealPlan mp ON mp.Id = pm.MealPlanId
                 WHERE mp.UserId = @UserId AND pm.IsDeleted = 0 AND mp.IsDeleted = 0
                   AND pm.PlannedDate >= @WeekStart AND pm.PlannedDate <= @WeekEnd) AS MealsThisWeek,
                (SELECT COUNT(*) FROM PlannedMeal pm
                 INNER JOIN MealPlan mp ON mp.Id = pm.MealPlanId
                 WHERE mp.UserId = @UserId AND pm.IsDeleted = 0 AND mp.IsDeleted = 0 AND pm.IsCompleted = 1
                   AND pm.PlannedDate >= @WeekStart AND pm.PlannedDate <= @WeekEnd) AS PreparedThisWeek";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@Today", today);
        command.Parameters.AddWithValue("@WeekStart", weekStart);
        command.Parameters.AddWithValue("@WeekEnd", weekEnd);

        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new MealPlanSummaryData
            {
                TotalActivePlans = reader.GetInt32(0),
                TotalUpcomingMeals = reader.GetInt32(1),
                MealsThisWeek = reader.GetInt32(2),
                PreparedThisWeek = reader.GetInt32(3)
            };
        }

        return new MealPlanSummaryData();
    }

    // ──────────────────────── Nutritional Goals ────────────────────────

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
        command.Parameters.AddWithValue("@Unit", (object?)unit ?? DBNull.Value);
        command.Parameters.AddWithValue("@StartDate", (object?)startDate ?? DBNull.Value);
        command.Parameters.AddWithValue("@EndDate", (object?)endDate ?? DBNull.Value);

        var result = await command.ExecuteScalarAsync();
        return (Guid)result!;
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

    public Task<NutritionSummaryDto> GetNutritionSummaryAsync(Guid userId, DateTime date)
    {
        // Nutrition data comes from recipes/ingredients; return a placeholder pending recipe integration
        return Task.FromResult(new NutritionSummaryDto
        {
            Date = date,
            TotalCalories = 0,
            TotalProtein = 0,
            TotalCarbs = 0,
            TotalFat = 0,
            GoalProgress = new Dictionary<string, decimal>()
        });
    }

    // ──────────────────────── Plan Templates ────────────────────────

    public Task<Guid> SavePlanTemplateAsync(Guid userId, string name, string? description, List<TemplateMealDto> meals)
    {
        // Template storage via JSON is a future feature
        return Task.FromResult(Guid.NewGuid());
    }

    public Task<List<PlanTemplateDto>> GetUserTemplatesAsync(Guid userId)
    {
        return Task.FromResult(new List<PlanTemplateDto>());
    }

    public Task<Guid> ApplyTemplateAsync(Guid templateId, Guid userId, DateTime startDate)
    {
        // Template application is a future feature
        return Task.FromResult(Guid.NewGuid());
    }
}
