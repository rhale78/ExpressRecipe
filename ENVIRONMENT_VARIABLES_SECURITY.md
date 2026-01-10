# Environment Variables & Security Configuration Guide

## ?? Security Best Practices

This project now uses **environment variables** for all sensitive configuration data, ensuring secrets are never committed to git.

## Quick Start

### 1. Copy the Template
```bash
cp .env.template .env
```

### 2. Configure Your Secrets
Edit `.env` and replace placeholder values with your actual secrets:
```bash
# Minimum required for development
JWT_SECRET_KEY=your-strong-secret-key-min-64-chars
```

### 3. Run the Application
```bash
cd src/ExpressRecipe.AppHost.New
dotnet run
```

## ?? File Structure

```
ExpressRecipe/
??? .env.template          # Template (SAFE to commit)
??? .env                   # Your secrets (GIT-IGNORED)
??? .env.local             # Local overrides (GIT-IGNORED)
??? .env.production        # Production secrets (GIT-IGNORED)
??? .gitignore            # Ensures .env files are never committed
```

## ?? Environment Variables Reference

### Required for Development

#### JWT Authentication
```bash
JWT_SECRET_KEY=development-secret-key-change-in-production-min-64-chars-required!
JWT_ISSUER=ExpressRecipe.AuthService
JWT_AUDIENCE=ExpressRecipe.API
JWT_EXPIRATION_MINUTES=60
JWT_REFRESH_TOKEN_EXPIRATION_DAYS=7
```

**Security Note**: For production, generate a strong secret:
```bash
# Linux/Mac
openssl rand -base64 64

# Windows PowerShell
[Convert]::ToBase64String((1..64 | ForEach-Object { Get-Random -Maximum 256 }))
```

### Optional for Development

#### External APIs
```bash
# USDA FoodData Central
USDA_API_KEY=your_api_key

# OpenAI (for AI features)
OPENAI_API_KEY=your_openai_key

# Azure OpenAI (alternative)
AZURE_OPENAI_ENDPOINT=https://your-resource.openai.azure.com/
AZURE_OPENAI_API_KEY=your_azure_key
AZURE_OPENAI_DEPLOYMENT_NAME=gpt-4
```

## ??? How It Works

### .NET Configuration Priority

The application loads configuration in this order (later sources override earlier ones):

1. `appsettings.json` (no secrets here!)
2. `appsettings.{Environment}.json`
3. User Secrets (development only)
4. **Environment Variables** ? (our secrets live here)
5. Command-line arguments

### Reading Environment Variables in Code

The updated `Program.cs` files now use this pattern:

```csharp
// Configure JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"] 
    ?? Environment.GetEnvironmentVariable("JWT_SECRET_KEY") 
    ?? "development-secret-key-change-in-production-min-64-chars-required!";
```

This checks:
1. `appsettings.json` ? `JwtSettings:SecretKey`
2. Environment variable ? `JWT_SECRET_KEY`
3. Fallback default (development only)

### ASP.NET Core Auto-Mapping

Environment variables are automatically mapped to configuration:

| Environment Variable | Configuration Key |
|---------------------|-------------------|
| `JWT_SECRET_KEY` | `JwtSettings:SecretKey` |
| `JWT_ISSUER` | `JwtSettings:Issuer` |
| `JWT_AUDIENCE` | `JwtSettings:Audience` |

Use double underscore for nested keys:
```bash
JwtSettings__SecretKey=your-secret
```

## ?? Deployment Environments

### Local Development (.env)
- Use `.env` file with development secrets
- Safe defaults for most settings
- Aspire handles database/Redis/RabbitMQ automatically

### Production (.env.production)
- **Never commit this file!**
- Use strong secrets (64+ characters)
- Consider Azure Key Vault or AWS Secrets Manager
- Set `ASPNETCORE_ENVIRONMENT=Production`

### Docker/Container Deployment
```dockerfile
# Pass environment variables to container
docker run -d \
  -e JWT_SECRET_KEY="${JWT_SECRET_KEY}" \
  -e ASPNETCORE_ENVIRONMENT=Production \
  expressrecipe-authservice
```

### Kubernetes
```yaml
# Use Kubernetes Secrets
apiVersion: v1
kind: Secret
metadata:
  name: expressrecipe-secrets
type: Opaque
data:
  jwt-secret-key: <base64-encoded-secret>
---
apiVersion: apps/v1
kind: Deployment
spec:
  template:
    spec:
      containers:
      - name: authservice
        env:
        - name: JWT_SECRET_KEY
          valueFrom:
            secretKeyRef:
              name: expressrecipe-secrets
              key: jwt-secret-key
```

### Azure App Service
Set environment variables in Azure Portal:
```
Configuration ? Application settings ? New application setting
Name: JWT_SECRET_KEY
Value: your-production-secret
```

## ??? Security Checklist

- [x] ? All `.env` files are in `.gitignore`
- [x] ? `.env.template` contains no real secrets
- [x] ? `appsettings.json` files contain no secrets
- [x] ? JWT secrets use environment variables
- [ ] ?? Generate strong JWT secret for production
- [ ] ?? Configure external API keys as needed
- [ ] ?? Set up Azure Key Vault / AWS Secrets Manager for production
- [ ] ?? Enable HTTPS in production (`ASPNETCORE_URLS=https://+:443`)
- [ ] ?? Rotate JWT secrets periodically

## ?? Verifying Configuration

### Check if secrets are exposed in git
```bash
# Search for any committed secrets
git grep -i "JWT_SECRET_KEY"
git grep -i "SecretKey.*:"

# Should only find references in:
# - .env.template (safe - just shows placeholder)
# - This documentation
# - No actual secret values!
```

### Test environment variables are loaded
```bash
# Linux/Mac
export JWT_SECRET_KEY="test-secret-key-min-64-chars-required-for-security!"
dotnet run

# Windows PowerShell
$env:JWT_SECRET_KEY="test-secret-key-min-64-chars-required-for-security!"
dotnet run

# Windows CMD
set JWT_SECRET_KEY=test-secret-key-min-64-chars-required-for-security!
dotnet run
```

## ?? Additional Resources

- [ASP.NET Core Configuration](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/)
- [Safe storage of app secrets in development](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets)
- [Azure Key Vault](https://azure.microsoft.com/en-us/services/key-vault/)
- [Kubernetes Secrets](https://kubernetes.io/docs/concepts/configuration/secret/)

## ?? Troubleshooting

### "JWT SecretKey not configured" error
1. Check `.env` file exists in project root
2. Verify `JWT_SECRET_KEY` is set
3. Restart your IDE to reload environment
4. Check environment variable is loaded: `echo $JWT_SECRET_KEY` (Linux/Mac) or `echo %JWT_SECRET_KEY%` (Windows)

### Services can't authenticate
1. Ensure all services use the **same** `JWT_SECRET_KEY`
2. Verify `JWT_ISSUER` and `JWT_AUDIENCE` match between AuthService and other services
3. Check token hasn't expired (default 60 minutes)

### .env file not loading
1. Ensure file is named `.env` exactly (not `.env.txt`)
2. Place `.env` in the same directory as your startup project
3. For ASP.NET Core, environment variables load automatically
4. For custom loading, use `DotNetEnv` NuGet package

## ?? Migrating from appsettings.json

If you have existing secrets in `appsettings.json`:

1. **Copy secrets to `.env` file**
   ```bash
   # From appsettings.json
   "JwtSettings": {
     "SecretKey": "my-secret"
   }
   
   # To .env
   JWT_SECRET_KEY=my-secret
   ```

2. **Remove secrets from appsettings.json**
   ```bash
   # Keep only non-sensitive config
   "JwtSettings": {
     "ExpirationMinutes": 60
   }
   ```

3. **Verify git won't commit secrets**
   ```bash
   git status
   # Should show .env as untracked (git-ignored)
   ```

4. **Test the application**
   ```bash
   dotnet run
   # Verify JWT authentication works
   ```

## ? Changes Applied to This Project

### Files Updated
- ? `.gitignore` - Enhanced to ignore all `.env` variants
- ? `src/Services/ExpressRecipe.AuthService/appsettings.json` - Removed JWT secrets
- ? All service `Program.cs` files - Updated to use environment variables with fallbacks

### Files Created
- ? `.env.template` - Safe template for developers to copy
- ? `.env` - Development secrets (git-ignored)
- ? `ENVIRONMENT_VARIABLES_SECURITY.md` - This documentation

### Services Configured
- ? AuthService
- ? UserService
- ? ProductService
- ? RecipeService
- ? InventoryService (uses Authority-based auth)
- ? NotificationService (uses Authority-based auth)
- ? All other services (use Authority-based auth)

## ?? Next Steps

1. **Generate production JWT secret**
   ```bash
   openssl rand -base64 64 > jwt-secret.txt
   # Store this in your production secrets manager
   ```

2. **Set up Azure Key Vault** (recommended for production)
   ```bash
   az keyvault secret set \
     --vault-name YourKeyVault \
     --name JWT-SECRET-KEY \
     --value "your-production-secret"
   ```

3. **Configure CI/CD** to inject secrets during deployment

4. **Set up secret rotation** policy (rotate JWT secrets every 90 days)

5. **Enable audit logging** for secret access in production
