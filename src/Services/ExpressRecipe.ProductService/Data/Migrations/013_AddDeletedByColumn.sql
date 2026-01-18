-- =================================================================
-- Migration: Add DeletedBy Column to All Tables
-- Date: 2026-01-14
-- Purpose: Add missing DeletedBy column required by HighSpeedDAL [SoftDelete] attribute
-- =================================================================

-- HighSpeedDAL [SoftDelete] generates: IsDeleted, DeletedDate, DeletedBy
-- Original schema only had: IsDeleted, DeletedAt (now DeletedDate)
-- This migration adds the missing DeletedBy column

PRINT 'Adding DeletedBy column to all tables...';
GO

-- Product table
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Product' AND COLUMN_NAME = 'DeletedBy')
BEGIN
    ALTER TABLE Product ADD DeletedBy UNIQUEIDENTIFIER NULL;
    PRINT 'Added DeletedBy to Product table';
END
GO

-- Ingredient table
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Ingredient' AND COLUMN_NAME = 'DeletedBy')
BEGIN
    ALTER TABLE Ingredient ADD DeletedBy UNIQUEIDENTIFIER NULL;
    PRINT 'Added DeletedBy to Ingredient table';
END
GO

-- ProductIngredient table
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ProductIngredient' AND COLUMN_NAME = 'DeletedBy')
BEGIN
    ALTER TABLE ProductIngredient ADD DeletedBy UNIQUEIDENTIFIER NULL;
    PRINT 'Added DeletedBy to ProductIngredient table';
END
GO

-- ProductLabel table
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ProductLabel' AND COLUMN_NAME = 'DeletedBy')
BEGIN
    ALTER TABLE ProductLabel ADD DeletedBy UNIQUEIDENTIFIER NULL;
    PRINT 'Added DeletedBy to ProductLabel table';
END
GO

-- ProductAllergen table
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ProductAllergen' AND COLUMN_NAME = 'DeletedBy')
BEGIN
    ALTER TABLE ProductAllergen ADD DeletedBy UNIQUEIDENTIFIER NULL;
    PRINT 'Added DeletedBy to ProductAllergen table';
END
GO

-- ProductExternalLink table
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ProductExternalLink' AND COLUMN_NAME = 'DeletedBy')
BEGIN
    ALTER TABLE ProductExternalLink ADD DeletedBy UNIQUEIDENTIFIER NULL;
    PRINT 'Added DeletedBy to ProductExternalLink table';
END
GO

-- ProductMetadata table
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ProductMetadata' AND COLUMN_NAME = 'DeletedBy')
BEGIN
    ALTER TABLE ProductMetadata ADD DeletedBy UNIQUEIDENTIFIER NULL;
    PRINT 'Added DeletedBy to ProductMetadata table';
END
GO

-- ProductNutrition table
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ProductNutrition' AND COLUMN_NAME = 'DeletedBy')
BEGIN
    ALTER TABLE ProductNutrition ADD DeletedBy UNIQUEIDENTIFIER NULL;
    PRINT 'Added DeletedBy to ProductNutrition table';
END
GO

-- ProductRating table
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ProductRating' AND COLUMN_NAME = 'DeletedBy')
BEGIN
    ALTER TABLE ProductRating ADD DeletedBy UNIQUEIDENTIFIER NULL;
    PRINT 'Added DeletedBy to ProductRating table';
END
GO

-- ProductRecall table
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ProductRecall' AND COLUMN_NAME = 'DeletedBy')
BEGIN
    ALTER TABLE ProductRecall ADD DeletedBy UNIQUEIDENTIFIER NULL;
    PRINT 'Added DeletedBy to ProductRecall table';
END
GO

-- IngredientAllergen table
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'IngredientAllergen' AND COLUMN_NAME = 'DeletedBy')
BEGIN
    ALTER TABLE IngredientAllergen ADD DeletedBy UNIQUEIDENTIFIER NULL;
    PRINT 'Added DeletedBy to IngredientAllergen table';
END
GO

-- ProductImage table (conditional)
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'ProductImage')
   AND NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ProductImage' AND COLUMN_NAME = 'DeletedBy')
BEGIN
    ALTER TABLE ProductImage ADD DeletedBy UNIQUEIDENTIFIER NULL;
    PRINT 'Added DeletedBy to ProductImage table';
END
GO

-- ProductStaging table (conditional)
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'ProductStaging')
   AND NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ProductStaging' AND COLUMN_NAME = 'DeletedBy')
BEGIN
    ALTER TABLE ProductStaging ADD DeletedBy UNIQUEIDENTIFIER NULL;
    PRINT 'Added DeletedBy to ProductStaging table';
END
GO

-- ProductPrice table (conditional)
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'ProductPrice')
   AND NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ProductPrice' AND COLUMN_NAME = 'DeletedBy')
BEGIN
    ALTER TABLE ProductPrice ADD DeletedBy UNIQUEIDENTIFIER NULL;
    PRINT 'Added DeletedBy to ProductPrice table';
END
GO

-- BaseIngredient table (conditional)
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'BaseIngredient')
   AND NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BaseIngredient' AND COLUMN_NAME = 'DeletedBy')
BEGIN
    ALTER TABLE BaseIngredient ADD DeletedBy UNIQUEIDENTIFIER NULL;
    PRINT 'Added DeletedBy to BaseIngredient table';
END
GO

-- IngredientBaseComponent table (conditional)
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'IngredientBaseComponent')
   AND NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'IngredientBaseComponent' AND COLUMN_NAME = 'DeletedBy')
BEGIN
    ALTER TABLE IngredientBaseComponent ADD DeletedBy UNIQUEIDENTIFIER NULL;
    PRINT 'Added DeletedBy to IngredientBaseComponent table';
END
GO

PRINT 'DeletedBy column migration complete!';
PRINT 'All tables now have complete HighSpeedDAL audit columns:';
PRINT '  - CreatedDate, CreatedBy (AutoAudit)';
PRINT '  - ModifiedDate, ModifiedBy (AutoAudit)';
PRINT '  - IsDeleted, DeletedDate, DeletedBy (SoftDelete)';
GO
