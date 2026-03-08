using System.Text;
using ExpressRecipe.Shared.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace Microsoft.Extensions.Hosting;

public static class JwtAuthenticationExtensions
{
    /// <summary>
    /// Registers JWT bearer authentication that is consistent across all ExpressRecipe services.
    /// <list type="bullet">
    ///   <item>Reads the secret from <c>JwtSettings:SecretKey</c> → <c>JWT_SECRET_KEY</c> env var → dev fallback.</item>
    ///   <item>Sets a deterministic <see cref="JwtConstants.SigningKeyId"/> on the signing key so every
    ///         token carries a <c>kid</c> header that validators can match without ambiguity.</item>
    ///   <item>Adds an <see cref="TokenValidationParameters.IssuerSigningKeyResolver"/> that always
    ///         returns the configured key, bypassing <c>kid</c>-based key filtering in
    ///         <c>JsonWebTokenHandler</c> (the default in .NET 10).</item>
    /// </list>
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <param name="configureOptions">Optional per-service overrides (e.g. <c>JwtBearerEvents</c>).</param>
    public static IHostApplicationBuilder AddExpressRecipeAuthentication(
        this IHostApplicationBuilder builder,
        Action<JwtBearerOptions>? configureOptions = null)
    {
        var jwtSettings = builder.Configuration.GetSection("JwtSettings");
        var secretKey = jwtSettings["SecretKey"] ??
                        Environment.GetEnvironmentVariable("JWT_SECRET_KEY") ??
                        "development-secret-key-change-in-production-min-32-chars-required!";

        if (builder.Environment.IsProduction() &&
            (secretKey == "development-secret-key-change-in-production-min-32-chars-required!" || secretKey.Length < 32))
            throw new InvalidOperationException(
                "[FATAL] JWT_SECRET_KEY must be configured in production and be at least 32 characters.");

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
        {
            KeyId = JwtConstants.SigningKeyId
        };

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings["Issuer"] ?? "ExpressRecipe.AuthService",
                    ValidAudience = jwtSettings["Audience"] ?? "ExpressRecipe.API",
                    IssuerSigningKey = signingKey,
                    // Always resolve to the configured key regardless of kid in the token.
                    // This prevents IDX10517 from JsonWebTokenHandler's strict kid matching.
                    IssuerSigningKeyResolver = (_, _, _, _) => [signingKey],
                    ClockSkew = TimeSpan.Zero
                };

                configureOptions?.Invoke(options);
            });

        builder.Services.AddAuthorization();

        return builder;
    }
}
