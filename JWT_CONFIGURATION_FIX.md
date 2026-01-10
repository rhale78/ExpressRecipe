# JWT Configuration Fix Applied

## Issue
Services using JWT authentication with SecretKey-based configuration were throwing startup exception:
```
System.InvalidOperationException: 'JWT SecretKey not configured'
```

## Root Cause
Services expected JWT configuration from `appsettings.json` via `JwtSettings:SecretKey`, `JwtSettings:Issuer`, and `JwtSettings:Audience`, but no appsettings files exist in the services.

## Solution Applied
Added fallback defaults to JWT configuration with this priority:
1. Configuration from appsettings.json (if present)
2. Environment variable `JWT_SECRET_KEY`
3. Development default key (for local development only)

## Services Fixed

### Services with SecretKey-based JWT (Fixed)

#### 1. RecipeService
**File:** `src/Services/ExpressRecipe.RecipeService/Program.cs`
- Added fallback defaults for SecretKey, Issuer, and Audience
- Now allows development without appsettings.json

#### 2. ProductService
**File:** `src/Services/ExpressRecipe.ProductService/Program.cs`
- Added fallback defaults for SecretKey, Issuer, and Audience
- Consistent with RecipeService configuration

#### 3. AuthService
**File:** `src/Services/ExpressRecipe.AuthService/Program.cs`
- Added fallback defaults for SecretKey, Issuer, and Audience
- Critical since this service generates JWT tokens

#### 4. UserService
**File:** `src/Services/ExpressRecipe.UserService/Program.cs`
- Added fallback defaults for SecretKey, Issuer, and Audience
- Consistent configuration across services

### Services with Authority-based JWT (No Change Needed)

These services use a simpler JWT configuration pattern with Authority URL and don't require SecretKey:

- **InventoryService** - Uses `Auth:Authority` with fallback to `http://localhost:5000`
- **NotificationService** - Uses `Auth:Authority` with fallback
- **ShoppingService** - Uses `Auth:Authority` with fallback
- **RecallService** - Uses `Auth:Authority` pattern
- **ScannerService** - Uses `Auth:Authority` pattern
- **MealPlanningService** - Uses `Auth:Authority` pattern
- **Other services** - Similar Authority-based pattern

## Code Pattern Applied

```csharp
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
```

## JWT Configuration Priority

1. **Appsettings.json** (if present)
   ```json
   {
     "JwtSettings": {
       "SecretKey": "your-secret-key-min-32-chars",
       "Issuer": "ExpressRecipe.AuthService",
       "Audience": "ExpressRecipe.API"
     }
   }
   ```

2. **Environment Variable**
   ```
   JWT_SECRET_KEY=your-production-secret-key
   ```

3. **Development Default**
   - SecretKey: `development-secret-key-change-in-production-min-32-chars-required!`
   - Issuer: `ExpressRecipe.AuthService`
   - Audience: `ExpressRecipe.API`

## Security Notes

?? **IMPORTANT**: The development default key should NEVER be used in production!

For production deployment:
- Set `JWT_SECRET_KEY` environment variable with a strong secret key
- Or provide `appsettings.Production.json` with secure JWT settings
- Ensure the same SecretKey is used across all services
- Use a minimum of 32 characters for the secret key

## JWT Configuration Patterns in Codebase

### Pattern A: Authority-Based (Simpler)
Used by most services. Validates tokens issued by the authority (AuthService).
```csharp
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.Authority = builder.Configuration["Auth:Authority"] ?? "http://localhost:5000";
        options.RequireHttpsMetadata = false;
        // ...
    });
```

### Pattern B: SecretKey-Based (More Control)
Used by AuthService (generates tokens), UserService, ProductService, RecipeService.
Requires explicit SecretKey, Issuer, and Audience configuration.

## Recommendation

Consider standardizing all services to use **Pattern A (Authority-based)** for consistency, except for AuthService which needs Pattern B since it generates tokens.

## Next Steps
1. Continue debugging services for other configuration issues
2. Verify all services can authenticate properly
3. Consider creating a shared appsettings file or using AppHost to inject JWT config
4. Test token generation and validation across services
