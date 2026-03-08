using ExpressRecipe.Data.Common;
using ExpressRecipe.SafeForkService.Contracts.Requests;
using ExpressRecipe.SafeForkService.Contracts.Responses;

namespace ExpressRecipe.SafeForkService.Data;

public class TemporaryScheduleRepository : SqlHelper, ITemporaryScheduleRepository
{
    public TemporaryScheduleRepository(string connectionString) : base(connectionString)
    {
    }

    public async Task<List<TemporaryScheduleDto>> GetActiveAsync(Guid memberId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT Id, MemberId, ScheduleType, ActiveFrom, ActiveUntil, ConfigJson
            FROM TemporarySchedule
            WHERE MemberId = @MemberId
              AND ActiveUntil > GETUTCDATE()
              AND IsDeleted = 0
            ORDER BY ActiveFrom";

        return await ExecuteReaderAsync(
            sql,
            reader => new TemporaryScheduleDto
            {
                Id = GetGuid(reader, "Id"),
                MemberId = GetGuid(reader, "MemberId"),
                ScheduleType = GetString(reader, "ScheduleType") ?? string.Empty,
                ActiveFrom = new DateTimeOffset(GetDateTime(reader, "ActiveFrom"), TimeSpan.Zero),
                ActiveUntil = new DateTimeOffset(GetDateTime(reader, "ActiveUntil"), TimeSpan.Zero),
                ConfigJson = GetString(reader, "ConfigJson")
            },
            CreateParameter("@MemberId", memberId));
    }

    public async Task<Guid> AddAsync(Guid memberId, AddTemporaryScheduleRequest request, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO TemporarySchedule
                (Id, MemberId, ScheduleType, ActiveFrom, ActiveUntil, ConfigJson, CreatedAt)
            VALUES
                (@Id, @MemberId, @ScheduleType, @ActiveFrom, @ActiveUntil, @ConfigJson, GETUTCDATE())";

        Guid newId = Guid.NewGuid();

        await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", newId),
            CreateParameter("@MemberId", memberId),
            CreateParameter("@ScheduleType", request.ScheduleType),
            CreateParameter("@ActiveFrom", request.ActiveFrom.UtcDateTime),
            CreateParameter("@ActiveUntil", request.ActiveUntil.UtcDateTime),
            CreateParameter("@ConfigJson", request.ConfigJson));

        return newId;
    }

    public async Task<bool> SoftDeleteAsync(Guid scheduleId, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE TemporarySchedule
            SET IsDeleted = 1, UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND IsDeleted = 0";

        int rowsAffected = await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", scheduleId));

        return rowsAffected > 0;
    }
}
