using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ExpressRecipe.MealPlanningService.Tests.Helpers;

public static class ControllerTestHelpers
{
    public static ControllerContext CreateAuthenticatedContext(Guid userId, Guid? householdId = null)
    {
        List<Claim> claims = new()
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, "testuser"),
            new Claim(ClaimTypes.Email, "testuser@example.com")
        };

        if (householdId.HasValue)
            claims.Add(new Claim("household_id", householdId.Value.ToString()));

        ClaimsIdentity identity = new(claims, "TestAuthentication");
        ClaimsPrincipal claimsPrincipal = new(identity);

        DefaultHttpContext httpContext = new() { User = claimsPrincipal };

        return new ControllerContext { HttpContext = httpContext };
    }
}
