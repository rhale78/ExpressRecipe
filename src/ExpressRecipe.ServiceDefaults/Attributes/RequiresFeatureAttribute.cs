using System.Security.Claims;
using ExpressRecipe.Shared.Services.FeatureGates;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace ExpressRecipe.Shared.Attributes;

/// <summary>
/// Action filter that enforces a feature flag gate.
/// Checks the global admin toggle (Layer 1), per-user override (Layer 2),
/// the rollout percentage (Layer 3a) and the flag's subscription tier requirement (Layer 3b).
/// <para>
/// Returns 402 PaymentRequired when <see cref="FeatureCheckReason.TierInsufficient"/>;
/// returns 403 Forbidden for all other disabled reasons
/// (<see cref="FeatureCheckReason.GloballyDisabled"/>, <see cref="FeatureCheckReason.UserDisabled"/>,
/// <see cref="FeatureCheckReason.NotInRollout"/>).
/// </para>
/// <para>
/// Bypassed entirely when <see cref="ILocalModeConfig.IsLocalMode"/> is <c>true</c>.
/// </para>
/// </summary>
/// <example>
/// <code>
/// [RequiresFeature("allergy-engine")]
/// [ApiController]
/// [Route("api/allergy")]
/// public class AllergyController : ControllerBase { }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequiresFeatureAttribute : ActionFilterAttribute
{
    private readonly string _featureKey;

    public RequiresFeatureAttribute(string featureKey) => _featureKey = featureKey;

    public override async Task OnActionExecutionAsync(ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        var flagService = context.HttpContext.RequestServices
            .GetRequiredService<IFeatureFlagService>();
        var localMode = context.HttpContext.RequestServices
            .GetRequiredService<ILocalModeConfig>();

        if (localMode.IsLocalMode)
        {
            await next();
            return;
        }

        ClaimsPrincipal user = context.HttpContext.User;
        if (!(user.Identity?.IsAuthenticated ?? false))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        if (!Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out Guid userId))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        string userTier = user.FindFirstValue("subscription_tier") ?? "Free";

        var evaluation = await flagService.IsEnabledAsync(_featureKey, userId, userTier,
            context.HttpContext.RequestAborted);

        if (!evaluation.IsEnabled)
        {
            if (evaluation.Reason == FeatureCheckReason.TierInsufficient)
            {
                // Tier too low — prompt the user to upgrade
                context.Result = new ObjectResult(new FeatureGateResult
                {
                    FeatureKey = _featureKey,
                    Reason     = "SubscriptionRequired",
                    UpgradeUrl = "/settings/billing",
                    Message    = "This feature requires a higher subscription tier."
                }) { StatusCode = 402 };
            }
            else
            {
                // Globally disabled, user revoked, not in rollout, etc.
                context.Result = new ObjectResult(new FeatureGateResult
                {
                    FeatureKey = _featureKey,
                    Reason     = "FeatureDisabled",
                    Message    = "This feature is not currently available."
                }) { StatusCode = 403 };
            }

            return;
        }

        await next();
    }
}
