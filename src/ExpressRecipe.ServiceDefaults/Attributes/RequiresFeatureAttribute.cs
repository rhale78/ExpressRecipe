using System.Security.Claims;
using ExpressRecipe.Shared.Services.FeatureGates;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace ExpressRecipe.Shared.Attributes;

/// <summary>
/// Action filter that enforces a feature flag gate.
/// Checks the global admin toggle (Layer 1), per-user override (Layer 2),
/// and the flag's subscription tier requirement (Layer 3).
/// <para>
/// Returns 402 PaymentRequired with an upgrade payload when the feature is globally on
/// but the user's tier is insufficient, or 403 Forbidden when the feature is globally disabled.
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

        Guid userId = Guid.TryParse(
            user.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id : Guid.Empty;

        string userTier = user.FindFirstValue("subscription_tier") ?? "Free";

        bool enabled = await flagService.IsEnabledAsync(_featureKey, userId, userTier,
            context.HttpContext.RequestAborted);

        if (!enabled)
        {
            bool globallyOn = await flagService.IsGloballyEnabledAsync(_featureKey,
                context.HttpContext.RequestAborted);

            if (globallyOn)
            {
                // Globally on but this user's tier doesn't qualify → 402
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
                // Globally disabled → 403
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
