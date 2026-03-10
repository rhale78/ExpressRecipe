namespace ExpressRecipe.UserService.Services;

/// <summary>
/// No-op payment service used in local / development mode.
/// All operations succeed immediately and return placeholder URLs.
/// </summary>
public sealed class MockPaymentService : IPaymentService
{
    private readonly ILogger<MockPaymentService> _logger;

    public MockPaymentService(ILogger<MockPaymentService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task<string> CreateCheckoutSessionAsync(
        Guid userId,
        string stripePriceId,
        bool withTrial,
        string successUrl,
        string cancelUrl,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[MockPayment] CreateCheckoutSession: userId={UserId} priceId={PriceId} trial={Trial}",
            userId, stripePriceId, withTrial);

        return Task.FromResult(successUrl);
    }

    /// <inheritdoc/>
    public Task<string> CreateBillingPortalSessionAsync(
        Guid userId,
        string returnUrl,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[MockPayment] CreateBillingPortalSession: userId={UserId}",
            userId);

        return Task.FromResult(returnUrl);
    }

    /// <inheritdoc/>
    public Task CancelSubscriptionAsync(string stripeSubscriptionId, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[MockPayment] CancelSubscription: stripeSubscriptionId={SubscriptionId}",
            stripeSubscriptionId);

        return Task.CompletedTask;
    }
}
