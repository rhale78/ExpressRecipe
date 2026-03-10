using Microsoft.Data.SqlClient;

namespace ExpressRecipe.MealPlanningService.Data;

// Priority constants — use these everywhere, don't hardcode numbers
public static class WorkQueuePriority
{
    public const int Expired              = 1;
    public const int SafetyAlert          = 2;
    public const int ExpiringCritical     = 3;  // 1-2 days
    public const int HouseholdTaskOverdue = 4;
    public const int PriceDrop            = 5;
    public const int ExpiringSoon         = 6;  // 3-7 days
    public const int HouseholdTaskPending = 7;
    public const int LowStockReorder      = 8;
    public const int MoveToFreezer        = 8;
    public const int RateRecipe           = 9;
    public const int SaveCookingNote      = 10;
}

public sealed record WorkQueueItemDto
{
    public Guid Id { get; init; }
    public Guid HouseholdId { get; init; }
    public string ItemType { get; init; } = string.Empty;
    public int Priority { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Body { get; init; }
    public string? ActionPayload { get; init; }   // raw JSON, parsed by UI/controller
    public string Status { get; init; } = string.Empty;
    public DateTime? ExpiresAt { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed record ActionQueueItemRequest
{
    public string ActionTaken { get; init; } = string.Empty;  // "AddedToShoppingList"|"Moved"|etc.
    public string? ActionData { get; init; }   // optional JSON for the action (e.g. shopping list id)
}

public interface IWorkQueueRepository
{
    // Returns pending items for this household, filtered by per-user snooze
    Task<List<WorkQueueItemDto>> GetPendingItemsAsync(Guid householdId, Guid userId,
        int limit = 50, CancellationToken ct = default);

    // Insert with deduplication — uses MERGE on (HouseholdId+ItemType+SourceEntityId)
    // If an active item already exists for this entity, does nothing
    Task UpsertItemAsync(Guid householdId, string itemType, int priority,
        string title, string? body, string? actionPayload,
        Guid? sourceEntityId, string? sourceService,
        DateTime? expiresAt, CancellationToken ct = default);

    // Mark as actioned — removes from queue for all household members
    Task ActionItemAsync(Guid itemId, Guid userId, string actionTaken,
        string? actionData, CancellationToken ct = default);

    // Dismiss permanently (household-wide)
    Task DismissItemAsync(Guid itemId, Guid userId, CancellationToken ct = default);

    // Snooze for this user only — item stays visible to other members
    Task SnoozeItemAsync(Guid itemId, Guid userId, DateTime snoozedUntil,
        CancellationToken ct = default);

    // Auto-expire items past ExpiresAt (called by cleanup worker)
    Task ExpireStaleItemsAsync(CancellationToken ct = default);
}

public sealed class WorkQueueRepository : IWorkQueueRepository
{
    private readonly string _connectionString;

    public WorkQueueRepository(string connectionString) { _connectionString = connectionString; }

    public async Task<List<WorkQueueItemDto>> GetPendingItemsAsync(
        Guid householdId, Guid userId, int limit = 50, CancellationToken ct = default)
    {
        // Exclude items snoozed by this user (join snooze table)
        const string sql = @"SELECT TOP (@Limit)
            w.Id, w.HouseholdId, w.ItemType, w.Priority, w.Title,
            w.Body, w.ActionPayload, w.Status, w.ExpiresAt, w.CreatedAt
        FROM WorkQueueItem w
        WHERE w.HouseholdId = @HouseholdId
          AND w.Status = 'Pending'
          AND (w.ExpiresAt IS NULL OR w.ExpiresAt > GETUTCDATE())
          AND NOT EXISTS (
              SELECT 1 FROM WorkQueueItemSnooze s
              WHERE s.WorkQueueItemId = w.Id
                AND s.UserId = @UserId
                AND s.SnoozedUntil > GETUTCDATE()
          )
        ORDER BY w.Priority ASC, w.CreatedAt ASC";

        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@Limit",       limit);
        cmd.Parameters.AddWithValue("@HouseholdId", householdId);
        cmd.Parameters.AddWithValue("@UserId",      userId);

        List<WorkQueueItemDto> items = new();
        await using SqlDataReader r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            items.Add(new WorkQueueItemDto
            {
                Id            = r.GetGuid(0),
                HouseholdId   = r.GetGuid(1),
                ItemType      = r.GetString(2),
                Priority      = r.GetInt32(3),
                Title         = r.GetString(4),
                Body          = r.IsDBNull(5) ? null : r.GetString(5),
                ActionPayload = r.IsDBNull(6) ? null : r.GetString(6),
                Status        = r.GetString(7),
                ExpiresAt     = r.IsDBNull(8) ? null : r.GetDateTime(8),
                CreatedAt     = r.GetDateTime(9)
            });
        }
        return items;
    }

    public async Task UpsertItemAsync(Guid householdId, string itemType, int priority,
        string title, string? body, string? actionPayload,
        Guid? sourceEntityId, string? sourceService,
        DateTime? expiresAt, CancellationToken ct = default)
    {
        // MERGE: if active item already exists for same household+type+sourceEntity → do nothing
        const string sql = @"
            MERGE WorkQueueItem AS t
            USING (SELECT @HouseholdId, @ItemType, @SourceEntityId)
                AS s(HouseholdId, ItemType, SourceEntityId)
            ON t.HouseholdId=s.HouseholdId
               AND t.ItemType=s.ItemType
               AND t.SourceEntityId=s.SourceEntityId
               AND t.Status='Pending'
            WHEN NOT MATCHED THEN INSERT (
                Id, HouseholdId, ItemType, Priority, Title, Body,
                ActionPayload, Status, SourceEntityId, SourceService, ExpiresAt, CreatedAt)
            VALUES (NEWID(), @HouseholdId, @ItemType, @Priority, @Title, @Body,
                @Payload, 'Pending', @SourceEntityId, @SourceService, @ExpiresAt, GETUTCDATE());";

        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@HouseholdId",    householdId);
        cmd.Parameters.AddWithValue("@ItemType",       itemType);
        cmd.Parameters.AddWithValue("@Priority",       priority);
        cmd.Parameters.AddWithValue("@Title",          title);
        cmd.Parameters.AddWithValue("@Body",           (object?)body ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Payload",        (object?)actionPayload ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@SourceEntityId", (object?)sourceEntityId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@SourceService",  (object?)sourceService ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ExpiresAt",      (object?)expiresAt ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task ActionItemAsync(Guid itemId, Guid userId, string actionTaken,
        string? actionData, CancellationToken ct = default)
    {
        const string sql = @"UPDATE WorkQueueItem SET
            Status='Actioned', ActionedAt=GETUTCDATE(), ActionedBy=@UserId,
            ActionTaken=@Action, UpdatedAt=GETUTCDATE()
        WHERE Id=@Id AND Status='Pending'";
        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@Id",     itemId);
        cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.Parameters.AddWithValue("@Action", actionTaken);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DismissItemAsync(Guid itemId, Guid userId, CancellationToken ct = default)
    {
        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(
            "UPDATE WorkQueueItem SET Status='Dismissed', ActionedBy=@U, " +
            "UpdatedAt=GETUTCDATE() WHERE Id=@Id AND Status='Pending'", conn);
        cmd.Parameters.AddWithValue("@Id", itemId);
        cmd.Parameters.AddWithValue("@U",  userId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task SnoozeItemAsync(Guid itemId, Guid userId, DateTime snoozedUntil,
        CancellationToken ct = default)
    {
        const string sql = @"MERGE WorkQueueItemSnooze AS t
            USING (SELECT @ItemId,@UserId) AS s(WorkQueueItemId,UserId)
            ON t.WorkQueueItemId=s.WorkQueueItemId AND t.UserId=s.UserId
            WHEN MATCHED THEN UPDATE SET SnoozedUntil=@Until
            WHEN NOT MATCHED THEN INSERT (Id,WorkQueueItemId,UserId,SnoozedUntil)
                VALUES(NEWID(),@ItemId,@UserId,@Until);";
        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@ItemId",  itemId);
        cmd.Parameters.AddWithValue("@UserId",  userId);
        cmd.Parameters.AddWithValue("@Until",   snoozedUntil);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task ExpireStaleItemsAsync(CancellationToken ct = default)
    {
        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(
            "UPDATE WorkQueueItem SET Status='Expired', UpdatedAt=GETUTCDATE() " +
            "WHERE Status='Pending' AND ExpiresAt IS NOT NULL AND ExpiresAt < GETUTCDATE()", conn);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
