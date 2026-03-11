namespace ExpressRecipe.UserService.Services;

/// <summary>
/// Abstracts the external payment processor (Stripe in production).
/// </summary>
public interface IPaymentService
{
    /// <summary>
    /// Creates a hosted Stripe Checkout Session and returns the redirect URL.
    /// </summary>
    Task<string> CreateCheckoutSessionAsync(
        Guid userId,
        string stripePriceId,
        bool withTrial,
        string successUrl,
        string cancelUrl,
        CancellationToken ct = default);

    /// <summary>
    /// Creates a Stripe Billing Portal Session and returns the redirect URL.
    /// </summary>
    Task<string> CreateBillingPortalSessionAsync(
        Guid userId,
        string stripeCustomerId,
        string returnUrl,
        CancellationToken ct = default);

    /// <summary>
    /// Cancels an active Stripe subscription at the end of the current period.
    /// </summary>
    Task CancelSubscriptionAsync(string stripeSubscriptionId, CancellationToken ct = default);

    /// <summary>Returns true if a Stripe event with this ID has already been processed (idempotency).</summary>
    Task<bool> EventAlreadyProcessedAsync(string stripeEventId, CancellationToken ct = default);

    /// <summary>Records a processed Stripe event to prevent duplicate processing.</summary>
    Task MarkEventProcessedAsync(string stripeEventId, string eventType, CancellationToken ct = default);
}
