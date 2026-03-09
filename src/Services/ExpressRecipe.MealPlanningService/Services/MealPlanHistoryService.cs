using System.Text.Json;
using ExpressRecipe.MealPlanningService.Data;
using Microsoft.Data.SqlClient;

namespace ExpressRecipe.MealPlanningService.Services;

public interface IMealPlanHistoryService
{
    Task<Guid> TakePlanSnapshotAsync(Guid planId, Guid userId, string snapshotType, string? label = null, CancellationToken ct = default);
    Task<Guid> TakeDaySnapshotAsync(Guid planId, Guid userId, DateOnly date, string snapshotType, CancellationToken ct = default);
    Task<Guid> TakeWeekSnapshotAsync(Guid planId, Guid userId, DateOnly weekStart, string snapshotType, CancellationToken ct = default);
    Task<List<MealPlanSnapshotDto>> GetSnapshotsAsync(Guid planId, string? scope = null, CancellationToken ct = default);
    Task RestoreSnapshotAsync(Guid snapshotId, Guid userId, CancellationToken ct = default);
    Task LogChangeAsync(Guid plannedMealId, Guid planId, string changeType, Guid changedBy, object? before, object? after, CancellationToken ct = default);
    Task<List<MealChangeLogDto>> GetMealHistoryAsync(Guid plannedMealId, CancellationToken ct = default);
}

public sealed record MealPlanSnapshotDto
{
    public Guid Id { get; init; }
    public Guid MealPlanId { get; init; }
    public string SnapshotType { get; init; } = string.Empty;
    public string? Label { get; init; }
    public string Scope { get; init; } = string.Empty;
    public DateOnly? ScopeDate { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed record MealChangeLogDto
{
    public Guid Id { get; init; }
    public string ChangeType { get; init; } = string.Empty;
    public Guid ChangedBy { get; init; }
    public string? BeforeJson { get; init; }
    public string? AfterJson { get; init; }
    public DateTime ChangedAt { get; init; }
}

public sealed class MealPlanHistoryService : IMealPlanHistoryService
{
    private readonly string _connectionString;
    private readonly IMealPlanningRepository _plans;
    private static readonly TimeSpan DedupWindow = TimeSpan.FromMinutes(5);
    private static readonly JsonSerializerOptions SnapshotOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public MealPlanHistoryService(string connectionString, IMealPlanningRepository plans)
    {
        _connectionString = connectionString;
        _plans = plans;
    }

    public async Task<Guid> TakePlanSnapshotAsync(Guid planId, Guid userId,
        string snapshotType, string? label = null, CancellationToken ct = default)
    {
        if (snapshotType == "Auto" && await RecentSnapshotExistsAsync(planId, "Plan", null, ct)) { return Guid.Empty; }
        List<PlannedMealDto> meals = await _plans.GetPlannedMealsAsync(planId, null, null, ct);
        return await InsertSnapshotAsync(planId, userId, snapshotType, label, "Plan", null, SerializeSnapshot(planId, meals), ct);
    }

    public async Task<Guid> TakeDaySnapshotAsync(Guid planId, Guid userId,
        DateOnly date, string snapshotType, CancellationToken ct = default)
    {
        if (snapshotType == "Auto" && await RecentSnapshotExistsAsync(planId, "Day", date, ct)) { return Guid.Empty; }
        List<PlannedMealDto> meals = await _plans.GetMealsByDateAsync(planId, date, ct);
        return await InsertSnapshotAsync(planId, userId, snapshotType, null, "Day", date, SerializeSnapshot(planId, meals), ct);
    }

    public async Task<Guid> TakeWeekSnapshotAsync(Guid planId, Guid userId,
        DateOnly weekStart, string snapshotType, CancellationToken ct = default)
    {
        List<PlannedMealDto> meals = await _plans.GetPlannedMealsAsync(
            planId, weekStart.ToDateTime(TimeOnly.MinValue), weekStart.AddDays(6).ToDateTime(TimeOnly.MaxValue), ct);
        return await InsertSnapshotAsync(planId, userId, snapshotType, null, "Week", weekStart, SerializeSnapshot(planId, meals), ct);
    }

    public async Task RestoreSnapshotAsync(Guid snapshotId, Guid userId, CancellationToken ct = default)
    {
        const string selectSql = "SELECT MealPlanId, Scope, ScopeDate, SnapshotData FROM MealPlanSnapshot WHERE Id = @Id";
        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);

        string rawPlanId;
        string scope;
        string scopeData;
        DateOnly? scopeDate;
        await using (SqlCommand cmd = new(selectSql, conn))
        {
            cmd.Parameters.AddWithValue("@Id", snapshotId);
            await using SqlDataReader reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) { throw new KeyNotFoundException("Snapshot not found"); }
            rawPlanId = reader.GetGuid(0).ToString();
            scope     = reader.GetString(1);
            scopeDate = reader.IsDBNull(2) ? null : DateOnly.FromDateTime(reader.GetDateTime(2));
            scopeData = reader.GetString(3);
        }

        Guid planId = Guid.Parse(rawPlanId);
        await TakePlanSnapshotAsync(planId, userId, "Auto", "Pre-restore", ct);
        await DeleteMealsInScopeAsync(planId, scope, scopeDate, conn, ct);
        await RecreateMealsFromJsonAsync(scopeData, planId, userId, conn, ct);
    }

    public async Task LogChangeAsync(Guid plannedMealId, Guid planId, string changeType,
        Guid changedBy, object? before, object? after, CancellationToken ct = default)
    {
        const string sql = @"INSERT INTO MealChangeLog
            (Id, PlannedMealId, MealPlanId, ChangeType, ChangedBy, BeforeJson, AfterJson, ChangedAt)
            VALUES (NEWID(), @PlannedMealId, @MealPlanId, @ChangeType, @ChangedBy, @BeforeJson, @AfterJson, GETUTCDATE())";
        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@PlannedMealId", plannedMealId);
        cmd.Parameters.AddWithValue("@MealPlanId",    planId);
        cmd.Parameters.AddWithValue("@ChangeType",    changeType);
        cmd.Parameters.AddWithValue("@ChangedBy",     changedBy);
        cmd.Parameters.AddWithValue("@BeforeJson",    before is null ? DBNull.Value : JsonSerializer.Serialize(before, SnapshotOptions));
        cmd.Parameters.AddWithValue("@AfterJson",     after  is null ? DBNull.Value : JsonSerializer.Serialize(after, SnapshotOptions));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<MealChangeLogDto>> GetMealHistoryAsync(Guid plannedMealId, CancellationToken ct = default)
    {
        const string sql = "SELECT Id, ChangeType, ChangedBy, BeforeJson, AfterJson, ChangedAt FROM MealChangeLog WHERE PlannedMealId = @Id ORDER BY ChangedAt DESC";
        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@Id", plannedMealId);
        List<MealChangeLogDto> results = new();
        await using SqlDataReader r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            results.Add(new MealChangeLogDto
            {
                Id         = r.GetGuid(0),
                ChangeType = r.GetString(1),
                ChangedBy  = r.GetGuid(2),
                BeforeJson = r.IsDBNull(3) ? null : r.GetString(3),
                AfterJson  = r.IsDBNull(4) ? null : r.GetString(4),
                ChangedAt  = r.GetDateTime(5)
            });
        }
        return results;
    }

    public async Task<List<MealPlanSnapshotDto>> GetSnapshotsAsync(Guid planId, string? scope = null, CancellationToken ct = default)
    {
        string sql = "SELECT Id, MealPlanId, SnapshotType, Label, Scope, ScopeDate, CreatedAt FROM MealPlanSnapshot WHERE MealPlanId = @PlanId"
            + (scope is null ? "" : " AND Scope = @Scope") + " ORDER BY CreatedAt DESC";
        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@PlanId", planId);
        if (scope is not null) { cmd.Parameters.AddWithValue("@Scope", scope); }
        List<MealPlanSnapshotDto> results = new();
        await using SqlDataReader r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            results.Add(new MealPlanSnapshotDto
            {
                Id           = r.GetGuid(0),
                MealPlanId   = r.GetGuid(1),
                SnapshotType = r.GetString(2),
                Label        = r.IsDBNull(3) ? null : r.GetString(3),
                Scope        = r.GetString(4),
                ScopeDate    = r.IsDBNull(5) ? null : DateOnly.FromDateTime(r.GetDateTime(5)),
                CreatedAt    = r.GetDateTime(6)
            });
        }
        return results;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string SerializeSnapshot(Guid planId, List<PlannedMealDto> meals)
        => JsonSerializer.Serialize(new { planId = planId.ToString(), snapshotDate = DateTime.UtcNow, meals }, SnapshotOptions);

    private async Task<bool> RecentSnapshotExistsAsync(Guid planId, string scope, DateOnly? scopeDate, CancellationToken ct)
    {
        const string sql = @"SELECT COUNT(1) FROM MealPlanSnapshot
            WHERE MealPlanId=@PlanId AND Scope=@Scope
              AND (@ScopeDate IS NULL OR ScopeDate=@ScopeDate)
              AND CreatedAt >= @Cutoff";
        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@PlanId",    planId);
        cmd.Parameters.AddWithValue("@Scope",     scope);
        cmd.Parameters.AddWithValue("@ScopeDate", scopeDate.HasValue ? scopeDate.Value.ToDateTime(TimeOnly.MinValue) : DBNull.Value);
        cmd.Parameters.AddWithValue("@Cutoff",    DateTime.UtcNow - DedupWindow);
        return ((int?)await cmd.ExecuteScalarAsync(ct) ?? 0) > 0;
    }

    private async Task<Guid> InsertSnapshotAsync(Guid planId, Guid userId, string type, string? label,
        string scope, DateOnly? scopeDate, string json, CancellationToken ct)
    {
        const string sql = @"INSERT INTO MealPlanSnapshot
            (Id, MealPlanId, UserId, SnapshotType, Label, Scope, ScopeDate, SnapshotData, CreatedAt)
            OUTPUT INSERTED.Id
            VALUES (NEWID(), @PlanId, @UserId, @Type, @Label, @Scope, @ScopeDate, @Json, GETUTCDATE())";
        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@PlanId",    planId);
        cmd.Parameters.AddWithValue("@UserId",    userId);
        cmd.Parameters.AddWithValue("@Type",      type);
        cmd.Parameters.AddWithValue("@Label",     label ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@Scope",     scope);
        cmd.Parameters.AddWithValue("@ScopeDate", scopeDate.HasValue ? scopeDate.Value.ToDateTime(TimeOnly.MinValue) : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@Json",      json);
        return (Guid)await cmd.ExecuteScalarAsync(ct)!;
    }

    private async Task DeleteMealsInScopeAsync(Guid planId, string scope, DateOnly? scopeDate, SqlConnection conn, CancellationToken ct)
    {
        string sql = scope switch
        {
            "Day"  => "DELETE FROM PlannedMeal WHERE MealPlanId=@PlanId AND PlannedDate=@Date",
            "Week" => "DELETE FROM PlannedMeal WHERE MealPlanId=@PlanId AND PlannedDate BETWEEN @Date AND DATEADD(day,6,@Date)",
            _      => "DELETE FROM PlannedMeal WHERE MealPlanId=@PlanId"
        };
        await using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@PlanId", planId);
        if (scopeDate.HasValue) { cmd.Parameters.AddWithValue("@Date", scopeDate.Value.ToDateTime(TimeOnly.MinValue)); }
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task RecreateMealsFromJsonAsync(string json, Guid planId, Guid userId, SqlConnection conn, CancellationToken ct)
    {
        using JsonDocument doc = JsonDocument.Parse(json);
        foreach (JsonElement mealEl in doc.RootElement.GetProperty("meals").EnumerateArray())
        {
            const string insertMeal = @"INSERT INTO PlannedMeal
                (Id, MealPlanId, RecipeId, CustomMealName, MealType, PlannedDate, Servings, Notes, IsCompleted, SortOrder)
                VALUES (NEWID(), @PlanId, @RecipeId, @CustomName, @MealType, @PlannedDate, @Servings, @Notes, 0, @SortOrder)
                OUTPUT INSERTED.Id";
            await using SqlCommand cmd = new(insertMeal, conn);
            cmd.Parameters.AddWithValue("@PlanId",      planId);
            cmd.Parameters.AddWithValue("@RecipeId",    mealEl.TryGetProperty("recipeId", out JsonElement rid) && rid.ValueKind != JsonValueKind.Null ? rid.GetGuid() : DBNull.Value);
            cmd.Parameters.AddWithValue("@CustomName",  mealEl.TryGetProperty("customMealName", out JsonElement cn) && cn.ValueKind != JsonValueKind.Null ? cn.GetString() : DBNull.Value);
            cmd.Parameters.AddWithValue("@MealType",    mealEl.GetProperty("mealType").GetString()!);
            cmd.Parameters.AddWithValue("@PlannedDate", DateTime.Parse(mealEl.GetProperty("plannedFor").GetString()!, null, System.Globalization.DateTimeStyles.RoundtripKind));
            cmd.Parameters.AddWithValue("@Servings",    mealEl.GetProperty("servings").GetInt32());
            cmd.Parameters.AddWithValue("@Notes",       mealEl.TryGetProperty("notes", out JsonElement n) && n.ValueKind != JsonValueKind.Null ? n.GetString() : DBNull.Value);
            cmd.Parameters.AddWithValue("@SortOrder",   mealEl.TryGetProperty("sortOrder", out JsonElement so) ? so.GetInt32() : 0);
            Guid newMealId = (Guid)await cmd.ExecuteScalarAsync(ct)!;

            if (mealEl.TryGetProperty("courses", out JsonElement coursesEl))
            {
                foreach (JsonElement c in coursesEl.EnumerateArray())
                {
                    const string insertCourse = @"INSERT INTO MealCourse (Id, PlannedMealId, CourseType, RecipeId, Servings, SortOrder)
                        VALUES (NEWID(), @MealId, @CourseType, @RecipeId, @Servings, @SortOrder)";
                    await using SqlCommand cc = new(insertCourse, conn);
                    cc.Parameters.AddWithValue("@MealId",     newMealId);
                    cc.Parameters.AddWithValue("@CourseType", c.GetProperty("courseType").GetString()!);
                    cc.Parameters.AddWithValue("@RecipeId",   c.TryGetProperty("recipeId", out JsonElement crid) && crid.ValueKind != JsonValueKind.Null ? crid.GetGuid() : DBNull.Value);
                    cc.Parameters.AddWithValue("@Servings",   c.GetProperty("servings").GetDecimal());
                    cc.Parameters.AddWithValue("@SortOrder",  c.GetProperty("sortOrder").GetInt32());
                    await cc.ExecuteNonQueryAsync(ct);
                }
            }
        }
    }
}
