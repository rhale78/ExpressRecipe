# Layered Configuration System

## Overview

ExpressRecipe uses a **layered configuration system** that provides:
- ? **Global settings** shared across all microservices
- ? **Environment-specific overrides** (Development, Staging, Production)
- ? **Service-specific customization** for individual needs
- ? **Single point of control** for common configuration changes

## Configuration Loading Order

Settings are loaded in the following order (last wins):

1. **`Config/appsettings.Global.json`** - Base settings for all services
2. **`Config/appsettings.{Environment}.json`** - Environment-specific global overrides
3. **`{ServiceDir}/appsettings.json`** - Service-specific settings
4. **`{ServiceDir}/appsettings.{Environment}.json`** - Service + environment specific
5. **Environment Variables** - Highest priority (for secrets)
6. **Command Line Arguments** - Override everything

---

## Directory Structure

```
ExpressRecipe/
??? Config/                                    # ? Shared configuration directory
?   ??? appsettings.Global.json               # Global defaults for all services
?   ??? appsettings.Development.json          # Dev environment overrides
?   ??? appsettings.Staging.json              # Staging environment overrides
?   ??? appsettings.Production.json           # Production environment overrides
?
??? src/
?   ??? Services/
?   ?   ??? ExpressRecipe.AuthService/
?   ?   ?   ??? appsettings.json              # AuthService-specific settings
?   ?   ?   ??? appsettings.Development.json  # AuthService dev overrides
?   ?   ??? ExpressRecipe.UserService/
?   ?   ?   ??? appsettings.json              # UserService-specific settings
?   ?   ??? ...
?   ?
?   ??? ExpressRecipe.Data.Common/
?       ??? ConfigurationExtensions.cs        # Configuration loader
?       ??? GlobalSettings.cs                 # Strongly-typed settings
```

---

## Global Configuration Files

### `Config/appsettings.Global.json`

Base configuration applied to **all microservices**. Includes:

- **Logging** - Default log levels
- **Database** - Connection timeout, retry policies
- **Redis** - Cache expiration settings
- **RabbitMQ** - Message queue configuration
- **JWT** - Token settings (issuer, audience, expiration)
- **Rate Limiting** - API throttling settings
- **CORS** - Cross-origin resource sharing
- **Resilience** - Circuit breaker, retry, timeout policies
- **Health Checks** - Health endpoint configuration
- **OpenTelemetry** - Observability settings
- **Features** - Feature flags (Swagger, metrics, etc.)

### `Config/appsettings.Development.json`

Development-specific overrides:
- More verbose logging (Debug level)
- Detailed error messages enabled
- Swagger UI enabled
- HTTP logging enabled
- Sensitive data logging enabled (for debugging)

### `Config/appsettings.Production.json`

Production hardening:
- Warning-level logging only
- Detailed errors disabled
- Swagger disabled
- Stricter rate limiting
- Circuit breakers enabled
- Sensitive data logging disabled

---

## Usage in Services

### For ASP.NET Core Services (WebApplicationBuilder)

```csharp
using ExpressRecipe.Data.Common;

var builder = WebApplication.CreateBuilder(args);

// Add layered configuration (replaces default config loading)
builder.AddLayeredConfiguration(args);

// Add Aspire service defaults
builder.AddServiceDefaults();

// Access settings
var globalSettings = builder.Configuration.GetGlobalSettings();
var jwtSettings = builder.Configuration.GetSettings<JwtSettings>("JwtSettings");

// Use settings
Console.WriteLine($"JWT Issuer: {jwtSettings.Issuer}");
Console.WriteLine($"Database Timeout: {globalSettings.Database.CommandTimeout}s");

var app = builder.Build();
app.Run();
```

### For Aspire-Hosted Services (IHostApplicationBuilder)

```csharp
using ExpressRecipe.Data.Common;

var builder = DistributedApplication.CreateBuilder(args);

// Services automatically load layered config via AddServiceDefaults()
// which now includes AddLayeredConfiguration()

var authService = builder.AddProject<Projects.ExpressRecipe_AuthService>("authservice");
```

---

## Strongly-Typed Settings Access

### Using GlobalSettings Class

```csharp
// Get all global settings
var settings = builder.Configuration.GetGlobalSettings();

// Access specific sections
var databaseTimeout = settings.Database.CommandTimeout;
var cacheExpiration = settings.Redis.DefaultCacheExpiration;
var jwtIssuer = settings.JwtSettings.Issuer;
```

### Using Generic GetSettings Method

```csharp
// Get specific section
var dbSettings = builder.Configuration.GetSettings<DatabaseSettings>("Database");
var jwtSettings = builder.Configuration.GetSettings<JwtSettings>("JwtSettings");

// Register as singleton
builder.Services.AddSingleton(builder.Configuration.GetGlobalSettings());
```

### Direct IConfiguration Access

```csharp
// Standard ASP.NET Core configuration access still works
var logLevel = builder.Configuration["Logging:LogLevel:Default"];
var timeout = builder.Configuration.GetValue<int>("Database:CommandTimeout");
```

---

## Common Configuration Tasks

### Changing Global Database Timeout

**File:** `Config/appsettings.Global.json`

```json
{
  "Database": {
    "CommandTimeout": 180  // ? Change from 120 to 180 seconds
  }
}
```

**Result:** All services now use 180-second timeout (unless overridden locally).

### Enabling Swagger in Production (Not Recommended)

**File:** `Config/appsettings.Production.json`

```json
{
  "Features": {
    "EnableSwagger": true  // ? Change from false to true
  }
}
```

### Override JWT Secret for Development

**File:** `Config/appsettings.Development.json`

```json
{
  "JwtSettings": {
    "SecretKey": "my-dev-secret-key-at-least-32-characters-long!"
  }
}
```

**Better:** Use environment variable:

```bash
export JWT_SECRET_KEY="my-dev-secret-key"
# or in Windows:
set JWT_SECRET_KEY=my-dev-secret-key
```

### Drop All Databases (Development)

**File:** `Config/appsettings.Development.json`

Add a custom setting:

```json
{
  "DatabaseManagement": {
    "DropDatabasesOnStartup": true,
    "RunMigrationsOnStartup": true
  }
}
```

Then in your service startup:

```csharp
var dbManagement = builder.Configuration.GetSection("DatabaseManagement");
if (dbManagement.GetValue<bool>("DropDatabasesOnStartup"))
{
    // Drop and recreate database logic
    await DropAndRecreateDatabaseAsync(connectionString);
}
```

---

## Environment-Specific Configuration

### Setting Environment Variable

**Development (launchSettings.json):**
```json
{
  "profiles": {
    "http": {
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
```

**Production (Docker/Azure):**
```bash
# Docker
docker run -e ASPNETCORE_ENVIRONMENT=Production ...

# Azure App Service
az webapp config appsettings set --settings ASPNETCORE_ENVIRONMENT=Production
```

**Command Line:**
```bash
dotnet run --environment Production
```

---

## Service-Specific Overrides

### Example: AuthService Needs Longer Token Expiration

**File:** `src/Services/ExpressRecipe.AuthService/appsettings.json`

```json
{
  "JwtSettings": {
    "AccessTokenExpirationMinutes": 30  // Override global 15 minutes
  }
}
```

### Example: RecipeService Needs Higher Rate Limit

**File:** `src/Services/ExpressRecipe.RecipeService/appsettings.json`

```json
{
  "RateLimiting": {
    "PermitLimit": 500  // Override global 100
  }
}
```

---

## Configuration Precedence Examples

### Example 1: Database Timeout

| Source | Value | Applied? |
|--------|-------|----------|
| `Config/appsettings.Global.json` | 120s | ? Overridden |
| `Config/appsettings.Development.json` | *(not set)* | - |
| `src/Services/AuthService/appsettings.json` | 180s | ? **Used** |
| Environment Variable `Database__CommandTimeout` | *(not set)* | - |

**Result:** AuthService uses **180 seconds**.

### Example 2: JWT Secret Key

| Source | Value | Applied? |
|--------|-------|----------|
| `Config/appsettings.Global.json` | *(not set)* | - |
| `Config/appsettings.Development.json` | `${JWT_SECRET_KEY}` | ? Overridden |
| Environment Variable `JWT_SECRET_KEY` | `my-secret-key` | ? **Used** |

**Result:** JWT uses **`my-secret-key`** from environment variable.

---

## Best Practices

### ? DO:
- **Store common settings in `Config/appsettings.Global.json`**
- **Use environment variables for secrets** (JWT keys, API keys, passwords)
- **Override only what's needed** in service-specific configs
- **Use strongly-typed settings classes** instead of magic strings
- **Document custom settings** in service README files

### ? DON'T:
- **Don't store secrets in JSON files** - use environment variables or Azure Key Vault
- **Don't duplicate settings** across multiple services - use global config
- **Don't hardcode configuration** - always use IConfiguration
- **Don't commit sensitive data** - use .env files (in .gitignore)

---

## Updating Service Startup Code

### Before (Standard ASP.NET Core)

```csharp
var builder = WebApplication.CreateBuilder(args);
// Configuration loaded automatically from:
// - appsettings.json
// - appsettings.{Environment}.json
// - Environment variables
```

### After (Layered Configuration)

```csharp
using ExpressRecipe.Data.Common;

var builder = WebApplication.CreateBuilder(args);

// Add layered configuration (includes global + environment + local + env vars)
builder.AddLayeredConfiguration(args);

// Rest of setup...
builder.AddServiceDefaults();
```

---

## Integration with Aspire

The layered configuration system works seamlessly with Aspire's service defaults:

```csharp
// In each service Program.cs
builder.AddLayeredConfiguration(args);  // Load configs
builder.AddServiceDefaults();           // Add Aspire defaults (includes config from above)
```

Aspire's connection strings and environment variables take **highest priority** and override all JSON settings.

---

## Troubleshooting

### Configuration Not Loading

**Error:** `DirectoryNotFoundException: Config directory not found`

**Solution:** Ensure `Config` folder exists at solution root:
```
ExpressRecipe/
??? Config/              ? Must exist here
??? src/
??? ...
```

### Settings Not Applying

**Debug configuration loading:**
```csharp
var config = builder.Configuration as IConfigurationRoot;
if (config != null)
{
    Console.WriteLine("Configuration Sources:");
    foreach (var source in config.Providers)
    {
        Console.WriteLine($"  - {source}");
    }
}

// Print specific value
var value = builder.Configuration["Database:CommandTimeout"];
Console.WriteLine($"Database:CommandTimeout = {value}");
```

### Environment Not Recognized

**Check environment name:**
```csharp
Console.WriteLine($"Environment: {builder.Environment.EnvironmentName}");
```

**Valid names:** `Development`, `Staging`, `Production` (case-sensitive)

---

## Migration Guide

### Step 1: Update Each Service

Add layered configuration to each service's `Program.cs`:

```csharp
// At the top
using ExpressRecipe.Data.Common;

// After builder creation
var builder = WebApplication.CreateBuilder(args);
builder.AddLayeredConfiguration(args);  // ? Add this line
```

### Step 2: Move Common Settings to Global Config

Identify duplicated settings across services and move to `Config/appsettings.Global.json`.

### Step 3: Remove Duplicates from Local Configs

Keep only service-specific settings in local `appsettings.json` files.

### Step 4: Test Each Environment

```bash
# Development
dotnet run --environment Development

# Production
dotnet run --environment Production
```

---

## Related Files

- `src/ExpressRecipe.Data.Common/ConfigurationExtensions.cs` - Configuration loader
- `src/ExpressRecipe.Data.Common/GlobalSettings.cs` - Strongly-typed settings classes
- `Config/appsettings.Global.json` - Global defaults
- `Config/appsettings.Development.json` - Development overrides
- `Config/appsettings.Production.json` - Production overrides

---

## Quick Reference

### Add to Service
```csharp
builder.AddLayeredConfiguration(args);
```

### Get Settings
```csharp
var settings = builder.Configuration.GetGlobalSettings();
var dbTimeout = settings.Database.CommandTimeout;
```

### Override in Service
```json
// src/Services/MyService/appsettings.json
{
  "Database": {
    "CommandTimeout": 240  // Override global 120
  }
}
```

### Secret in Environment
```bash
export JWT_SECRET_KEY="my-secret-key"
```
