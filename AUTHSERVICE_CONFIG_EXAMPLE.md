# Example: Integrating Layered Configuration into AuthService

## Current Code (Before)

```csharp
using ExpressRecipe.AuthService.Data;
using ExpressRecipe.AuthService.Services;
using ExpressRecipe.Data.Common;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add Aspire service defaults (logging, telemetry, health checks)
builder.AddServiceDefaults();

// Add database connection
builder.AddSqlServerClient("authdb");

// Add Redis for caching
builder.AddRedisClient("redis");

// Configure JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"] 
    ?? Environment.GetEnvironmentVariable("JWT_SECRET_KEY") 
    ?? "development-secret-key-change-in-production-min-32-chars-required!";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"] ?? "ExpressRecipe.AuthService",
            ValidAudience = jwtSettings["Audience"] ?? "ExpressRecipe.API",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// Register repositories
var connectionString = builder.Configuration.GetConnectionString("authdb")
    ?? throw new InvalidOperationException("Database connection string 'authdb' not found");

builder.Services.AddScoped<IAuthRepository>(sp =>
    new AuthRepository(connectionString, sp.GetRequiredService<ILogger<AuthRepository>>()));

// Register services
builder.Services.AddScoped<TokenService>();

// Add controllers
builder.Services.AddControllers();

var app = builder.Build();

// Run migrations on startup
var migrator = new DatabaseMigrator(connectionString);
await migrator.MigrateAsync();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapDefaultEndpoints();

app.Run();
```

---

## Updated Code (After)

```csharp
using ExpressRecipe.AuthService.Data;
using ExpressRecipe.AuthService.Services;
using ExpressRecipe.Data.Common;  // ? For layered configuration
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ========================================
// ADD LAYERED CONFIGURATION (NEW!)
// ========================================
builder.AddLayeredConfiguration(args);  // ? ONE LINE CHANGE!

// Now settings are loaded from:
// 1. Config/appsettings.Global.json
// 2. Config/appsettings.Development.json (or Production/Staging)
// 3. Local appsettings.json
// 4. Local appsettings.Development.json
// 5. Environment variables
// 6. Command line args

// ========================================
// Get strongly-typed settings (NEW!)
// ========================================
var globalSettings = builder.Configuration.GetGlobalSettings();
var jwtSettings = globalSettings.JwtSettings;

// Register global settings for DI (NEW!)
builder.Services.AddSingleton(globalSettings);
builder.Services.AddSingleton(jwtSettings);

// ========================================
// Existing Aspire setup
// ========================================
builder.AddServiceDefaults();
builder.AddSqlServerClient("authdb");
builder.AddRedisClient("redis");

// ========================================
// Configure JWT Authentication (SIMPLIFIED!)
// ========================================
// JWT settings now come from Config/appsettings.Global.json
// Secret key from environment variable (highest priority)
var secretKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY") 
    ?? jwtSettings.SecretKey
    ?? throw new InvalidOperationException("JWT_SECRET_KEY not configured");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,          // ? From global config
            ValidAudience = jwtSettings.Audience,      // ? From global config
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// ========================================
// Register repositories
// ========================================
var connectionString = builder.Configuration.GetConnectionString("authdb")
    ?? throw new InvalidOperationException("Database connection string 'authdb' not found");

builder.Services.AddScoped<IAuthRepository>(sp =>
    new AuthRepository(connectionString, sp.GetRequiredService<ILogger<AuthRepository>>()));

// ========================================
// Register services
// ========================================
builder.Services.AddScoped<TokenService>();
builder.Services.AddControllers();

var app = builder.Build();

// ========================================
// Database management (NEW!)
// ========================================
// Optionally drop/recreate database based on Config/appsettings.DatabaseManagement.json
await app.RunDatabaseManagementAsync("AuthService");

// ========================================
// Run migrations
// ========================================
var migrator = new DatabaseMigrator(connectionString);
await migrator.MigrateAsync();

// ========================================
// Middleware & routing
// ========================================
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapDefaultEndpoints();

app.Run();
```

---

## What Changed?

### 1. Added One Line

```csharp
builder.AddLayeredConfiguration(args);  // ? This one line!
```

This automatically loads configuration from:
- `Config/appsettings.Global.json` (global defaults)
- `Config/appsettings.Development.json` (environment-specific)
- Local service configs
- Environment variables

### 2. Added Strongly-Typed Settings

```csharp
var globalSettings = builder.Configuration.GetGlobalSettings();
var jwtSettings = globalSettings.JwtSettings;

// Register for dependency injection
builder.Services.AddSingleton(globalSettings);
builder.Services.AddSingleton(jwtSettings);
```

**Benefits:**
- IntelliSense support
- Type safety
- No magic strings
- Easier refactoring

### 3. Simplified JWT Configuration

**Before:**
```csharp
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var issuer = jwtSettings["Issuer"] ?? "ExpressRecipe.AuthService";  // Magic strings!
```

**After:**
```csharp
var jwtSettings = globalSettings.JwtSettings;
var issuer = jwtSettings.Issuer;  // Type-safe, from global config!
```

### 4. Added Database Management

```csharp
await app.RunDatabaseManagementAsync("AuthService");
```

This checks `Config/appsettings.DatabaseManagement.json` and can automatically:
- Drop database (if configured)
- Recreate schema
- Run migrations

---

## Configuration Files

### Global Settings (All Services)

**File:** `Config/appsettings.Global.json`

```json
{
  "JwtSettings": {
    "Issuer": "ExpressRecipe.AuthService",
    "Audience": "ExpressRecipe.API",
    "AccessTokenExpirationMinutes": 15,
    "RefreshTokenExpirationDays": 7
  },
  "Database": {
    "CommandTimeout": 120,
    "EnableRetryOnFailure": true
  }
}
```

**Result:** All services use these JWT settings by default.

### Development Overrides

**File:** `Config/appsettings.Development.json`

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug"
    }
  },
  "Features": {
    "EnableSwagger": true,
    "EnableDetailedErrors": true
  }
}
```

**Result:** Development environment has more verbose logging and Swagger enabled.

### AuthService-Specific Settings

**File:** `src/Services/ExpressRecipe.AuthService/appsettings.json`

```json
{
  "JwtSettings": {
    "AccessTokenExpirationMinutes": 30  // Override global 15 minutes
  },
  "TokenCleanup": {
    "RunIntervalMinutes": 60,
    "DeleteExpiredTokensOlderThanDays": 30
  }
}
```

**Result:** AuthService gets longer token expiration + unique TokenCleanup settings.

---

## Using Settings in Controllers

### Before (Magic Strings)

```csharp
public class AuthController : ControllerBase
{
    private readonly IConfiguration _configuration;
    
    public AuthController(IConfiguration configuration)
    {
        _configuration = configuration;
    }
    
    [HttpGet("settings")]
    public IActionResult GetSettings()
    {
        var issuer = _configuration["JwtSettings:Issuer"];  // ? Magic string!
        var expiration = _configuration.GetValue<int>("JwtSettings:AccessTokenExpirationMinutes");
        
        return Ok(new { issuer, expiration });
    }
}
```

### After (Strongly-Typed)

```csharp
public class AuthController : ControllerBase
{
    private readonly JwtSettings _jwtSettings;
    
    public AuthController(JwtSettings jwtSettings)  // ? Injected!
    {
        _jwtSettings = jwtSettings;
    }
    
    [HttpGet("settings")]
    public IActionResult GetSettings()
    {
        return Ok(new
        {
            issuer = _jwtSettings.Issuer,              // ? Type-safe!
            expiration = _jwtSettings.AccessTokenExpirationMinutes
        });
    }
}
```

---

## Environment-Specific Behavior

### Development
```bash
dotnet run --environment Development
```

**Loads:**
1. `Config/appsettings.Global.json`
2. `Config/appsettings.Development.json` ? Overrides globals
3. Local configs
4. Environment variables

**Result:** Debug logging, Swagger enabled, detailed errors

### Production
```bash
dotnet run --environment Production
```

**Loads:**
1. `Config/appsettings.Global.json`
2. `Config/appsettings.Production.json` ? Overrides globals
3. Local configs
4. Environment variables

**Result:** Warning-only logging, Swagger disabled, errors hidden

---

## Testing

### Unit Test Configuration

```csharp
public class AuthServiceTests
{
    [Fact]
    public void JwtSettings_Loads_From_Global_Config()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("Config/appsettings.Global.json")
            .Build();
        
        var settings = configuration.GetSettings<JwtSettings>("JwtSettings");
        
        Assert.Equal("ExpressRecipe.AuthService", settings.Issuer);
        Assert.Equal(15, settings.AccessTokenExpirationMinutes);
    }
    
    [Fact]
    public void ServiceSpecific_Settings_Override_Global()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("Config/appsettings.Global.json")
            .AddJsonFile("appsettings.json")  // Service-specific
            .Build();
        
        var settings = configuration.GetSettings<JwtSettings>("JwtSettings");
        
        // AuthService overrides to 30 minutes
        Assert.Equal(30, settings.AccessTokenExpirationMinutes);
    }
}
```

---

## Database Management Example

### Drop AuthService Database on Startup (Development)

**File:** `Config/appsettings.Development.json`

```json
{
  "DatabaseManagement": {
    "DropDatabasesOnStartup": true,
    "RunMigrationsOnStartup": true,
    "Services": {
      "AuthService": {
        "DatabaseName": "ExpressRecipe.Auth",
        "EnableManagement": true  // ? Enable for AuthService
      }
    }
  }
}
```

**In Program.cs:**
```csharp
await app.RunDatabaseManagementAsync("AuthService");
```

**Result:** 
1. Drops `ExpressRecipe.Auth` database on startup
2. Migrations run immediately after, recreating schema
3. Fresh database every time you start the service

---

## Migration Checklist for AuthService

- [x] Add `using ExpressRecipe.Data.Common;`
- [x] Add `builder.AddLayeredConfiguration(args);`
- [x] Get strongly-typed settings via `GetGlobalSettings()`
- [x] Register settings for DI
- [x] Simplify JWT configuration to use typed settings
- [x] Add database management call
- [ ] Test in Development
- [ ] Test in Production
- [ ] Update AuthService documentation

---

## Benefits Realized

### Before
? JWT settings hardcoded or using magic strings  
? No central control over configuration  
? Duplicate settings in every service  
? Manual database drop required  

### After
? JWT settings from global config (one place)  
? Type-safe, IntelliSense-enabled access  
? Environment-specific behavior  
? Automatic database management  
? Single line of code to enable  

---

## Next Steps

1. **Apply to other services** - Use this example for UserService, ProductService, etc.
2. **Review global settings** - Adjust `Config/appsettings.Global.json` as needed
3. **Set environment variables** - Configure `JWT_SECRET_KEY` for production
4. **Test each environment** - Verify Dev, Staging, Production configurations

---

## Related Files

- `LAYERED_CONFIG_GUIDE.md` - Quick start guide
- `Config/README.md` - Full documentation
- `LAYERED_CONFIG_IMPLEMENTATION.md` - Implementation summary
- `src/ExpressRecipe.Data.Common/ConfigurationExtensions.cs` - Extension methods
- `src/ExpressRecipe.Data.Common/GlobalSettings.cs` - Settings classes
