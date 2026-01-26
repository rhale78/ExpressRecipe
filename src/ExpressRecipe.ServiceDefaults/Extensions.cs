using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Serilog;

namespace Microsoft.Extensions.Hosting
{
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
            builder.Services.ConfigureHttpClientDefaults(http =>
            {
                // Turn on resilience by default
                http.AddStandardResilienceHandler();

                // Turn on service discovery by default
                http.AddServiceDiscovery();
            });

            return builder;
        }

        /// <summary>
        /// Configures OpenTelemetry for distributed tracing, metrics, and logging.
        /// </summary>
        public static IHostApplicationBuilder ConfigureOpenTelemetry(this IHostApplicationBuilder builder)
        {
            var enableOtel = builder.Configuration.GetValue<bool>("Features:EnableOpenTelemetry", defaultValue: true);

            if (!enableOtel)
            {
                return builder;
            }

            var otelEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] ?? "http://localhost:4317";

            OpenTelemetryBuilder otelBuilder = builder.Services.AddOpenTelemetry()
                .WithMetrics(metrics =>
                {
                    metrics.AddAspNetCoreInstrumentation()
                        .AddHttpClientInstrumentation()
                        .AddRuntimeInstrumentation()
                        .AddMeter("ExpressRecipe")
                        .AddMeter("ExpressRecipe.DAL")
                        .AddOtlpExporter(otlpOptions =>
                        {
                            otlpOptions.Endpoint = new Uri(otelEndpoint);
                        });
                })
                .WithTracing(tracing =>
                {
                    tracing.AddAspNetCoreInstrumentation()
                        .AddHttpClientInstrumentation()
                        .AddOtlpExporter(otlpOptions =>
                        {
                            otlpOptions.Endpoint = new Uri(otelEndpoint);
                        });
                });

            builder.Logging.AddOpenTelemetry(logging =>
            {
                logging.IncludeFormattedMessage = true;
                logging.IncludeScopes = true;
            });

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
        /// Keeps default providers so Aspire's dashboard log aggregation works with colors.
        /// </summary>
        public static IHostApplicationBuilder AddSerilog(this IHostApplicationBuilder builder)
        {
            // Setup Serilog for enrichment and structured logging
            LoggerConfiguration loggerConfig = new LoggerConfiguration()
                .MinimumLevel.Information()
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", builder.Environment.ApplicationName)
                .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName);

            // In development, add console output for Serilog
            if (builder.Environment.IsDevelopment())
            {
                loggerConfig.WriteTo.Console(
                    theme: Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme.Code,
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}");
            }

            Log.Logger = loggerConfig.CreateLogger();

            // Add Serilog but keep default providers so Aspire can color logs
            builder.Services.AddSerilog(Log.Logger, dispose: true);

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
}
