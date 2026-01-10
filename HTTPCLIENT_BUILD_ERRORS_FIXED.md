# HttpClient Configuration Build Errors - Fixed

## Problem
After implementing the HttpClient IHttpClientFactory pattern to fix the USDA connection issues, compilation errors occurred:

```
CS1929: 'IHttpStandardResiliencePipelineBuilder' does not contain a definition for 'ConfigurePrimaryHttpMessageHandler' and the best extension method overload 'HttpClientBuilderExtensions.ConfigurePrimaryHttpMessageHandler(IHttpClientBuilder, Func<IServiceProvider, HttpMessageHandler>)' requires a receiver of type 'Microsoft.Extensions.DependencyInjection.IHttpClientBuilder'
```

## Root Cause
The fluent API method call order was incorrect. When `AddStandardResilienceHandler()` is called, it returns an `IHttpStandardResiliencePipelineBuilder` instead of `IHttpClientBuilder`. The `ConfigurePrimaryHttpMessageHandler()` extension method only works on `IHttpClientBuilder`.

## Solution
Reordered the method calls to ensure `ConfigurePrimaryHttpMessageHandler()` is called BEFORE `AddStandardResilienceHandler()`.

### Before (Incorrect Order ?)
```csharp
builder.Services.AddHttpClient("FDA", client => { ... })
.ConfigureHttpClient((sp, client) => { ... })
.AddStandardResilienceHandler(options => { ... })  // Returns IHttpStandardResiliencePipelineBuilder
.ConfigurePrimaryHttpMessageHandler(() => { ... }); // ? ERROR: Can't call this on IHttpStandardResiliencePipelineBuilder
```

### After (Correct Order ?)
```csharp
builder.Services.AddHttpClient("FDA", client => { ... })
.ConfigureHttpClient((sp, client) => { ... })
.ConfigurePrimaryHttpMessageHandler(() => { ... })  // ? Call BEFORE AddStandardResilienceHandler
.AddStandardResilienceHandler(options => { ... });  // Returns IHttpStandardResiliencePipelineBuilder (last in chain)
```

## Correct Method Chain Order

For HttpClient configuration with resilience, always use this order:

1. **`AddHttpClient(name, config)`** - Creates the named client
   - Returns: `IHttpClientBuilder`
   
2. **`ConfigureHttpClient()`** (optional) - Additional client configuration
   - Returns: `IHttpClientBuilder`
   
3. **`ConfigurePrimaryHttpMessageHandler()`** - Configure connection pooling
   - Returns: `IHttpClientBuilder`
   - ?? MUST be called BEFORE resilience handler
   
4. **`AddStandardResilienceHandler()`** - Add retry/circuit breaker/timeout
   - Returns: `IHttpStandardResiliencePipelineBuilder`
   - ?? Should be LAST in the configuration chain

## Files Fixed

### src/Services/ExpressRecipe.RecallService/Program.cs
- ? Fixed FDA HttpClient configuration (lines 37-66)
- ? Fixed USDA HttpClient configuration (lines 68-103)

### docs/HttpClient-Best-Practices.md
- ? Added critical warning about method call order
- ? Updated example code to show correct pattern

## Verification
```bash
dotnet build
```
**Result**: ? Build successful - No compilation errors

## Key Takeaway
When using .NET's HttpClient resilience features, the fluent API method order matters. Always configure the primary message handler (connection pooling settings) BEFORE adding the resilience handler (retry/timeout/circuit breaker).

---

**Date**: January 2026  
**Status**: ? Fixed and Verified  
**Related Issue**: HttpRequestException - Connection forcibly closed by remote host
