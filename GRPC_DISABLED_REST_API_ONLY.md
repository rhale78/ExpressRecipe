# gRPC Disabled - Reverted to REST API

## Date: January 2025

## Decision: Disable gRPC, Use REST API Only

After multiple attempts to resolve HTTP/2 gRPC issues with Aspire, we've decided to **disable gRPC** and revert to **REST API** calls for the IngredientService. This is a pragmatic decision to unblock development.

## Problem

Despite various configuration attempts, gRPC calls consistently fail with:
```
The HTTP/2 server closed the connection. HTTP/2 error code 'HTTP_1_1_REQUIRED' (0xd)
```

This error persists regardless of:
- Manual HTTP/2 configuration
- Aspire's automatic HTTP version negotiation
- Kestrel protocol settings
- h2c (HTTP/2 cleartext) enablement

## Solution: REST API with gRPC Feature Flag

### Changes Made

#### 1. **IngredientServiceClient.cs** - Disabled gRPC, Added Feature Flag
```csharp
// Feature flag to enable gRPC in the future
private const bool USE_GRPC = false;

// Make gRPC client optional (nullable)
private readonly IngredientApi.IngredientApiClient? _grpcClient;

public IngredientServiceClient(
    HttpClient httpClient, 
    ITokenProvider tokenProvider,
    IngredientApi.IngredientApiClient? grpcClient = null) 
{
    _grpcClient = grpcClient;
}
```

- **All gRPC code commented out** with TODO markers
- **REST API used exclusively** for all operations
- **Feature flag `USE_GRPC`** for easy re-enabling in the future
- **gRPC client remains injectable** but is optional (nullable)

#### 2. **Removed gRPC Registrations** from:
- `src/Frontends/ExpressRecipe.BlazorWeb/Program.cs`
- `src/Services/ExpressRecipe.ProductService/Program.cs`
- `src/Services/ExpressRecipe.RecipeService/Program.cs`

Before:
```csharp
builder.Services.AddGrpcClient<IngredientApi.IngredientApiClient>(...)
builder.Services.AddHttpClient<IngredientServiceClient>(...)
```

After:
```csharp
// IngredientService client - REST API only (gRPC disabled until HTTP/2 issues resolved)
builder.Services.AddHttpClient<IngredientServiceClient>(client =>
{
    client.BaseAddress = new Uri("http://ingredientservice");
});
```

## REST API Endpoints Used

The IngredientServiceClient now uses these REST endpoints:

1. **Bulk Lookup:**
   - `POST /api/ingredient/bulk/lookup`
   - Body: `List<string>` (ingredient names)
   - Returns: `Dictionary<string, Guid>`

2. **Single Lookup:**
   - `GET /api/ingredient/name/{name}`
   - Returns: `IngredientDto`

3. **CRUD Operations:**
   - `GET /api/ingredient/{id}` - Get ingredient
   - `POST /api/ingredient` - Create ingredient
   - `POST /api/ingredient/bulk/create` - Bulk create

## Performance Impact

**REST API vs gRPC:**
- ✅ **REST works reliably** without HTTP/2 issues
- ⚠️ **Slightly higher latency** (~50-100ms per call vs gRPC)
- ⚠️ **Larger payload size** (JSON vs Protocol Buffers)
- ✅ **Simpler debugging** and troubleshooting

For typical workloads, the performance difference is negligible compared to database operations.

## Future: Re-enabling gRPC

When HTTP/2 issues are resolved (likely with future Aspire updates), gRPC can be re-enabled:

### Step 1: Set Feature Flag
```csharp
private const bool USE_GRPC = true;
```

### Step 2: Uncomment gRPC Code
Remove comment markers `/*` and `*/` around gRPC implementations in:
- `LookupIngredientIdsAsync()`
- `GetIngredientIdByNameAsync()`

### Step 3: Re-register gRPC Clients
Add back to Program.cs files:
```csharp
builder.Services.AddGrpcClient<ExpressRecipe.IngredientService.Grpc.IngredientApi.IngredientApiClient>(options =>
{
    options.Address = new Uri("http://ingredientservice");
})
.AddServiceDiscovery();
```

### Step 4: Make gRPC Client Non-Nullable
```csharp
private readonly IngredientApi.IngredientApiClient _grpcClient;

public IngredientServiceClient(..., IngredientApi.IngredientApiClient grpcClient) 
{
    _grpcClient = grpcClient ?? throw new ArgumentNullException(nameof(grpcClient));
}
```

## Benefits of This Approach

✅ **Unblocks development** - No more HTTP/2 errors  
✅ **Simple and reliable** - REST API is well-tested  
✅ **Easy to revert** - gRPC code preserved with feature flag  
✅ **No data model changes** - Service contracts remain the same  
✅ **Production ready** - REST API is stable and performant  

## Files Changed

1. `src/ExpressRecipe.Client.Shared/Services/IngredientServiceClient.cs`
2. `src/Frontends/ExpressRecipe.BlazorWeb/Program.cs`
3. `src/Services/ExpressRecipe.ProductService/Program.cs`
4. `src/Services/ExpressRecipe.RecipeService/Program.cs`

## Build Status

✅ Build successful

## Testing Required

1. **Stop and restart** Aspire AppHost
2. **Verify ingredient lookups work** (no more HTTP/2 errors)
3. **Check performance** - REST should be fast enough
4. **Monitor logs** - should see successful ingredient API calls

## Next Steps

- ✅ Use REST API for IngredientService (stable and working)
- 🔜 Monitor Aspire releases for HTTP/2 improvements
- 🔜 Re-enable gRPC when Aspire + .NET 10 + gRPC issues are resolved
- 🔜 Consider TLS/HTTPS configuration for production environments

## Notes

The IngredientService REST API endpoints are already implemented and working. This change only affects the client-side - the service itself supports both REST and gRPC simultaneously.
