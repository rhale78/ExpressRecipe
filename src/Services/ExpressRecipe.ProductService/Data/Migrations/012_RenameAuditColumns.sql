-- =================================================================
-- Migration: Rename Audit Columns to Match HighSpeedDAL Conventions
-- Date: 2026-01-14
-- Purpose: Align database schema with HighSpeedDAL auto-generated property names
-- =================================================================

-- HighSpeedDAL generates:  CreatedDate, ModifiedDate, ModifiedBy, DeletedDate
-- Database currently has:   CreatedAt, UpdatedAt, UpdatedBy, DeletedAt
-- This migration renames columns to match the framework conventions

PRINT 'Starting audit column rename migration...';
GO

-- Product table
PRINT 'Renaming Product table columns...';
EXEC sp_rename 'Product.CreatedAt', 'CreatedDate', 'COLUMN';
EXEC sp_rename 'Product.UpdatedAt', 'ModifiedDate', 'COLUMN';
EXEC sp_rename 'Product.UpdatedBy', 'ModifiedBy', 'COLUMN';
EXEC sp_rename 'Product.DeletedAt', 'DeletedDate', 'COLUMN';
GO

-- Ingredient table
PRINT 'Renaming Ingredient table columns...';
EXEC sp_rename 'Ingredient.CreatedAt', 'CreatedDate', 'COLUMN';
EXEC sp_rename 'Ingredient.UpdatedAt', 'ModifiedDate', 'COLUMN';
EXEC sp_rename 'Ingredient.UpdatedBy', 'ModifiedBy', 'COLUMN';
EXEC sp_rename 'Ingredient.DeletedAt', 'DeletedDate', 'COLUMN';
GO

-- ProductIngredient table
PRINT 'Renaming ProductIngredient table columns...';
EXEC sp_rename 'ProductIngredient.CreatedAt', 'CreatedDate', 'COLUMN';
EXEC sp_rename 'ProductIngredient.UpdatedAt', 'ModifiedDate', 'COLUMN';
EXEC sp_rename 'ProductIngredient.UpdatedBy', 'ModifiedBy', 'COLUMN';
EXEC sp_rename 'ProductIngredient.DeletedAt', 'DeletedDate', 'COLUMN';
GO

-- ProductLabel table
PRINT 'Renaming ProductLabel table columns...';
EXEC sp_rename 'ProductLabel.CreatedAt', 'CreatedDate', 'COLUMN';
EXEC sp_rename 'ProductLabel.UpdatedAt', 'ModifiedDate', 'COLUMN';
EXEC sp_rename 'ProductLabel.UpdatedBy', 'ModifiedBy', 'COLUMN';
EXEC sp_rename 'ProductLabel.DeletedAt', 'DeletedDate', 'COLUMN';
GO

-- ProductAllergen table
PRINT 'Renaming ProductAllergen table columns...';
EXEC sp_rename 'ProductAllergen.CreatedAt', 'CreatedDate', 'COLUMN';
EXEC sp_rename 'ProductAllergen.UpdatedAt', 'ModifiedDate', 'COLUMN';
EXEC sp_rename 'ProductAllergen.UpdatedBy', 'ModifiedBy', 'COLUMN';
EXEC sp_rename 'ProductAllergen.DeletedAt', 'DeletedDate', 'COLUMN';
GO

-- ProductExternalLink table
PRINT 'Renaming ProductExternalLink table columns...';
EXEC sp_rename 'ProductExternalLink.CreatedAt', 'CreatedDate', 'COLUMN';
EXEC sp_rename 'ProductExternalLink.UpdatedAt', 'ModifiedDate', 'COLUMN';
EXEC sp_rename 'ProductExternalLink.UpdatedBy', 'ModifiedBy', 'COLUMN';
EXEC sp_rename 'ProductExternalLink.DeletedAt', 'DeletedDate', 'COLUMN';
GO

-- ProductMetadata table
PRINT 'Renaming ProductMetadata table columns...';
EXEC sp_rename 'ProductMetadata.CreatedAt', 'CreatedDate', 'COLUMN';
EXEC sp_rename 'ProductMetadata.UpdatedAt', 'ModifiedDate', 'COLUMN';
EXEC sp_rename 'ProductMetadata.UpdatedBy', 'ModifiedBy', 'COLUMN';
EXEC sp_rename 'ProductMetadata.DeletedAt', 'DeletedDate', 'COLUMN';
GO

-- ProductNutrition table
PRINT 'Renaming ProductNutrition table columns...';
EXEC sp_rename 'ProductNutrition.CreatedAt', 'CreatedDate', 'COLUMN';
EXEC sp_rename 'ProductNutrition.UpdatedAt', 'ModifiedDate', 'COLUMN';
EXEC sp_rename 'ProductNutrition.UpdatedBy', 'ModifiedBy', 'COLUMN';
EXEC sp_rename 'ProductNutrition.DeletedAt', 'DeletedDate', 'COLUMN';
GO

-- ProductRating table
PRINT 'Renaming ProductRating table columns...';
EXEC sp_rename 'ProductRating.CreatedAt', 'CreatedDate', 'COLUMN';
EXEC sp_rename 'ProductRating.UpdatedAt', 'ModifiedDate', 'COLUMN';
EXEC sp_rename 'ProductRating.UpdatedBy', 'ModifiedBy', 'COLUMN';
EXEC sp_rename 'ProductRating.DeletedAt', 'DeletedDate', 'COLUMN';
GO

-- ProductRecall table
PRINT 'Renaming ProductRecall table columns...';
EXEC sp_rename 'ProductRecall.CreatedAt', 'CreatedDate', 'COLUMN';
EXEC sp_rename 'ProductRecall.UpdatedAt', 'ModifiedDate', 'COLUMN';
EXEC sp_rename 'ProductRecall.UpdatedBy', 'ModifiedBy', 'COLUMN';
EXEC sp_rename 'ProductRecall.DeletedAt', 'DeletedDate', 'COLUMN';
GO

-- IngredientAllergen table
PRINT 'Renaming IngredientAllergen table columns...';
EXEC sp_rename 'IngredientAllergen.CreatedAt', 'CreatedDate', 'COLUMN';
EXEC sp_rename 'IngredientAllergen.UpdatedAt', 'ModifiedDate', 'COLUMN';
EXEC sp_rename 'IngredientAllergen.UpdatedBy', 'ModifiedBy', 'COLUMN';
EXEC sp_rename 'IngredientAllergen.DeletedAt', 'DeletedDate', 'COLUMN';
GO

-- ProductImage table (conditional - only if exists)
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ProductImage' AND COLUMN_NAME = 'CreatedAt')
BEGIN
    PRINT 'Renaming ProductImage table columns...';
    EXEC sp_rename 'ProductImage.CreatedAt', 'CreatedDate', 'COLUMN';
    IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ProductImage' AND COLUMN_NAME = 'UpdatedAt')
        EXEC sp_rename 'ProductImage.UpdatedAt', 'ModifiedDate', 'COLUMN';
    IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ProductImage' AND COLUMN_NAME = 'UpdatedBy')
        EXEC sp_rename 'ProductImage.UpdatedBy', 'ModifiedBy', 'COLUMN';
    IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ProductImage' AND COLUMN_NAME = 'DeletedAt')
        EXEC sp_rename 'ProductImage.DeletedAt', 'DeletedDate', 'COLUMN';
END
GO

-- ProductStaging table (conditional - only if exists)
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ProductStaging' AND COLUMN_NAME = 'CreatedAt')
BEGIN
    PRINT 'Renaming ProductStaging table columns...';
    EXEC sp_rename 'ProductStaging.CreatedAt', 'CreatedDate', 'COLUMN';
    IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ProductStaging' AND COLUMN_NAME = 'UpdatedAt')
        EXEC sp_rename 'ProductStaging.UpdatedAt', 'ModifiedDate', 'COLUMN';
    IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ProductStaging' AND COLUMN_NAME = 'UpdatedBy')
        EXEC sp_rename 'ProductStaging.UpdatedBy', 'ModifiedBy', 'COLUMN';
    IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ProductStaging' AND COLUMN_NAME = 'DeletedAt')
        EXEC sp_rename 'ProductStaging.DeletedAt', 'DeletedDate', 'COLUMN';
END
GO

-- ProductPrice table
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ProductPrice' AND COLUMN_NAME = 'DeletedAt')
BEGIN
    PRINT 'Renaming ProductPrice table columns...';
    EXEC sp_rename 'ProductPrice.DeletedAt', 'DeletedDate', 'COLUMN';
END
GO

PRINT 'Audit column rename migration complete!';
PRINT 'All tables now use HighSpeedDAL naming convention:';
PRINT '  - CreatedDate, CreatedBy, ModifiedDate, ModifiedBy, DeletedDate, DeletedBy';
GO
