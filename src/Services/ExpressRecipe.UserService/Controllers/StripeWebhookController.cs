using ExpressRecipe.UserService.Data;
using ExpressRecipe.UserService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Stripe;

namespace ExpressRecipe.UserService.Controllers;

[ApiController]
[Route("api/webhooks/stripe")]
public sealed class StripeWebhookController : ControllerBase
{
    private readonly IPaymentService _payment;
    private readonly ISubscriptionRepository _subs;
    private readonly string _webhookSecret;
    private readonly ILogger<StripeWebhookController> _logger;
    private readonly Func<string, string, string, Event> _constructEvent;

    public StripeWebhookController(
        IPaymentService payment,
        ISubscriptionRepository subs,
        IConfiguration config,
        ILogger<StripeWebhookController> logger,
        Func<string, string, string, Event> constructEvent)
    {
        _payment       = payment;
        _subs          = subs;
        _logger        = logger;
        _constructEvent = constructEvent;

        string? webhookSecret = config["Stripe:WebhookSecret"];
        if (string.IsNullOrWhiteSpace(webhookSecret))
        {
            logger.LogCritical("Stripe:WebhookSecret is not configured. Webhook handling will fail.");
            throw new InvalidOperationException(
                "Stripe:WebhookSecret is not configured. Please set this configuration key.");
        }

        _webhookSecret = webhookSecret;
    }

    /// <summary>
    /// Receives and processes Stripe webhook events.
    /// Authentication is performed via Stripe-Signature header — no JWT required.
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> HandleWebhook(CancellationToken ct)
    {
        string payload;
        using (StreamReader reader = new(Request.Body))
        {
            payload = await reader.ReadToEndAsync(ct);
        }

        if (!Request.Headers.TryGetValue("Stripe-Signature", out StringValues sig))
        {
            return BadRequest();
        }

        Event stripeEvent;
        try
        {
            stripeEvent = _constructEvent(payload, sig.ToString(), _webhookSecret);
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Stripe signature validation failed");
            return BadRequest();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse Stripe event payload");
            return BadRequest();
        }

        // Idempotency check — skip already-processed events
        if (await _payment.EventAlreadyProcessedAsync(stripeEvent.Id, ct))
        {
            return Ok(new { status = "already_processed" });
        }

        try
        {
            await ProcessEventAsync(stripeEvent, ct);
            await _payment.MarkEventProcessedAsync(stripeEvent.Id, stripeEvent.Type, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process Stripe event {EventId} ({EventType})",
                stripeEvent.Id, stripeEvent.Type);
            return StatusCode(500); // Stripe retries on 5xx
        }

        return Ok();
    }

    private async Task ProcessEventAsync(Event ev, CancellationToken ct)
    {
        switch (ev.Type)
        {
            case EventTypes.CustomerSubscriptionCreated:
            case EventTypes.CustomerSubscriptionUpdated:
            {
                Subscription sub = (Subscription)ev.Data.Object;
                var items = sub.Items?.Data;
                if (items == null || items.Count == 0)
                {
                    _logger.LogWarning(
                        "Subscription {SubId} has no items; skipping update", sub.Id);
                    return;
                }
                var price = items[0].Price;
                if (price == null || string.IsNullOrWhiteSpace(price.Id))
                {
                    _logger.LogWarning(
                        "Subscription {SubId} item has no valid price; skipping update", sub.Id);
                    return;
                }
                string tier  = MapPriceIdToTier(price.Id);
                Guid? userId = GetUserIdFromMetadata(sub.Metadata);
                if (!userId.HasValue)
                {
                    _logger.LogWarning("No valid userId in Stripe subscription metadata for sub {SubId}", sub.Id);
                    return;
                }
                DateTime periodEnd = items[0].CurrentPeriodEnd != default
                    ? items[0].CurrentPeriodEnd
                    : DateTime.UtcNow.AddMonths(1);
                await _subs.UpdateUserSubscriptionAsync(userId.Value, tier, sub.Id, periodEnd, ct);
                _logger.LogInformation(
                    "Subscription {Event} for user {UserId}: tier={Tier}, sub={SubId}",
                    ev.Type, userId.Value, tier, sub.Id);
                break;
            }

            case EventTypes.CustomerSubscriptionDeleted:
            {
                Subscription sub = (Subscription)ev.Data.Object;
                Guid? userId     = GetUserIdFromMetadata(sub.Metadata);
                if (!userId.HasValue)
                {
                    _logger.LogWarning("No valid userId in Stripe subscription metadata for sub {SubId}", sub.Id);
                    return;
                }
                await _subs.DowngradeToFreeAsync(userId.Value, ct);
                _logger.LogInformation("Subscription deleted — downgraded user {UserId} to Free", userId.Value);
                break;
            }

            case EventTypes.CustomerSubscriptionTrialWillEnd:
            {
                Subscription sub = (Subscription)ev.Data.Object;
                Guid? userId     = GetUserIdFromMetadata(sub.Metadata);
                if (!userId.HasValue)
                {
                    _logger.LogWarning("No valid userId in Stripe subscription metadata for sub {SubId}", sub.Id);
                    return;
                }
                _logger.LogInformation("Trial ending soon for user {UserId}", userId.Value);
                // TODO: Send trial-ending notification via NotificationService
                break;
            }

            default:
                _logger.LogDebug("Unhandled Stripe event type: {Type}", ev.Type);
                break;
        }
    }

    private static string MapPriceIdToTier(string priceId) => priceId switch
    {
        var p when p.StartsWith("price_adfree",  StringComparison.OrdinalIgnoreCase) => "AdFree",
        var p when p.StartsWith("price_plus",    StringComparison.OrdinalIgnoreCase) => "Plus",
        var p when p.StartsWith("price_premium", StringComparison.OrdinalIgnoreCase) => "Premium",
        _ => "Free"
    };

    private static Guid? GetUserIdFromMetadata(IDictionary<string, string> metadata)
        => metadata.TryGetValue("userId", out string? id) && Guid.TryParse(id, out Guid guid)
            ? guid
            : null;
}
