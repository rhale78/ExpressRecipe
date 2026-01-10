# Layered Configuration System - Complete Guide

## Quick Start

### 1. Add to Service (One Line!)

```csharp
// At the top of Program.cs
using ExpressRecipe.Data.Common;

var builder = WebApplication.CreateBuilder(args);
builder.AddLayeredConfiguration(args);  // ? Add this one line!
```

### 2. That's It!

Your service now automatically loads:
- ? Global defaults from `Config/appsettings.Global.json`
- ? Environment overrides from `Config/appsettings.{Environment}.json`
- ? Local settings from your service's `appsettings.json`
- ? Environment variables (for secrets)

---

## Common Operations

### Drop All Databases (Development Reset)

**File:** `Config/appsettings.Development.json`

```json
{
  "DatabaseManagement": {
    "DropDatabasesOnStartup": true,
    "RunMigrationsOnStartup": true
  }
}
```

**OR** Use the standalone script:

```bash
# PowerShell
.\scripts\drop-all-databases.ps1

# Bash
./scripts/drop-all-databases.sh
```

### Change Global Setting (All Services)

**Example:** Increase database timeout for all services

**File:** `Config/appsettings.Global.json`

```json
{
  "Database": {
    "CommandTimeout": 180  // ? Changed from 120
  }
}
```

**Result:** All services now use 180-second timeout.

### Override for One Service

**Example:** AuthService needs longer JWT expiration

**File:** `src/Services/ExpressRecipe.AuthService/appsettings.json`

```json
{
  "JwtSettings": {
    "AccessTokenExpirationMinutes": 30  // Override global 15
  }
}
```

### Enable Feature Globally

**Example:** Enable HTTP logging for all services in development

**File:** `Config/appsettings.Development.json`

```json
{
  "Features": {
    "EnableHttpLogging": true
  }
}
```

---

## Configuration Files Reference

### Global Defaults: `Config/appsettings.Global.json`

Contains base settings for all services:

```json
{
  "Logging": { "LogLevel": { "Default": "Information" } },
  "Database": { "CommandTimeout": 120, "EnableRetryOnFailure": true },
  "Redis": { "DefaultCacheExpiration": 300 },
  "RabbitMQ": { "PrefetchCount": 10 },
  "JwtSettings": { "Issuer": "ExpressRecipe.AuthService" },
  "RateLimiting": { "PermitLimit": 100 },
  "Resilience": { "EnableCircuitBreaker": true },
  "Features": { "EnableSwagger": true }
}
```

### Development: `Config/appsettings.Development.json`

Development overrides:

```json
{
  "Logging": { "LogLevel": { "Default": "Debug" } },
  "Database": { "EnableSensitiveDataLogging": true },
  "Features": { "EnableDetailedErrors": true, "EnableHttpLogging": true }
}
```

### Production: `Config/appsettings.Production.json`

Production hardening:

```json
{
  "Logging": { "LogLevel": { "Default": "Warning" } },
  "Database": { "EnableSensitiveDataLogging": false },
  "Features": { "EnableSwagger": false, "EnableDetailedErrors": false }
}
```

### Database Management: `Config/appsettings.DatabaseManagement.json`

**?? DANGER ZONE** - Database drop/recreate operations:

```json
{
  "DatabaseManagement": {
    "DropDatabasesOnStartup": false,  // ? Set true to drop DBs!
    "RecreateSchemaOnStartup": false,
    "RunMigrationsOnStartup": true,
    "Services": {
      "AuthService": {
        "DatabaseName": "ExpressRecipe.Auth",
        "EnableManagement": false  // ? Enable per-service
      }
    }
  }
}
```

---

## Code Examples

### Basic Usage

```csharp
using ExpressRecipe.Data.Common;

var builder = WebApplication.CreateBuilder(args);

// Load layered configuration
builder.AddLayeredConfiguration(args);

// Add service defaults
builder.AddServiceDefaults();

// Access settings
var jwtSettings = builder.Configuration.GetSettings<JwtSettings>("JwtSettings");
Console.WriteLine($"JWT Issuer: {jwtSettings.Issuer}");

var app = builder.Build();
app.Run();
```

### Advanced: Strongly-Typed Settings

```csharp
using ExpressRecipe.Data.Common;

var builder = WebApplication.CreateBuilder(args);
builder.AddLayeredConfiguration(args);

// Get all settings as strongly-typed object
var globalSettings = builder.Configuration.GetGlobalSettings();

// Register as singleton for DI
builder.Services.AddSingleton(globalSettings);
builder.Services.AddSingleton(globalSettings.Database);
builder.Services.AddSingleton(globalSettings.JwtSettings);

// Use in controller
public class MyController : ControllerBase
{
    private readonly DatabaseSettings _dbSettings;
    
    public MyController(DatabaseSettings dbSettings)
    {
        _dbSettings = dbSettings;
    }
    
    public IActionResult Get()
    {
        var timeout = _dbSettings.CommandTimeout;
        return Ok($"Timeout: {timeout}s");
    }
}
```

### Database Management in Service Startup

```csharp
using ExpressRecipe.Data.Common;

var builder = WebApplication.CreateBuilder(args);
builder.AddLayeredConfiguration(args);
builder.AddServiceDefaults();

var app = builder.Build();

// Run database management (drop/recreate if configured)
await app.RunDatabaseManagementAsync("AuthService");

// Then run migrations
var migrator = app.Services.GetRequiredService<DatabaseMigrator>();
await migrator.MigrateAsync();

app.Run();
```

---

## Environment Variables for Secrets

### Setting Secrets

**Windows:**
```cmd
set JWT_SECRET_KEY=my-secret-key-at-least-32-characters-long!
set SQL_SA_PASSWORD=MyStr0ng!Password
```

**Linux/Mac:**
```bash
export JWT_SECRET_KEY="my-secret-key-at-least-32-characters-long!"
export SQL_SA_PASSWORD="MyStr0ng!Password"
```

**Docker:**
```yaml
environment:
  - JWT_SECRET_KEY=my-secret-key
  - SQL_SA_PASSWORD=MyStr0ng!Password
```

### Using in Configuration

**File:** `Config/appsettings.Development.json`

```json
{
  "JwtSettings": {
    "SecretKey": "${JWT_SECRET_KEY}"  // ? Placeholder
  }
}
```

**Access in Code:**
```csharp
var secretKey = builder.Configuration["JwtSettings:SecretKey"] 
    ?? Environment.GetEnvironmentVariable("JWT_SECRET_KEY")
    ?? throw new Exception("JWT_SECRET_KEY not configured");
```

---

## Migration Steps for Existing Services

### Step 1: Update Program.cs

**Before:**
```csharp
var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
```

**After:**
```csharp
using ExpressRecipe.Data.Common;

var builder = WebApplication.CreateBuilder(args);
builder.AddLayeredConfiguration(args);  // ? Add this
builder.AddServiceDefaults();
```

### Step 2: Move Duplicate Settings

**From:** Each service's `appsettings.json`
```json
{
  "Logging": { "LogLevel": { "Default": "Information" } },
  "Database": { "CommandTimeout": 120 }
}
```

**To:** `Config/appsettings.Global.json` (one place)
```json
{
  "Logging": { "LogLevel": { "Default": "Information" } },
  "Database": { "CommandTimeout": 120 }
}
```

**Then:** Delete from service files (unless overriding)

### Step 3: Keep Service-Specific Settings

**Example:** AuthService keeps JWT-specific settings

**File:** `src/Services/ExpressRecipe.AuthService/appsettings.json`
```json
{
  "JwtSettings": {
    "AccessTokenExpirationMinutes": 30  // AuthService-specific
  },
  "TokenCleanup": {
    "RunIntervalMinutes": 60  // AuthService-only feature
  }
}
```

### Step 4: Test

```bash
# Development
dotnet run --environment Development

# Verify settings loaded
# Check logs for "Configuration loaded from Config/appsettings.Global.json"
```

---

## Troubleshooting

### Config Directory Not Found

**Error:**
```
DirectoryNotFoundException: Config directory not found at: C:\...\Config
```

**Solution:** Create `Config` folder at solution root:
```
ExpressRecipe/
??? Config/              ? Must be here
?   ??? appsettings.Global.json
?   ??? ...
??? src/
??? ExpressRecipe.sln
```

### Setting Not Applying

**Debug configuration:**

```csharp
// Add temporary debugging
var config = builder.Configuration as IConfigurationRoot;
foreach (var provider in config.Providers)
{
    Console.WriteLine($"Provider: {provider}");
}

var value = builder.Configuration["Database:CommandTimeout"];
Console.WriteLine($"Value: {value}");
```

**Check precedence:**
1. Is it in `Config/appsettings.Global.json`?
2. Is it overridden in `Config/appsettings.Development.json`?
3. Is it overridden in local `appsettings.json`?
4. Is there an environment variable?

**Remember:** Last loaded wins!

### Service-Specific Setting Not Working

**Problem:** Global setting always applies

**Solution:** Ensure local override comes AFTER global load:

```csharp
// Correct order:
builder.AddLayeredConfiguration(args);  // Loads global then local
```

**Verify file exists:**
```bash
dir src\Services\MyService\appsettings.json
```

---

## Advanced Features

### Custom Configuration Section

**Add to Global Config:**
```json
{
  "MyCustomSection": {
    "Setting1": "value1",
    "Setting2": 42
  }
}
```

**Create Settings Class:**
```csharp
public class MyCustomSettings
{
    public string Setting1 { get; set; } = string.Empty;
    public int Setting2 { get; set; }
}
```

**Load in Service:**
```csharp
var mySettings = builder.Configuration.GetSettings<MyCustomSettings>("MyCustomSection");
builder.Services.AddSingleton(mySettings);
```

### Conditional Configuration

**Load different config based on condition:**
```csharp
builder.AddLayeredConfiguration(args);

if (builder.Environment.IsDevelopment())
{
    // Additional dev-only config
    builder.Configuration.AddJsonFile("appsettings.DevLocal.json", optional: true);
}
```

### Override Config at Runtime

**From command line:**
```bash
dotnet run --Database:CommandTimeout=300 --Features:EnableSwagger=false
```

**From environment:**
```bash
export Database__CommandTimeout=300
export Features__EnableSwagger=false
dotnet run
```

---

## Best Practices Summary

### ? DO:

1. **Use global config for common settings** (logging, database, JWT)
2. **Use environment variables for secrets** (never commit secrets!)
3. **Use service-specific config for unique settings** (only that service needs)
4. **Use strongly-typed settings classes** (type safety, IntelliSense)
5. **Document custom settings** in service README

### ? DON'T:

1. **Don't duplicate settings** across services (use global config)
2. **Don't commit secrets** to JSON files (use environment variables)
3. **Don't hardcode configuration** (always use IConfiguration)
4. **Don't enable dangerous features in production** (Swagger, detailed errors)
5. **Don't forget to test each environment** (Dev, Staging, Prod)

---

## Quick Reference Commands

### Drop All Databases
```powershell
# Edit Config/appsettings.Development.json
{
  "DatabaseManagement": { "DropDatabasesOnStartup": true }
}

# Restart all services
dotnet run --project src/ExpressRecipe.AppHost.New/ExpressRecipe.AppHost.New.csproj
```

### Change Global Timeout
```json
// Edit Config/appsettings.Global.json
{ "Database": { "CommandTimeout": 180 } }
```

### Enable Feature for All Services
```json
// Edit Config/appsettings.Development.json
{ "Features": { "EnableHttpLogging": true } }
```

### Override for One Service
```json
// Edit src/Services/MyService/appsettings.json
{ "RateLimiting": { "PermitLimit": 500 } }
```

---

## Support & Questions

### Configuration Not Working?
1. Check file locations (`Config/` at solution root)
2. Verify JSON syntax (use JSON validator)
3. Check environment name (case-sensitive!)
4. Review precedence order (later files override earlier)

### Need Help?
- See `Config/README.md` for detailed documentation
- Check existing service implementations for examples
- Review `src/ExpressRecipe.Data.Common/ConfigurationExtensions.cs` for implementation

---

## Related Files

| File | Purpose |
|------|---------|
| `Config/appsettings.Global.json` | Global defaults |
| `Config/appsettings.Development.json` | Dev overrides |
| `Config/appsettings.Production.json` | Prod overrides |
| `Config/appsettings.DatabaseManagement.json` | DB operations |
| `Config/README.md` | Full documentation |
| `src/ExpressRecipe.Data.Common/ConfigurationExtensions.cs` | Loader code |
| `src/ExpressRecipe.Data.Common/GlobalSettings.cs` | Settings classes |
| `src/ExpressRecipe.Data.Common/DatabaseManager.cs` | DB management |

---

## Configuration Cheat Sheet

```csharp
// 1. Add to service
builder.AddLayeredConfiguration(args);

// 2. Get settings
var settings = builder.Configuration.GetGlobalSettings();
var dbSettings = builder.Configuration.GetSettings<DatabaseSettings>("Database");

// 3. Access values
var timeout = settings.Database.CommandTimeout;
var logLevel = builder.Configuration["Logging:LogLevel:Default"];

// 4. Register for DI
builder.Services.AddSingleton(settings);

// 5. Use in controller
public MyController(GlobalSettings settings) { }
```

**That's it! You're ready to use layered configuration! ??**
