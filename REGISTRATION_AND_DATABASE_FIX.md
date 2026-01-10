# Registration and Database Migration Issues - RESOLVED

## Issues Fixed

### 1. Registration Error - "Email may already be in use"

**Root Cause**: Schema mismatch between AuthService migration and repository code.

**Problem**: 
- Migration defined: `EmailConfirmed`, `SecurityStamp`, `PhoneNumberConfirmed`, `LockoutEnd`, etc.
- Repository expected: `FirstName`, `LastName`, `EmailVerified`, `IsActive`, `LastLoginAt`

**Solution**: Updated `001_CreateUserTable.sql` to match actual repository usage.

**Changes Made**:
```sql
-- OLD schema (mismatched)
CREATE TABLE [User] (
    EmailConfirmed BIT,
    SecurityStamp NVARCHAR(MAX),
    LockoutEnd DATETIME2,
    ...
)

-- NEW schema (correct)
CREATE TABLE [User] (
    FirstName NVARCHAR(100) NOT NULL,
    LastName NVARCHAR(100) NOT NULL,
    EmailVerified BIT NOT NULL DEFAULT 0,
    IsActive BIT NOT NULL DEFAULT 1,
    LastLoginAt DATETIME2 NULL,
    ...
)
```

**RefreshToken table also simplified**:
```sql
-- Added IsRevoked BIT NOT NULL DEFAULT 0
-- Removed unused IP tracking and reason fields
```

### 2. Recall Table Missing Error

**Root Cause**: RecallService migrations not being executed or database not created.

**Current State**:
- Migration file exists: `src/Services/ExpressRecipe.RecallService/Data/Migrations/001_CreateRecallTables.sql`
- Program.cs runs migrations on startup
- Using custom `DatabaseMigrator` (not common `MigrationRunner`)

**Possible Causes**:
1. SQL Server container not running
2. Database connection string incorrect
3. Migrations failing silently (error logged but not thrown)
4. Database not created by Aspire

### 3. RabbitMQ Connection Error (Already Fixed in Previous Session)

**Solution Applied**: Made RabbitMQ optional with graceful degradation.

## Required Actions

### Step 1: Reset and Rebuild Auth Database

Since the schema was wrong, you need to drop and recreate the authdb:

**Option A: Via SQL Server Management Studio**
```sql
USE master;
GO
DROP DATABASE IF EXISTS [ExpressRecipe.Auth];
GO
```

**Option B: Via Docker/Aspire**
Stop the AppHost, delete SQL Server volumes, restart.

### Step 2: Verify RecallService Database Setup

The RecallService migration should run automatically. Check logs for:
```
Applying migration: 001_CreateRecallTables.sql
Migration 001_CreateRecallTables.sql applied successfully
```

If you see errors, they might be swallowed by the try-catch. 

**Enhanced logging needed**: See recommendations below.

### Step 3: Restart Application

```cmd
# Clean restart
dotnet build src\ExpressRecipe.AppHost.New\ExpressRecipe.AppHost.New.csproj
dotnet run --project src\ExpressRecipe.AppHost.New\ExpressRecipe.AppHost.New.csproj
```

## Recommendations for Better Diagnostics

### 1. Use Common MigrationRunner Everywhere

Replace custom DatabaseMigrator in RecallService with the common one:

```csharp
// OLD (RecallService specific)
var migrator = new DatabaseMigrator(connectionString, logger);
await migrator.MigrateAsync();

// NEW (Common approach)
var migrationRunner = new MigrationRunner(connectionString, logger);
var migrations = new Dictionary<string, string>
{
    ["001_CreateRecallTables"] = await File.ReadAllTextAsync(
        Path.Combine(AppContext.BaseDirectory, "Data", "Migrations", "001_CreateRecallTables.sql"))
};
await migrationRunner.ApplyMigrationsAsync(migrations);
```

### 2. Fail Fast on Migration Errors

Change from:
```csharp
catch (Exception ex)
{
    logger.LogError(ex, "Failed to run database migrations");
}
```

To:
```csharp
catch (Exception ex)
{
    logger.LogError(ex, "Failed to run database migrations");
    throw; // Don't start service with broken database
}
```

### 3. Add Health Checks for Database

```csharp
builder.Services.AddHealthChecks()
    .AddSqlServer(connectionString, name: "authdb");
```

## Testing the Fix

### 1. Test Registration

1. Navigate to: `http://localhost:51215/register`
2. Fill in form with:
   - First Name: John
   - Last Name: Doe  
   - Email: john.doe@example.com
   - Password: SecurePassword123
3. Click "Create Account"

**Expected**: Successful registration and redirect to dashboard.

### 2. Test Recall Import

Check RecallService logs for:
```
Importing 100 recent FDA food recalls
Imported recall FDA-XXXX-XXXX: [Product Description]
Import completed: X successful, 0 failed
```

## Files Modified

1. ? `src/Services/ExpressRecipe.AuthService/Data/Migrations/001_CreateUserTable.sql`
   - Updated User table schema
   - Simplified RefreshToken table

2. ? `src/Services/ExpressRecipe.NotificationService/Program.cs` (previous session)
   - Made RabbitMQ optional

3. ? `src/ExpressRecipe.Shared/Services/EventSubscriber.cs` (previous session)
   - Added retry logic and graceful failure

## Next Steps

1. **Drop and recreate authdb** to apply corrected schema
2. **Restart AppHost** to trigger all migrations
3. **Test registration** with new account
4. **Monitor logs** for any migration errors
5. **Verify Recall table** exists after startup

## Status

- ? Auth schema fixed
- ? RabbitMQ optional (from previous session)
- ?? Recall migration requires verification
- ? Awaiting database reset and testing

