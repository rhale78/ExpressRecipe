using ExpressRecipe.Shared.DTOs.User;

namespace ExpressRecipe.UserService.Data;

public interface ISubscriptionRepository
{
    // Subscription Tiers
    Task<List<SubscriptionTierDto>> GetSubscriptionTiersAsync();
    Task<SubscriptionTierDto?> GetSubscriptionTierByIdAsync(Guid id);
    Task<SubscriptionTierDto?> GetSubscriptionTierByNameAsync(string tierName);

    // User Subscriptions
    Task<UserSubscriptionDto?> GetUserSubscriptionAsync(Guid userId);
    Task<Guid> SubscribeAsync(Guid userId, SubscribeRequest request);
    Task<bool> CancelSubscriptionAsync(Guid userId, string? cancellationReason = null);
    Task<bool> RenewSubscriptionAsync(Guid subscriptionId);
    Task<bool> UpdatePaymentMethodAsync(Guid subscriptionId, string paymentMethodId);

    // Subscription History
    Task<List<SubscriptionHistoryDto>> GetSubscriptionHistoryAsync(Guid userId);

    // Feature Access
    Task<bool> HasFeatureAccessAsync(Guid userId, string featureName);
    Task<Dictionary<string, bool>> GetFeatureAccessMapAsync(Guid userId);

    // Admin: grant a credit / extend a subscription tier
    Task<Guid> GrantCreditAsync(Guid userId, string tier, int durationDays, string reason, Guid grantedBy, CancellationToken ct = default);
}
