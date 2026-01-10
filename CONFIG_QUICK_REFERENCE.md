# Layered Configuration - Quick Reference Card

## ?? Quick Start (3 Steps)

### 1. Add to Service
```csharp
using ExpressRecipe.Data.Common;

var builder = WebApplication.CreateBuilder(args);
builder.AddLayeredConfiguration(args);  // ? Add this!
```

### 2. Get Settings
```csharp
var settings = builder.Configuration.GetGlobalSettings();
var jwtSettings = settings.JwtSettings;
```

### 3. Done!
Settings automatically loaded from global config + environment overrides + local config.

---

## ?? File Locations

```
Config/appsettings.Global.json        ? Global defaults (all services)
Config/appsettings.Development.json   ? Dev overrides
Config/appsettings.Production.json    ? Prod overrides
{Service}/appsettings.json            ? Service-specific settings
```

---

## ?? Common Tasks

### Change Global Setting
```json
// Config/appsettings.Global.json
{ "Database": { "CommandTimeout": 180 } }
```
**Result:** All services use 180s timeout

### Override for One Service
```json
// src/Services/MyService/appsettings.json
{ "Database": { "CommandTimeout": 300 } }
```
**Result:** Only MyService uses 300s

### Drop All Databases
```powershell
.\scripts\drop-all-databases.ps1
```
**or**
```json
// Config/appsettings.Development.json
{ "DatabaseManagement": { "DropDatabasesOnStartup": true } }
```

### Set Secret (Never in JSON!)
```bash
export JWT_SECRET_KEY="my-secret-key"
```

---

## ??? Configuration Order (Last Wins)

```
1. Config/appsettings.Global.json          ? Lowest priority
2. Config/appsettings.{Environment}.json
3. {Service}/appsettings.json
4. {Service}/appsettings.{Environment}.json
5. Environment Variables
6. Command Line Arguments                  ? Highest priority
```

---

## ?? Code Examples

### Strongly-Typed Access
```csharp
var settings = builder.Configuration.GetGlobalSettings();
var timeout = settings.Database.CommandTimeout;
var issuer = settings.JwtSettings.Issuer;
```

### Dependency Injection
```csharp
builder.Services.AddSingleton(settings);
builder.Services.AddSingleton(settings.JwtSettings);

// In controller:
public MyController(JwtSettings jwt) { }
```

### Database Management
```csharp
await app.RunDatabaseManagementAsync("AuthService");
```

---

## ?? Settings Categories

| Category | Examples |
|----------|----------|
| **Logging** | Log levels |
| **Database** | Timeout, retry |
| **Redis** | Cache expiration |
| **RabbitMQ** | Prefetch count |
| **JwtSettings** | Token expiration |
| **RateLimiting** | API throttling |
| **Resilience** | Circuit breaker |
| **Features** | Feature flags |

---

## ? Best Practices

### DO
? Use global config for common settings  
? Use environment variables for secrets  
? Use strongly-typed settings  
? Override only what's needed  

### DON'T
? Duplicate settings across services  
? Commit secrets to JSON  
? Use magic strings  
? Enable dangerous features in prod  

---

## ?? Troubleshooting

### Config Not Loading?
1. Check `Config/` exists at solution root
2. Verify file names (case-sensitive!)
3. Check environment name

### Setting Not Applying?
1. Check precedence order
2. Verify JSON syntax
3. Debug with:
```csharp
var value = builder.Configuration["Database:CommandTimeout"];
Console.WriteLine($"Value: {value}");
```

---

## ?? Documentation

| File | Purpose |
|------|---------|
| `LAYERED_CONFIG_GUIDE.md` | Quick start & operations |
| `Config/README.md` | Full documentation |
| `LAYERED_CONFIG_IMPLEMENTATION.md` | Architecture |
| `AUTHSERVICE_CONFIG_EXAMPLE.md` | Service example |

---

## ?? Examples

### Global: All Services Get Setting
```json
// Config/appsettings.Global.json
{ "Logging": { "LogLevel": { "Default": "Information" } } }
```

### Environment: Dev vs Prod
```json
// Config/appsettings.Development.json
{ "Features": { "EnableSwagger": true } }

// Config/appsettings.Production.json
{ "Features": { "EnableSwagger": false } }
```

### Service: Override Global
```json
// Service/appsettings.json
{ "RateLimiting": { "PermitLimit": 500 } }  // Override global 100
```

---

## ?? Secrets

### Set Environment Variable
```bash
# Windows
set JWT_SECRET_KEY=my-secret

# Linux/Mac
export JWT_SECRET_KEY=my-secret

# Docker
-e JWT_SECRET_KEY=my-secret
```

### Access in Code
```csharp
var key = Environment.GetEnvironmentVariable("JWT_SECRET_KEY")
    ?? throw new Exception("JWT_SECRET_KEY required");
```

---

## ?? Cheat Sheet

```csharp
// 1. Load config
builder.AddLayeredConfiguration(args);

// 2. Get settings
var settings = builder.Configuration.GetGlobalSettings();

// 3. Access values
var timeout = settings.Database.CommandTimeout;

// 4. Register for DI
builder.Services.AddSingleton(settings);

// 5. Drop databases (dev only!)
await app.RunDatabaseManagementAsync("ServiceName");
```

---

## ?? One-Liners

| Task | Command |
|------|---------|
| **Add to service** | `builder.AddLayeredConfiguration(args);` |
| **Get settings** | `var s = builder.Configuration.GetGlobalSettings();` |
| **Drop DBs** | `.\scripts\drop-all-databases.ps1` |
| **Set secret** | `export JWT_SECRET_KEY="key"` |
| **Test env** | `dotnet run --environment Production` |

---

## ?? Emergency Commands

### Reset Everything
```powershell
# Drop all databases
.\scripts\drop-all-databases.ps1

# Clean build
dotnet clean
dotnet build

# Restart services
dotnet run --project src/ExpressRecipe.AppHost.New/ExpressRecipe.AppHost.New.csproj
```

### View Current Config
```csharp
// Temporary debug code
var config = builder.Configuration as IConfigurationRoot;
foreach (var provider in config.Providers)
    Console.WriteLine($"Provider: {provider}");
```

---

## ?? Pro Tips

1. **Global First**: Put common settings in global config
2. **Override Sparingly**: Only override what's truly different
3. **Type Safety**: Always use strongly-typed settings
4. **Secrets Out**: Never commit secrets, use env vars
5. **Test Envs**: Always test Dev, Staging, Prod

---

**Need more info?** See `LAYERED_CONFIG_GUIDE.md` ??
