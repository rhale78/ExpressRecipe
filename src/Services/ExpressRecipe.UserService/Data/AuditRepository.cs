using ExpressRecipe.Data.Common;
using Microsoft.Data.SqlClient;

namespace ExpressRecipe.UserService.Data;

public sealed class AuditLogEntry
{
    public Guid Id { get; set; }
    public Guid ActorId { get; set; }
    public string Action { get; set; } = string.Empty;
    public Guid? TargetId { get; set; }
    public string? TargetType { get; set; }
    public string? Notes { get; set; }
    public DateTime OccurredAt { get; set; }
}

public interface IAuditRepository
{
    Task LogAsync(Guid actorId, string action, Guid? targetId, string? notes = null, CancellationToken ct = default);
    Task<List<AuditLogEntry>> GetByTargetAsync(Guid targetId, int limit = 50, CancellationToken ct = default);
}

public sealed class AuditRepository : SqlHelper, IAuditRepository
{
    public AuditRepository(string connectionString) : base(connectionString) { }

    public async Task LogAsync(Guid actorId, string action, Guid? targetId, string? notes = null, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO AdminAuditLog (Id, ActorId, Action, TargetId, Notes, OccurredAt)
            VALUES (@Id, @ActorId, @Action, @TargetId, @Notes, GETUTCDATE())";

        await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", Guid.NewGuid()),
            CreateParameter("@ActorId", actorId),
            CreateParameter("@Action", action),
            CreateParameter("@TargetId", (object?)targetId ?? DBNull.Value),
            CreateParameter("@Notes", (object?)notes ?? DBNull.Value));
    }

    public async Task<List<AuditLogEntry>> GetByTargetAsync(Guid targetId, int limit = 50, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT TOP (@Limit) Id, ActorId, Action, TargetId, TargetType, Notes, OccurredAt
            FROM AdminAuditLog
            WHERE TargetId = @TargetId
            ORDER BY OccurredAt DESC";

        return await ExecuteReaderAsync(
            sql,
            reader => new AuditLogEntry
            {
                Id = GetGuid(reader, "Id"),
                ActorId = GetGuid(reader, "ActorId"),
                Action = GetString(reader, "Action") ?? string.Empty,
                TargetId = GetGuidNullable(reader, "TargetId"),
                TargetType = GetString(reader, "TargetType"),
                Notes = GetString(reader, "Notes"),
                OccurredAt = reader.GetDateTime(reader.GetOrdinal("OccurredAt"))
            },
            CreateParameter("@TargetId", targetId),
            CreateParameter("@Limit", limit));
    }
}
