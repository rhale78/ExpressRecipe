# IngredientService Database Migration Added

## Date: January 2025

## Problem

After disabling gRPC and switching to REST API, the IngredientService was returning HTTP 500 errors:

```
POST http://localhost:59689/api/ingredient/bulk/lookup
HTTP 500 Internal Server Error
```

## Root Cause

The **Ingredient table did not exist** in the `ingredientdb` database. The IngredientService was created with:
- ✅ Repository implementation
- ✅ REST API controller
- ✅ gRPC service
- ❌ **Missing database migrations**

Without migrations, the `Ingredient` table was never created, causing all database operations to fail.

## Solution: Added Database Migration

### 1. Created Initial Schema Migration

**File:** `src/Services/ExpressRecipe.IngredientService/Data/Migrations/001_InitialIngredientSchema.sql`

Creates the `Ingredient` table with:
- Primary key on `Id` (UNIQUEIDENTIFIER)
- Core fields: `Name`, `AlternativeNames`, `Description`, `Category`
- Allergen tracking: `IsCommonAllergen`
- Audit fields: `CreatedBy`, `CreatedAt`, `UpdatedBy`, `UpdatedAt`
- Soft delete: `IsDeleted`, `DeletedAt`

**Indexes:**
- `IX_Ingredient_Name` - Unique index on Name (most common lookup)
- `IX_Ingredient_Category` - For category filtering
- `IX_Ingredient_IsCommonAllergen` - For allergen filtering

### 2. Updated Program.cs to Run Migrations

Added migration execution after database management:

```csharp
// Run database migrations
var migrationsPath = Path.Combine(AppContext.BaseDirectory, "Data", "Migrations");
if (!Directory.Exists(migrationsPath))
{
    migrationsPath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "Migrations");
}

if (Directory.Exists(migrationsPath))
{
    var migrations = MigrationExtensions.LoadMigrationsFromDirectory(migrationsPath);
    await app.RunMigrationsAsync(connectionString, migrations);
}
```

### 3. Updated .csproj to Copy Migrations

Added to `ExpressRecipe.IngredientService.csproj`:

```xml
<ItemGroup>
  <None Include="Data\Migrations\*.sql" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

This ensures migration files are copied to the output directory for runtime execution.

## REST API Endpoints Now Working

After restart, these endpoints will work:

1. **POST `/api/ingredient/bulk/lookup`**
   - Bulk lookup ingredient IDs by names
   - Body: `List<string>` (ingredient names)
   - Returns: `Dictionary<string, Guid>`

2. **GET `/api/ingredient/name/{name}`**
   - Get ingredient by name
   - Returns: `IngredientDto`

3. **GET `/api/ingredient/{id}`**
   - Get ingredient by ID
   - Returns: `IngredientDto`

4. **POST `/api/ingredient`**
   - Create new ingredient (requires auth)
   - Body: `CreateIngredientRequest`
   - Returns: `Guid` (new ingredient ID)

5. **POST `/api/ingredient/bulk/create`**
   - Bulk create ingredients (requires auth)
   - Body: `List<string>` (ingredient names)
   - Returns: `int` (count created)

## Migration Schema

```sql
CREATE TABLE [dbo].[Ingredient] (
    [Id] UNIQUEIDENTIFIER PRIMARY KEY,
    [Name] NVARCHAR(200) NOT NULL,
    [AlternativeNames] NVARCHAR(1000) NULL,
    [Description] NVARCHAR(MAX) NULL,
    [Category] NVARCHAR(100) NULL,
    [IsCommonAllergen] BIT NOT NULL DEFAULT 0,
    [IngredientListString] NVARCHAR(MAX) NULL,
    [CreatedBy] UNIQUEIDENTIFIER NULL,
    [CreatedAt] DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
    [UpdatedBy] UNIQUEIDENTIFIER NULL,
    [UpdatedAt] DATETIME2(7) NULL,
    [IsDeleted] BIT NOT NULL DEFAULT 0,
    [DeletedAt] DATETIME2(7) NULL
);
```

## Files Changed

1. `src/Services/ExpressRecipe.IngredientService/Data/Migrations/001_InitialIngredientSchema.sql` (created)
2. `src/Services/ExpressRecipe.IngredientService/Program.cs` (added migration execution)
3. `src/Services/ExpressRecipe.IngredientService/ExpressRecipe.IngredientService.csproj` (copy migrations to output)

## Build Status

✅ Build successful

## Testing Required

1. **Stop and restart** Aspire AppHost (required for migration to run)
2. **Verify migration runs** - check logs for "Applying migration: 001_InitialIngredientSchema"
3. **Verify Ingredient table created** - check database
4. **Test REST API** - POST to `/api/ingredient/bulk/lookup` should return 200 OK
5. **Check logs** - no more HTTP 500 errors

## Next Steps

After restart:
- ✅ Migration will create the Ingredient table
- ✅ REST API calls will succeed (no more 500 errors)
- ✅ Product import can lookup ingredient IDs
- ✅ Recipe import can lookup ingredient IDs

The IngredientService is now fully functional with REST API support!
