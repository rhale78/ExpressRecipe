using Microsoft.Data.SqlClient;

namespace ExpressRecipe.MealPlanningService.Data;

public interface ICookingTimerRepository
{
    Task<List<CookingTimerDto>> GetActiveTimersAsync(Guid userId, CancellationToken ct = default);
    Task<CookingTimerDto?> GetByIdAsync(Guid timerId, CancellationToken ct = default);
    Task<Guid> CreateTimerAsync(Guid userId, Guid householdId, string label, int durationSeconds,
        Guid? recipeId, Guid? plannedMealId, bool startImmediately, CancellationToken ct = default);
    Task StartTimerAsync(Guid timerId, CancellationToken ct = default);
    Task PauseTimerAsync(Guid timerId, CancellationToken ct = default);
    Task ResumeTimerAsync(Guid timerId, CancellationToken ct = default);
    Task CancelTimerAsync(Guid timerId, CancellationToken ct = default);
    Task AcknowledgeTimerAsync(Guid timerId, CancellationToken ct = default);
    Task<List<CookingTimerDto>> GetExpiredUnnotifiedTimersAsync(CancellationToken ct = default);
    Task MarkNotificationSentAsync(Guid timerId, CancellationToken ct = default);
}

public sealed record CookingTimerDto
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public Guid HouseholdId { get; init; }
    public string Label { get; init; } = string.Empty;
    public Guid? RecipeId { get; init; }
    public Guid? PlannedMealId { get; init; }
    public int DurationSeconds { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime? PausedAt { get; init; }
    public int PausedSeconds { get; init; }
    public bool NotificationSent { get; init; }
    // Computed: remaining seconds (negative = overrun)
    public int RemainingSeconds => Status == "Running" && ExpiresAt.HasValue
        ? (int)(ExpiresAt.Value - DateTime.UtcNow).TotalSeconds
        : DurationSeconds - PausedSeconds;
}

public sealed class CookingTimerRepository : ICookingTimerRepository
{
    private readonly string _connectionString;

    public CookingTimerRepository(string connectionString) { _connectionString = connectionString; }

    public async Task<List<CookingTimerDto>> GetActiveTimersAsync(Guid userId, CancellationToken ct = default)
    {
        const string sql = @"SELECT Id,UserId,HouseholdId,Label,RecipeId,PlannedMealId,
                   DurationSeconds,StartedAt,ExpiresAt,Status,PausedAt,PausedSeconds,NotificationSent
            FROM CookingTimer WHERE UserId=@UserId AND Status IN ('Preset','Running','Paused','Expired')
            ORDER BY CreatedAt";
        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@UserId", userId);
        return await ReadTimersAsync(cmd, ct);
    }

    public async Task<CookingTimerDto?> GetByIdAsync(Guid timerId, CancellationToken ct = default)
    {
        const string sql = @"SELECT Id,UserId,HouseholdId,Label,RecipeId,PlannedMealId,
                   DurationSeconds,StartedAt,ExpiresAt,Status,PausedAt,PausedSeconds,NotificationSent
            FROM CookingTimer WHERE Id=@Id";
        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@Id", timerId);
        List<CookingTimerDto> results = await ReadTimersAsync(cmd, ct);
        return results.Count > 0 ? results[0] : null;
    }

    public async Task<List<CookingTimerDto>> GetExpiredUnnotifiedTimersAsync(CancellationToken ct = default)
    {
        const string sql = @"SELECT Id,UserId,HouseholdId,Label,RecipeId,PlannedMealId,
                   DurationSeconds,StartedAt,ExpiresAt,Status,PausedAt,PausedSeconds,NotificationSent
            FROM CookingTimer WHERE Status='Running' AND ExpiresAt<=GETUTCDATE() AND NotificationSent=0";
        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        return await ReadTimersAsync(cmd, ct);
    }

    public async Task<Guid> CreateTimerAsync(Guid userId, Guid householdId, string label,
        int durationSeconds, Guid? recipeId, Guid? plannedMealId, bool startImmediately,
        CancellationToken ct = default)
    {
        DateTime? startedAt = startImmediately ? DateTime.UtcNow : null;
        DateTime? expiresAt = startImmediately ? DateTime.UtcNow.AddSeconds(durationSeconds) : null;
        string status = startImmediately ? "Running" : "Preset";

        const string sql = @"INSERT INTO CookingTimer
            (Id,UserId,HouseholdId,Label,RecipeId,PlannedMealId,DurationSeconds,
             StartedAt,ExpiresAt,Status,CreatedAt)
            OUTPUT INSERTED.Id
            VALUES (NEWID(),@UserId,@HouseholdId,@Label,@RecipeId,@MealId,@Duration,
                    @StartedAt,@ExpiresAt,@Status,GETUTCDATE())";
        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@UserId",      userId);
        cmd.Parameters.AddWithValue("@HouseholdId", householdId);
        cmd.Parameters.AddWithValue("@Label",       label);
        cmd.Parameters.AddWithValue("@RecipeId",    recipeId.HasValue ? recipeId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@MealId",      plannedMealId.HasValue ? plannedMealId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@Duration",    durationSeconds);
        cmd.Parameters.AddWithValue("@StartedAt",   startedAt.HasValue ? (object)startedAt.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@ExpiresAt",   expiresAt.HasValue ? (object)expiresAt.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@Status",      status);
        return (Guid)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task StartTimerAsync(Guid timerId, CancellationToken ct = default)
    {
        // Compute remaining from DurationSeconds minus any PausedSeconds already accumulated
        const string sql = @"UPDATE CookingTimer
            SET Status='Running', StartedAt=GETUTCDATE(),
                ExpiresAt=DATEADD(second, DurationSeconds - PausedSeconds, GETUTCDATE()),
                UpdatedAt=GETUTCDATE()
            WHERE Id=@Id AND Status='Preset'";
        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@Id", timerId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task PauseTimerAsync(Guid timerId, CancellationToken ct = default)
    {
        // Freeze ExpiresAt; record paused timestamp to compute elapsed when resumed
        const string sql = @"UPDATE CookingTimer
            SET Status='Paused', PausedAt=GETUTCDATE(), UpdatedAt=GETUTCDATE()
            WHERE Id=@Id AND Status='Running'";
        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@Id", timerId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task ResumeTimerAsync(Guid timerId, CancellationToken ct = default)
    {
        // Re-extend ExpiresAt by the seconds remaining at the time of pause
        const string sql = @"UPDATE CookingTimer
            SET Status='Running',
                PausedSeconds = PausedSeconds + DATEDIFF(second, PausedAt, GETUTCDATE()),
                ExpiresAt = DATEADD(second, DATEDIFF(second, GETUTCDATE(), ExpiresAt)
                            + DATEDIFF(second, PausedAt, GETUTCDATE()), GETUTCDATE()),
                PausedAt=NULL, UpdatedAt=GETUTCDATE()
            WHERE Id=@Id AND Status='Paused'";
        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@Id", timerId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task CancelTimerAsync(Guid timerId, CancellationToken ct = default)
        => await SetStatusAsync(timerId, "Cancelled", ct);

    public async Task AcknowledgeTimerAsync(Guid timerId, CancellationToken ct = default)
        => await SetStatusAsync(timerId, "Acknowledged", ct);

    public async Task MarkNotificationSentAsync(Guid timerId, CancellationToken ct = default)
    {
        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(
            "UPDATE CookingTimer SET NotificationSent=1,Status='Expired',UpdatedAt=GETUTCDATE() WHERE Id=@Id", conn);
        cmd.Parameters.AddWithValue("@Id", timerId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task SetStatusAsync(Guid timerId, string status, CancellationToken ct)
    {
        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new("UPDATE CookingTimer SET Status=@Status,UpdatedAt=GETUTCDATE() WHERE Id=@Id", conn);
        cmd.Parameters.AddWithValue("@Status", status);
        cmd.Parameters.AddWithValue("@Id",     timerId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task<List<CookingTimerDto>> ReadTimersAsync(SqlCommand cmd, CancellationToken ct)
    {
        List<CookingTimerDto> results = new();
        await using SqlDataReader r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            results.Add(new CookingTimerDto
            {
                Id               = r.GetGuid(0),
                UserId           = r.GetGuid(1),
                HouseholdId      = r.GetGuid(2),
                Label            = r.GetString(3),
                RecipeId         = r.IsDBNull(4)  ? null : r.GetGuid(4),
                PlannedMealId    = r.IsDBNull(5)  ? null : r.GetGuid(5),
                DurationSeconds  = r.GetInt32(6),
                StartedAt        = r.IsDBNull(7)  ? null : r.GetDateTime(7),
                ExpiresAt        = r.IsDBNull(8)  ? null : r.GetDateTime(8),
                Status           = r.GetString(9),
                PausedAt         = r.IsDBNull(10) ? null : r.GetDateTime(10),
                PausedSeconds    = r.GetInt32(11),
                NotificationSent = r.GetBoolean(12)
            });
        }
        return results;
    }
}
