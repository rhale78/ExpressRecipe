using Microsoft.Data.SqlClient;

namespace ExpressRecipe.MealPlanningService.Services.GoogleCalendar;

public interface IGoogleCalendarTokenRepository
{
    Task<CalendarTokenDto?> GetTokenAsync(Guid userId, CancellationToken ct = default);
    Task SaveTokenAsync(Guid userId, string accessToken, string refreshToken, DateTime expiresAt, string scopes, CancellationToken ct = default);
    Task UpdateTokenAsync(Guid userId, string accessToken, DateTime expiresAt, CancellationToken ct = default);
    Task DeleteTokenAsync(Guid userId, CancellationToken ct = default);
}

public sealed class GoogleCalendarTokenRepository : IGoogleCalendarTokenRepository
{
    private readonly string _connectionString;

    public GoogleCalendarTokenRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<CalendarTokenDto?> GetTokenAsync(Guid userId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT AccessToken, RefreshToken, ExpiresAt
            FROM ExternalCalendarToken
            WHERE UserId = @UserId AND Provider = 'Google'";

        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@UserId", userId);
        await using SqlDataReader reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            return new CalendarTokenDto
            {
                AccessToken  = reader.GetString(0),
                RefreshToken = reader.GetString(1),
                ExpiresAt    = reader.GetDateTime(2)
            };
        }
        return null;
    }

    public async Task SaveTokenAsync(Guid userId, string accessToken, string refreshToken, DateTime expiresAt, string scopes, CancellationToken ct = default)
    {
        const string sql = @"
            MERGE ExternalCalendarToken AS t
            USING (SELECT @UserId AS UserId, 'Google' AS Provider) AS s(UserId, Provider)
            ON t.UserId = s.UserId AND t.Provider = s.Provider
            WHEN MATCHED THEN
                UPDATE SET AccessToken = @AccessToken, RefreshToken = @RefreshToken,
                           ExpiresAt = @ExpiresAt, Scopes = @Scopes, UpdatedAt = GETUTCDATE()
            WHEN NOT MATCHED THEN
                INSERT (Id, UserId, Provider, AccessToken, RefreshToken, ExpiresAt, Scopes, CreatedAt)
                VALUES (NEWID(), @UserId, 'Google', @AccessToken, @RefreshToken, @ExpiresAt, @Scopes, GETUTCDATE());";

        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@UserId",       userId);
        cmd.Parameters.AddWithValue("@AccessToken",  accessToken);
        cmd.Parameters.AddWithValue("@RefreshToken", refreshToken);
        cmd.Parameters.AddWithValue("@ExpiresAt",    expiresAt);
        cmd.Parameters.AddWithValue("@Scopes",       scopes);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task UpdateTokenAsync(Guid userId, string accessToken, DateTime expiresAt, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE ExternalCalendarToken
            SET AccessToken = @AccessToken, ExpiresAt = @ExpiresAt, UpdatedAt = GETUTCDATE()
            WHERE UserId = @UserId AND Provider = 'Google'";

        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@UserId",      userId);
        cmd.Parameters.AddWithValue("@AccessToken", accessToken);
        cmd.Parameters.AddWithValue("@ExpiresAt",   expiresAt);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteTokenAsync(Guid userId, CancellationToken ct = default)
    {
        const string sql = @"
            DELETE FROM ExternalCalendarToken
            WHERE UserId = @UserId AND Provider = 'Google'";

        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@UserId", userId);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
