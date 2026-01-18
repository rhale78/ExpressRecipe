# HighSpeedDAL Schema Column Name Mismatch - Critical Issue

**Date**: 2026-01-14
**Status**: 🚨 **BLOCKING** - Database schema doesn't match HighSpeedDAL conventions

---

## Problem Summary

The ProductService has a **critical schema mismatch** between:
1. **Database schema**: Uses `CreatedAt`, `UpdatedAt`, `UpdatedBy`, `DeletedAt`
2. **HighSpeedDAL generated code**: Uses `CreatedDate`, `ModifiedDate`, `ModifiedBy`, `DeletedDate`

This causes all INSERT/UPDATE operations to fail with "Invalid column name" errors.

---

## Root Cause

When `[AutoAudit]` and `[SoftDelete]` attributes are applied to entities, HighSpeedDAL auto-generates properties with **hardcoded names**:

### HighSpeedDAL Auto-Generated Properties
```csharp
[AutoAudit]  → CreatedDate, CreatedBy, ModifiedDate, ModifiedBy
[SoftDelete] → IsDeleted, DeletedDate, DeletedBy
```

### Actual Database Schema (from 001_CreateProductTables.sql)
```sql
CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
CreatedBy UNIQUEIDENTIFIER NULL,
UpdatedAt DATETIME2 NULL,
UpdatedBy UNIQUEIDENTIFIER NULL,
IsDeleted BIT NOT NULL DEFAULT 0,
DeletedAt DATETIME2 NULL
```

---

## Affected Tables

All tables in the database use the old naming convention:
- Product, Ingredient, ProductIngredient
- ProductLabel, ProductAllergen, ProductExternalLink
- ProductMetadata, ProductNutrition, ProductPrice
- ProductRating, ProductRecall, ProductImage
- ProductStaging, IngredientAllergen, etc.

---

## Recommended Solution: Database Migration

Create migration file `Data/Migrations/012_RenameAuditColumns.sql`:

```sql
-- Product table
EXEC sp_rename 'Product.CreatedAt', 'CreatedDate', 'COLUMN';
EXEC sp_rename 'Product.UpdatedAt', 'ModifiedDate', 'COLUMN';
EXEC sp_rename 'Product.UpdatedBy', 'ModifiedBy', 'COLUMN';
EXEC sp_rename 'Product.DeletedAt', 'DeletedDate', 'COLUMN';
GO

-- Ingredient table
EXEC sp_rename 'Ingredient.CreatedAt', 'CreatedDate', 'COLUMN';
EXEC sp_rename 'Ingredient.UpdatedAt', 'ModifiedDate', 'COLUMN';
EXEC sp_rename 'Ingredient.UpdatedBy', 'ModifiedBy', 'COLUMN';
EXEC sp_rename 'Ingredient.DeletedAt', 'DeletedDate', 'COLUMN';
GO

-- ProductIngredient table
EXEC sp_rename 'ProductIngredient.CreatedAt', 'CreatedDate', 'COLUMN';
EXEC sp_rename 'ProductIngredient.UpdatedAt', 'ModifiedDate', 'COLUMN';
EXEC sp_rename 'ProductIngredient.UpdatedBy', 'ModifiedBy', 'COLUMN';
EXEC sp_rename 'ProductIngredient.DeletedAt', 'DeletedDate', 'COLUMN';
GO

-- (Continue for all other tables...)
```

See full migration SQL in this document.

---

## Repository Adapter Fixes Required

After migration, update SQL queries in:
- `ProductRepositoryAdapter.cs` (lines 47-50, 97, 111, 123, 135, 148, 161-164)
- `IngredientRepositoryAdapter.cs` (lines 177, 209, 266)

Change:
- `CreatedAt` → `CreatedDate`
- `UpdatedAt` → `ModifiedDate`
- `UpdatedBy` → `ModifiedBy`
- `DeletedAt` → `DeletedDate`

---

## Why This Matters

- **Current State**: ALL HighSpeedDAL operations fail
- **Impact**: Cannot use generated DAL methods
- **Fix**: One-time migration aligns everything with framework conventions
