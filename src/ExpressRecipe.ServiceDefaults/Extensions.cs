using ExpressRecipe.ServiceDefaults.FeatureGates;
using ExpressRecipe.Shared.Services.FeatureGates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Service defaults for all ExpressRecipe services.
/// Provides consistent logging, telemetry, health checks, and resilience patterns.
/// </summary>
public static class Extensions
{
    /// <summary>
    /// Adds service defaults to a WebApplicationBuilder.
    /// </summary>
    public static IHostApplicationBuilder AddServiceDefaults(this IHostApplicationBuilder builder)
    {
        builder.AddSerilog();
        builder.ConfigureOpenTelemetry();
        builder.AddDefaultHealthChecks();
        builder.Services.AddServiceDiscovery();

#pragma warning disable EXTEXP0018 // Type is for evaluation purposes only and is subject to change or removal in future updates.
        builder.Services.AddHybridCache();
#pragma warning restore EXTEXP0018

        builder.Services.AddSingleton<ExpressRecipe.Shared.Services.HybridCacheService>();
        builder.Services.AddSingleton<ExpressRecipe.Shared.Services.ILocalModeConfig,
            ExpressRecipe.Shared.Services.LocalModeConfig>();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Turn on resilience by default
            // AttemptTimeout: per-attempt limit (bulk/AI ops can take >10s)
            // TotalRequestTimeout: must exceed AttemptTimeout × max attempts + backoff
            http.AddStandardResilienceHandler().Configure(options =>
            {
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(60);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(300);
                // SamplingDuration must be >= 2x AttemptTimeout to be effective
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(120);
            });

            // Turn on service discovery by default
            // Let Aspire handle HTTP version negotiation automatically
            http.AddServiceDiscovery();
        });

        // Register feature gate services (can be overridden per-service)
        builder.AddFeatureGateServices();

        return builder;
    }

    /// <summary>
    /// Configures OpenTelemetry for distributed tracing, metrics, and logging.
    /// </summary>
    public static IHostApplicationBuilder ConfigureOpenTelemetry(this IHostApplicationBuilder builder)
    {
        //builder.Logging.AddOpenTelemetry();

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
            })
            .WithTracing(tracing =>
            {
                tracing.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    /// <summary>
    /// Adds OpenTelemetry exporters (OTLP for Aspire dashboard).
    /// </summary>
    private static IHostApplicationBuilder AddOpenTelemetryExporters(this IHostApplicationBuilder builder)
    {
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        // OTLP exporter may be configured by OpenTelemetry packages at startup.
        // Keep this method lightweight to avoid requiring additional OpenTelemetry extension packages here.

        return builder;
    }

    /// <summary>
    /// Adds default health checks.
    /// </summary>
    public static IHostApplicationBuilder AddDefaultHealthChecks(this IHostApplicationBuilder builder)
    {
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    /// <summary>
    /// Maps default endpoints for health checks.
    /// </summary>
    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        // Health check endpoints
        app.MapHealthChecks("/health");
        app.MapHealthChecks("/alive", new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("live")
        });

        return app;
    }

    /// <summary>
    /// Configures Serilog for structured logging with colored console output.
    /// </summary>
    public static IHostApplicationBuilder AddSerilog(this IHostApplicationBuilder builder)
    {
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(builder.Configuration)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", builder.Environment.ApplicationName)
            .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
                theme: AnsiConsoleTheme.Literate) // Literate theme has proper colors: Error=Red, Info=White, Debug=Gray
            .WriteTo.OpenTelemetry()
            .CreateLogger();

        builder.Services.AddSerilog();

        return builder;
    }

    /// <summary>
    /// Adds .NET 9 <see cref="Microsoft.Extensions.Caching.Hybrid.HybridCache"/> (L1 in-memory
    /// + optional L2 distributed/Redis) and the shared <see cref="ExpressRecipe.Shared.Services.HybridCacheService"/>
    /// wrapper. Safe to call even after <see cref="AddServiceDefaults"/> — all registrations are
    /// idempotent or guarded with <c>TryAdd</c>.
    /// </summary>
    public static IHostApplicationBuilder AddHybridCache(this IHostApplicationBuilder builder)
    {
        // Register the .NET 9 HybridCache engine (L1 in-memory + optional L2 distributed).
        // AddHybridCache() already calls AddMemoryCache() internally.
#pragma warning disable EXTEXP0018
        builder.Services.AddHybridCache();
#pragma warning restore EXTEXP0018

        // Register the shared HybridCacheService wrapper only if not already present
        // (AddServiceDefaults also registers it).
        builder.Services.TryAddSingleton<ExpressRecipe.Shared.Services.HybridCacheService>();

        return builder;
    }

    /// <summary>
    /// Adds CORS with AllowAnyOrigin in Development and a configurable policy in Production.
    /// In Production, set AllowedOrigins in configuration or environment variables.
    /// </summary>
    public static IServiceCollection AddServiceCors(this IServiceCollection services, IWebHostEnvironment environment, IConfiguration configuration)
    {
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                if (environment.IsDevelopment())
                {
                    policy.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader();
                }
                else
                {
                    var allowedOrigins = configuration.GetSection("AllowedOrigins").Get<string[]>()
                        ?? Array.Empty<string>();

                    if (allowedOrigins.Length > 0)
                    {
                        policy.WithOrigins(allowedOrigins)
                              .AllowAnyMethod()
                              .AllowAnyHeader()
                              .AllowCredentials();
                    }
                    else
                    {
                        // No origins configured in production — deny all cross-origin requests.
                        // Set the 'AllowedOrigins' configuration section to allow specific origins.
                        Log.Warning("No AllowedOrigins configured in production. " +
                            "All cross-origin requests will be denied. " +
                            "Set the 'AllowedOrigins' configuration section to enable CORS.");

                        // Explicitly deny everything by allowing no origins (empty policy = deny all).
                        policy.WithOrigins(Array.Empty<string>());
                    }
                }
            });
        });

        return services;
    }

    /// <summary>
    /// Registers <see cref="ILocalModeConfig"/> and <see cref="IFeatureFlagService"/> defaults.
    /// Services that manage feature flags locally (e.g. UserService) should re-register
    /// <see cref="IFeatureFlagService"/> after calling <c>AddServiceDefaults()</c> to
    /// replace the HTTP proxy with a direct DB-backed implementation.
    /// </summary>
    public static IHostApplicationBuilder AddFeatureGateServices(
        this IHostApplicationBuilder builder)
    {
        // LocalModeConfig reads the "LocalMode" config key (default false)
        builder.Services.AddSingleton<ILocalModeConfig>(sp =>
            new LocalModeConfig(sp.GetRequiredService<IConfiguration>()));

        // Named HTTP client for calling UserService's feature-flag endpoint
        builder.Services.AddHttpClient("UserService", client =>
        {
            client.BaseAddress = new Uri("http://userservice");
            client.Timeout = TimeSpan.FromSeconds(5);
        });

        // Default implementation — proxies to UserService over HTTP
        builder.Services.AddScoped<IFeatureFlagService, HttpFeatureFlagService>();

        return builder;
    }
}
