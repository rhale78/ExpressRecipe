using Microsoft.Data.SqlClient;

namespace ExpressRecipe.MealPlanningService.Data;

public interface IMealAttendeeRepository
{
    Task<List<MealAttendeeDto>> GetAttendeesAsync(Guid plannedMealId, CancellationToken ct = default);
    Task SetAttendeesAsync(Guid plannedMealId, List<MealAttendeeDto> attendees, CancellationToken ct = default);
}

public sealed record MealAttendeeDto
{
    public Guid? UserId { get; init; }
    public Guid? FamilyMemberId { get; init; }
    public string? GuestName { get; init; }
    public string DisplayName { get; init; } = string.Empty;
}

public class MealAttendeeRepository : IMealAttendeeRepository
{
    private readonly string _connectionString;

    public MealAttendeeRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<List<MealAttendeeDto>> GetAttendeesAsync(Guid plannedMealId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT UserId, FamilyMemberId, GuestName
            FROM MealAttendee
            WHERE PlannedMealId = @PlannedMealId
            ORDER BY CreatedAt";

        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using SqlCommand command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@PlannedMealId", plannedMealId);

        List<MealAttendeeDto> attendees = new();
        await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            Guid? userId = reader.IsDBNull(0) ? null : reader.GetGuid(0);
            Guid? familyMemberId = reader.IsDBNull(1) ? null : reader.GetGuid(1);
            string? guestName = reader.IsDBNull(2) ? null : reader.GetString(2);

            attendees.Add(new MealAttendeeDto
            {
                UserId = userId,
                FamilyMemberId = familyMemberId,
                GuestName = guestName,
                DisplayName = guestName ?? userId?.ToString() ?? familyMemberId?.ToString() ?? string.Empty
            });
        }

        return attendees;
    }

    public async Task SetAttendeesAsync(Guid plannedMealId, List<MealAttendeeDto> attendees, CancellationToken ct = default)
    {
        const string deleteSql = "DELETE FROM MealAttendee WHERE PlannedMealId = @PlannedMealId";
        const string insertSql = @"
            INSERT INTO MealAttendee (PlannedMealId, UserId, FamilyMemberId, GuestName)
            VALUES (@PlannedMealId, @UserId, @FamilyMemberId, @GuestName)";

        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using SqlTransaction tx = (SqlTransaction)await connection.BeginTransactionAsync(ct);

        try
        {
            await using (SqlCommand deleteCmd = new SqlCommand(deleteSql, connection, tx))
            {
                deleteCmd.Parameters.AddWithValue("@PlannedMealId", plannedMealId);
                await deleteCmd.ExecuteNonQueryAsync(ct);
            }

            foreach (MealAttendeeDto attendee in attendees)
            {
                await using SqlCommand insertCmd = new SqlCommand(insertSql, connection, tx);
                insertCmd.Parameters.AddWithValue("@PlannedMealId", plannedMealId);
                insertCmd.Parameters.AddWithValue("@UserId", attendee.UserId.HasValue ? attendee.UserId.Value : (object)DBNull.Value);
                insertCmd.Parameters.AddWithValue("@FamilyMemberId", attendee.FamilyMemberId.HasValue ? attendee.FamilyMemberId.Value : (object)DBNull.Value);
                insertCmd.Parameters.AddWithValue("@GuestName", attendee.GuestName ?? (object)DBNull.Value);
                await insertCmd.ExecuteNonQueryAsync(ct);
            }

            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }
}
