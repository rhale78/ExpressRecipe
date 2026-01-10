# Service Discovery Configuration Guide

## Overview
You now have **two options** for running your Blazor web app with proper service discovery.

---

## ? Option 1: Run via AppHost (Recommended)

**Best for:** Full stack development, integration testing, production-like environment

### How to Use:

1. **Set AppHost as Startup Project:**
   - Right-click `ExpressRecipe.AppHost.New` in Solution Explorer
   - Select **"Set as Startup Project"**

2. **Press F5** to start debugging (or Ctrl+F5 to run without debugging)

3. **What Happens:**
   - ? Aspire Dashboard opens at `https://localhost:17239`
   - ? Infrastructure starts: SQL Server, Redis, RabbitMQ
   - ? All 15 microservices start automatically
   - ? Blazor web app starts automatically
   - ? Service discovery resolves `http://authservice` ? actual endpoints

4. **Access Your App:**
   - Dashboard opens automatically in browser
   - Find the **webapp** resource in the dashboard
   - Click the URL (e.g., `https://localhost:5173`)
   - Your Blazor app is now connected to all services! ??

### Why This Works:
- Aspire service discovery intercepts service names like `http://authservice`
- Routes them to actual running endpoints (e.g., `http://localhost:5001`)
- No manual configuration needed
- Works exactly like production Kubernetes/Docker

---

## ? Option 2: Standalone Blazor with Fallback URLs

**Best for:** Quick frontend debugging without starting all services

### Configuration Added:
I've created **`appsettings.Development.json`** with localhost fallback URLs:

```json
{
  "Services": {
    "AuthService": "http://localhost:5001",
    "ProductService": "http://localhost:5002",
    "RecipeService": "http://localhost:5003",
    // ... all 16 services mapped to localhost ports
  }
}
```

### How It Works:
The Blazor app now uses a smart URL resolver:
1. **Check configuration** ? Use localhost URL if found
2. **Fallback to Aspire** ? Use service name (works when running via AppHost)

### To Use This Mode:

1. **Manually start services** you need (e.g., just AuthService):
   ```bash
   cd src/Services/ExpressRecipe.AuthService
   dotnet run
   ```

2. **Set Blazor as startup project** and press F5

3. **Configure ports** in `appsettings.Development.json` to match running services

### Limitations:
- ? Must manually start each service you need
- ? Port management is manual
- ? No infrastructure orchestration (SQL Server, Redis, etc.)
- ? More error-prone

---

## ?? Recommendation

**Use Option 1 (AppHost)** for development:
- ? One-click start for entire stack
- ? Aspire Dashboard for monitoring
- ? Service discovery works automatically
- ? Production-like environment
- ? Easier debugging across services

**Use Option 2 only when:**
- You need to debug a single frontend component
- You're working on UI-only changes
- Your machine can't handle all services at once

---

## Troubleshooting

### "Blazor app not starting in AppHost"
**Solution:** The app IS starting, you just need to find it in the dashboard:
1. Open Aspire Dashboard (opens automatically)
2. Look for **webapp** resource
3. Click the URL shown next to it

### "No such host is known (authservice:80)"
**Cause:** Running Blazor standalone without AppHost or fallback URLs
**Solution:** Either:
- Use AppHost (Option 1), OR
- Ensure `appsettings.Development.json` has correct URLs (Option 2)

### "Service not found in configuration"
**Cause:** Missing entry in `appsettings.Development.json`
**Solution:** Add the service URL to the `Services` section

---

## How Service Discovery Works

### When Running via AppHost:
```
Blazor requests: http://authservice
      ?
Aspire intercepts and resolves to: http://localhost:5001
      ?
Request succeeds! ?
```

### When Running Standalone (with fallback):
```
Blazor requests service URL
      ?
GetServiceUrl() checks appsettings.Development.json
      ?
Returns: http://localhost:5001
      ?
Request succeeds! ?
```

### When Running Standalone (without fallback):
```
Blazor requests: http://authservice
      ?
DNS lookup fails (no such host)
      ?
Request fails! ?
```

---

## Code Changes Made

1. ? Created `appsettings.Development.json` with localhost URLs
2. ? Added `GetServiceUrl()` helper in `Program.cs`
3. ? Updated all 16 HttpClient registrations to use helper
4. ? Updated SignalR hub URLs to use helper

### The Smart URL Resolver:
```csharp
string GetServiceUrl(string serviceName)
{
    // Try configuration first (appsettings.Development.json)
    var configUrl = builder.Configuration[$"Services:{serviceName}"];
    if (!string.IsNullOrEmpty(configUrl))
    {
        return configUrl;
    }
    
    // Fallback to Aspire service discovery
    return $"http://{serviceName.ToLowerInvariant()}";
}
```

This makes your app work in **both modes** seamlessly! ??

---

## Next Steps

1. **Try AppHost first** (highly recommended)
2. If you encounter issues, check the Aspire Dashboard logs
3. For quick frontend debugging, use standalone mode with fallbacks
4. Report any service discovery issues for troubleshooting

---

## Summary

? **You now have flexible service discovery!**
- **Best option:** Run via AppHost for full stack
- **Fallback option:** Standalone with localhost URLs
- **Smart resolver:** Works in both modes automatically

Happy coding! ??
