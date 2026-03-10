namespace ExpressRecipe.UserService.Services;

public interface IPaymentService
{
    /// <summary>Creates a Stripe customer record for a new user and stores the customer ID.</summary>
    Task CreateCustomerAsync(Guid userId, string email, string name,
        CancellationToken ct = default);

    /// <summary>Creates a Stripe Checkout session URL to redirect the user to the hosted payment page.</summary>
    Task<string> CreateCheckoutSessionAsync(Guid userId, string stripePriceId,
        bool withTrial, string successUrl, string cancelUrl,
        CancellationToken ct = default);

    /// <summary>Creates a Stripe Billing Portal session URL so the user can manage their subscription.</summary>
    Task<string> CreateBillingPortalSessionAsync(Guid userId, string returnUrl,
        CancellationToken ct = default);

    /// <summary>Cancels a Stripe subscription at the end of the current billing period.</summary>
    Task CancelSubscriptionAsync(string stripeSubscriptionId, CancellationToken ct = default);

    /// <summary>Applies a promotion code to an existing Stripe subscription.</summary>
    Task ApplyPromotionCodeAsync(string stripeSubscriptionId, string promoCode,
        CancellationToken ct = default);

    /// <summary>Returns true if the Stripe event has already been processed (idempotency check).</summary>
    Task<bool> EventAlreadyProcessedAsync(string stripeEventId, CancellationToken ct = default);

    /// <summary>Marks a Stripe event as processed so it is not handled again.</summary>
    Task MarkEventProcessedAsync(string stripeEventId, string eventType,
        CancellationToken ct = default);
}
