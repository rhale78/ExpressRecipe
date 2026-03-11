using System.Collections.Concurrent;

namespace ExpressRecipe.UserService.Services;

/// <summary>
/// In-process mock payment service used when APP_LOCAL_MODE=true.
/// Never calls Stripe — suitable for local development and unit tests.
/// </summary>
public sealed class MockPaymentService : IPaymentService
{
    private readonly ConcurrentDictionary<string, byte> _processedEvents = new(StringComparer.Ordinal);

    public Task CreateCustomerAsync(Guid userId, string email, string name,
        CancellationToken ct = default)
        => Task.CompletedTask;

    public Task<string> CreateCheckoutSessionAsync(Guid userId, string stripePriceId,
        bool withTrial, string successUrl, string cancelUrl, CancellationToken ct = default)
    {
        UriBuilder builder = new(successUrl);
        string existing = builder.Query.TrimStart('?');
        string added    = $"mock=true&tier={System.Net.WebUtility.UrlEncode(stripePriceId)}";
        builder.Query   = string.IsNullOrEmpty(existing) ? added : $"{existing}&{added}";
        return Task.FromResult(builder.Uri.AbsoluteUri);
    }

    public Task<string> CreateBillingPortalSessionAsync(Guid userId, string returnUrl,
        CancellationToken ct = default)
        => Task.FromResult(returnUrl);

    public Task CancelSubscriptionAsync(string stripeSubscriptionId,
        CancellationToken ct = default)
        => Task.CompletedTask;

    public Task ApplyPromotionCodeAsync(string stripeSubscriptionId, string promoCode,
        CancellationToken ct = default)
        => Task.CompletedTask;

    public Task<bool> EventAlreadyProcessedAsync(string stripeEventId,
        CancellationToken ct = default)
        => Task.FromResult(_processedEvents.ContainsKey(stripeEventId));

    public Task MarkEventProcessedAsync(string stripeEventId, string eventType,
        CancellationToken ct = default)
    {
        _processedEvents.TryAdd(stripeEventId, 0);
        return Task.CompletedTask;
    }
}
