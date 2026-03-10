using ExpressRecipe.AIService.Data;
using ExpressRecipe.AIService.Providers;

namespace ExpressRecipe.AIService.Services;

public interface IApprovalQueueService
{
    Task SubmitForApprovalAsync(Guid entityId, string entityType, string content,
        CancellationToken ct = default);

    Task ProcessPendingAIAsync(CancellationToken ct = default);
}

public sealed class ApprovalQueueService : IApprovalQueueService
{
    private readonly IAIProviderFactory _factory;
    private readonly IApprovalQueueRepository _queue;
    private readonly IHttpClientFactory _http;
    private readonly ILogger<ApprovalQueueService> _logger;

    public ApprovalQueueService(IAIProviderFactory factory, IApprovalQueueRepository queue,
        IHttpClientFactory http, ILogger<ApprovalQueueService> logger)
    {
        _factory = factory;
        _queue   = queue;
        _http    = http;
        _logger  = logger;
    }

    public async Task SubmitForApprovalAsync(Guid entityId, string entityType,
        string content, CancellationToken ct = default)
    {
        ApprovalConfigDto? config = await _queue.GetApprovalConfigAsync(entityType, ct);
        string mode = config?.Mode ?? "HumanFirst";

        await _queue.InsertPendingAsync(entityId, entityType, mode, ct);

        if (mode == "AIFirst" || mode == "AIOnly")
        {
            await RunAIApprovalAsync(entityId, entityType, content, config, ct);
        }
        else
        {
            await NotifyModeratorsAsync(entityId, entityType, ct);
        }
    }

    public async Task ProcessPendingAIAsync(CancellationToken ct = default)
    {
        List<PendingApprovalDto> timedOut = await _queue.GetHumanTimedOutItemsAsync(ct);
        foreach (PendingApprovalDto item in timedOut)
        {
            await RunAIApprovalAsync(item.EntityId, item.EntityType, item.Content, null, ct);
        }
    }

    private async Task RunAIApprovalAsync(Guid entityId, string entityType,
        string content, ApprovalConfigDto? config, CancellationToken ct)
    {
        string useCase = entityType == "Recipe" ? "recipe-approval" : "product-approval";
        IAIProvider provider = await _factory.GetProviderForUseCaseAsync(useCase, ct);

        AIApprovalResult result = await provider.ScoreForApprovalAsync(content, entityType, null, ct);

        if (!result.Success || result.KickToHuman)
        {
            await _queue.MoveToHumanQueueAsync(entityId, entityType,
                result.ErrorMessage ?? "AI uncertain", ct);
            _logger.LogInformation(
                "AI kicked {EntityType} {EntityId} to human queue", entityType, entityId);
            return;
        }

        decimal threshold = config?.AIConfidenceMin ?? 0.75m;
        if (result.Score >= threshold)
        {
            await _queue.ApproveAsync(entityId, entityType,
                $"AI approved (score={result.Score:F2})", ct);
        }
        else
        {
            await _queue.RejectAsync(entityId, entityType, result.Reasoning, ct);
        }
    }

    private async Task NotifyModeratorsAsync(Guid entityId, string entityType,
        CancellationToken ct)
    {
        HttpClient client = _http.CreateClient("NotificationService");
        await client.PostAsJsonAsync("/api/notifications/moderators", new
        {
            type       = "ModerationRequired",
            title      = $"New {entityType} pending review",
            entityType,
            entityId
        }, ct);
    }
}
