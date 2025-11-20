using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;
using Polly.Wrap;

namespace ExpressRecipe.Shared.Resilience;

/// <summary>
/// Centralized Polly resilience policies for HTTP communication
/// </summary>
public static class ResiliencePolicies
{
    /// <summary>
    /// Creates a combined policy with circuit breaker, retry, and timeout
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> GetHttpPolicy(ILogger logger, string policyName = "default")
    {
        var retryPolicy = GetRetryPolicy(logger, policyName);
        var circuitBreakerPolicy = GetCircuitBreakerPolicy(logger, policyName);
        var timeoutPolicy = GetTimeoutPolicy(logger, policyName);

        // Wrap policies: Timeout -> Retry -> Circuit Breaker
        return Policy.WrapAsync(timeoutPolicy, retryPolicy, circuitBreakerPolicy);
    }

    /// <summary>
    /// Retry policy with exponential backoff
    /// </summary>
    public static AsyncRetryPolicy<HttpResponseMessage> GetRetryPolicy(ILogger logger, string policyName = "default")
    {
        return Policy
            .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode && (int)r.StatusCode >= 500)
            .Or<HttpRequestException>()
            .Or<TaskCanceledException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    var statusCode = outcome.Result?.StatusCode.ToString() ?? "N/A";
                    var exception = outcome.Exception?.Message ?? "N/A";

                    logger.LogWarning(
                        "[{PolicyName}] Retry {RetryCount} after {Delay}s. Status: {StatusCode}, Exception: {Exception}",
                        policyName, retryCount, timespan.TotalSeconds, statusCode, exception);
                });
    }

    /// <summary>
    /// Circuit breaker policy to prevent cascading failures
    /// </summary>
    public static AsyncCircuitBreakerPolicy<HttpResponseMessage> GetCircuitBreakerPolicy(ILogger logger, string policyName = "default")
    {
        return Policy
            .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode && (int)r.StatusCode >= 500)
            .Or<HttpRequestException>()
            .Or<TaskCanceledException>()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (outcome, duration) =>
                {
                    var statusCode = outcome.Result?.StatusCode.ToString() ?? "N/A";
                    var exception = outcome.Exception?.Message ?? "N/A";

                    logger.LogError(
                        "[{PolicyName}] Circuit breaker opened for {Duration}s. Status: {StatusCode}, Exception: {Exception}",
                        policyName, duration.TotalSeconds, statusCode, exception);
                },
                onReset: () =>
                {
                    logger.LogInformation("[{PolicyName}] Circuit breaker reset", policyName);
                },
                onHalfOpen: () =>
                {
                    logger.LogInformation("[{PolicyName}] Circuit breaker half-open, testing service", policyName);
                });
    }

    /// <summary>
    /// Timeout policy to prevent long-running requests
    /// </summary>
    public static AsyncTimeoutPolicy<HttpResponseMessage> GetTimeoutPolicy(ILogger logger, string policyName = "default", int timeoutSeconds = 30)
    {
        return Policy
            .TimeoutAsync<HttpResponseMessage>(
                timeout: TimeSpan.FromSeconds(timeoutSeconds),
                timeoutStrategy: TimeoutStrategy.Optimistic,
                onTimeoutAsync: (context, timespan, task) =>
                {
                    logger.LogWarning(
                        "[{PolicyName}] Request timed out after {Timeout}s",
                        policyName, timespan.TotalSeconds);
                    return Task.CompletedTask;
                });
    }

    /// <summary>
    /// Bulkhead isolation policy to limit concurrent requests
    /// </summary>
    public static AsyncPolicy<HttpResponseMessage> GetBulkheadPolicy(ILogger logger, string policyName = "default", int maxParallelization = 10, int maxQueuedActions = 20)
    {
        return Policy
            .BulkheadAsync<HttpResponseMessage>(
                maxParallelization: maxParallelization,
                maxQueuingActions: maxQueuedActions,
                onBulkheadRejectedAsync: context =>
                {
                    logger.LogWarning(
                        "[{PolicyName}] Bulkhead rejected request - too many concurrent operations",
                        policyName);
                    return Task.CompletedTask;
                });
    }

    /// <summary>
    /// Creates a full resilience policy with all protections
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> GetFullResiliencePolicy(ILogger logger, string policyName = "full")
    {
        var retryPolicy = GetRetryPolicy(logger, policyName);
        var circuitBreakerPolicy = GetCircuitBreakerPolicy(logger, policyName);
        var timeoutPolicy = GetTimeoutPolicy(logger, policyName);
        var bulkheadPolicy = GetBulkheadPolicy(logger, policyName);

        // Wrap policies: Timeout -> Retry -> Circuit Breaker -> Bulkhead
        return Policy.WrapAsync(bulkheadPolicy, timeoutPolicy, retryPolicy, circuitBreakerPolicy);
    }

    /// <summary>
    /// Fallback policy to provide default response on failure
    /// </summary>
    public static AsyncFallbackPolicy<HttpResponseMessage> GetFallbackPolicy(
        ILogger logger,
        Func<HttpResponseMessage> fallbackAction,
        string policyName = "fallback")
    {
        return Policy<HttpResponseMessage>
            .Handle<Exception>()
            .FallbackAsync(
                fallbackAction: (cancellationToken) => Task.FromResult(fallbackAction()),
                onFallbackAsync: (outcome, context) =>
                {
                    logger.LogWarning(
                        "[{PolicyName}] Fallback executed. Exception: {Exception}",
                        policyName, outcome.Exception?.Message ?? "N/A");
                    return Task.CompletedTask;
                });
    }
}

/// <summary>
/// Configuration options for resilience policies
/// </summary>
public class ResiliencePolicyOptions
{
    public bool Enabled { get; set; } = true;

    // Retry settings
    public int RetryCount { get; set; } = 3;
    public int RetryBaseDelaySeconds { get; set; } = 2;

    // Circuit breaker settings
    public int CircuitBreakerFailureThreshold { get; set; } = 5;
    public int CircuitBreakerDurationSeconds { get; set; } = 30;

    // Timeout settings
    public int TimeoutSeconds { get; set; } = 30;

    // Bulkhead settings
    public int BulkheadMaxParallelization { get; set; } = 10;
    public int BulkheadMaxQueuedActions { get; set; } = 20;
}
