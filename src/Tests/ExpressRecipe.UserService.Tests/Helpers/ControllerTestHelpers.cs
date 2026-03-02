using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ExpressRecipe.UserService.Tests.Helpers;

/// <summary>
/// Helper methods for creating test contexts for controllers
/// </summary>
public static class ControllerTestHelpers
{
    /// <summary>
    /// Creates an authenticated ClaimsPrincipal with the specified user ID
    /// </summary>
    public static ClaimsPrincipal CreateAuthenticatedUser(Guid userId)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, $"TestUser{userId}"),
            new Claim(ClaimTypes.Email, $"test{userId}@example.com")
        };

        var identity = new ClaimsIdentity(claims, "TestAuth");
        return new ClaimsPrincipal(identity);
    }

    /// <summary>
    /// Creates an unauthenticated ClaimsPrincipal
    /// </summary>
    public static ClaimsPrincipal CreateUnauthenticatedUser()
    {
        var identity = new ClaimsIdentity();
        return new ClaimsPrincipal(identity);
    }

    /// <summary>
    /// Sets up the HttpContext for a controller with authentication
    /// </summary>
    public static void SetupControllerContext<T>(T controller, Guid userId) where T : ControllerBase
    {
        var user = CreateAuthenticatedUser(userId);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
    }

    /// <summary>
    /// Sets up the HttpContext for a controller without authentication
    /// </summary>
    public static void SetupUnauthenticatedControllerContext<T>(T controller) where T : ControllerBase
    {
        var user = CreateUnauthenticatedUser();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
    }
}
