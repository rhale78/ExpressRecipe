# Missing Recall Tables - ROOT CAUSE AND FIX

## Problem

The Recall database was created but had no tables, causing the error:
```
Microsoft.Data.SqlClient.SqlException (0x80131904): Invalid object name 'Recall'.
```

## Root Cause

**Migration SQL files were not being copied to the output directory during build.**

The RecallService (and 11 other services) had migration SQL files in `Data/Migrations/` but their `.csproj` files were missing the ItemGroup that tells MSBuild to copy these files:

```xml
<ItemGroup>
  <None Include="Data\Migrations\*.sql" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

### Why This Matters

The `DatabaseMigrator` class looks for migration files at runtime:
```csharp
var migrationsDir = Path.Combine(AppContext.BaseDirectory, "Data", "Migrations");
```

Without copying the files, `AppContext.BaseDirectory` points to:
```
bin/Debug/net10.0/Data/Migrations/  (empty - no files!)
```

So migrations never ran, leaving databases empty.

## Services Fixed

? **Fixed 12 services** that had migrations but missing configuration:

1. ? ExpressRecipe.RecallService (manually fixed first)
2. ? ExpressRecipe.NotificationService (manually fixed)
3. ? ExpressRecipe.AnalyticsService
4. ? ExpressRecipe.CommunityService
5. ? ExpressRecipe.InventoryService
6. ? ExpressRecipe.MealPlanningService
7. ? ExpressRecipe.PriceService
8. ? ExpressRecipe.RecipeService
9. ? ExpressRecipe.ScannerService
10. ? ExpressRecipe.SearchService
11. ? ExpressRecipe.ShoppingService
12. ? ExpressRecipe.SyncService

? **Already Configured** (no changes needed):
- ExpressRecipe.AuthService
- ExpressRecipe.ProductService
- ExpressRecipe.UserService

## Solution Applied

### 1. Created PowerShell Fix Script

Created `fix-migration-files.ps1` that:
- Scans all service projects
- Checks for `Data/Migrations` directory
- Adds the ItemGroup if missing
- Creates backup before modifying

### 2. Updated All Project Files

Added to each service's `.csproj`:
```xml
<ItemGroup>
  <None Include="Data\Migrations\*.sql" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

### 3. Migration Files Now Copy Correctly

After rebuild, SQL files will be in:
```
bin/Debug/net10.0/Data/Migrations/001_CreateRecallTables.sql
```

## Required Actions

### Step 1: Clean and Rebuild

```cmd
dotnet clean
dotnet build src\ExpressRecipe.AppHost.New\ExpressRecipe.AppHost.New.csproj
```

### Step 2: Verify Migration Files Copied

```cmd
# Check RecallService
dir src\Services\ExpressRecipe.RecallService\bin\Debug\net10.0\Data\Migrations

# Should show: 001_CreateRecallTables.sql
```

### Step 3: Drop and Recreate Databases

Since migrations didn't run before, databases exist but are empty. You have two options:

**Option A: Delete All Databases (Clean Start)**
```sql
USE master;
GO
DROP DATABASE IF EXISTS [ExpressRecipe.Recalls];
DROP DATABASE IF EXISTS [ExpressRecipe.Notifications];
DROP DATABASE IF EXISTS [ExpressRecipe.Auth];
-- etc for all databases
GO
```

**Option B: Drop Just Affected Databases**
```sql
USE master;
GO
DROP DATABASE IF EXISTS [ExpressRecipe.Recalls];
DROP DATABASE IF EXISTS [ExpressRecipe.Notifications];
GO
```

### Step 4: Restart AppHost

```cmd
dotnet run --project src\ExpressRecipe.AppHost.New\ExpressRecipe.AppHost.New.csproj
```

### Step 5: Verify Migrations Ran

Check console output for:
```
[RecallService] Applying migration: 001_CreateRecallTables.sql
[RecallService] Migration 001_CreateRecallTables.sql applied successfully
```

### Step 6: Verify Tables Created

```sql
-- Connect to ExpressRecipe.Recalls database
SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES;

-- Should show:
-- Recall
-- RecallProduct  
-- RecallAlert
-- RecallSubscription
-- MigrationHistory
```

## Testing

### 1. Test Registration (AuthService)

Navigate to: `http://localhost:51215/register`
- Create account
- Should succeed (auth schema was already fixed)

### 2. Test Recall Import (RecallService)

Check RecallService logs:
```
Importing 50 recent FDA food recalls
Imported recall FDA-XXXX-XXXX: [Product Description]
Import completed: X successful, 0 failed
```

### 3. Test Other Services

Each service with migrations should log:
```
Applying migration: XXX_Migration.sql
Migration XXX_Migration.sql applied successfully
```

## Prevention

### For Future Services

When creating a new service with migrations:

1. **Always add to `.csproj`:**
```xml
<ItemGroup>
  <None Include="Data\Migrations\*.sql" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

2. **Verify after first build:**
```cmd
dir bin\Debug\net10.0\Data\Migrations
```

3. **Check migration logs** on first run

### Documentation Reference

See `src/ExpressRecipe.Data.Common/README-MIGRATIONS.md` for complete migration system guide.

## Files Modified

1. ? `src/Services/ExpressRecipe.RecallService/ExpressRecipe.RecallService.csproj`
2. ? `src/Services/ExpressRecipe.NotificationService/ExpressRecipe.NotificationService.csproj`
3. ? `src/Services/ExpressRecipe.AnalyticsService/ExpressRecipe.AnalyticsService.csproj`
4. ? `src/Services/ExpressRecipe.CommunityService/ExpressRecipe.CommunityService.csproj`
5. ? `src/Services/ExpressRecipe.InventoryService/ExpressRecipe.InventoryService.csproj`
6. ? `src/Services/ExpressRecipe.MealPlanningService/ExpressRecipe.MealPlanningService.csproj`
7. ? `src/Services/ExpressRecipe.PriceService/ExpressRecipe.PriceService.csproj`
8. ? `src/Services/ExpressRecipe.RecipeService/ExpressRecipe.RecipeService.csproj`
9. ? `src/Services/ExpressRecipe.ScannerService/ExpressRecipe.ScannerService.csproj`
10. ? `src/Services/ExpressRecipe.SearchService/ExpressRecipe.SearchService.csproj`
11. ? `src/Services/ExpressRecipe.ShoppingService/ExpressRecipe.ShoppingService.csproj`
12. ? `src/Services/ExpressRecipe.SyncService/ExpressRecipe.SyncService.csproj`

## Summary

- ? Identified: Migration SQL files not copying to output
- ? Created: PowerShell script to fix all projects
- ? Fixed: 12 service projects  
- ? Verified: Script ran successfully
- ? **Next: Clean, rebuild, and restart to apply migrations**

## Status

**Fixed but requires rebuild and restart to take effect.**

Run:
```cmd
dotnet clean
dotnet build src\ExpressRecipe.AppHost.New\ExpressRecipe.AppHost.New.csproj
dotnet run --project src\ExpressRecipe.AppHost.New\ExpressRecipe.AppHost.New.csproj
```

Then verify migrations run successfully in console logs.
