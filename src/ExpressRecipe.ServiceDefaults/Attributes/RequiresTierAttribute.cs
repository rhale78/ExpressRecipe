using System.Security.Claims;
using ExpressRecipe.Shared.Services.FeatureGates;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace ExpressRecipe.Shared.Attributes;

/// <summary>
/// Action filter that enforces a minimum subscription tier regardless of feature flags.
/// Use this to gate entire controllers by tier without needing a feature flag entry.
/// <para>
/// Returns 402 PaymentRequired when the user's tier rank is below the required minimum.
/// </para>
/// <para>
/// Bypassed entirely when <see cref="ILocalModeConfig.IsLocalMode"/> is <c>true</c>.
/// </para>
/// </summary>
/// <example>
/// <code>
/// [RequiresTier("Plus")]
/// [ApiController]
/// [Route("api/inventory")]
/// public class InventoryController : ControllerBase { }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequiresTierAttribute : ActionFilterAttribute
{
    private static readonly Dictionary<string, int> TierRank =
        new(StringComparer.OrdinalIgnoreCase)
        {
            { "Free",    0 },
            { "AdFree",  1 },
            { "Plus",    2 },
            { "Premium", 3 }
        };

    private readonly string _minimumTier;

    public RequiresTierAttribute(string minimumTier) => _minimumTier = minimumTier;

    public override Task OnActionExecutionAsync(ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        var localMode = context.HttpContext.RequestServices
            .GetRequiredService<ILocalModeConfig>();

        if (localMode.IsLocalMode) return next();

        string userTier = context.HttpContext.User.FindFirstValue("subscription_tier") ?? "Free";
        int required    = TierRank.GetValueOrDefault(_minimumTier, 99);
        int actual      = TierRank.GetValueOrDefault(userTier, 0);

        if (actual < required)
        {
            context.Result = new ObjectResult(new FeatureGateResult
            {
                FeatureKey = $"tier:{_minimumTier}",
                Reason     = "SubscriptionRequired",
                UpgradeUrl = "/settings/billing",
                Message    = $"This feature requires a {_minimumTier} subscription or higher."
            }) { StatusCode = 402 };

            return Task.CompletedTask;
        }

        return next();
    }
}
