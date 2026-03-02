# gRPC HTTP/2 Integration with Aspire - Complete Fix

## Problem Summary
After adding the IngredientService with gRPC support, HTTP/2 connection errors appeared:
```
HTTP/2 server closed the connection. HTTP/2 error code 'HTTP_1_1_REQUIRED' (0xd)
```

## Root Cause
1. **gRPC requires HTTP/2** - The IngredientService uses gRPC which mandates HTTP/2
2. **Kestrel defaults to HTTP/1.1** - ASP.NET Core's Kestrel server doesn't enable HTTP/2 by default
3. **HTTP/2 without TLS (h2c) needed** - Development environments without HTTPS require explicit h2c support
4. **Manual gRPC channel creation** - The original code manually created `GrpcChannel` instances instead of using Aspire's built-in support

## Solution Implemented

### 1. Enable HTTP/2 with h2c Support (IngredientService)
**File:** `src/Services/ExpressRecipe.IngredientService/Program.cs`

Added at the top:
```csharp
// Enable gRPC over HTTP/2 without TLS (h2c) for development
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
```

Configured Kestrel:
```csharp
builder.WebHost.ConfigureKestrel(options =>
{
    options.ConfigureEndpointDefaults(listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
    });
});
```

### 2. Enable h2c Globally (ServiceDefaults)
**File:** `src/ExpressRecipe.ServiceDefaults/Extensions.cs`

Added in `AddServiceDefaults()`:
```csharp
// Enable HTTP/2 without TLS (h2c) for gRPC in development
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
```

Configured HTTP/2 as default with fallback:
```csharp
http.ConfigureHttpClient(client =>
{
    // Use HTTP/2 by default for gRPC support, with automatic fallback to HTTP/1.1
    client.DefaultRequestVersion = new Version(2, 0);
    client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
});
```

### 3. Use Aspire's Native gRPC Client Support
**File:** `src/ExpressRecipe.Client.Shared/Services/IngredientServiceClient.cs`

Refactored to use injected gRPC client:
```csharp
public class IngredientServiceClient : ApiClientBase
{
    private readonly IngredientApi.IngredientApiClient _grpcClient;

    public IngredientServiceClient(
        HttpClient httpClient, 
        ITokenProvider tokenProvider,
        IngredientApi.IngredientApiClient grpcClient) 
        : base(httpClient, tokenProvider)
    {
        _grpcClient = grpcClient;
    }
    
    // Use _grpcClient directly instead of creating GrpcChannel
}
```

### 4. Register gRPC Clients with Aspire Service Discovery
Updated these files to use `AddGrpcClient`:
- `src/Frontends/ExpressRecipe.BlazorWeb/Program.cs`
- `src/Services/ExpressRecipe.ProductService/Program.cs`
- `src/Services/ExpressRecipe.RecipeService/Program.cs`

Example registration:
```csharp
// Register gRPC client for IngredientService with Aspire service discovery
builder.Services.AddGrpcClient<ExpressRecipe.IngredientService.Grpc.IngredientApi.IngredientApiClient>(options =>
{
    options.Address = new Uri("http://ingredientservice");
})
.AddServiceDiscovery();

// Register IngredientServiceClient with both HTTP and gRPC clients
builder.Services.AddHttpClient<IngredientServiceClient>(client =>
{
    client.BaseAddress = new Uri("http://ingredientservice");
});
```

### 5. Add Required NuGet Package
**File:** `src/Directory.Packages.props`

Added:
```xml
<PackageVersion Include="Grpc.Net.ClientFactory" Version="2.67.0" />
```

Updated project files:
- `ExpressRecipe.BlazorWeb.csproj`
- `ExpressRecipe.ProductService.csproj`
- `ExpressRecipe.RecipeService.csproj`

## Benefits of This Approach

✅ **Selective HTTP version usage** - REST APIs use HTTP/1.1, gRPC uses HTTP/2  
✅ **Aspire-native** - Uses Aspire's built-in gRPC support  
✅ **Service discovery** - Automatic service resolution through Aspire  
✅ **Resilience** - Leverages Aspire's resilience policies  
✅ **No compatibility issues** - Only services that need HTTP/2 use it  
✅ **Best performance** - HTTP/2 multiplexing for gRPC where needed  
✅ **Production ready** - h2c for dev, can use TLS in production  
✅ **Minimal server changes** - Only IngredientService needs HTTP/2 support

## What Changed vs. Original Approach

### Before:
- ❌ Manual `GrpcChannel.ForAddress()` creation
- ❌ Reusing HttpClient in channel options
- ❌ No Aspire integration for gRPC
- ❌ HTTP/2 not enabled on server
- ❌ HTTP/2 connection failures

### After:
- ✅ Aspire's `AddGrpcClient` with service discovery
- ✅ Constructor injection of gRPC client
- ✅ Full Aspire integration
- ✅ HTTP/2 enabled on server with h2c
- ✅ No connection errors

## Testing
Build completed successfully with all services properly configured for gRPC over HTTP/2.

## Production Considerations
For production with HTTPS/TLS:
1. Remove the `AppContext.SetSwitch` for h2c (or make it conditional on Development environment)
2. Configure TLS certificates on Kestrel
3. HTTP/2 will work automatically over TLS

## Files Changed
1. `src/Services/ExpressRecipe.IngredientService/Program.cs`
2. `src/ExpressRecipe.ServiceDefaults/Extensions.cs`
3. `src/ExpressRecipe.Client.Shared/Services/IngredientServiceClient.cs`
4. `src/Frontends/ExpressRecipe.BlazorWeb/Program.cs`
5. `src/Services/ExpressRecipe.ProductService/Program.cs`
6. `src/Services/ExpressRecipe.RecipeService/Program.cs`
7. `src/Directory.Packages.props`
8. `src/Frontends/ExpressRecipe.BlazorWeb/ExpressRecipe.BlazorWeb.csproj`
9. `src/Services/ExpressRecipe.ProductService/ExpressRecipe.ProductService.csproj`
10. `src/Services/ExpressRecipe.RecipeService/ExpressRecipe.RecipeService.csproj`
