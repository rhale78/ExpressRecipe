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

    public StripeWebhookController(
        IPaymentService payment,
        ISubscriptionRepository subs,
        IConfiguration config,
        ILogger<StripeWebhookController> logger)
    {
        _payment       = payment;
        _subs          = subs;
        _webhookSecret = config["Stripe:WebhookSecret"] ?? string.Empty;
        _logger        = logger;
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
            stripeEvent = EventUtility.ConstructEvent(payload, sig.ToString(), _webhookSecret);
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Stripe signature validation failed");
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
                string tier     = MapPriceIdToTier(sub.Items.Data[0].Price.Id);
                Guid userId     = GetUserIdFromMetadata(sub.Metadata);
                DateTime periodEnd = sub.Items.Data.Count > 0
                    ? sub.Items.Data[0].CurrentPeriodEnd
                    : DateTime.UtcNow.AddMonths(1);
                await _subs.UpdateUserSubscriptionAsync(userId, tier, sub.Id, periodEnd, ct);
                _logger.LogInformation(
                    "Subscription {Event} for user {UserId}: tier={Tier}, sub={SubId}",
                    ev.Type, userId, tier, sub.Id);
                break;
            }

            case EventTypes.CustomerSubscriptionDeleted:
            {
                Subscription sub = (Subscription)ev.Data.Object;
                Guid userId      = GetUserIdFromMetadata(sub.Metadata);
                await _subs.DowngradeToFreeAsync(userId, ct);
                _logger.LogInformation("Subscription deleted — downgraded user {UserId} to Free", userId);
                break;
            }

            case EventTypes.CustomerSubscriptionTrialWillEnd:
            {
                Subscription sub = (Subscription)ev.Data.Object;
                Guid userId      = GetUserIdFromMetadata(sub.Metadata);
                _logger.LogInformation("Trial ending soon for user {UserId}", userId);
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

    private static Guid GetUserIdFromMetadata(IDictionary<string, string> metadata)
        => metadata.TryGetValue("userId", out string? id) && Guid.TryParse(id, out Guid guid)
            ? guid
            : Guid.Empty;
}
