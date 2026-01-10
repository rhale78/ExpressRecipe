using Microsoft.Extensions.Configuration;

namespace ExpressRecipe.Data.Common;

/// <summary>
/// Strongly-typed configuration settings from the layered config system
/// </summary>
public class GlobalSettings
{
    public DatabaseSettings Database { get; set; } = new();
    public RedisSettings Redis { get; set; } = new();
    public RabbitMQSettings RabbitMQ { get; set; } = new();
    public JwtSettings JwtSettings { get; set; } = new();
    public RateLimitingSettings RateLimiting { get; set; } = new();
    public CorsSettings Cors { get; set; } = new();
    public ResilienceSettings Resilience { get; set; } = new();
    public HealthCheckSettings HealthChecks { get; set; } = new();
    public OpenTelemetrySettings OpenTelemetry { get; set; } = new();
    public FeatureSettings Features { get; set; } = new();
}

public class DatabaseSettings
{
    public int CommandTimeout { get; set; } = 120;
    public bool EnableRetryOnFailure { get; set; } = true;
    public int MaxRetryCount { get; set; } = 3;
    public int MaxRetryDelay { get; set; } = 30;
    public bool EnableSensitiveDataLogging { get; set; } = false;
    public bool EnableDetailedErrors { get; set; } = false;
}

public class RedisSettings
{
    public int DefaultCacheExpiration { get; set; } = 300;
    public bool SlidingExpiration { get; set; } = true;
    public int AbsoluteExpirationInMinutes { get; set; } = 60;
}

public class RabbitMQSettings
{
    public int PrefetchCount { get; set; } = 10;
    public bool AutomaticRecoveryEnabled { get; set; } = true;
    public int NetworkRecoveryInterval { get; set; } = 5;
}

public class JwtSettings
{
    public string Issuer { get; set; } = "ExpressRecipe.AuthService";
    public string Audience { get; set; } = "ExpressRecipe.API";
    public string SecretKey { get; set; } = string.Empty;
    public int AccessTokenExpirationMinutes { get; set; } = 15;
    public int RefreshTokenExpirationDays { get; set; } = 7;
}

public class RateLimitingSettings
{
    public bool EnableRateLimiting { get; set; } = true;
    public int PermitLimit { get; set; } = 100;
    public int WindowSeconds { get; set; } = 60;
    public int QueueLimit { get; set; } = 10;
}

public class CorsSettings
{
    public List<string> AllowedOrigins { get; set; } = new();
    public bool AllowCredentials { get; set; } = true;
}

public class ResilienceSettings
{
    public bool EnableCircuitBreaker { get; set; } = true;
    public int CircuitBreakerFailureThreshold { get; set; } = 5;
    public int CircuitBreakerSamplingDuration { get; set; } = 60;
    public int CircuitBreakerMinimumThroughput { get; set; } = 3;
    public int CircuitBreakerBreakDuration { get; set; } = 30;
    public bool EnableRetry { get; set; } = true;
    public int RetryCount { get; set; } = 3;
    public int RetryDelaySeconds { get; set; } = 2;
    public bool EnableTimeout { get; set; } = true;
    public int TimeoutSeconds { get; set; } = 30;
}

public class HealthCheckSettings
{
    public bool EnableDetailedErrors { get; set; } = false;
    public int CacheDurationSeconds { get; set; } = 30;
}

public class OpenTelemetrySettings
{
    public string ServiceName { get; set; } = "ExpressRecipe";
    public string ServiceVersion { get; set; } = "1.0.0";
    public bool EnableTracing { get; set; } = true;
    public bool EnableMetrics { get; set; } = true;
    public bool EnableLogging { get; set; } = true;
}

public class FeatureSettings
{
    public bool EnableSwagger { get; set; } = true;
    public bool EnableMetrics { get; set; } = true;
    public bool EnableDetailedErrors { get; set; } = false;
    public bool EnableResponseCompression { get; set; } = true;
    public bool EnableHttpLogging { get; set; } = false;
}

/// <summary>
/// Extension methods for binding configuration to strongly-typed settings
/// </summary>
public static class SettingsExtensions
{
    public static GlobalSettings GetGlobalSettings(this IConfiguration configuration)
    {
        var settings = new GlobalSettings();
        configuration.Bind(settings);
        return settings;
    }

    public static T GetSettings<T>(this IConfiguration configuration, string sectionName) where T : new()
    {
        var settings = new T();
        configuration.GetSection(sectionName).Bind(settings);
        return settings;
    }
}
