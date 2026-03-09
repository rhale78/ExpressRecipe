using Microsoft.Data.SqlClient;

namespace ExpressRecipe.MealPlanningService.Data;

public interface IHouseholdTaskRepository
{
    Task<List<HouseholdTaskDto>> GetActiveTasksAsync(Guid householdId, CancellationToken ct = default);
    Task<List<HouseholdTaskDto>> GetTaskHistoryAsync(Guid householdId, DateOnly from, DateOnly to, CancellationToken ct = default);
    Task<HouseholdTaskDto?> GetByIdAsync(Guid taskId, CancellationToken ct = default);
    Task<Guid> CreateTaskAsync(Guid householdId, string taskType, string title, string? description,
        DateTime dueAt, string? relatedEntityType, Guid? relatedEntityId,
        int escalateAfterMins, CancellationToken ct = default);
    Task UpsertThawTaskAsync(Guid householdId, Guid plannedMealId, string title,
        string? description, DateTime dueAt, CancellationToken ct = default);
    Task ActionTaskAsync(Guid taskId, Guid actionedBy, string actionTaken, CancellationToken ct = default);
    Task DismissTaskAsync(Guid taskId, CancellationToken ct = default);
    Task DeleteTasksByRelatedEntityAsync(Guid relatedEntityId, CancellationToken ct = default);
    Task<List<HouseholdTaskDto>> GetEscalationDueTasksAsync(CancellationToken ct = default);
    Task MarkEscalationSentAsync(Guid taskId, CancellationToken ct = default);
    Task MarkReminderSentAsync(Guid taskId, CancellationToken ct = default);
}

public sealed record HouseholdTaskDto
{
    public Guid Id { get; init; }
    public Guid HouseholdId { get; init; }
    public string TaskType { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public DateTime DueAt { get; init; }
    public string? RelatedEntityType { get; init; }
    public Guid? RelatedEntityId { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? ActionTaken { get; init; }
    public Guid? ActionedBy { get; init; }
    public DateTime? ActionedAt { get; init; }
    public bool ReminderSent { get; init; }
    public bool EscalationSent { get; init; }
    public int EscalateAfterMins { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed class HouseholdTaskRepository : IHouseholdTaskRepository
{
    private readonly string _connectionString;

    public HouseholdTaskRepository(string connectionString) { _connectionString = connectionString; }

    private const string SelectColumns = @"
        Id, HouseholdId, TaskType, Title, Description, DueAt,
        RelatedEntityType, RelatedEntityId, Status, ActionTaken,
        ActionedBy, ActionedAt, ReminderSent, EscalationSent,
        EscalateAfterMins, CreatedAt";

    public async Task<List<HouseholdTaskDto>> GetActiveTasksAsync(Guid householdId, CancellationToken ct = default)
    {
        string sql = $@"
            SELECT {SelectColumns}
            FROM HouseholdTask
            WHERE HouseholdId=@HouseholdId AND Status IN ('Pending','Escalated')
            ORDER BY DueAt";

        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@HouseholdId", householdId);
        return await ReadTasksAsync(cmd, ct);
    }

    public async Task<List<HouseholdTaskDto>> GetTaskHistoryAsync(Guid householdId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        string sql = $@"
            SELECT {SelectColumns}
            FROM HouseholdTask
            WHERE HouseholdId=@HouseholdId
              AND CAST(CreatedAt AS DATE) >= @From
              AND CAST(CreatedAt AS DATE) <= @To
            ORDER BY CreatedAt DESC";

        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@HouseholdId", householdId);
        cmd.Parameters.AddWithValue("@From", from.ToDateTime(TimeOnly.MinValue));
        cmd.Parameters.AddWithValue("@To", to.ToDateTime(TimeOnly.MaxValue));
        return await ReadTasksAsync(cmd, ct);
    }

    public async Task<HouseholdTaskDto?> GetByIdAsync(Guid taskId, CancellationToken ct = default)
    {
        string sql = $@"
            SELECT {SelectColumns}
            FROM HouseholdTask
            WHERE Id=@Id";

        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@Id", taskId);
        List<HouseholdTaskDto> results = await ReadTasksAsync(cmd, ct);
        return results.FirstOrDefault();
    }

    public async Task<Guid> CreateTaskAsync(Guid householdId, string taskType, string title,
        string? description, DateTime dueAt, string? relatedEntityType, Guid? relatedEntityId,
        int escalateAfterMins, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO HouseholdTask
                (Id, HouseholdId, TaskType, Title, Description, DueAt,
                 RelatedEntityType, RelatedEntityId, Status, EscalateAfterMins, CreatedAt)
            OUTPUT INSERTED.Id
            VALUES
                (NEWID(), @HouseholdId, @TaskType, @Title, @Desc, @DueAt,
                 @RelatedEntityType, @RelatedEntityId, 'Pending', @EscalateAfterMins, GETUTCDATE())";

        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@HouseholdId",        householdId);
        cmd.Parameters.AddWithValue("@TaskType",           taskType);
        cmd.Parameters.AddWithValue("@Title",              title);
        cmd.Parameters.AddWithValue("@Desc",               description ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@DueAt",              dueAt);
        cmd.Parameters.AddWithValue("@RelatedEntityType",  relatedEntityType ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@RelatedEntityId",    relatedEntityId.HasValue ? relatedEntityId.Value : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@EscalateAfterMins",  escalateAfterMins);
        return (Guid)(await cmd.ExecuteScalarAsync(ct))!;
    }

    // UpsertThawTaskAsync creates or updates a ThawReminder for a specific meal.
    // Uses RelatedEntityId=plannedMealId to find existing pending task.
    public async Task UpsertThawTaskAsync(Guid householdId, Guid plannedMealId, string title,
        string? description, DateTime dueAt, CancellationToken ct = default)
    {
        const string sql = @"
            MERGE HouseholdTask AS t
            USING (SELECT @HouseholdId AS HouseholdId, @MealId AS MealId) AS s
            ON t.HouseholdId=s.HouseholdId AND t.RelatedEntityId=s.MealId
               AND t.TaskType='ThawReminder' AND t.Status IN ('Pending','Escalated')
            WHEN MATCHED THEN
                UPDATE SET DueAt=@DueAt, Title=@Title, Description=@Desc,
                    ReminderSent=0, EscalationSent=0, UpdatedAt=GETUTCDATE()
            WHEN NOT MATCHED THEN
                INSERT (Id, HouseholdId, TaskType, Title, Description, DueAt,
                        RelatedEntityType, RelatedEntityId, Status, CreatedAt)
                VALUES (NEWID(), @HouseholdId, 'ThawReminder', @Title, @Desc, @DueAt,
                        'PlannedMeal', @MealId, 'Pending', GETUTCDATE());";

        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@HouseholdId", householdId);
        cmd.Parameters.AddWithValue("@MealId",      plannedMealId);
        cmd.Parameters.AddWithValue("@Title",       title);
        cmd.Parameters.AddWithValue("@Desc",        description ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@DueAt",       dueAt);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task ActionTaskAsync(Guid taskId, Guid actionedBy, string actionTaken, CancellationToken ct = default)
    {
        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(@"
            UPDATE HouseholdTask
            SET Status='Actioned', ActionTaken=@Action, ActionedBy=@By,
                ActionedAt=GETUTCDATE(), UpdatedAt=GETUTCDATE()
            WHERE Id=@Id", conn);
        cmd.Parameters.AddWithValue("@Action", actionTaken);
        cmd.Parameters.AddWithValue("@By",     actionedBy);
        cmd.Parameters.AddWithValue("@Id",     taskId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DismissTaskAsync(Guid taskId, CancellationToken ct = default)
    {
        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(
            "UPDATE HouseholdTask SET Status='Dismissed', UpdatedAt=GETUTCDATE() WHERE Id=@Id", conn);
        cmd.Parameters.AddWithValue("@Id", taskId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteTasksByRelatedEntityAsync(Guid relatedEntityId, CancellationToken ct = default)
    {
        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(
            "DELETE FROM HouseholdTask WHERE RelatedEntityId=@Id AND Status='Pending'", conn);
        cmd.Parameters.AddWithValue("@Id", relatedEntityId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<HouseholdTaskDto>> GetEscalationDueTasksAsync(CancellationToken ct = default)
    {
        // Tasks that are Pending, past DueAt+EscalateAfterMins, and haven't been escalated yet
        string sql = $@"
            SELECT {SelectColumns}
            FROM HouseholdTask
            WHERE Status='Pending' AND EscalationSent=0
              AND DATEADD(minute, EscalateAfterMins, DueAt) <= GETUTCDATE()";

        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        return await ReadTasksAsync(cmd, ct);
    }

    public async Task MarkEscalationSentAsync(Guid taskId, CancellationToken ct = default)
    {
        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(
            "UPDATE HouseholdTask SET EscalationSent=1, Status='Escalated', UpdatedAt=GETUTCDATE() WHERE Id=@Id", conn);
        cmd.Parameters.AddWithValue("@Id", taskId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task MarkReminderSentAsync(Guid taskId, CancellationToken ct = default)
    {
        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(
            "UPDATE HouseholdTask SET ReminderSent=1, UpdatedAt=GETUTCDATE() WHERE Id=@Id", conn);
        cmd.Parameters.AddWithValue("@Id", taskId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task<List<HouseholdTaskDto>> ReadTasksAsync(SqlCommand cmd, CancellationToken ct)
    {
        List<HouseholdTaskDto> results = new();
        await using SqlDataReader r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            results.Add(new HouseholdTaskDto
            {
                Id                = r.GetGuid(0),
                HouseholdId       = r.GetGuid(1),
                TaskType          = r.GetString(2),
                Title             = r.GetString(3),
                Description       = r.IsDBNull(4)  ? null : r.GetString(4),
                DueAt             = r.GetDateTime(5),
                RelatedEntityType = r.IsDBNull(6)  ? null : r.GetString(6),
                RelatedEntityId   = r.IsDBNull(7)  ? null : r.GetGuid(7),
                Status            = r.GetString(8),
                ActionTaken       = r.IsDBNull(9)  ? null : r.GetString(9),
                ActionedBy        = r.IsDBNull(10) ? null : r.GetGuid(10),
                ActionedAt        = r.IsDBNull(11) ? null : r.GetDateTime(11),
                ReminderSent      = r.GetBoolean(12),
                EscalationSent    = r.GetBoolean(13),
                EscalateAfterMins = r.GetInt32(14),
                CreatedAt         = r.GetDateTime(15)
            });
        }
        return results;
    }
}
