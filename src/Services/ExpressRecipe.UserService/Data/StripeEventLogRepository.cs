using ExpressRecipe.Data.Common;
using Microsoft.Data.SqlClient;

namespace ExpressRecipe.UserService.Data;

public interface IStripeEventLogRepository
{
    Task<bool> ExistsAsync(string stripeEventId, CancellationToken ct = default);
    Task InsertAsync(string stripeEventId, string eventType, CancellationToken ct = default);
}

public class StripeEventLogRepository : SqlHelper, IStripeEventLogRepository
{
    public StripeEventLogRepository(string connectionString) : base(connectionString) { }

    public async Task<bool> ExistsAsync(string stripeEventId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT COUNT(1) FROM StripeEventLog
            WHERE StripeEventId = @StripeEventId";

        var count = await ExecuteScalarAsync<int>(sql, ct, CreateParameter("@StripeEventId", stripeEventId));
        return count > 0;
    }

    public async Task InsertAsync(string stripeEventId, string eventType, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO StripeEventLog (Id, StripeEventId, EventType, ProcessedAt, Success)
            VALUES (NEWID(), @StripeEventId, @EventType, GETUTCDATE(), 1)";

        try
        {
            await ExecuteNonQueryAsync(sql, ct,
                CreateParameter("@StripeEventId", stripeEventId),
                CreateParameter("@EventType", eventType));
        }
        catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
        {
            // Unique constraint violation on StripeEventId — event already recorded.
            // Treat as success to ensure idempotency under concurrent webhook deliveries.
        }
    }
}
