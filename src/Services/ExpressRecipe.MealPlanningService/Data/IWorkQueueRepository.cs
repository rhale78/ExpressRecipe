using ExpressRecipe.Data.Common;
using Microsoft.Data.SqlClient;

namespace ExpressRecipe.MealPlanningService.Data;

public sealed record WorkQueueItemDto
{
    public Guid Id { get; init; }
    public Guid HouseholdId { get; init; }
    public Guid? UserId { get; init; }
    public string ItemType { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Body { get; init; }
    public int Priority { get; init; } = 5;
    public string Status { get; init; } = "Pending";
    public DateTime? DueAt { get; init; }
    public string? RelatedEntityType { get; init; }
    public Guid? RelatedEntityId { get; init; }
    public string? DeduplicationKey { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed record UpsertWorkQueueItemRequest
{
    public Guid HouseholdId { get; init; }
    public Guid? UserId { get; init; }
    public string ItemType { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Body { get; init; }
    public int Priority { get; init; } = 5;
    public DateTime? DueAt { get; init; }
    public string? RelatedEntityType { get; init; }
    public Guid? RelatedEntityId { get; init; }
    /// <summary>
    /// If provided, an existing Pending/Snoozed item with the same key is updated
    /// instead of creating a new one.
    /// </summary>
    public string? DeduplicationKey { get; init; }
}

public interface IWorkQueueRepository
{
    Task<List<WorkQueueItemDto>> GetPendingItemsAsync(Guid householdId, CancellationToken ct = default);
    Task<Guid> UpsertAsync(UpsertWorkQueueItemRequest request, CancellationToken ct = default);
    Task MarkDoneAsync(Guid itemId, CancellationToken ct = default);
    Task SnoozeAsync(Guid itemId, Guid snoozedByUserId, DateTime resumeAt, string? notes = null, CancellationToken ct = default);
}

public sealed class WorkQueueRepository : SqlHelper, IWorkQueueRepository
{
    public WorkQueueRepository(string connectionString) : base(connectionString) { }

    public async Task<List<WorkQueueItemDto>> GetPendingItemsAsync(Guid householdId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT wq.Id, wq.HouseholdId, wq.UserId, wq.ItemType, wq.Title, wq.Body,
                   wq.Priority, wq.Status, wq.DueAt, wq.RelatedEntityType, wq.RelatedEntityId,
                   wq.DeduplicationKey, wq.CreatedAt, wq.UpdatedAt
            FROM WorkQueueItem wq
            WHERE wq.HouseholdId = @HouseholdId
              AND wq.IsDeleted  = 0
              AND wq.Status IN ('Pending', 'Snoozed')
              AND (wq.DueAt IS NULL OR wq.DueAt <= DATEADD(day, 1, GETUTCDATE()))
              AND NOT EXISTS (
                  SELECT 1 FROM WorkQueueItemSnooze s
                  WHERE s.WorkQueueItemId = wq.Id AND s.ResumeAt > GETUTCDATE()
              )
            ORDER BY wq.Priority, wq.DueAt";

        return await ExecuteReaderAsync(sql, MapDto,
            new SqlParameter("@HouseholdId", householdId));
    }

    public async Task<Guid> UpsertAsync(UpsertWorkQueueItemRequest request, CancellationToken ct = default)
    {
        // If a deduplication key is provided, try to find an existing pending/snoozed item
        if (!string.IsNullOrWhiteSpace(request.DeduplicationKey))
        {
            const string findSql = @"
                SELECT TOP 1 Id FROM WorkQueueItem
                WHERE HouseholdId = @HouseholdId
                  AND DeduplicationKey = @Key
                  AND Status IN ('Pending','Snoozed')
                  AND IsDeleted = 0";

            var existing = await ExecuteReaderAsync(findSql,
                r => r.GetGuid(0),
                new SqlParameter("@HouseholdId", request.HouseholdId),
                new SqlParameter("@Key", request.DeduplicationKey));

            if (existing.Count > 0)
            {
                var id = existing[0];
                const string updateSql = @"
                    UPDATE WorkQueueItem
                    SET Title     = @Title,
                        Body      = @Body,
                        Priority  = @Priority,
                        DueAt     = @DueAt,
                        UpdatedAt = GETUTCDATE()
                    WHERE Id = @Id";

                await ExecuteNonQueryAsync(updateSql,
                    new SqlParameter("@Id", id),
                    new SqlParameter("@Title", request.Title),
                    new SqlParameter("@Body", (object?)request.Body ?? DBNull.Value),
                    new SqlParameter("@Priority", request.Priority),
                    new SqlParameter("@DueAt", (object?)request.DueAt ?? DBNull.Value));

                return id;
            }
        }

        // Insert new item
        var newId = Guid.NewGuid();
        const string insertSql = @"
            INSERT INTO WorkQueueItem
                (Id, HouseholdId, UserId, ItemType, Title, Body, Priority, Status,
                 DueAt, RelatedEntityType, RelatedEntityId, DeduplicationKey, CreatedAt)
            VALUES
                (@Id, @HouseholdId, @UserId, @ItemType, @Title, @Body, @Priority, 'Pending',
                 @DueAt, @RelatedEntityType, @RelatedEntityId, @DeduplicationKey, GETUTCDATE())";

        await ExecuteNonQueryAsync(insertSql,
            new SqlParameter("@Id",                newId),
            new SqlParameter("@HouseholdId",       request.HouseholdId),
            new SqlParameter("@UserId",            (object?)request.UserId        ?? DBNull.Value),
            new SqlParameter("@ItemType",          request.ItemType),
            new SqlParameter("@Title",             request.Title),
            new SqlParameter("@Body",              (object?)request.Body          ?? DBNull.Value),
            new SqlParameter("@Priority",          request.Priority),
            new SqlParameter("@DueAt",             (object?)request.DueAt         ?? DBNull.Value),
            new SqlParameter("@RelatedEntityType", (object?)request.RelatedEntityType ?? DBNull.Value),
            new SqlParameter("@RelatedEntityId",   (object?)request.RelatedEntityId   ?? DBNull.Value),
            new SqlParameter("@DeduplicationKey",  (object?)request.DeduplicationKey  ?? DBNull.Value));

        return newId;
    }

    public async Task MarkDoneAsync(Guid itemId, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE WorkQueueItem
            SET Status = 'Done', DoneAt = GETUTCDATE(), UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND IsDeleted = 0";

        await ExecuteNonQueryAsync(sql, new SqlParameter("@Id", itemId));
    }

    public async Task SnoozeAsync(Guid itemId, Guid snoozedByUserId, DateTime resumeAt, string? notes = null, CancellationToken ct = default)
    {
        const string updateSql = @"
            UPDATE WorkQueueItem
            SET Status = 'Snoozed', UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND IsDeleted = 0";

        const string snoozeSql = @"
            INSERT INTO WorkQueueItemSnooze (Id, WorkQueueItemId, SnoozedByUserId, ResumeAt, Notes)
            VALUES (NEWID(), @ItemId, @UserId, @ResumeAt, @Notes)";

        await ExecuteNonQueryAsync(updateSql, new SqlParameter("@Id", itemId));
        await ExecuteNonQueryAsync(snoozeSql,
            new SqlParameter("@ItemId",   itemId),
            new SqlParameter("@UserId",   snoozedByUserId),
            new SqlParameter("@ResumeAt", resumeAt),
            new SqlParameter("@Notes",    (object?)notes ?? DBNull.Value));
    }

    private static WorkQueueItemDto MapDto(SqlDataReader r) => new()
    {
        Id                 = r.GetGuid(0),
        HouseholdId        = r.GetGuid(1),
        UserId             = r.IsDBNull(2)  ? null : r.GetGuid(2),
        ItemType           = r.GetString(3),
        Title              = r.GetString(4),
        Body               = r.IsDBNull(5)  ? null : r.GetString(5),
        Priority           = r.GetInt32(6),
        Status             = r.GetString(7),
        DueAt              = r.IsDBNull(8)  ? null : r.GetDateTime(8),
        RelatedEntityType  = r.IsDBNull(9)  ? null : r.GetString(9),
        RelatedEntityId    = r.IsDBNull(10) ? null : r.GetGuid(10),
        DeduplicationKey   = r.IsDBNull(11) ? null : r.GetString(11),
        CreatedAt          = r.GetDateTime(12),
        UpdatedAt          = r.IsDBNull(13) ? null : r.GetDateTime(13)
    };
}
