using ExpressRecipe.Shared.DTOs.User;
using ExpressRecipe.UserService.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ExpressRecipe.UserService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SubscriptionsController : ControllerBase
{
    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly ILogger<SubscriptionsController> _logger;

    public SubscriptionsController(
        ISubscriptionRepository subscriptionRepository,
        ILogger<SubscriptionsController> logger)
    {
        _subscriptionRepository = subscriptionRepository;
        _logger = logger;
    }

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return null;
        }
        return userId;
    }

    /// <summary>
    /// Get available subscription tiers
    /// </summary>
    [HttpGet("tiers")]
    [AllowAnonymous]
    public async Task<ActionResult<List<SubscriptionTierDto>>> GetTiers()
    {
        try
        {
            var tiers = await _subscriptionRepository.GetSubscriptionTiersAsync();
            return Ok(tiers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving subscription tiers");
            return StatusCode(500, new { message = "An error occurred while retrieving subscription tiers" });
        }
    }

    /// <summary>
    /// Get subscription tier by ID
    /// </summary>
    [HttpGet("tiers/{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<SubscriptionTierDto>> GetTier(Guid id)
    {
        try
        {
            var tier = await _subscriptionRepository.GetSubscriptionTierByIdAsync(id);

            if (tier == null)
            {
                return NotFound(new { message = "Subscription tier not found" });
            }

            return Ok(tier);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving subscription tier {TierId}", id);
            return StatusCode(500, new { message = "An error occurred while retrieving the subscription tier" });
        }
    }

    /// <summary>
    /// Get user's current subscription
    /// </summary>
    [HttpGet("current")]
    public async Task<ActionResult<UserSubscriptionDto>> GetCurrentSubscription()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var subscription = await _subscriptionRepository.GetUserSubscriptionAsync(userId.Value);

            if (subscription == null)
            {
                // User has no subscription, return Free tier info
                var freeTier = await _subscriptionRepository.GetSubscriptionTierByNameAsync("Free");
                return Ok(new
                {
                    tier = freeTier,
                    status = "Free",
                    message = "No active subscription - using Free tier"
                });
            }

            return Ok(subscription);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving current subscription");
            return StatusCode(500, new { message = "An error occurred while retrieving your subscription" });
        }
    }

    /// <summary>
    /// Subscribe to a tier
    /// </summary>
    [HttpPost("subscribe")]
    public async Task<ActionResult<Guid>> Subscribe([FromBody] SubscribeRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            // Validate tier exists and is not Free
            var tier = await _subscriptionRepository.GetSubscriptionTierByIdAsync(request.SubscriptionTierId);
            if (tier == null)
            {
                return BadRequest(new { message = "Invalid subscription tier" });
            }

            if (tier.TierName == "Free")
            {
                return BadRequest(new { message = "Cannot subscribe to Free tier" });
            }

            var subscriptionId = await _subscriptionRepository.SubscribeAsync(userId.Value, request);

            _logger.LogInformation("User {UserId} subscribed to {TierName} ({BillingCycle})",
                userId.Value, tier.TierName, request.BillingCycle);

            return Ok(new
            {
                id = subscriptionId,
                message = $"Successfully subscribed to {tier.DisplayName}",
                tierName = tier.TierName
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error subscribing user");
            return StatusCode(500, new { message = "An error occurred while processing your subscription" });
        }
    }

    /// <summary>
    /// Cancel subscription
    /// </summary>
    [HttpPost("cancel")]
    public async Task<ActionResult> CancelSubscription([FromBody] CancelSubscriptionRequest? request = null)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var success = await _subscriptionRepository.CancelSubscriptionAsync(
                userId.Value,
                request?.CancellationReason);

            if (!success)
            {
                return NotFound(new { message = "No active subscription found" });
            }

            _logger.LogInformation("User {UserId} cancelled subscription. Reason: {Reason}",
                userId.Value, request?.CancellationReason ?? "Not provided");

            return Ok(new
            {
                message = "Subscription cancelled. You will continue to have access until the end of your billing period."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling subscription");
            return StatusCode(500, new { message = "An error occurred while cancelling your subscription" });
        }
    }

    /// <summary>
    /// Update payment method
    /// </summary>
    [HttpPut("{id:guid}/payment-method")]
    public async Task<ActionResult> UpdatePaymentMethod(Guid id, [FromBody] UpdatePaymentMethodRequest request)
    {
        try
        {
            var success = await _subscriptionRepository.UpdatePaymentMethodAsync(id, request.PaymentMethodId);

            if (!success)
            {
                return NotFound(new { message = "Subscription not found or is not active" });
            }

            return Ok(new { message = "Payment method updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating payment method for subscription {SubscriptionId}", id);
            return StatusCode(500, new { message = "An error occurred while updating your payment method" });
        }
    }

    /// <summary>
    /// Get subscription history
    /// </summary>
    [HttpGet("history")]
    public async Task<ActionResult<List<SubscriptionHistoryDto>>> GetHistory()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var history = await _subscriptionRepository.GetSubscriptionHistoryAsync(userId.Value);
            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving subscription history");
            return StatusCode(500, new { message = "An error occurred while retrieving subscription history" });
        }
    }

    /// <summary>
    /// Check feature access
    /// </summary>
    [HttpGet("features/{featureName}")]
    public async Task<ActionResult<bool>> CheckFeatureAccess(string featureName)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var hasAccess = await _subscriptionRepository.HasFeatureAccessAsync(userId.Value, featureName);
            return Ok(new { featureName, hasAccess });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking feature access for {FeatureName}", featureName);
            return StatusCode(500, new { message = "An error occurred while checking feature access" });
        }
    }

    /// <summary>
    /// Get all feature access for user
    /// </summary>
    [HttpGet("features")]
    public async Task<ActionResult<Dictionary<string, bool>>> GetFeatureAccess()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var features = await _subscriptionRepository.GetFeatureAccessMapAsync(userId.Value);
            return Ok(features);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving feature access");
            return StatusCode(500, new { message = "An error occurred while retrieving feature access" });
        }
    }
}
