using ExpressRecipe.Data.Common;
using Microsoft.Data.SqlClient;

namespace ExpressRecipe.AIService.Data;

public sealed class ApprovalQueueRepository : SqlHelper, IApprovalQueueRepository
{
    public ApprovalQueueRepository(string connectionString) : base(connectionString)
    {
    }

    public async Task InsertPendingAsync(Guid entityId, string entityType, string mode,
        CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO ApprovalQueue (EntityId, EntityType, Mode, Status, CreatedAt)
            SELECT @EntityId, @EntityType, @Mode, 'Pending', GETUTCDATE()
            WHERE NOT EXISTS (
                SELECT 1 FROM ApprovalQueue
                WHERE EntityId = @EntityId AND EntityType = @EntityType AND IsDeleted = 0)
            """;

        await ExecuteNonQueryAsync(sql,
            CreateParameter("@EntityId",   entityId),
            CreateParameter("@EntityType", entityType),
            CreateParameter("@Mode",       mode));
    }

    public async Task<ApprovalConfigDto?> GetApprovalConfigAsync(string entityType,
        CancellationToken ct = default)
    {
        const string sql = """
            SELECT EntityType, Mode, AIConfidenceMin, HumanTimeoutMins
            FROM ApprovalConfig
            WHERE EntityType = @EntityType AND IsDeleted = 0
            """;

        return await ExecuteReaderSingleAsync(
            sql,
            reader => new ApprovalConfigDto
            {
                EntityType      = GetString(reader, "EntityType") ?? string.Empty,
                Mode            = GetString(reader, "Mode") ?? "HumanFirst",
                AIConfidenceMin = GetDecimal(reader, "AIConfidenceMin"),
                HumanTimeoutMins = GetInt32(reader, "HumanTimeoutMins")
            },
            CreateParameter("@EntityType", entityType));
    }

    public async Task<List<PendingApprovalDto>> GetHumanTimedOutItemsAsync(
        CancellationToken ct = default)
    {
        const string sql = """
            SELECT aq.EntityId, aq.EntityType, aq.Content
            FROM ApprovalQueue aq
            INNER JOIN ApprovalConfig ac
                ON ac.EntityType = aq.EntityType AND ac.IsDeleted = 0
            WHERE aq.Status = 'PendingHuman'
              AND aq.IsDeleted = 0
              AND DATEADD(MINUTE, ac.HumanTimeoutMins, aq.CreatedAt) <= GETUTCDATE()
            """;

        return await ExecuteReaderAsync(
            sql,
            reader => new PendingApprovalDto
            {
                EntityId   = GetGuid(reader, "EntityId"),
                EntityType = GetString(reader, "EntityType") ?? string.Empty,
                Content    = GetString(reader, "Content") ?? string.Empty
            });
    }

    public async Task MoveToHumanQueueAsync(Guid entityId, string entityType,
        string reason, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE ApprovalQueue
            SET Status = 'PendingHuman', AIReason = @Reason, UpdatedAt = GETUTCDATE()
            WHERE EntityId = @EntityId AND EntityType = @EntityType AND IsDeleted = 0
            """;

        await ExecuteNonQueryAsync(sql,
            CreateParameter("@EntityId",   entityId),
            CreateParameter("@EntityType", entityType),
            CreateParameter("@Reason",     reason));
    }

    public async Task ApproveAsync(Guid entityId, string entityType, string reason,
        CancellationToken ct = default)
    {
        const string sql = """
            UPDATE ApprovalQueue
            SET Status = 'Approved', AIReason = @Reason, UpdatedAt = GETUTCDATE()
            WHERE EntityId = @EntityId AND EntityType = @EntityType AND IsDeleted = 0
            """;

        await ExecuteNonQueryAsync(sql,
            CreateParameter("@EntityId",   entityId),
            CreateParameter("@EntityType", entityType),
            CreateParameter("@Reason",     reason));
    }

    public async Task RejectAsync(Guid entityId, string entityType, string reason,
        CancellationToken ct = default)
    {
        const string sql = """
            UPDATE ApprovalQueue
            SET Status = 'Rejected', AIReason = @Reason, UpdatedAt = GETUTCDATE()
            WHERE EntityId = @EntityId AND EntityType = @EntityType AND IsDeleted = 0
            """;

        await ExecuteNonQueryAsync(sql,
            CreateParameter("@EntityId",   entityId),
            CreateParameter("@EntityType", entityType),
            CreateParameter("@Reason",     reason));
    }
}
