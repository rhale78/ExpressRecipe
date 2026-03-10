using Microsoft.Data.SqlClient;

namespace ExpressRecipe.InventoryService.Data;

// ─────────────────────────────────────────────────────────────────────────────
//  DTO
// ─────────────────────────────────────────────────────────────────────────────

public sealed record WorkQueueItemDto
{
    public Guid   Id                { get; init; }
    public Guid   UserId            { get; init; }
    public Guid   HouseholdId       { get; init; }
    public string ItemType          { get; init; } = string.Empty;
    public string Title             { get; init; } = string.Empty;
    public string? Body             { get; init; }
    public int    Priority          { get; init; }
    public string? ActionPayload    { get; init; }
    public string Status            { get; init; } = "Pending";
    public string? ActionTaken      { get; init; }
    public DateTime? ActionedAt     { get; init; }
    public DateTime? SnoozeUntil    { get; init; }
    public string? RelatedEntityType { get; init; }
    public Guid?  RelatedEntityId   { get; init; }
    public DateTime CreatedAt       { get; init; }
}

// ─────────────────────────────────────────────────────────────────────────────
//  Interface
// ─────────────────────────────────────────────────────────────────────────────

public interface IWorkQueueRepository
{
    /// <summary>Get all pending (non-snoozed) work queue items for a user, ordered by priority.</summary>
    Task<List<WorkQueueItemDto>> GetPendingItemsAsync(Guid userId, Guid householdId, CancellationToken ct = default);

    /// <summary>Get total pending count (for badge).</summary>
    Task<int> GetPendingCountAsync(Guid userId, Guid householdId, CancellationToken ct = default);

    /// <summary>Create a new work queue item. Returns the new item ID.</summary>
    Task<Guid> CreateItemAsync(Guid userId, Guid householdId, string itemType, string title,
        string? body, int priority, string? actionPayload,
        string? relatedEntityType, Guid? relatedEntityId,
        CancellationToken ct = default);

    /// <summary>Mark an item as actioned.</summary>
    Task<bool> ActionItemAsync(Guid itemId, Guid userId, string actionTaken,
        string? actionData = null, CancellationToken ct = default);

    /// <summary>Dismiss an item for all household members (soft delete).</summary>
    Task<bool> DismissItemAsync(Guid itemId, Guid userId, CancellationToken ct = default);

    /// <summary>Snooze an item for the user for the given number of hours.</summary>
    Task<bool> SnoozeItemAsync(Guid itemId, Guid userId, int hours = 24, CancellationToken ct = default);

    /// <summary>Re-surface snoozed items whose SnoozeUntil has passed.</summary>
    Task WakeSnoozedItemsAsync(CancellationToken ct = default);

    /// <summary>
    /// Upsert a work queue item by type + relatedEntityId so duplicate items are not created
    /// when a background worker regenerates the queue.
    /// </summary>
    Task UpsertItemAsync(Guid userId, Guid householdId, string itemType, string title,
        string? body, int priority, string? actionPayload,
        string? relatedEntityType, Guid? relatedEntityId,
        CancellationToken ct = default);

    /// <summary>Remove all Pending items for a related entity (e.g. when item is consumed).</summary>
    Task DeleteItemsByRelatedEntityAsync(Guid relatedEntityId, CancellationToken ct = default);
}

// ─────────────────────────────────────────────────────────────────────────────
//  Repository implementation (ADO.NET)
// ─────────────────────────────────────────────────────────────────────────────

public sealed class WorkQueueRepository : IWorkQueueRepository
{
    private readonly string _connectionString;

    public WorkQueueRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    private const string SelectColumns = @"
        Id, UserId, HouseholdId, ItemType, Title, Body, Priority,
        ActionPayload, Status, ActionTaken, ActionedAt, SnoozeUntil,
        RelatedEntityType, RelatedEntityId, CreatedAt";

    // ── Read ─────────────────────────────────────────────────────────────────

    public async Task<List<WorkQueueItemDto>> GetPendingItemsAsync(
        Guid userId, Guid householdId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT Id, UserId, HouseholdId, ItemType, Title, Body, Priority,
                   ActionPayload, Status, ActionTaken, ActionedAt, SnoozeUntil,
                   RelatedEntityType, RelatedEntityId, CreatedAt
            FROM WorkQueueItem
            WHERE UserId    = @UserId
              AND IsDeleted = 0
              AND Status    = 'Pending'
              AND (SnoozeUntil IS NULL OR SnoozeUntil <= GETUTCDATE())
            ORDER BY Priority ASC, CreatedAt ASC";

        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@UserId", userId);
        return await ReadItemsAsync(cmd, ct);
    }

    public async Task<int> GetPendingCountAsync(
        Guid userId, Guid householdId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT COUNT(*)
            FROM WorkQueueItem
            WHERE UserId    = @UserId
              AND IsDeleted = 0
              AND Status    = 'Pending'
              AND (SnoozeUntil IS NULL OR SnoozeUntil <= GETUTCDATE())";

        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@UserId", userId);
        object? result = await cmd.ExecuteScalarAsync(ct);
        return result is int count ? count : Convert.ToInt32(result ?? 0);
    }

    // ── Write ────────────────────────────────────────────────────────────────

    public async Task<Guid> CreateItemAsync(
        Guid userId, Guid householdId, string itemType, string title,
        string? body, int priority, string? actionPayload,
        string? relatedEntityType, Guid? relatedEntityId,
        CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO WorkQueueItem
                (Id, UserId, HouseholdId, ItemType, Title, Body, Priority,
                 ActionPayload, Status, RelatedEntityType, RelatedEntityId, CreatedAt)
            OUTPUT INSERTED.Id
            VALUES
                (NEWID(), @UserId, @HouseholdId, @ItemType, @Title, @Body, @Priority,
                 @ActionPayload, 'Pending', @RelatedEntityType, @RelatedEntityId, GETUTCDATE())";

        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@UserId",            userId);
        cmd.Parameters.AddWithValue("@HouseholdId",       householdId);
        cmd.Parameters.AddWithValue("@ItemType",          itemType);
        cmd.Parameters.AddWithValue("@Title",             title);
        cmd.Parameters.AddWithValue("@Body",              (object?)body ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Priority",          priority);
        cmd.Parameters.AddWithValue("@ActionPayload",     (object?)actionPayload ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@RelatedEntityType", (object?)relatedEntityType ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@RelatedEntityId",   (object?)relatedEntityId ?? DBNull.Value);

        object? scalar = await cmd.ExecuteScalarAsync(ct);
        return scalar is Guid id ? id : Guid.Empty;
    }

    public async Task UpsertItemAsync(
        Guid userId, Guid householdId, string itemType, string title,
        string? body, int priority, string? actionPayload,
        string? relatedEntityType, Guid? relatedEntityId,
        CancellationToken ct = default)
    {
        // If a Pending item already exists for this user+type+relatedEntity, update it.
        // Otherwise insert a new one.
        const string sql = @"
            IF EXISTS (
                SELECT 1 FROM WorkQueueItem
                WHERE UserId = @UserId AND ItemType = @ItemType
                  AND RelatedEntityId = @RelatedEntityId
                  AND Status = 'Pending' AND IsDeleted = 0)
            BEGIN
                UPDATE WorkQueueItem
                SET Title = @Title, Body = @Body, Priority = @Priority,
                    ActionPayload = @ActionPayload, UpdatedAt = GETUTCDATE()
                WHERE UserId = @UserId AND ItemType = @ItemType
                  AND RelatedEntityId = @RelatedEntityId
                  AND Status = 'Pending' AND IsDeleted = 0
            END
            ELSE
            BEGIN
                INSERT INTO WorkQueueItem
                    (Id, UserId, HouseholdId, ItemType, Title, Body, Priority,
                     ActionPayload, Status, RelatedEntityType, RelatedEntityId, CreatedAt)
                VALUES
                    (NEWID(), @UserId, @HouseholdId, @ItemType, @Title, @Body, @Priority,
                     @ActionPayload, 'Pending', @RelatedEntityType, @RelatedEntityId, GETUTCDATE())
            END";

        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@UserId",            userId);
        cmd.Parameters.AddWithValue("@HouseholdId",       householdId);
        cmd.Parameters.AddWithValue("@ItemType",          itemType);
        cmd.Parameters.AddWithValue("@Title",             title);
        cmd.Parameters.AddWithValue("@Body",              (object?)body ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Priority",          priority);
        cmd.Parameters.AddWithValue("@ActionPayload",     (object?)actionPayload ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@RelatedEntityType", (object?)relatedEntityType ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@RelatedEntityId",   (object?)relatedEntityId ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<bool> ActionItemAsync(
        Guid itemId, Guid userId, string actionTaken,
        string? actionData = null, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE WorkQueueItem
            SET Status     = 'Actioned',
                ActionTaken = @ActionTaken,
                ActionedAt  = GETUTCDATE(),
                UpdatedAt   = GETUTCDATE()
            WHERE Id = @Id AND UserId = @UserId AND IsDeleted = 0";

        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@Id",          itemId);
        cmd.Parameters.AddWithValue("@UserId",      userId);
        cmd.Parameters.AddWithValue("@ActionTaken", actionTaken);
        int rows = await cmd.ExecuteNonQueryAsync(ct);
        return rows > 0;
    }

    public async Task<bool> DismissItemAsync(
        Guid itemId, Guid userId, CancellationToken ct = default)
    {
        // Dismiss soft-deletes the item so all household members stop seeing it.
        const string sql = @"
            UPDATE WorkQueueItem
            SET Status    = 'Dismissed',
                IsDeleted = 1,
                DeletedAt = GETUTCDATE(),
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND UserId = @UserId AND IsDeleted = 0";

        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@Id",     itemId);
        cmd.Parameters.AddWithValue("@UserId", userId);
        int rows = await cmd.ExecuteNonQueryAsync(ct);
        return rows > 0;
    }

    public async Task<bool> SnoozeItemAsync(
        Guid itemId, Guid userId, int hours = 24, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE WorkQueueItem
            SET Status      = 'Snoozed',
                SnoozeUntil = DATEADD(HOUR, @Hours, GETUTCDATE()),
                UpdatedAt   = GETUTCDATE()
            WHERE Id = @Id AND UserId = @UserId AND IsDeleted = 0";

        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@Id",     itemId);
        cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.Parameters.AddWithValue("@Hours",  hours);
        int rows = await cmd.ExecuteNonQueryAsync(ct);
        return rows > 0;
    }

    public async Task WakeSnoozedItemsAsync(CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE WorkQueueItem
            SET Status    = 'Pending',
                UpdatedAt = GETUTCDATE()
            WHERE Status      = 'Snoozed'
              AND SnoozeUntil <= GETUTCDATE()
              AND IsDeleted   = 0";

        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteItemsByRelatedEntityAsync(Guid relatedEntityId, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE WorkQueueItem
            SET IsDeleted = 1, DeletedAt = GETUTCDATE(), UpdatedAt = GETUTCDATE()
            WHERE RelatedEntityId = @RelatedEntityId
              AND Status IN ('Pending','Snoozed')
              AND IsDeleted = 0";

        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@RelatedEntityId", relatedEntityId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static async Task<List<WorkQueueItemDto>> ReadItemsAsync(
        SqlCommand cmd, CancellationToken ct)
    {
        var items = new List<WorkQueueItemDto>();
        await using SqlDataReader reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            items.Add(new WorkQueueItemDto
            {
                Id                = reader.GetGuid(reader.GetOrdinal("Id")),
                UserId            = reader.GetGuid(reader.GetOrdinal("UserId")),
                HouseholdId       = reader.GetGuid(reader.GetOrdinal("HouseholdId")),
                ItemType          = reader.GetString(reader.GetOrdinal("ItemType")),
                Title             = reader.GetString(reader.GetOrdinal("Title")),
                Body              = reader.IsDBNull(reader.GetOrdinal("Body")) ? null : reader.GetString(reader.GetOrdinal("Body")),
                Priority          = reader.GetInt32(reader.GetOrdinal("Priority")),
                ActionPayload     = reader.IsDBNull(reader.GetOrdinal("ActionPayload")) ? null : reader.GetString(reader.GetOrdinal("ActionPayload")),
                Status            = reader.GetString(reader.GetOrdinal("Status")),
                ActionTaken       = reader.IsDBNull(reader.GetOrdinal("ActionTaken")) ? null : reader.GetString(reader.GetOrdinal("ActionTaken")),
                ActionedAt        = reader.IsDBNull(reader.GetOrdinal("ActionedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("ActionedAt")),
                SnoozeUntil       = reader.IsDBNull(reader.GetOrdinal("SnoozeUntil")) ? null : reader.GetDateTime(reader.GetOrdinal("SnoozeUntil")),
                RelatedEntityType = reader.IsDBNull(reader.GetOrdinal("RelatedEntityType")) ? null : reader.GetString(reader.GetOrdinal("RelatedEntityType")),
                RelatedEntityId   = reader.IsDBNull(reader.GetOrdinal("RelatedEntityId")) ? null : reader.GetGuid(reader.GetOrdinal("RelatedEntityId")),
                CreatedAt         = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
            });
        }
        return items;
    }
}
