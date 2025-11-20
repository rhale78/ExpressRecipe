# ExpressRecipe Auth Service

Authentication and authorization service for ExpressRecipe.

## Features

- User registration with email/password
- Login with JWT tokens
- Refresh token support
- Account lockout after failed attempts
- External login providers (OAuth) support
- Health check endpoints

## API Endpoints

### Public Endpoints

- `POST /api/auth/register` - Register new user
- `POST /api/auth/login` - Login
- `POST /api/auth/refresh` - Refresh access token
- `GET /api/auth/health` - Health check

### Protected Endpoints

- `GET /api/auth/me` - Get current user info (requires Bearer token)

## Running Locally

### Prerequisites

- .NET 9 SDK
- SQL Server (via Docker or local install)
- Aspire AppHost (for full stack)

### Option 1: Run with Aspire (Recommended)

```bash
# From repository root
cd src/ExpressRecipe.AppHost
dotnet run
```

This will start all services including Auth Service.

### Option 2: Run Standalone

```bash
cd src/Services/ExpressRecipe.AuthService

# Set connection string
dotnet user-secrets set "ConnectionStrings:authdb" "Server=localhost;Database=ExpressRecipe.Auth;User Id=sa;Password=YourPassword;TrustServerCertificate=True"

# Run migrations (see below)

# Run service
dotnet run
```

Service will be available at: `http://localhost:5100`
Swagger UI: `http://localhost:5100/swagger`

## Database Migrations

### Apply Migrations Manually

```bash
# Connect to SQL Server
sqlcmd -S localhost -U sa -P YourPassword

# Create database
CREATE DATABASE [ExpressRecipe.Auth];
GO

USE [ExpressRecipe.Auth];
GO

# Run migration scripts in order
:r Data/Migrations/001_CreateUserTable.sql
```

### Using SQL Server Management Studio

1. Connect to SQL Server
2. Create database `ExpressRecipe.Auth`
3. Open and execute migration scripts in `Data/Migrations/` folder in order

## Configuration

### appsettings.json

```json
{
  "JwtSettings": {
    "SecretKey": "your-secret-key-here",
    "Issuer": "ExpressRecipe.AuthService",
    "Audience": "ExpressRecipe.Client",
    "ExpirationMinutes": 60,
    "RefreshTokenExpirationDays": 7
  }
}
```

### User Secrets (Development)

```bash
dotnet user-secrets set "JwtSettings:SecretKey" "your-development-secret-key-at-least-32-characters-long"
dotnet user-secrets set "ConnectionStrings:authdb" "your-connection-string"
```

## Testing

### Test Registration

```bash
curl -X POST http://localhost:5100/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "password": "Password123!",
    "confirmPassword": "Password123!",
    "firstName": "Test",
    "lastName": "User"
  }'
```

### Test Login

```bash
curl -X POST http://localhost:5100/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "password": "Password123!",
    "rememberMe": false
  }'
```

### Test Protected Endpoint

```bash
# Use access token from login response
curl -X GET http://localhost:5100/api/auth/me \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN"
```

## Security Considerations

### Production Checklist

- [ ] Change `JwtSettings:SecretKey` to a strong random value (min 32 characters)
- [ ] Store secrets in Azure Key Vault or similar
- [ ] Enable HTTPS only
- [ ] Configure CORS properly (not AllowAnyOrigin)
- [ ] Enable rate limiting
- [ ] Implement account lockout policy
- [ ] Add email verification
- [ ] Add two-factor authentication
- [ ] Implement password complexity requirements
- [ ] Add security headers
- [ ] Enable audit logging
- [ ] Implement refresh token rotation
- [ ] Add brute force protection

## TODO

- [ ] Implement email verification
- [ ] Add two-factor authentication
- [ ] Implement password reset flow
- [ ] Add OAuth providers (Google, Microsoft, etc.)
- [ ] Implement refresh token rotation and storage
- [ ] Add rate limiting
- [ ] Implement account lockout after X failed attempts
- [ ] Add password complexity validation
- [ ] Implement user role management
- [ ] Add audit logging
