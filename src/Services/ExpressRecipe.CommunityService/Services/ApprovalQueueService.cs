using ExpressRecipe.CommunityService.Data;
using ExpressRecipe.Shared.Events;
using ExpressRecipe.Shared.Services;
using System.Text.Json;

namespace ExpressRecipe.CommunityService.Services;

/// <summary>
/// Manages the recipe / product approval pipeline.
/// Mode: HumanFirst (default) — all items go to human queue first.
///       AIFirst — AI score decides; high-confidence items auto-published.
/// </summary>
public class ApprovalQueueService : IApprovalQueueService
{
    private const decimal AIAutoApproveThreshold = 0.80m;

    private readonly ICommunityRecipeRepository _communityRecipeRepository;
    private readonly ICommunityRepository _communityRepository;
    private readonly IConfiguration _configuration;
    private readonly EventPublisher? _eventPublisher;
    private readonly ILogger<ApprovalQueueService> _logger;

    public ApprovalQueueService(
        ICommunityRecipeRepository communityRecipeRepository,
        ICommunityRepository communityRepository,
        IConfiguration configuration,
        ILogger<ApprovalQueueService> logger,
        EventPublisher? eventPublisher = null)
    {
        _communityRecipeRepository = communityRecipeRepository;
        _communityRepository = communityRepository;
        _configuration = configuration;
        _logger = logger;
        _eventPublisher = eventPublisher;
    }

    public async Task<Guid> SubmitForApprovalAsync(Guid entityId, string entityType, string? content, CancellationToken ct = default)
    {
        var mode = _configuration["Approval:Mode"] ?? "HumanFirst";
        var initialStatus = mode == "AIFirst" ? "AIReviewing" : "InHumanQueue";

        if (entityType == "Recipe")
        {
            var existing = await _communityRecipeRepository.GetByRecipeIdAsync(entityId, ct);
            if (existing == null)
            {
                await _communityRecipeRepository.SubmitRecipeAsync(entityId, Guid.Empty, ct);
                existing = await _communityRecipeRepository.GetByRecipeIdAsync(entityId, ct);
            }

            if (existing != null)
            {
                await _communityRecipeRepository.UpdateStatusAsync(
                    existing.Id, initialStatus, null, null, null, ct);
            }
        }

        var queueId = await _communityRecipeRepository.EnqueueForApprovalAsync(entityId, entityType, content, ct);

        _logger.LogInformation("Enqueued {EntityType} {EntityId} for approval (mode={Mode})",
            entityType, entityId, mode);

        return queueId;
    }

    public async Task ProcessAIApprovalAsync(Guid queueItemId, decimal aiScore, CancellationToken ct = default)
    {
        var items = await _communityRecipeRepository.GetPendingApprovalItemsAsync(100, ct);
        var item = items.FirstOrDefault(i => i.Id == queueItemId);
        if (item == null)
        {
            _logger.LogWarning("ApprovalQueue item {Id} not found", queueItemId);
            return;
        }

        await _communityRecipeRepository.MarkApprovalItemProcessedAsync(queueItemId, aiScore, ct);

        var mode = _configuration["Approval:Mode"] ?? "HumanFirst";

        if (item.EntityType == "Recipe")
        {
            var recipe = await _communityRecipeRepository.GetByRecipeIdAsync(item.EntityId, ct);
            if (recipe == null) return;

            if (mode == "AIFirst" && aiScore >= AIAutoApproveThreshold)
            {
                await _communityRecipeRepository.UpdateStatusAsync(
                    recipe.Id, "Approved", "AI", null, aiScore, ct);

                // Award points to submitter
                if (_eventPublisher != null)
                {
                    await FireAndForgetAsync(_eventPublisher.PublishAsync(EventKeys.PointsEarned, new PointsEarnedEvent
                    {
                        UserId = recipe.SubmittedBy,
                        Reason = "RecipePublished",
                        Points = 100,
                        RelatedEntityId = recipe.RecipeId
                    }));
                }

                _logger.LogInformation("Recipe {RecipeId} auto-approved by AI (score={Score})",
                    item.EntityId, aiScore);
            }
            else
            {
                await _communityRecipeRepository.UpdateStatusAsync(
                    recipe.Id, "InHumanQueue", null, null, aiScore, ct);
                _logger.LogInformation("Recipe {RecipeId} queued for human review (AI score={Score})",
                    item.EntityId, aiScore);
            }
        }
        else if (item.EntityType == "Product")
        {
            _logger.LogInformation("Product {EntityId} processed by AI (score={Score}), awaiting human review",
                item.EntityId, aiScore);
        }
    }

    public async Task ApproveAsync(Guid entityId, string entityType, string approvedBy, CancellationToken ct = default)
    {
        if (entityType == "Recipe")
        {
            var recipe = await _communityRecipeRepository.GetByRecipeIdAsync(entityId, ct);
            if (recipe == null) return;

            await _communityRecipeRepository.UpdateStatusAsync(
                recipe.Id, "Approved", approvedBy, null, recipe.AIScore, ct);

            if (_eventPublisher != null)
            {
                await FireAndForgetAsync(_eventPublisher.PublishAsync(EventKeys.PointsEarned, new PointsEarnedEvent
                {
                    UserId = recipe.SubmittedBy,
                    Reason = "RecipePublished",
                    Points = 100,
                    RelatedEntityId = recipe.RecipeId
                }));
            }

            _logger.LogInformation("Recipe {RecipeId} approved by {ApprovedBy}", entityId, approvedBy);
        }
        else if (entityType == "Product")
        {
            await _communityRepository.ApproveSubmissionAsync(entityId, Guid.Parse(approvedBy), Guid.NewGuid());

            if (_eventPublisher != null)
            {
                await FireAndForgetAsync(_eventPublisher.PublishAsync(EventKeys.PointsEarned, new PointsEarnedEvent
                {
                    UserId = entityId,
                    Reason = "ProductApproved",
                    Points = 50,
                    RelatedEntityId = entityId
                }));
            }
        }
    }

    public async Task RejectAsync(Guid entityId, string entityType, string rejectedBy, string reason, CancellationToken ct = default)
    {
        if (entityType == "Recipe")
        {
            var recipe = await _communityRecipeRepository.GetByRecipeIdAsync(entityId, ct);
            if (recipe == null) return;

            await _communityRecipeRepository.UpdateStatusAsync(
                recipe.Id, "Rejected", rejectedBy, reason, recipe.AIScore, ct);

            _logger.LogInformation("Recipe {RecipeId} rejected by {RejectedBy}: {Reason}", entityId, rejectedBy, reason);
        }
        else if (entityType == "Product")
        {
            await _communityRepository.RejectSubmissionAsync(entityId, Guid.Parse(rejectedBy), reason);
        }
    }

    private Task FireAndForgetAsync(Task task)
    {
        _ = task.ContinueWith(t =>
        {
            if (t.IsFaulted)
            {
                _logger.LogError(t.Exception, "Failed to publish PointsEarnedEvent");
            }
        }, TaskContinuationOptions.OnlyOnFaulted);
        return Task.CompletedTask;
    }
}

public interface IApprovalQueueService
{
    Task<Guid> SubmitForApprovalAsync(Guid entityId, string entityType, string? content, CancellationToken ct = default);
    Task ProcessAIApprovalAsync(Guid queueItemId, decimal aiScore, CancellationToken ct = default);
    Task ApproveAsync(Guid entityId, string entityType, string approvedBy, CancellationToken ct = default);
    Task RejectAsync(Guid entityId, string entityType, string rejectedBy, string reason, CancellationToken ct = default);
}
