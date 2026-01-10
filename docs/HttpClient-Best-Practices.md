# HttpClient Best Practices for ExpressRecipe

## Overview
This document outlines the proper HttpClient usage patterns implemented across the ExpressRecipe codebase to prevent socket exhaustion, connection failures, and other HTTP-related issues.

## The Problem

### Anti-Pattern: Direct HttpClient Instantiation
```csharp
// ? NEVER DO THIS
using var client = new HttpClient
{
    Timeout = TimeSpan.FromSeconds(30)
};
var response = await client.GetAsync("https://example.com");
```

### Why This is Bad
1. **Socket Exhaustion**: Each new HttpClient creates a new connection pool. Sockets remain in TIME_WAIT state for 240 seconds after disposal.
2. **No Connection Pooling**: Each request creates new TCP connections instead of reusing them.
3. **DNS Issues**: Doesn't benefit from DNS refresh handling, leading to stale DNS cache problems.
4. **No Resilience**: Missing retry policies, circuit breakers, and proper timeout handling.
5. **Performance**: Higher latency due to connection establishment overhead.

## The Solution: IHttpClientFactory with Named Clients

### Pattern 1: Named Clients for External APIs

#### Configuration (Program.cs)
```csharp
// Configure named client with custom resilience policies
// IMPORTANT: ConfigurePrimaryHttpMessageHandler MUST come BEFORE AddStandardResilienceHandler
builder.Services.AddHttpClient("ApiName", client =>
{
    client.BaseAddress = new Uri("https://api.example.com/");
    // ? CRITICAL: Set to Infinite - let Polly manage timeouts
    client.Timeout = Timeout.InfiniteTimeSpan;
})
.ConfigureHttpClient((sp, client) =>
{
    // Add default headers
    client.DefaultRequestHeaders.Add("User-Agent", "ExpressRecipe/1.0");
})
.ConfigurePrimaryHttpMessageHandler(() =>
{
    return new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
        MaxConnectionsPerServer = 10,
        // Connection establishment timeout
        ConnectTimeout = TimeSpan.FromSeconds(15)
    };
})
.AddStandardResilienceHandler(options =>
{
    // Timeout per individual attempt
    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(30);
    
    // Total timeout across all retry attempts
    options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(1);
    
    options.Retry.MaxRetryAttempts = 3;
    options.Retry.Delay = TimeSpan.FromSeconds(2);
    options.Retry.UseJitter = true; // Add random jitter to prevent thundering herd
});
```

**?? CRITICAL: Method Call Order**
The order of fluent method calls matters:
1. `AddHttpClient(name, config)` - Returns `IHttpClientBuilder`
2. `ConfigureHttpClient()` - Returns `IHttpClientBuilder`
3. `ConfigurePrimaryHttpMessageHandler()` - Returns `IHttpClientBuilder`
4. `AddStandardResilienceHandler()` - Returns `IHttpStandardResiliencePipelineBuilder`

Once you call `AddStandardResilienceHandler()`, you can no longer call `ConfigurePrimaryHttpMessageHandler()` because it changes the builder type. Always configure the message handler BEFORE adding resilience.

#### Usage in Service
```csharp
public class MyService
{
    private readonly IHttpClientFactory _httpClientFactory;
    
    public MyService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }
    
    public async Task<Result> FetchDataAsync()
    {
        var client = _httpClientFactory.CreateClient("ApiName");
        var response = await client.GetAsync("endpoint");
        // Process response
    }
}
```

## Critical Configuration: HttpClient.Timeout vs Resilience Timeouts

?? **IMPORTANT**: When using `AddStandardResilienceHandler()`, you MUST set `HttpClient.Timeout` to `Timeout.InfiniteTimeSpan`.

### Why?
The resilience handler (Polly) manages timeouts at the policy level. If `HttpClient.Timeout` is set to a specific value, it will conflict with Polly's timeout management and cause premature cancellations.

### Correct Pattern
```csharp
builder.Services.AddHttpClient("ApiName", client =>
{
    client.BaseAddress = new Uri("https://api.example.com/");
    // ? CRITICAL: Set to Infinite - let Polly manage timeouts
    client.Timeout = Timeout.InfiniteTimeSpan;
})
.ConfigurePrimaryHttpMessageHandler(() =>
{
    return new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
        MaxConnectionsPerServer = 10,
        // Connection establishment timeout
        ConnectTimeout = TimeSpan.FromSeconds(15)
    };
})
.AddStandardResilienceHandler(options =>
{
    // Timeout per individual attempt
    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(30);
    
    // Total timeout across all retry attempts
    options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(1);
    
    options.Retry.MaxRetryAttempts = 3;
    options.Retry.Delay = TimeSpan.FromSeconds(2);
    options.Retry.UseJitter = true; // Add random jitter to prevent thundering herd
});
```

### Wrong Pattern ?
```csharp
builder.Services.AddHttpClient("ApiName", client =>
{
    // ? WRONG: This will conflict with resilience handler timeouts
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddStandardResilienceHandler(options =>
{
    // This will be overridden by HttpClient.Timeout above!
    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(60);
});
```

### Timeout Hierarchy
When using resilience handlers, timeouts are managed in this order:

1. **ConnectTimeout** (SocketsHttpHandler) - Time to establish TCP connection
2. **AttemptTimeout** (Polly) - Timeout per individual request attempt
3. **TotalRequestTimeout** (Polly) - Total timeout including all retries
4. **HttpClient.Timeout** - Should be `Infinite` when using Polly

### Symptoms of Incorrect Configuration
- Requests timing out at exactly 10 seconds (default AttemptTimeout)
- `TimeoutRejectedException` from Polly even with configured longer timeouts
- Connection errors: "The operation was canceled"
- Logs showing: `The operation didn't complete within the allowed timeout of '00:00:10'`

## Implemented Examples

### 1. RecallService - FDA and USDA APIs

**Problem Solved**: USDA API connection was being forcibly closed due to socket exhaustion from creating new HttpClient instances.

**Configuration**:
```csharp
// FDA API - Fast, JSON responses
builder.Services.AddHttpClient("FDA", client =>
{
    client.BaseAddress = new Uri("https://api.fda.gov/");
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddStandardResilienceHandler(options =>
{
    options.Retry.MaxRetryAttempts = 3;
    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(15);
});

// USDA API - Slower, large XML responses
builder.Services.AddHttpClient("USDA", client =>
{
    client.BaseAddress = new Uri("https://www.fsis.usda.gov/");
    client.Timeout = TimeSpan.FromMinutes(2);
})
.AddStandardResilienceHandler(options =>
{
    options.Retry.MaxRetryAttempts = 3;
    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(45);
    options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(2);
})
.ConfigurePrimaryHttpMessageHandler(() =>
{
    return new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(10),
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
    };
});
```

### 2. AIService - Ollama Local AI

**Problem Solved**: Setting properties on HttpClient after creation from factory bypasses configuration.

**Configuration**:
```csharp
builder.Services.AddHttpClient("Ollama", client =>
{
    client.BaseAddress = new Uri(ollamaUrl);
    client.Timeout = TimeSpan.FromMinutes(2); // AI inference takes time
})
.ConfigurePrimaryHttpMessageHandler(() =>
{
    return new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(15),
        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5),
        MaxConnectionsPerServer = 5
    };
});
```

## Configuration Guidelines

### Timeout Settings
- **Fast APIs (JSON)**: 15-30 seconds
- **Slow APIs (Large responses)**: 1-2 minutes  
- **AI/ML Services**: 2-5 minutes
- **File Downloads**: 5-10 minutes

### Connection Pool Settings
- **PooledConnectionLifetime**: 5-15 minutes (prevents stale connections)
- **PooledConnectionIdleTimeout**: 2-5 minutes (releases unused connections)
- **MaxConnectionsPerServer**: 5-20 (based on expected concurrency)

### Retry Policy Settings
- **MaxRetryAttempts**: 3 (for transient failures)
- **Delay**: 1-3 seconds
- **BackoffType**: Exponential (for APIs with rate limiting)

### Circuit Breaker Settings
- **SamplingDuration**: 1-2 minutes
- **FailureRatio**: 0.3-0.5 (30-50% failure rate)
- **MinimumThroughput**: 3-10 requests

## Testing Recommendations

### Unit Tests
```csharp
[Fact]
public async Task TestApiCall_UsesNamedClient()
{
    // Arrange
    var mockFactory = new Mock<IHttpClientFactory>();
    var mockHandler = new MockHttpMessageHandler();
    var client = new HttpClient(mockHandler);
    
    mockFactory.Setup(f => f.CreateClient("ApiName"))
        .Returns(client);
    
    var service = new MyService(mockFactory.Object);
    
    // Act
    await service.FetchDataAsync();
    
    // Assert
    mockFactory.Verify(f => f.CreateClient("ApiName"), Times.Once);
}
```

### Integration Tests
- Test connection pooling by making multiple concurrent requests
- Verify retry behavior with transient failures
- Test timeout handling with delayed responses
- Validate DNS refresh by changing DNS records

## Monitoring

### Key Metrics to Track
1. **Active Connections**: Monitor connection pool usage
2. **Connection Lifetime**: Track how long connections are kept alive
3. **Retry Attempts**: Count retry occurrences by API
4. **Circuit Breaker State**: Monitor open/closed state
5. **Request Duration**: Track P50, P95, P99 latencies
6. **Socket Exhaustion**: Monitor available ephemeral ports

### Logging
```csharp
System.Net.Http.HttpClient.Default.LogicalHandler: Information
System.Net.Http.HttpClient.Default.ClientHandler: Information
Polly: Information
```

## Migration Checklist

When updating existing code:
- [ ] Replace `new HttpClient()` with `IHttpClientFactory`
- [ ] Configure named clients in Program.cs
- [ ] Set appropriate timeouts for the API
- [ ] Configure resilience policies (retry, circuit breaker)
- [ ] Set connection pool parameters
- [ ] Update service constructor to inject `IHttpClientFactory`
- [ ] Update methods to use `_factory.CreateClient("Name")`
- [ ] Remove direct property assignments on HttpClient
- [ ] Add proper logging
- [ ] Update unit tests to mock IHttpClientFactory

## Additional Resources

- [Microsoft Docs: IHttpClientFactory](https://learn.microsoft.com/en-us/dotnet/core/extensions/httpclient-factory)
- [Polly Resilience Documentation](https://www.pollydocs.org/)
- [HttpClient Connection Pooling](https://learn.microsoft.com/en-us/dotnet/fundamentals/networking/http/httpclient-guidelines)
- [Socket Exhaustion Issues](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/http-requests)

---

**Last Updated**: January 2026  
**Status**: Active - All services migrated to IHttpClientFactory pattern
