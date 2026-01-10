# Layered Configuration System - Implementation Summary

## Overview

A **centralized, hierarchical configuration system** has been implemented for the ExpressRecipe microservices solution, providing:

? **Single point of control** for global settings  
? **Environment-specific overrides** (Dev, Staging, Prod)  
? **Service-specific customization** when needed  
? **Strongly-typed settings** with IntelliSense support  
? **Database management utilities** for operations like drop/recreate  

---

## Files Created

### Configuration Files

| File | Purpose | Location |
|------|---------|----------|
| `appsettings.Global.json` | Global defaults for all services | `Config/` |
| `appsettings.Development.json` | Development overrides | `Config/` |
| `appsettings.Production.json` | Production overrides | `Config/` |
| `appsettings.Staging.json` | Staging overrides | `Config/` |
| `appsettings.DatabaseManagement.json` | DB operations config | `Config/` |
| `README.md` | Complete documentation | `Config/` |

### Code Files

| File | Purpose | Location |
|------|---------|----------|
| `ConfigurationExtensions.cs` | Config loader & extensions | `src/ExpressRecipe.Data.Common/` |
| `GlobalSettings.cs` | Strongly-typed settings classes | `src/ExpressRecipe.Data.Common/` |
| `DatabaseManager.cs` | DB management operations | `src/ExpressRecipe.Data.Common/` |

### Documentation Files

| File | Purpose | Location |
|------|---------|----------|
| `LAYERED_CONFIG_GUIDE.md` | Quick start & common operations | Root |
| `Config/README.md` | Full technical documentation | `Config/` |

### Utility Scripts

| File | Purpose | Location |
|------|---------|----------|
| `drop-all-databases.ps1` | Drop all DBs (PowerShell) | `scripts/` |
| `drop-all-databases.sh` | Drop all DBs (Bash) | `scripts/` |

---

## Architecture

### Configuration Hierarchy

```
???????????????????????????????????????????
? 6. Command Line Arguments (Highest)     ? ? Override everything
???????????????????????????????????????????
? 5. Environment Variables                ? ? Secrets (JWT keys, passwords)
???????????????????????????????????????????
? 4. appsettings.{Env}.json (Service)     ? ? Service + environment specific
???????????????????????????????????????????
? 3. appsettings.json (Service)           ? ? Service-specific settings
???????????????????????????????????????????
? 2. Config/appsettings.{Env}.json        ? ? Environment-specific global
???????????????????????????????????????????
? 1. Config/appsettings.Global.json       ? ? Base defaults (Lowest)
???????????????????????????????????????????
```

### Directory Structure

```
ExpressRecipe/
??? Config/                                    # ? NEW: Shared config directory
?   ??? appsettings.Global.json               # Global defaults
?   ??? appsettings.Development.json          # Dev overrides
?   ??? appsettings.Production.json           # Prod overrides
?   ??? appsettings.Staging.json              # Staging overrides
?   ??? appsettings.DatabaseManagement.json   # DB operations
?   ??? README.md                              # Full documentation
?
??? scripts/                                   # ? NEW: Utility scripts
?   ??? drop-all-databases.ps1                # Drop all DBs (PowerShell)
?   ??? drop-all-databases.sh                 # Drop all DBs (Bash)
?
??? src/
?   ??? ExpressRecipe.Data.Common/            # ? UPDATED
?   ?   ??? ConfigurationExtensions.cs        # NEW: Config loader
?   ?   ??? GlobalSettings.cs                 # NEW: Settings classes
?   ?   ??? DatabaseManager.cs                # NEW: DB management
?   ?   ??? MigrationRunner.cs                # Existing
?   ?
?   ??? Services/
?       ??? ExpressRecipe.{Service}/
?           ??? appsettings.json              # Service-specific settings
?           ??? Program.cs                    # Uses AddLayeredConfiguration()
?
??? LAYERED_CONFIG_GUIDE.md                   # ? NEW: Quick reference
```

---

## Key Features

### 1. Global Configuration

**File:** `Config/appsettings.Global.json`

All services inherit these settings:

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

**Use Case:** Change database timeout once, apply to all 15+ services.

### 2. Environment-Specific Overrides

**Development:** Verbose logging, detailed errors, Swagger enabled  
**Production:** Minimal logging, errors hidden, Swagger disabled  
**Staging:** Balance between dev and prod  

**Example:** Enable HTTP logging for all services in dev:

```json
// Config/appsettings.Development.json
{
  "Features": {
    "EnableHttpLogging": true
  }
}
```

### 3. Service-Specific Customization

Services can override global settings when needed:

```json
// src/Services/ExpressRecipe.AuthService/appsettings.json
{
  "JwtSettings": {
    "AccessTokenExpirationMinutes": 30  // Override global 15
  }
}
```

### 4. Strongly-Typed Settings

Type-safe access with IntelliSense:

```csharp
// Get all settings
var settings = builder.Configuration.GetGlobalSettings();

// Access properties
var timeout = settings.Database.CommandTimeout;
var cacheExpiration = settings.Redis.DefaultCacheExpiration;
var jwtIssuer = settings.JwtSettings.Issuer;

// Register for DI
builder.Services.AddSingleton(settings);
```

### 5. Database Management

**Single point of control** for database operations:

```json
// Config/appsettings.DatabaseManagement.json
{
  "DatabaseManagement": {
    "DropDatabasesOnStartup": false,  // ? Set true to drop all DBs!
    "RunMigrationsOnStartup": true,
    "Services": {
      "AuthService": {
        "DatabaseName": "ExpressRecipe.Auth",
        "EnableManagement": false
      }
    }
  }
}
```

**Use Case:** Drop all databases for fresh start in development.

---

## Integration Guide

### Step 1: Update Service (One Line!)

**Before:**
```csharp
var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
```

**After:**
```csharp
using ExpressRecipe.Data.Common;

var builder = WebApplication.CreateBuilder(args);
builder.AddLayeredConfiguration(args);  // ? Add this line!
builder.AddServiceDefaults();
```

### Step 2: Remove Duplicate Settings

**Before:** Settings duplicated in every service

```
src/Services/AuthService/appsettings.json      ? "Logging": { "LogLevel": ... }
src/Services/UserService/appsettings.json      ? "Logging": { "LogLevel": ... }
src/Services/ProductService/appsettings.json   ? "Logging": { "LogLevel": ... }
... (15+ services with duplicated settings)
```

**After:** Settings centralized in one place

```
Config/appsettings.Global.json                 ? "Logging": { "LogLevel": ... }
(All services inherit automatically)
```

### Step 3: Keep Service-Specific Settings

Only keep settings **unique to that service**:

```json
// src/Services/ExpressRecipe.AuthService/appsettings.json
{
  "TokenCleanup": {
    "RunIntervalMinutes": 60  // AuthService-only feature
  }
}
```

---

## Common Operations

### 1. Drop All Databases (Development Reset)

**Option A: Configuration**

```json
// Config/appsettings.Development.json
{
  "DatabaseManagement": {
    "DropDatabasesOnStartup": true
  }
}
```

Then restart services - databases will be dropped automatically.

**Option B: Script**

```powershell
# PowerShell
.\scripts\drop-all-databases.ps1

# Bash
./scripts/drop-all-databases.sh
```

### 2. Change Global Setting

**Example:** Increase timeout for all services

```json
// Config/appsettings.Global.json
{
  "Database": {
    "CommandTimeout": 180  // Changed from 120
  }
}
```

**Result:** All 15+ services now use 180-second timeout.

### 3. Enable Feature Globally

**Example:** Enable Swagger for all services in staging

```json
// Config/appsettings.Staging.json
{
  "Features": {
    "EnableSwagger": true
  }
}
```

---

## Benefits

### Before Layered Configuration

? Settings duplicated across 15+ services  
? Changes require updating multiple files  
? Inconsistencies between services  
? No central control for database operations  
? Hard to maintain environment-specific settings  

### After Layered Configuration

? **Single point of control** for common settings  
? **One change** applies to all services  
? **Consistency** across microservices  
? **Environment-specific** overrides (Dev, Staging, Prod)  
? **Database management** utilities built-in  
? **Type-safe** access with IntelliSense  
? **Service-specific** customization when needed  

---

## Settings Reference

### Global Settings Categories

| Category | Purpose | Examples |
|----------|---------|----------|
| **Logging** | Log levels, providers | `Default: Information` |
| **Database** | Connection, timeouts, retry | `CommandTimeout: 120s` |
| **Redis** | Cache expiration, options | `DefaultCacheExpiration: 300s` |
| **RabbitMQ** | Message queue settings | `PrefetchCount: 10` |
| **JwtSettings** | Authentication tokens | `AccessTokenExpiration: 15min` |
| **RateLimiting** | API throttling | `PermitLimit: 100/min` |
| **CORS** | Cross-origin rules | `AllowedOrigins: [...]` |
| **Resilience** | Circuit breaker, retry | `EnableCircuitBreaker: true` |
| **HealthChecks** | Health endpoint config | `CacheDuration: 30s` |
| **OpenTelemetry** | Observability settings | `EnableTracing: true` |
| **Features** | Feature flags | `EnableSwagger: true` |

### Strongly-Typed Classes

```csharp
GlobalSettings                  // Root settings object
??? DatabaseSettings           // Database configuration
??? RedisSettings              // Redis cache settings
??? RabbitMQSettings           // Message queue settings
??? JwtSettings                // JWT token settings
??? RateLimitingSettings       // API rate limiting
??? CorsSettings               // CORS configuration
??? ResilienceSettings         // Resilience policies
??? HealthCheckSettings        // Health check config
??? OpenTelemetrySettings      // Observability config
??? FeatureSettings            // Feature flags
```

---

## Security Considerations

### ? Best Practices

1. **Never commit secrets** to JSON files
2. **Use environment variables** for sensitive data (JWT keys, passwords)
3. **Store .env files** in `.gitignore`
4. **Use Azure Key Vault** in production
5. **Rotate secrets** regularly

### Secrets Management

**Development:**
```bash
# .env file (in .gitignore)
JWT_SECRET_KEY=dev-secret-key-at-least-32-chars
SQL_SA_PASSWORD=DevP@ssw0rd!
```

**Production:**
```bash
# Azure Key Vault
az keyvault secret set --vault-name MyVault --name JwtSecretKey --value "..."
```

**Docker:**
```yaml
# docker-compose.yml
environment:
  - JWT_SECRET_KEY=${JWT_SECRET_KEY}  # From .env file
```

---

## Testing Strategy

### Test Configuration Loading

```csharp
[Fact]
public void Configuration_Loads_Global_Settings()
{
    var config = new ConfigurationBuilder()
        .AddLayeredConfiguration(environment, null)
        .Build();
    
    var settings = config.GetGlobalSettings();
    
    Assert.NotNull(settings);
    Assert.Equal(120, settings.Database.CommandTimeout);
}
```

### Test Precedence

```csharp
[Theory]
[InlineData("Development", true)]   // EnableSwagger in dev
[InlineData("Production", false)]   // Disabled in prod
public void Configuration_Respects_Environment(string envName, bool expectedSwagger)
{
    var environment = new TestEnvironment { EnvironmentName = envName };
    var config = new ConfigurationBuilder()
        .AddLayeredConfiguration(environment, null)
        .Build();
    
    var features = config.GetSettings<FeatureSettings>("Features");
    
    Assert.Equal(expectedSwagger, features.EnableSwagger);
}
```

---

## Migration Checklist

For each service:

- [ ] Add `using ExpressRecipe.Data.Common;` to Program.cs
- [ ] Add `builder.AddLayeredConfiguration(args);` after builder creation
- [ ] Review local `appsettings.json` for duplicate settings
- [ ] Move common settings to `Config/appsettings.Global.json`
- [ ] Keep only service-specific settings locally
- [ ] Test in Development environment
- [ ] Test in Staging environment
- [ ] Verify production configuration
- [ ] Update service documentation

---

## Next Steps

### 1. Immediate (Required)

1. **Update each service** to use `AddLayeredConfiguration()`
2. **Test in development** to verify settings load correctly
3. **Drop and recreate databases** for fresh start (optional)

### 2. Short Term (Recommended)

1. **Review and consolidate** duplicate settings
2. **Set up environment variables** for secrets
3. **Test in staging** environment
4. **Update CI/CD** pipelines for environment-specific configs

### 3. Long Term (Optional)

1. **Integrate Azure Key Vault** for production secrets
2. **Add custom settings** for your specific needs
3. **Create additional scripts** for common operations
4. **Document service-specific** configuration requirements

---

## Documentation Files

| File | Purpose | Audience |
|------|---------|----------|
| `LAYERED_CONFIG_GUIDE.md` | Quick start & common tasks | Developers |
| `Config/README.md` | Full technical documentation | Architects |
| Service `appsettings.json` | Service-specific settings | Service owners |
| `appsettings.Global.json` | Global defaults reference | Everyone |

---

## Support

### Getting Help

1. **Quick reference:** See `LAYERED_CONFIG_GUIDE.md`
2. **Full docs:** See `Config/README.md`
3. **Code examples:** See `ConfigurationExtensions.cs`
4. **Existing implementations:** Check any service's `Program.cs`

### Common Issues

**Config not loading?**
- Verify `Config/` folder exists at solution root
- Check file names (case-sensitive!)
- Review environment name

**Setting not applying?**
- Check precedence order (later files override)
- Verify JSON syntax
- Use debugging code to inspect configuration

---

## Summary

The layered configuration system provides:

? **Centralized control** via `Config/` directory  
? **Environment-specific** behavior (Dev, Staging, Prod)  
? **Service customization** when needed  
? **Type-safe access** with strongly-typed classes  
? **Database management** utilities  
? **Single-line integration** per service  

**Result:** Easier to maintain, consistent behavior, single point of control for configuration changes.
