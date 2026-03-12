using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ExpressRecipe.RecallService.Tests.Helpers;

/// <summary>
/// Helper methods for setting up controller test contexts
/// </summary>
public static class ControllerTestHelpers
{
    /// <summary>
    /// Creates an authenticated controller context with a specific user ID
    /// </summary>
    public static ControllerContext CreateAuthenticatedContext(Guid userId)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, "testuser"),
            new Claim(ClaimTypes.Email, "testuser@example.com")
        };

        var identity = new ClaimsIdentity(claims, "TestAuthentication");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext
        {
            User = claimsPrincipal
        };

        return new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    /// <summary>
    /// Creates an unauthenticated controller context
    /// </summary>
    public static ControllerContext CreateUnauthenticatedContext()
    {
        var httpContext = new DefaultHttpContext();

        return new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    /// <summary>
    /// Gets the user ID from a controller context
    /// </summary>
    public static Guid? GetUserId(ControllerContext context)
    {
        var userIdClaim = context.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return userId;
        }
        return null;
    }
}
