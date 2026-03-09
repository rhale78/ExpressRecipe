using ExpressRecipe.MealPlanningService.Data;
using ExpressRecipe.MealPlanningService.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace ExpressRecipe.MealPlanningService.Tests.Controllers;

public class MealPlanningControllerHelpers
{
    public static Microsoft.AspNetCore.Mvc.ControllerContext CreateAuthenticatedContext(Guid userId)
    {
        Claim[] claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        };
        ClaimsIdentity identity   = new(claims, "TestAuth");
        ClaimsPrincipal principal = new(identity);

        return new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }
}
