using Microsoft.Data.SqlClient;
using System.Text;

namespace ExpressRecipe.CommunityService.Data;

public interface ICommunityRecipeRepository
{
    Task<Guid> SubmitRecipeAsync(Guid recipeId, Guid submittedBy, CancellationToken ct = default);
    Task<GalleryPage> GetGalleryPageAsync(
        string? cuisine, string? diet, decimal minRating, string? search,
        Guid? afterId, int pageSize, CancellationToken ct = default);
    Task<CommunityRecipeDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<CommunityRecipeDto?> GetByRecipeIdAsync(Guid recipeId, CancellationToken ct = default);
    Task UpdateStatusAsync(Guid id, string status, string? approvedBy, string? rejectionReason, decimal? aiScore, CancellationToken ct = default);
    Task IncrementViewCountAsync(Guid id, CancellationToken ct = default);

    // Approval queue
    Task<Guid> EnqueueForApprovalAsync(Guid entityId, string entityType, string? contentJson, CancellationToken ct = default);
    Task<List<ApprovalQueueItemDto>> GetPendingApprovalItemsAsync(int limit = 50, CancellationToken ct = default);
    Task<ApprovalQueueItemDto?> GetApprovalQueueItemByIdAsync(Guid id, CancellationToken ct = default);
    Task MarkApprovalItemProcessedAsync(Guid queueId, decimal? aiScore, CancellationToken ct = default);
}

public sealed record CommunityRecipeDto
{
    public Guid Id { get; init; }
    public Guid RecipeId { get; init; }
    public Guid SubmittedBy { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime? ApprovedAt { get; init; }
    public string? ApprovedBy { get; init; }
    public string? RejectionReason { get; init; }
    public decimal? AIScore { get; init; }
    public int ViewCount { get; init; }
    public DateTime? FeaturedAt { get; init; }
    public DateTime SubmittedAt { get; init; }
}

public sealed record GalleryPage
{
    public List<CommunityRecipeDto> Items { get; init; } = new();
    public bool HasMore { get; init; }
    public Guid? NextCursorId { get; init; }
}

public sealed record ApprovalQueueItemDto
{
    public Guid Id { get; init; }
    public string EntityType { get; init; } = string.Empty;
    public Guid EntityId { get; init; }
    public string? ContentJson { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}

public class CommunityRecipeRepository : ICommunityRecipeRepository
{
    private readonly string _connectionString;

    public CommunityRecipeRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<Guid> SubmitRecipeAsync(Guid recipeId, Guid submittedBy, CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        const string sql = @"
            INSERT INTO CommunityRecipe (Id, RecipeId, SubmittedBy, Status, SubmittedAt)
            VALUES (@Id, @RecipeId, @SubmittedBy, 'Pending', GETUTCDATE())";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", id);
        command.Parameters.AddWithValue("@RecipeId", recipeId);
        command.Parameters.AddWithValue("@SubmittedBy", submittedBy);
        await command.ExecuteNonQueryAsync(ct);
        return id;
    }

    public async Task<GalleryPage> GetGalleryPageAsync(
        string? cuisine, string? diet, decimal minRating, string? search,
        Guid? afterId, int pageSize, CancellationToken ct = default)
    {
        // Cursor pagination: filter and order by SubmittedAt DESC, using (SubmittedAt, Id) as a stable cursor.
        // search/cuisine/diet filtering is a placeholder — the gallery can be extended later to join recipe data.
        var sb = new StringBuilder(@"
            SELECT TOP (@PageSize)
                   cr.Id, cr.RecipeId, cr.SubmittedBy, cr.Status, cr.ApprovedAt,
                   cr.ApprovedBy, cr.RejectionReason, cr.AIScore, cr.ViewCount, cr.FeaturedAt, cr.SubmittedAt
            FROM CommunityRecipe cr
            WHERE cr.Status = 'Approved'");

        if (afterId.HasValue)
        {
            // Stable cursor: same-SubmittedAt ties resolved by Id (ascending)
            sb.Append(@"
              AND (
                    cr.SubmittedAt < (SELECT SubmittedAt FROM CommunityRecipe WHERE Id = @AfterId)
                 OR (
                        cr.SubmittedAt = (SELECT SubmittedAt FROM CommunityRecipe WHERE Id = @AfterId)
                    AND cr.Id > @AfterId
                    )
              )");
        }

        sb.Append(" ORDER BY cr.SubmittedAt DESC, cr.Id ASC");

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(sb.ToString(), connection);
        command.Parameters.AddWithValue("@PageSize", pageSize + 1);

        if (afterId.HasValue)
        {
            command.Parameters.AddWithValue("@AfterId", afterId.Value);
        }

        var items = new List<CommunityRecipeDto>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            items.Add(MapCommunityRecipe(reader));
        }

        var hasMore = items.Count > pageSize;
        if (hasMore) items.RemoveAt(items.Count - 1);

        return new GalleryPage
        {
            Items = items,
            HasMore = hasMore,
            NextCursorId = hasMore && items.Count > 0 ? items[^1].Id : null
        };
    }

    public async Task<CommunityRecipeDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT Id, RecipeId, SubmittedBy, Status, ApprovedAt, ApprovedBy,
                   RejectionReason, AIScore, ViewCount, FeaturedAt, SubmittedAt
            FROM CommunityRecipe
            WHERE Id = @Id";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", id);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct)) return MapCommunityRecipe(reader);
        return null;
    }

    public async Task<CommunityRecipeDto?> GetByRecipeIdAsync(Guid recipeId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT Id, RecipeId, SubmittedBy, Status, ApprovedAt, ApprovedBy,
                   RejectionReason, AIScore, ViewCount, FeaturedAt, SubmittedAt
            FROM CommunityRecipe
            WHERE RecipeId = @RecipeId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@RecipeId", recipeId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct)) return MapCommunityRecipe(reader);
        return null;
    }

    public async Task UpdateStatusAsync(Guid id, string status, string? approvedBy, string? rejectionReason, decimal? aiScore, CancellationToken ct = default)
    {
        var sql = @"
            UPDATE CommunityRecipe
            SET Status = @Status,
                ApprovedBy = @ApprovedBy,
                RejectionReason = @RejectionReason,
                AIScore = @AIScore,
                ApprovedAt = CASE WHEN @Status = 'Approved' THEN GETUTCDATE() ELSE ApprovedAt END
            WHERE Id = @Id";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", id);
        command.Parameters.AddWithValue("@Status", status);
        command.Parameters.AddWithValue("@ApprovedBy", (object?)approvedBy ?? DBNull.Value);
        command.Parameters.AddWithValue("@RejectionReason", (object?)rejectionReason ?? DBNull.Value);
        command.Parameters.AddWithValue("@AIScore", (object?)aiScore ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task IncrementViewCountAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE CommunityRecipe SET ViewCount = ViewCount + 1 WHERE Id = @Id";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", id);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<Guid> EnqueueForApprovalAsync(Guid entityId, string entityType, string? contentJson, CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        const string sql = @"
            INSERT INTO ApprovalQueue (Id, EntityType, EntityId, ContentJson, Status, CreatedAt)
            VALUES (@Id, @EntityType, @EntityId, @ContentJson, 'Pending', GETUTCDATE())";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", id);
        command.Parameters.AddWithValue("@EntityType", entityType);
        command.Parameters.AddWithValue("@EntityId", entityId);
        command.Parameters.AddWithValue("@ContentJson", (object?)contentJson ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(ct);
        return id;
    }

    public async Task<List<ApprovalQueueItemDto>> GetPendingApprovalItemsAsync(int limit = 50, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT TOP (@Limit) Id, EntityType, EntityId, ContentJson, Status, CreatedAt
            FROM ApprovalQueue
            WHERE Status = 'Pending'
            ORDER BY CreatedAt ASC";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Limit", limit);

        var items = new List<ApprovalQueueItemDto>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            items.Add(new ApprovalQueueItemDto
            {
                Id = reader.GetGuid(0),
                EntityType = reader.GetString(1),
                EntityId = reader.GetGuid(2),
                ContentJson = reader.IsDBNull(3) ? null : reader.GetString(3),
                Status = reader.GetString(4),
                CreatedAt = reader.GetDateTime(5)
            });
        }
        return items;
    }

    public async Task<ApprovalQueueItemDto?> GetApprovalQueueItemByIdAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT Id, EntityType, EntityId, ContentJson, Status, CreatedAt
            FROM ApprovalQueue
            WHERE Id = @Id";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", id);

        await using var reader = await command.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            return new ApprovalQueueItemDto
            {
                Id = reader.GetGuid(0),
                EntityType = reader.GetString(1),
                EntityId = reader.GetGuid(2),
                ContentJson = reader.IsDBNull(3) ? null : reader.GetString(3),
                Status = reader.GetString(4),
                CreatedAt = reader.GetDateTime(5)
            };
        }
        return null;
    }

    public async Task MarkApprovalItemProcessedAsync(Guid queueId, decimal? aiScore, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE ApprovalQueue
            SET Status = 'Processed', AIScore = @AIScore, ProcessedAt = GETUTCDATE()
            WHERE Id = @Id";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", queueId);
        command.Parameters.AddWithValue("@AIScore", (object?)aiScore ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static CommunityRecipeDto MapCommunityRecipe(SqlDataReader reader) =>
        new()
        {
            Id = reader.GetGuid(0),
            RecipeId = reader.GetGuid(1),
            SubmittedBy = reader.GetGuid(2),
            Status = reader.GetString(3),
            ApprovedAt = reader.IsDBNull(4) ? null : reader.GetDateTime(4),
            ApprovedBy = reader.IsDBNull(5) ? null : reader.GetString(5),
            RejectionReason = reader.IsDBNull(6) ? null : reader.GetString(6),
            AIScore = reader.IsDBNull(7) ? null : reader.GetDecimal(7),
            ViewCount = reader.GetInt32(8),
            FeaturedAt = reader.IsDBNull(9) ? null : reader.GetDateTime(9),
            SubmittedAt = reader.GetDateTime(10)
        };
}
