using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using System.Text.Json;

namespace ExpressRecipe.Shared.Middleware;

/// <summary>
/// Enforces read-only access for impersonation tokens.
/// When a JWT contains the <c>readonly=true</c> claim (set by the impersonation flow)
/// all mutating HTTP methods (POST, PUT, PATCH, DELETE) are blocked with 403 Forbidden.
/// This prevents an admin who is "viewing as" a user from accidentally or maliciously
/// making changes on their behalf.
/// </summary>
public class ReadOnlyImpersonationMiddleware
{
    private static readonly HashSet<string> MutatingMethods =
        new(StringComparer.OrdinalIgnoreCase) { "POST", "PUT", "PATCH", "DELETE" };

    private readonly RequestDelegate _next;

    public ReadOnlyImpersonationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var readonlyClaim = context.User.FindFirst("readonly")?.Value;
        if (string.Equals(readonlyClaim, "true", StringComparison.OrdinalIgnoreCase)
            && MutatingMethods.Contains(context.Request.Method))
        {
            context.Response.StatusCode = 403;
            context.Response.ContentType = "application/json";

            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                error = "Forbidden",
                message = "This impersonation token is read-only. Mutating operations are not permitted."
            }));

            return;
        }

        await _next(context);
    }
}

public static class ReadOnlyImpersonationExtensions
{
    /// <summary>
    /// Registers the <see cref="ReadOnlyImpersonationMiddleware"/> in the pipeline.
    /// Must be placed <em>after</em> <c>UseAuthentication()</c> / <c>UseAuthorization()</c>
    /// so that the JWT has already been parsed and claims are available.
    /// </summary>
    public static IApplicationBuilder UseReadOnlyImpersonation(this IApplicationBuilder app)
        => app.UseMiddleware<ReadOnlyImpersonationMiddleware>();
}
