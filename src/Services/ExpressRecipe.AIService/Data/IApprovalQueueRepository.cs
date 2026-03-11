namespace ExpressRecipe.AIService.Data;

public sealed record ApprovalConfigDto
{
    public string EntityType { get; init; } = string.Empty;
    public string Mode { get; init; } = "HumanFirst";
    public decimal AIConfidenceMin { get; init; } = 0.75m;
    public int HumanTimeoutMins { get; init; } = 120;
}

public sealed record PendingApprovalDto
{
    public Guid EntityId { get; init; }
    public string EntityType { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
}

public interface IApprovalQueueRepository
{
    Task InsertPendingAsync(Guid entityId, string entityType, string mode,
        string content, CancellationToken ct = default);

    Task<ApprovalConfigDto?> GetApprovalConfigAsync(string entityType,
        CancellationToken ct = default);

    Task<List<PendingApprovalDto>> GetHumanTimedOutItemsAsync(
        CancellationToken ct = default);

    Task MoveToHumanQueueAsync(Guid entityId, string entityType, string reason,
        CancellationToken ct = default);

    Task ApproveAsync(Guid entityId, string entityType, string reason,
        CancellationToken ct = default);

    Task RejectAsync(Guid entityId, string entityType, string reason,
        CancellationToken ct = default);
}
