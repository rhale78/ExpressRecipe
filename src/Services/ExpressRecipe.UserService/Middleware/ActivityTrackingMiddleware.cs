using ExpressRecipe.Shared.DTOs.User;
using ExpressRecipe.UserService.Data;
using System.Security.Claims;

namespace ExpressRecipe.UserService.Middleware;

/// <summary>
/// Middleware to automatically track user activities based on HTTP requests
/// </summary>
public class ActivityTrackingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ActivityTrackingMiddleware> _logger;

    // Activity types mapped to route patterns
    private static readonly Dictionary<string, string> RouteActivityMap = new()
    {
        { "GET /api/recipes/", "RecipeViewed" },
        { "POST /api/recipes/", "RecipeCreated" },
        { "GET /api/products/", "ProductViewed" },
        { "POST /api/products/scan", "ProductScanned" },
        { "GET /api/stores/nearby", "StoreSearched" },
        { "POST /api/coupons/clip", "CouponClipped" },
        { "POST /api/coupons/use", "CouponRedeemed" },
        { "POST /api/mealplans", "MealPlanCreated" },
        { "POST /api/shopping", "ShoppingListCreated" },
        { "POST /api/inventory", "InventoryItemAdded" }
    };

    public ActivityTrackingMiddleware(RequestDelegate next, ILogger<ActivityTrackingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IActivityRepository activityRepository)
    {
        // Continue with the request
        await _next(context);

        // Only track successful requests (2xx status codes)
        if (context.Response.StatusCode >= 200 && context.Response.StatusCode < 300)
        {
            // Skip if user is not authenticated
            if (!context.User.Identity?.IsAuthenticated ?? true)
            {
                return;
            }

            // Get user ID from claims
            var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return;
            }

            // Check if this route should be tracked
            var route = $"{context.Request.Method} {context.Request.Path}";
            var activityType = GetActivityType(route);

            if (!string.IsNullOrEmpty(activityType))
            {
                try
                {
                    // Extract entity ID from route if present (e.g., /api/recipes/{id})
                    Guid? entityId = null;
                    string? entityType = null;

                    if (TryExtractEntityInfo(context.Request.Path, activityType, out var extractedEntityType, out var extractedEntityId))
                    {
                        entityType = extractedEntityType;
                        entityId = extractedEntityId;
                    }

                    // Log the activity asynchronously (fire and forget)
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await activityRepository.LogActivityAsync(userId, new LogActivityRequest
                            {
                                ActivityType = activityType,
                                EntityType = entityType,
                                EntityId = entityId,
                                DeviceType = context.Request.Headers.UserAgent.ToString(),
                                IPAddress = context.Connection.RemoteIpAddress?.ToString()
                            });
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to log activity for user {UserId}", userId);
                        }
                    });
                }
                catch (Exception ex)
                {
                    // Don't let activity tracking failures break the request
                    _logger.LogWarning(ex, "Error in activity tracking middleware");
                }
            }
        }
    }

    private static string? GetActivityType(string route)
    {
        foreach (var (pattern, activityType) in RouteActivityMap)
        {
            if (route.StartsWith(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return activityType;
            }
        }

        return null;
    }

    private static bool TryExtractEntityInfo(string path, string activityType, out string? entityType, out Guid? entityId)
    {
        entityType = null;
        entityId = null;

        // Parse the path to extract entity type and ID
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length < 3)
        {
            return false;
        }

        // Expected format: /api/{entityType}/{id}
        if (segments[0].Equals("api", StringComparison.OrdinalIgnoreCase) && segments.Length >= 3)
        {
            entityType = segments[1].TrimEnd('s'); // Remove trailing 's' (recipes -> recipe)

            // Capitalize first letter
            if (!string.IsNullOrEmpty(entityType))
            {
                entityType = char.ToUpper(entityType[0]) + entityType.Substring(1);
            }

            if (Guid.TryParse(segments[2], out var id))
            {
                entityId = id;
                return true;
            }
        }

        return false;
    }
}

/// <summary>
/// Extension methods for registering the activity tracking middleware
/// </summary>
public static class ActivityTrackingMiddlewareExtensions
{
    public static IApplicationBuilder UseActivityTracking(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ActivityTrackingMiddleware>();
    }
}
