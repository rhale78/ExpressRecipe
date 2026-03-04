# Dependency Injection Fix for Batch Services

## Issue
`BatchPriceInsertService` was registered as a **singleton** but tried to inject `IPriceRepository` which is **scoped**, causing:

```
System.InvalidOperationException: Cannot consume scoped service 'IPriceRepository' from singleton 'IBatchPriceInsertService'
```

## Root Cause

**Singleton services** live for the entire application lifetime and cannot directly inject **scoped services** (which are created per HTTP request or scope).

## Solution

Changed `BatchPriceInsertService` to resolve `IPriceRepository` from a **scope** when processing batches:

### Before ❌
```csharp
public class BatchPriceInsertService
{
    private readonly IPriceRepository _priceRepository; // SCOPED - can't inject into singleton!
    
    public BatchPriceInsertService(IPriceRepository priceRepository, ...)
    {
        _priceRepository = priceRepository; // ERROR!
    }
    
    private async Task ProcessBatchAsync(PriceInsertRequest[] batch)
    {
        await _priceRepository.BulkUpsertProductPricesAsync(prices);
    }
}
```

### After ✅
```csharp
public class BatchPriceInsertService
{
    private readonly IServiceProvider _serviceProvider; // Inject service provider instead
    
    public BatchPriceInsertService(IServiceProvider serviceProvider, ...)
    {
        _serviceProvider = serviceProvider;
    }
    
    private async Task ProcessBatchAsync(PriceInsertRequest[] batch)
    {
        // Create scope and resolve repository
        using var scope = _serviceProvider.CreateScope();
        var priceRepository = scope.ServiceProvider.GetRequiredService<IPriceRepository>();
        
        await priceRepository.BulkUpsertProductPricesAsync(prices);
    }
}
```

## Why Singleton?

`BatchPriceInsertService` should be a singleton because:
- Maintains TPL Dataflow blocks and timers (application-lifetime resources)
- Tracks statistics across all requests
- Provides centralized batching for all incoming prices
- Only creates database scopes when needed (in `ProcessBatchAsync`)

## Verification

Service lifetimes in `Program.cs`:

```csharp
// ✅ CORRECT - Singleton batch service resolves scoped repository from scope
builder.Services.AddSingleton<IBatchPriceInsertService, BatchPriceInsertService>();

// ✅ CORRECT - Repository is scoped (per request)
builder.Services.AddScoped<IPriceRepository>(...);

// ✅ CORRECT - Singleton lookup service uses transient HttpClient (OK to inject)
builder.Services.AddSingleton<IBatchProductLookupService, BatchProductLookupService>();
builder.Services.AddHttpClient<IProductServiceClient, ProductServiceClient>(); // Transient
```

## Pattern to Remember

When a singleton needs a scoped service:
1. Inject `IServiceProvider` instead of the scoped service
2. Create a scope when you need the service: `using var scope = _serviceProvider.CreateScope();`
3. Resolve the service: `var service = scope.ServiceProvider.GetRequiredService<IScopedService>();`
4. Use the service within the scope
5. Scope is disposed automatically

This pattern is used throughout ExpressRecipe for background workers and singleton services that need database access.

## Related Files
- `src/Services/ExpressRecipe.PriceService/Services/BatchPriceInsertService.cs` - Fixed
- `src/Services/ExpressRecipe.PriceService/Program.cs` - Service registrations
- `src/Services/ExpressRecipe.ProductService/Workers/ProductProcessingWorker.cs` - Similar pattern
- `src/Services/ExpressRecipe.RecipeService/Workers/RecipeProcessingWorker.cs` - Similar pattern
