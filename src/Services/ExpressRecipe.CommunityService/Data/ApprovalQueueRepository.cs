using ExpressRecipe.Data.Common;
using Microsoft.Data.SqlClient;

namespace ExpressRecipe.CommunityService.Data;

// ─── DTOs ──────────────────────────────────────────────────────────────────

public sealed class ApprovalQueueItemDto
{
    public Guid Id { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public Guid SubmittedBy { get; set; }
    public DateTime SubmittedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNotes { get; set; }
    public string? RejectionReason { get; set; }
    public decimal? AiScore { get; set; }
    public string? AiFlags { get; set; }
    public int HumanTimeoutMins { get; set; }
    public DateTime? EscalatedAt { get; set; }
    public bool IsEscalated => EscalatedAt.HasValue;
}

// ─── Interface ─────────────────────────────────────────────────────────────

public interface IApprovalQueueRepository
{
    Task<List<ApprovalQueueItemDto>> GetItemsAsync(string? entityType, string status, int limit, CancellationToken ct = default);
    Task<ApprovalQueueItemDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Guid> EnqueueAsync(string entityType, Guid entityId, Guid submittedBy, int humanTimeoutMins = 60, CancellationToken ct = default);
    Task ApproveAsync(Guid id, Guid reviewerId, string? notes, CancellationToken ct = default);
    Task RejectAsync(Guid id, string reason, CancellationToken ct = default);
    Task SetAiScoreAsync(Guid id, decimal score, string? flags, CancellationToken ct = default);
    Task SetStatusAsync(Guid id, string status, CancellationToken ct = default);
}

// ─── Implementation ────────────────────────────────────────────────────────

public sealed class ApprovalQueueRepository : SqlHelper, IApprovalQueueRepository
{
    public ApprovalQueueRepository(string connectionString) : base(connectionString) { }

    public async Task<List<ApprovalQueueItemDto>> GetItemsAsync(
        string? entityType, string status, int limit, CancellationToken ct = default)
    {
        var sql = @"
            SELECT TOP (@Limit)
                Id, EntityType, EntityId, SubmittedBy, SubmittedAt, Status,
                ReviewedBy, ReviewedAt, ReviewNotes, RejectionReason,
                AiScore, AiFlags, HumanTimeoutMins, EscalatedAt
            FROM ApprovalQueue
            WHERE Status = @Status
              AND (@EntityType IS NULL OR EntityType = @EntityType)
            ORDER BY
                CASE WHEN EscalatedAt IS NOT NULL THEN 0 ELSE 1 END,
                SubmittedAt ASC";

        return await ExecuteReaderAsync(sql, MapRow,
            CreateParameter("@Limit", limit),
            CreateParameter("@Status", status),
            CreateParameter("@EntityType", (object?)entityType ?? DBNull.Value));
    }

    public async Task<ApprovalQueueItemDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT Id, EntityType, EntityId, SubmittedBy, SubmittedAt, Status,
                   ReviewedBy, ReviewedAt, ReviewNotes, RejectionReason,
                   AiScore, AiFlags, HumanTimeoutMins, EscalatedAt
            FROM ApprovalQueue
            WHERE Id = @Id";

        var rows = await ExecuteReaderAsync(sql, MapRow, CreateParameter("@Id", id));
        return rows.FirstOrDefault();
    }

    public async Task<Guid> EnqueueAsync(
        string entityType, Guid entityId, Guid submittedBy, int humanTimeoutMins = 60, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO ApprovalQueue (Id, EntityType, EntityId, SubmittedBy, HumanTimeoutMins)
            VALUES (@Id, @EntityType, @EntityId, @SubmittedBy, @HumanTimeoutMins)";

        var id = Guid.NewGuid();
        await ExecuteNonQueryAsync(sql,
            CreateParameter("@Id", id),
            CreateParameter("@EntityType", entityType),
            CreateParameter("@EntityId", entityId),
            CreateParameter("@SubmittedBy", submittedBy),
            CreateParameter("@HumanTimeoutMins", humanTimeoutMins));
        return id;
    }

    public async Task ApproveAsync(Guid id, Guid reviewerId, string? notes, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE ApprovalQueue
            SET Status     = 'Approved',
                ReviewedBy = @ReviewedBy,
                ReviewedAt = GETUTCDATE(),
                ReviewNotes = @Notes
            WHERE Id = @Id";

        await ExecuteNonQueryAsync(sql,
            CreateParameter("@Id", id),
            CreateParameter("@ReviewedBy", reviewerId),
            CreateParameter("@Notes", (object?)notes ?? DBNull.Value));
    }

    public async Task RejectAsync(Guid id, string reason, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE ApprovalQueue
            SET Status          = 'Rejected',
                ReviewedAt      = GETUTCDATE(),
                RejectionReason = @Reason
            WHERE Id = @Id";

        await ExecuteNonQueryAsync(sql,
            CreateParameter("@Id", id),
            CreateParameter("@Reason", reason));
    }

    public async Task SetAiScoreAsync(Guid id, decimal score, string? flags, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE ApprovalQueue SET AiScore = @Score, AiFlags = @Flags WHERE Id = @Id";

        await ExecuteNonQueryAsync(sql,
            CreateParameter("@Id", id),
            CreateParameter("@Score", score),
            CreateParameter("@Flags", (object?)flags ?? DBNull.Value));
    }

    public async Task SetStatusAsync(Guid id, string status, CancellationToken ct = default)
    {
        const string sql = @"UPDATE ApprovalQueue SET Status = @Status WHERE Id = @Id";
        await ExecuteNonQueryAsync(sql,
            CreateParameter("@Id", id),
            CreateParameter("@Status", status));
    }

    // ── mapper ──────────────────────────────────────────────────────────────

    private static ApprovalQueueItemDto MapRow(System.Data.IDataReader reader) => new()
    {
        Id = GetGuid(reader, "Id"),
        EntityType = GetString(reader, "EntityType") ?? string.Empty,
        EntityId = GetGuid(reader, "EntityId"),
        SubmittedBy = GetGuid(reader, "SubmittedBy"),
        SubmittedAt = GetDateTime(reader, "SubmittedAt"),
        Status = GetString(reader, "Status") ?? string.Empty,
        ReviewedBy = GetGuidNullable(reader, "ReviewedBy"),
        ReviewedAt = GetNullableDateTime(reader, "ReviewedAt"),
        ReviewNotes = GetString(reader, "ReviewNotes"),
        RejectionReason = GetString(reader, "RejectionReason"),
        AiScore = GetNullableDecimal(reader, "AiScore"),
        AiFlags = GetString(reader, "AiFlags"),
        HumanTimeoutMins = GetInt32(reader, "HumanTimeoutMins"),
        EscalatedAt = GetNullableDateTime(reader, "EscalatedAt")
    };
}
