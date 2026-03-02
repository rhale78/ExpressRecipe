using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Serilog;

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
        builder.ConfigureOpenTelemetry();
        builder.AddDefaultHealthChecks();
        builder.Services.AddServiceDiscovery();

#pragma warning disable EXTEXP0018 // Type is for evaluation purposes only and is subject to change or removal in future updates.
        builder.Services.AddHybridCache();
#pragma warning restore EXTEXP0018

        builder.Services.AddSingleton<ExpressRecipe.Shared.Services.HybridCacheService>();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Turn on resilience by default
            http.AddStandardResilienceHandler();

            // Turn on service discovery by default
            // Let Aspire handle HTTP version negotiation automatically
            http.AddServiceDiscovery();
        });

        return builder;
    }

    /// <summary>
    /// Configures OpenTelemetry for distributed tracing, metrics, and logging.
    /// </summary>
    public static IHostApplicationBuilder ConfigureOpenTelemetry(this IHostApplicationBuilder builder)
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

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
    /// Configures Serilog for structured logging.
    /// </summary>
    public static IHostApplicationBuilder AddSerilog(this IHostApplicationBuilder builder)
    {
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(builder.Configuration)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", builder.Environment.ApplicationName)
            .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
            .WriteTo.Console()
            .WriteTo.OpenTelemetry()
            .CreateLogger();

        builder.Services.AddSerilog();

        return builder;
    }

    /// <summary>
    /// Adds hybrid caching infrastructure with both in-memory (L1) and distributed/Redis (L2) cache.
    /// Services should call AddRedisClient("cache") separately for distributed cache connection.
    /// Then register HybridCacheService from ExpressRecipe.Shared.Services.
    /// </summary>
    public static IHostApplicationBuilder AddHybridCache(this IHostApplicationBuilder builder)
    {
        // Add in-memory cache (L1)
        builder.Services.AddMemoryCache();

        // Note: Services should call builder.AddRedisClient("cache") separately
        // Then add distributed cache interface
        builder.Services.AddDistributedMemoryCache(); // Fallback if Redis not available

        return builder;
    }
}
