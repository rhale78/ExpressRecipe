using ExpressRecipe.UserService.Data;
using Stripe;
using Stripe.Checkout;

namespace ExpressRecipe.UserService.Services;

public sealed class StripePaymentService : IPaymentService
{
    private readonly CustomerService _customers;
    private readonly SessionService _checkout;
    private readonly Stripe.BillingPortal.SessionService _portal;
    private readonly SubscriptionService _subscriptions;
    private readonly PromotionCodeService _promotionCodes;
    private readonly IStripeEventLogRepository _eventLog;
    private readonly IUserProfileRepository _userProfiles;
    private readonly ILogger<StripePaymentService> _logger;

    public StripePaymentService(
        IConfiguration config,
        IStripeEventLogRepository eventLog,
        IUserProfileRepository userProfiles,
        ILogger<StripePaymentService> logger)
    {
        string secretKey = config["Stripe:SecretKey"]
            ?? throw new InvalidOperationException("Stripe:SecretKey not configured");

        StripeConfiguration.ApiKey = secretKey;
        _customers       = new CustomerService();
        _checkout        = new SessionService();
        _portal          = new Stripe.BillingPortal.SessionService();
        _subscriptions   = new SubscriptionService();
        _promotionCodes  = new PromotionCodeService();
        _eventLog        = eventLog;
        _userProfiles    = userProfiles;
        _logger          = logger;
    }

    public async Task CreateCustomerAsync(Guid userId, string email, string name,
        CancellationToken ct = default)
    {
        Customer customer = await _customers.CreateAsync(new CustomerCreateOptions
        {
            Email    = email,
            Name     = name,
            Metadata = new Dictionary<string, string> { { "userId", userId.ToString() } }
        }, cancellationToken: ct);

        await _userProfiles.UpdateStripeCustomerIdAsync(userId, customer.Id, ct);

        _logger.LogInformation("Created Stripe customer {CustomerId} for user {UserId}",
            customer.Id, userId);
    }

    public async Task<string> CreateCheckoutSessionAsync(Guid userId, string stripePriceId,
        bool withTrial, string successUrl, string cancelUrl, CancellationToken ct = default)
    {
        string? stripeCustomerId = await _userProfiles.GetStripeCustomerIdAsync(userId, ct);

        // Always set SubscriptionData so userId propagates to subscription webhooks via Metadata.
        // Session-level Metadata does NOT propagate to the Subscription object.
        SessionSubscriptionDataOptions subscriptionData = new()
        {
            Metadata = new Dictionary<string, string> { { "userId", userId.ToString() } }
        };

        if (withTrial)
        {
            subscriptionData.TrialPeriodDays = 14;
        }

        SessionCreateOptions opts = new()
        {
            Mode                = "subscription",
            LineItems           = new List<SessionLineItemOptions>
            {
                new() { Price = stripePriceId, Quantity = 1 }
            },
            SuccessUrl          = successUrl,
            CancelUrl           = cancelUrl,
            SubscriptionData    = subscriptionData,
            AllowPromotionCodes = true
        };

        // Attach existing Stripe customer when available; otherwise Stripe creates one during checkout.
        if (!string.IsNullOrEmpty(stripeCustomerId))
        {
            opts.Customer = stripeCustomerId;
        }

        Session session = await _checkout.CreateAsync(opts, cancellationToken: ct);

        _logger.LogInformation("Created Stripe checkout session {SessionId} for user {UserId}",
            session.Id, userId);

        return session.Url;
    }

    public async Task<string> CreateBillingPortalSessionAsync(Guid userId, string returnUrl,
        CancellationToken ct = default)
    {
        string? stripeCustomerId = await _userProfiles.GetStripeCustomerIdAsync(userId, ct);
        if (string.IsNullOrEmpty(stripeCustomerId))
        {
            throw new InvalidOperationException($"No Stripe customer ID found for user {userId}");
        }

        Stripe.BillingPortal.Session session = await _portal.CreateAsync(
            new Stripe.BillingPortal.SessionCreateOptions
            {
                Customer  = stripeCustomerId,
                ReturnUrl = returnUrl
            },
            cancellationToken: ct);

        return session.Url;
    }

    public async Task CancelSubscriptionAsync(string stripeSubscriptionId,
        CancellationToken ct = default)
    {
        await _subscriptions.UpdateAsync(
            stripeSubscriptionId,
            new SubscriptionUpdateOptions { CancelAtPeriodEnd = true },
            cancellationToken: ct);

        _logger.LogInformation("Scheduled cancellation for Stripe subscription {SubscriptionId}",
            stripeSubscriptionId);
    }

    public async Task ApplyPromotionCodeAsync(string stripeSubscriptionId, string promoCode,
        CancellationToken ct = default)
    {
        StripeList<PromotionCode> codes = await _promotionCodes.ListAsync(
            new PromotionCodeListOptions { Code = promoCode, Active = true, Limit = 1 },
            cancellationToken: ct);

        PromotionCode? code = codes.Data.FirstOrDefault();
        if (code == null)
        {
            throw new InvalidOperationException($"Promotion code '{promoCode}' not found or inactive");
        }

        await _subscriptions.UpdateAsync(
            stripeSubscriptionId,
            new SubscriptionUpdateOptions
            {
                Discounts = new List<SubscriptionDiscountOptions>
                {
                    new() { PromotionCode = code.Id }
                }
            },
            cancellationToken: ct);

        _logger.LogInformation("Applied promotion code {PromoCode} to subscription {SubscriptionId}",
            promoCode, stripeSubscriptionId);
    }

    public Task<bool> EventAlreadyProcessedAsync(string stripeEventId, CancellationToken ct = default)
        => _eventLog.ExistsAsync(stripeEventId, ct);

    public Task MarkEventProcessedAsync(string stripeEventId, string eventType,
        CancellationToken ct = default)
        => _eventLog.InsertAsync(stripeEventId, eventType, ct);
}
