using ExpressRecipe.Data.Common;
using Microsoft.Data.SqlClient;
using System.Data;

namespace ExpressRecipe.MealPlanningService.Data;

public sealed record DailyNutritionLogRow
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public DateOnly LogDate { get; init; }
    public string? MealType { get; init; }
    public Guid? CookingHistoryId { get; init; }
    public Guid? RecipeId { get; init; }
    public string? RecipeName { get; init; }
    public decimal ServingsEaten { get; init; }
    public decimal? Calories { get; init; }
    public decimal? Protein { get; init; }
    public decimal? Carbohydrates { get; init; }
    public decimal? TotalFat { get; init; }
    public decimal? DietaryFiber { get; init; }
    public decimal? Sodium { get; init; }
    public bool IsManualEntry { get; init; }
}

public sealed record DailySummaryRow
{
    public DateOnly? LogDate { get; init; }
    public decimal TotalCalories { get; init; }
    public decimal TotalProtein { get; init; }
    public decimal TotalCarbs { get; init; }
    public decimal TotalFat { get; init; }
    public decimal TotalFiber { get; init; }
    public decimal TotalSodium { get; init; }
    public int MealCount { get; init; }
    public int MealsWithoutNutrition { get; init; }
}

public sealed record NutritionalGoalRow
{
    public Guid Id { get; init; }
    public string GoalType { get; init; } = "Daily";
    public DateOnly StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public decimal? TargetCalories { get; init; }
    public decimal? TargetProtein { get; init; }
    public decimal? TargetCarbs { get; init; }
    public decimal? TargetFat { get; init; }
    public decimal? TargetFiber { get; init; }
    public decimal? TargetSodium { get; init; }
    public string? Notes { get; init; }
}

public interface INutritionLogRepository
{
    Task InsertLogAsync(DailyNutritionLogRow row, CancellationToken ct);
    Task MarkNutritionLoggedAsync(Guid cookingHistoryId, CancellationToken ct);
    Task<DailySummaryRow> GetDailySummaryAsync(Guid userId, DateOnly date, CancellationToken ct);
    Task<List<DailySummaryRow>> GetTrendAsync(Guid userId, int days, CancellationToken ct);
    Task<List<DailyNutritionLogRow>> GetDayDetailAsync(Guid userId, DateOnly date, CancellationToken ct);
    Task<NutritionalGoalRow?> GetActiveGoalAsync(Guid userId, CancellationToken ct);
    Task<Guid> UpsertGoalAsync(Guid userId, NutritionalGoalRow goal, CancellationToken ct);
    Task EndGoalAsync(Guid goalId, Guid userId, CancellationToken ct);
    Task<List<NutritionalGoalRow>> GetGoalHistoryAsync(Guid userId, CancellationToken ct);
}

public sealed class NutritionLogRepository : SqlHelper, INutritionLogRepository
{
    public NutritionLogRepository(string connectionString) : base(connectionString) { }

    public async Task InsertLogAsync(DailyNutritionLogRow row, CancellationToken ct)
    {
        const string sql = @"
            INSERT INTO DailyNutritionLog
                (Id, UserId, LogDate, MealType, CookingHistoryId, RecipeId, RecipeName,
                 ServingsEaten, Calories, Protein, Carbohydrates, TotalFat, DietaryFiber, Sodium, IsManualEntry, CreatedAt)
            VALUES
                (@Id, @UserId, @LogDate, @MealType, @CookingHistoryId, @RecipeId, @RecipeName,
                 @ServingsEaten, @Calories, @Protein, @Carbohydrates, @TotalFat, @DietaryFiber, @Sodium, @IsManualEntry, GETUTCDATE())";

        await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id",               row.Id),
            new SqlParameter("@UserId",           row.UserId),
            new SqlParameter("@LogDate",          row.LogDate.ToDateTime(TimeOnly.MinValue)),
            new SqlParameter("@MealType",         (object?)row.MealType ?? DBNull.Value),
            new SqlParameter("@CookingHistoryId", (object?)row.CookingHistoryId ?? DBNull.Value),
            new SqlParameter("@RecipeId",         (object?)row.RecipeId ?? DBNull.Value),
            new SqlParameter("@RecipeName",       (object?)row.RecipeName ?? DBNull.Value),
            new SqlParameter("@ServingsEaten",    row.ServingsEaten),
            new SqlParameter("@Calories",         (object?)row.Calories ?? DBNull.Value),
            new SqlParameter("@Protein",          (object?)row.Protein ?? DBNull.Value),
            new SqlParameter("@Carbohydrates",    (object?)row.Carbohydrates ?? DBNull.Value),
            new SqlParameter("@TotalFat",         (object?)row.TotalFat ?? DBNull.Value),
            new SqlParameter("@DietaryFiber",     (object?)row.DietaryFiber ?? DBNull.Value),
            new SqlParameter("@Sodium",           (object?)row.Sodium ?? DBNull.Value),
            new SqlParameter("@IsManualEntry",    row.IsManualEntry));
    }

    public async Task MarkNutritionLoggedAsync(Guid cookingHistoryId, CancellationToken ct) =>
        await ExecuteNonQueryAsync(
            "UPDATE CookingHistory SET NutritionLogged = 1 WHERE Id = @Id",
            new SqlParameter("@Id", cookingHistoryId));

    public async Task<DailySummaryRow> GetDailySummaryAsync(Guid userId, DateOnly date, CancellationToken ct)
    {
        const string sql = @"
            SELECT ISNULL(SUM(Calories), 0)       AS TotalCalories,
                   ISNULL(SUM(Protein), 0)        AS TotalProtein,
                   ISNULL(SUM(Carbohydrates), 0)  AS TotalCarbs,
                   ISNULL(SUM(TotalFat), 0)       AS TotalFat,
                   ISNULL(SUM(DietaryFiber), 0)   AS TotalFiber,
                   ISNULL(SUM(Sodium), 0)         AS TotalSodium,
                   COUNT(*)                        AS MealCount,
                   SUM(CASE WHEN Calories IS NULL AND IsManualEntry = 0 THEN 1 ELSE 0 END) AS MealsWithoutNutrition
            FROM DailyNutritionLog
            WHERE UserId = @UserId AND LogDate = @Date";

        List<DailySummaryRow> rows = await ExecuteReaderAsync(
            sql,
            MapSummaryNoDate,
            new SqlParameter("@UserId", userId),
            new SqlParameter("@Date",   date.ToDateTime(TimeOnly.MinValue)));

        return rows.FirstOrDefault() ?? new DailySummaryRow();
    }

    public async Task<List<DailySummaryRow>> GetTrendAsync(Guid userId, int days, CancellationToken ct)
    {
        const string sql = @"
            SELECT LogDate,
                   ISNULL(SUM(Calories), 0)      AS TotalCalories,
                   ISNULL(SUM(Protein), 0)       AS TotalProtein,
                   ISNULL(SUM(Carbohydrates), 0) AS TotalCarbs,
                   ISNULL(SUM(TotalFat), 0)      AS TotalFat,
                   ISNULL(SUM(DietaryFiber), 0)  AS TotalFiber,
                   ISNULL(SUM(Sodium), 0)        AS TotalSodium,
                   COUNT(*)                       AS MealCount,
                   0                              AS MealsWithoutNutrition
            FROM DailyNutritionLog
            WHERE UserId = @UserId
              AND LogDate >= CAST(DATEADD(day, -(@Days - 1), GETUTCDATE()) AS DATE)
            GROUP BY LogDate
            ORDER BY LogDate ASC";

        return await ExecuteReaderAsync(
            sql,
            MapSummaryWithDate,
            new SqlParameter("@UserId", userId),
            new SqlParameter("@Days",   days));
    }

    public async Task<List<DailyNutritionLogRow>> GetDayDetailAsync(Guid userId, DateOnly date, CancellationToken ct)
    {
        const string sql = @"
            SELECT Id, UserId, LogDate, MealType, CookingHistoryId, RecipeId, RecipeName,
                   ServingsEaten, Calories, Protein, Carbohydrates, TotalFat, DietaryFiber, Sodium, IsManualEntry
            FROM DailyNutritionLog
            WHERE UserId = @UserId AND LogDate = @Date
            ORDER BY CreatedAt ASC";

        return await ExecuteReaderAsync(
            sql,
            MapLogRow,
            new SqlParameter("@UserId", userId),
            new SqlParameter("@Date",   date.ToDateTime(TimeOnly.MinValue)));
    }

    public async Task<NutritionalGoalRow?> GetActiveGoalAsync(Guid userId, CancellationToken ct)
    {
        const string sql = @"
            SELECT TOP 1 Id, GoalType, StartDate, EndDate, TargetCalories, TargetProtein,
                         TargetCarbs, TargetFat, TargetFiber, TargetSodium, Notes
            FROM NutritionalGoal
            WHERE UserId = @UserId
              AND StartDate <= CAST(GETUTCDATE() AS DATE)
              AND (EndDate IS NULL OR EndDate >= CAST(GETUTCDATE() AS DATE))
            ORDER BY StartDate DESC";

        List<NutritionalGoalRow> rows = await ExecuteReaderAsync(
            sql,
            MapGoal,
            new SqlParameter("@UserId", userId));

        return rows.FirstOrDefault();
    }

    public async Task<Guid> UpsertGoalAsync(Guid userId, NutritionalGoalRow goal, CancellationToken ct)
    {
        Guid id = goal.Id == Guid.Empty ? Guid.NewGuid() : goal.Id;

        const string sql = @"
            MERGE NutritionalGoal AS target
            USING (SELECT @Id AS Id) AS src ON target.Id = src.Id
            WHEN MATCHED THEN
                UPDATE SET GoalType=@GoalType, StartDate=@StartDate, EndDate=@EndDate,
                           TargetCalories=@TargetCalories, TargetProtein=@TargetProtein,
                           TargetCarbs=@TargetCarbs, TargetFat=@TargetFat,
                           TargetFiber=@TargetFiber, TargetSodium=@TargetSodium, Notes=@Notes
            WHEN NOT MATCHED THEN
                INSERT (Id, UserId, GoalType, StartDate, EndDate, TargetCalories, TargetProtein,
                        TargetCarbs, TargetFat, TargetFiber, TargetSodium, Notes, CreatedAt)
                VALUES (@Id, @UserId, @GoalType, @StartDate, @EndDate, @TargetCalories, @TargetProtein,
                        @TargetCarbs, @TargetFat, @TargetFiber, @TargetSodium, @Notes, GETUTCDATE());";

        await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id",             id),
            new SqlParameter("@UserId",         userId),
            new SqlParameter("@GoalType",       goal.GoalType),
            new SqlParameter("@StartDate",      goal.StartDate.ToDateTime(TimeOnly.MinValue)),
            new SqlParameter("@EndDate",        (object?)goal.EndDate?.ToDateTime(TimeOnly.MinValue) ?? DBNull.Value),
            new SqlParameter("@TargetCalories", (object?)goal.TargetCalories ?? DBNull.Value),
            new SqlParameter("@TargetProtein",  (object?)goal.TargetProtein ?? DBNull.Value),
            new SqlParameter("@TargetCarbs",    (object?)goal.TargetCarbs ?? DBNull.Value),
            new SqlParameter("@TargetFat",      (object?)goal.TargetFat ?? DBNull.Value),
            new SqlParameter("@TargetFiber",    (object?)goal.TargetFiber ?? DBNull.Value),
            new SqlParameter("@TargetSodium",   (object?)goal.TargetSodium ?? DBNull.Value),
            new SqlParameter("@Notes",          (object?)goal.Notes ?? DBNull.Value));

        return id;
    }

    public async Task EndGoalAsync(Guid goalId, Guid userId, CancellationToken ct) =>
        await ExecuteNonQueryAsync(
            "UPDATE NutritionalGoal SET EndDate = CAST(GETUTCDATE() AS DATE) WHERE Id = @Id AND UserId = @UserId",
            new SqlParameter("@Id",     goalId),
            new SqlParameter("@UserId", userId));

    public async Task<List<NutritionalGoalRow>> GetGoalHistoryAsync(Guid userId, CancellationToken ct)
    {
        const string sql = @"
            SELECT Id, GoalType, StartDate, EndDate, TargetCalories, TargetProtein,
                   TargetCarbs, TargetFat, TargetFiber, TargetSodium, Notes
            FROM NutritionalGoal
            WHERE UserId = @UserId
            ORDER BY StartDate DESC";

        return await ExecuteReaderAsync(sql, MapGoal, new SqlParameter("@UserId", userId));
    }

    // ── Mappers ────────────────────────────────────────────────────────────────

    private static DailySummaryRow MapSummaryNoDate(IDataRecord r) => new()
    {
        TotalCalories          = (decimal)r["TotalCalories"],
        TotalProtein           = (decimal)r["TotalProtein"],
        TotalCarbs             = (decimal)r["TotalCarbs"],
        TotalFat               = (decimal)r["TotalFat"],
        TotalFiber             = (decimal)r["TotalFiber"],
        TotalSodium            = (decimal)r["TotalSodium"],
        MealCount              = (int)r["MealCount"],
        MealsWithoutNutrition  = (int)r["MealsWithoutNutrition"]
    };

    private static DailySummaryRow MapSummaryWithDate(IDataRecord r) => new()
    {
        LogDate                = r["LogDate"] == DBNull.Value ? null : DateOnly.FromDateTime((DateTime)r["LogDate"]),
        TotalCalories          = (decimal)r["TotalCalories"],
        TotalProtein           = (decimal)r["TotalProtein"],
        TotalCarbs             = (decimal)r["TotalCarbs"],
        TotalFat               = (decimal)r["TotalFat"],
        TotalFiber             = (decimal)r["TotalFiber"],
        TotalSodium            = (decimal)r["TotalSodium"],
        MealCount              = (int)r["MealCount"],
        MealsWithoutNutrition  = (int)r["MealsWithoutNutrition"]
    };

    private static DailyNutritionLogRow MapLogRow(IDataRecord r) => new()
    {
        Id               = (Guid)r["Id"],
        UserId           = (Guid)r["UserId"],
        LogDate          = DateOnly.FromDateTime((DateTime)r["LogDate"]),
        MealType         = r["MealType"] == DBNull.Value ? null : r["MealType"].ToString(),
        CookingHistoryId = r["CookingHistoryId"] == DBNull.Value ? null : (Guid?)r["CookingHistoryId"],
        RecipeId         = r["RecipeId"] == DBNull.Value ? null : (Guid?)r["RecipeId"],
        RecipeName       = r["RecipeName"] == DBNull.Value ? null : r["RecipeName"].ToString(),
        ServingsEaten    = (decimal)r["ServingsEaten"],
        Calories         = r["Calories"] == DBNull.Value ? null : (decimal?)r["Calories"],
        Protein          = r["Protein"] == DBNull.Value ? null : (decimal?)r["Protein"],
        Carbohydrates    = r["Carbohydrates"] == DBNull.Value ? null : (decimal?)r["Carbohydrates"],
        TotalFat         = r["TotalFat"] == DBNull.Value ? null : (decimal?)r["TotalFat"],
        DietaryFiber     = r["DietaryFiber"] == DBNull.Value ? null : (decimal?)r["DietaryFiber"],
        Sodium           = r["Sodium"] == DBNull.Value ? null : (decimal?)r["Sodium"],
        IsManualEntry    = (bool)r["IsManualEntry"]
    };

    private static NutritionalGoalRow MapGoal(IDataRecord r) => new()
    {
        Id             = (Guid)r["Id"],
        GoalType       = r["GoalType"].ToString() ?? "Daily",
        StartDate      = DateOnly.FromDateTime((DateTime)r["StartDate"]),
        EndDate        = r["EndDate"] == DBNull.Value ? null : DateOnly.FromDateTime((DateTime)r["EndDate"]),
        TargetCalories = r["TargetCalories"] == DBNull.Value ? null : (decimal?)r["TargetCalories"],
        TargetProtein  = r["TargetProtein"] == DBNull.Value ? null : (decimal?)r["TargetProtein"],
        TargetCarbs    = r["TargetCarbs"] == DBNull.Value ? null : (decimal?)r["TargetCarbs"],
        TargetFat      = r["TargetFat"] == DBNull.Value ? null : (decimal?)r["TargetFat"],
        TargetFiber    = r["TargetFiber"] == DBNull.Value ? null : (decimal?)r["TargetFiber"],
        TargetSodium   = r["TargetSodium"] == DBNull.Value ? null : (decimal?)r["TargetSodium"],
        Notes          = r["Notes"] == DBNull.Value ? null : r["Notes"].ToString()
    };
}
