using Microsoft.Data.SqlClient;

namespace ExpressRecipe.MealPlanningService.Data;

public interface IMealScheduleConfigRepository
{
    Task<List<MealScheduleConfigDto>> GetConfigsAsync(Guid userId, CancellationToken ct = default);
    Task SetConfigsAsync(Guid userId, List<MealScheduleConfigDto> configs, CancellationToken ct = default);
}

public sealed record MealScheduleConfigDto
{
    public Guid   Id                         { get; init; }
    public Guid   UserId                     { get; init; }
    public Guid?  HouseholdId                { get; init; }
    public bool   IsHouseholdDefault         { get; init; }
    public string MealType                   { get; init; } = string.Empty;
    public TimeOnly TargetTime               { get; init; }
    public bool   NotifyEnabled              { get; init; }
    public int    NotifyMinutesBefore        { get; init; }
    public bool   FreezerReminderEnabled     { get; init; }
    public int    FreezerReminderHoursBefore { get; init; }
}

public sealed class MealScheduleConfigRepository : IMealScheduleConfigRepository
{
    private readonly string _connectionString;

    public MealScheduleConfigRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<List<MealScheduleConfigDto>> GetConfigsAsync(Guid userId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT Id, UserId, HouseholdId, IsHouseholdDefault, MealType, TargetTime,
                   NotifyEnabled, NotifyMinutesBefore, FreezerReminderEnabled, FreezerReminderHoursBefore
            FROM MealScheduleConfig
            WHERE UserId = @UserId
            ORDER BY MealType";

        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@UserId", userId);
        await using SqlDataReader reader = await cmd.ExecuteReaderAsync(ct);

        List<MealScheduleConfigDto> results = new();
        while (await reader.ReadAsync(ct))
        {
            results.Add(new MealScheduleConfigDto
            {
                Id                         = reader.GetGuid(0),
                UserId                     = reader.GetGuid(1),
                HouseholdId                = reader.IsDBNull(2) ? null : reader.GetGuid(2),
                IsHouseholdDefault         = reader.GetBoolean(3),
                MealType                   = reader.GetString(4),
                TargetTime                 = TimeOnly.FromTimeSpan(reader.GetTimeSpan(5)),
                NotifyEnabled              = reader.GetBoolean(6),
                NotifyMinutesBefore        = reader.GetInt32(7),
                FreezerReminderEnabled     = reader.GetBoolean(8),
                FreezerReminderHoursBefore = reader.GetInt32(9)
            });
        }
        return results;
    }

    public async Task SetConfigsAsync(Guid userId, List<MealScheduleConfigDto> configs, CancellationToken ct = default)
    {
        const string sql = @"
            MERGE MealScheduleConfig AS t
            USING (SELECT @UserId AS UserId, @MealType AS MealType) AS s(UserId, MealType)
            ON t.UserId = s.UserId AND t.MealType = s.MealType
            WHEN MATCHED THEN
                UPDATE SET TargetTime                 = @TargetTime,
                           NotifyEnabled              = @NotifyEnabled,
                           NotifyMinutesBefore        = @NotifyMinutesBefore,
                           FreezerReminderEnabled     = @FreezerReminderEnabled,
                           FreezerReminderHoursBefore = @FreezerReminderHoursBefore,
                           HouseholdId                = @HouseholdId,
                           IsHouseholdDefault         = @IsHouseholdDefault
            WHEN NOT MATCHED THEN
                INSERT (Id, UserId, HouseholdId, IsHouseholdDefault, MealType, TargetTime,
                        NotifyEnabled, NotifyMinutesBefore, FreezerReminderEnabled, FreezerReminderHoursBefore)
                VALUES (NEWID(), @UserId, @HouseholdId, @IsHouseholdDefault, @MealType, @TargetTime,
                        @NotifyEnabled, @NotifyMinutesBefore, @FreezerReminderEnabled, @FreezerReminderHoursBefore);";

        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);

        foreach (MealScheduleConfigDto config in configs)
        {
            await using SqlCommand cmd = new(sql, conn);
            cmd.Parameters.AddWithValue("@UserId",                     userId);
            cmd.Parameters.AddWithValue("@MealType",                   config.MealType);
            cmd.Parameters.AddWithValue("@TargetTime",                 config.TargetTime.ToTimeSpan());
            cmd.Parameters.AddWithValue("@NotifyEnabled",              config.NotifyEnabled);
            cmd.Parameters.AddWithValue("@NotifyMinutesBefore",        config.NotifyMinutesBefore);
            cmd.Parameters.AddWithValue("@FreezerReminderEnabled",     config.FreezerReminderEnabled);
            cmd.Parameters.AddWithValue("@FreezerReminderHoursBefore", config.FreezerReminderHoursBefore);
            cmd.Parameters.AddWithValue("@HouseholdId",                config.HouseholdId.HasValue ? config.HouseholdId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@IsHouseholdDefault",         config.IsHouseholdDefault);
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }
}
