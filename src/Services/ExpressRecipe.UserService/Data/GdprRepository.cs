using ExpressRecipe.Data.Common;
using Microsoft.Data.SqlClient;

namespace ExpressRecipe.UserService.Data;

public sealed class GdprRequestDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string RequestType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? DownloadUrl { get; set; }
    public string? Notes { get; set; }
}

public interface IGdprRepository
{
    Task<Guid> CreateRequestAsync(Guid userId, string requestType, CancellationToken ct = default);
    Task<List<GdprRequestDto>> GetRequestsByUserAsync(Guid userId, CancellationToken ct = default);
    Task SetStatusAsync(Guid requestId, string status, string? downloadUrl = null, string? notes = null, CancellationToken ct = default);
    Task AnonymizeUserAsync(Guid userId, CancellationToken ct = default);
    Task HardDeleteUserDataAsync(Guid userId, CancellationToken ct = default);
}

public sealed class GdprRepository : SqlHelper, IGdprRepository
{
    public GdprRepository(string connectionString) : base(connectionString) { }

    public async Task<Guid> CreateRequestAsync(Guid userId, string requestType, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO GdprRequest (Id, UserId, RequestType, Status, RequestedAt)
            VALUES (@Id, @UserId, @RequestType, 'Pending', GETUTCDATE())";

        var id = Guid.NewGuid();
        await ExecuteNonQueryAsync(
            sql, ct,
            CreateParameter("@Id", id),
            CreateParameter("@UserId", userId),
            CreateParameter("@RequestType", requestType));
        return id;
    }

    public async Task<List<GdprRequestDto>> GetRequestsByUserAsync(Guid userId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT Id, UserId, RequestType, Status, RequestedAt, CompletedAt, DownloadUrl, Notes
            FROM GdprRequest
            WHERE UserId = @UserId
            ORDER BY RequestedAt DESC";

        return await ExecuteReaderAsync(
            sql,
            reader => new GdprRequestDto
            {
                Id = GetGuid(reader, "Id"),
                UserId = GetGuid(reader, "UserId"),
                RequestType = GetString(reader, "RequestType") ?? string.Empty,
                Status = GetString(reader, "Status") ?? string.Empty,
                RequestedAt = GetDateTime(reader, "RequestedAt"),
                CompletedAt = GetNullableDateTime(reader, "CompletedAt"),
                DownloadUrl = GetString(reader, "DownloadUrl"),
                Notes = GetString(reader, "Notes")
            },
            ct,
            CreateParameter("@UserId", userId));
    }

    public async Task SetStatusAsync(Guid requestId, string status, string? downloadUrl = null, string? notes = null, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE GdprRequest
            SET Status      = @Status,
                CompletedAt = CASE WHEN @Status IN ('Completed','Failed') THEN GETUTCDATE() ELSE CompletedAt END,
                DownloadUrl = COALESCE(@DownloadUrl, DownloadUrl),
                Notes       = COALESCE(@Notes, Notes)
            WHERE Id = @Id";

        await ExecuteNonQueryAsync(
            sql, ct,
            CreateParameter("@Id", requestId),
            CreateParameter("@Status", status),
            CreateParameter("@DownloadUrl", (object?)downloadUrl ?? DBNull.Value),
            CreateParameter("@Notes", (object?)notes ?? DBNull.Value));
    }

    /// <summary>
    /// Anonymise PII columns in UserProfile but keep the row for DB integrity.
    /// Also hard-deletes <c>UserAllergen</c> and <c>UserDietaryRestriction</c> rows for this user —
    /// these contain sensitive health data and serve no purpose without the user's identity.
    /// <para>
    /// Note: inventory, meal-plan, and community records are NOT modified here; they are
    /// handled by each respective service when they receive the <c>GdprForgetEvent</c> from
    /// the message bus.
    /// </para>
    /// </summary>
    public async Task AnonymizeUserAsync(Guid userId, CancellationToken ct = default)
    {
        await ExecuteTransactionAsync<int>(async (conn, tx) =>
        {
            // Anonymise UserProfile PII
            const string anonymiseSql = @"
                UPDATE UserProfile
                SET Email       = CONCAT('anonymous_', CAST(@UserId AS NVARCHAR(36)), '@deleted.local'),
                    FirstName   = 'Deleted',
                    LastName    = 'User',
                    DateOfBirth = NULL,
                    Gender      = NULL,
                    HeightCm    = NULL,
                    WeightKg    = NULL,
                    UpdatedAt   = GETUTCDATE()
                WHERE UserId = @UserId AND IsDeleted = 0";

            await using var cmd1 = conn.CreateCommand();
            cmd1.Transaction = tx;
            cmd1.CommandText = anonymiseSql;
            cmd1.Parameters.Add(new SqlParameter("@UserId", userId));
            await cmd1.ExecuteNonQueryAsync(ct);

            // Hard-delete allergen/dietary restriction data
            const string deleteAllergensSql = @"
                DELETE FROM UserAllergen WHERE UserId = @UserId;
                DELETE FROM UserDietaryRestriction WHERE UserId = @UserId;";

            await using var cmd2 = conn.CreateCommand();
            cmd2.Transaction = tx;
            cmd2.CommandText = deleteAllergensSql;
            cmd2.Parameters.Add(new SqlParameter("@UserId", userId));
            await cmd2.ExecuteNonQueryAsync(ct);

            return 0;
        });
    }

    /// <summary>
    /// Hard-delete all rows owned by this user across all UserService tables.
    /// Called as part of the account-deletion saga after the confirmation window.
    /// </summary>
    public async Task HardDeleteUserDataAsync(Guid userId, CancellationToken ct = default)
    {
        await ExecuteTransactionAsync<int>(async (conn, tx) =>
        {
            var tables = new[]
            {
                "UserAllergen",
                "UserDietaryRestriction",
                "UserHealthGoal",
                "UserFavorite",
                "UserProductRating",
                "UserCuisinePreference",
                "UserCoupon",
                "UserFavoriteStore",
                "UserFriend",
                "UserFamilyMember",
                "PointsTransaction",
                "UserActivity",
                "UserList",
                "UserReport",
                "GdprRequest",
                "UserProfile"
            };

            foreach (var table in tables)
            {
                await using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = $"DELETE FROM [{table}] WHERE UserId = @UserId";
                cmd.Parameters.Add(new SqlParameter("@UserId", userId));
                try
                {
                    await cmd.ExecuteNonQueryAsync(ct);
                }
                catch (Microsoft.Data.SqlClient.SqlException ex)
                    when (ex.Number == 208) // Invalid object name – table doesn't exist in this DB
                {
                    // Table not present in this service DB; safe to skip.
                }
                // All other exceptions (FK violations, permission errors, etc.) propagate
                // so that the deletion failure is visible and can be investigated.
            }

            return 0;
        });
    }
}
