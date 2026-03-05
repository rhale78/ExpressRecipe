-- Migration: 015_FixCrossServiceReferences
-- Description: Correct the cross-service reference on RecipeIngredient.
--   IngredientId and BaseIngredientId previously referenced ProductService.Ingredient
--   and ProductService.BaseIngredient.  The authoritative owner is IngredientService.
--   No data change is needed – these columns already contain plain GUIDs without FK
--   constraints.  This migration drops the now-obsolete BaseIngredientId column
--   (IngredientService does not expose a "BaseIngredient" concept separately;
--   the structured ingredient data comes from IngredientService.Ingredient) and
--   updates any remaining FK constraints if they exist.
-- Date: 2026-03-05

-- -------------------------------------------------------------------------
-- 1. Drop FK on BaseIngredientId if it exists
--    (It referenced ProductService.BaseIngredient, which is now dropped)
-- -------------------------------------------------------------------------
IF EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name = 'FK_RecipeIngredient_BaseIngredient'
      AND parent_object_id = OBJECT_ID('RecipeIngredient')
)
BEGIN
    ALTER TABLE RecipeIngredient DROP CONSTRAINT FK_RecipeIngredient_BaseIngredient;
    PRINT 'Dropped FK_RecipeIngredient_BaseIngredient';
END
ELSE
BEGIN
    PRINT 'FK_RecipeIngredient_BaseIngredient not found – skipping';
END
GO

-- -------------------------------------------------------------------------
-- 2. Drop the index on BaseIngredientId before dropping the column
-- -------------------------------------------------------------------------
IF EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_RecipeIngredient_BaseIngredientId'
      AND object_id = OBJECT_ID('RecipeIngredient')
)
BEGIN
    DROP INDEX IX_RecipeIngredient_BaseIngredientId ON RecipeIngredient;
    PRINT 'Dropped IX_RecipeIngredient_BaseIngredientId';
END
ELSE
BEGIN
    PRINT 'IX_RecipeIngredient_BaseIngredientId not found – skipping';
END
GO

-- -------------------------------------------------------------------------
-- 3. Drop BaseIngredientId column
--    This concept is internal to IngredientService; RecipeService stores only
--    the IngredientId (external reference to IngredientService.Ingredient).
-- -------------------------------------------------------------------------
IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('RecipeIngredient')
      AND name = 'BaseIngredientId'
)
BEGIN
    ALTER TABLE RecipeIngredient DROP COLUMN BaseIngredientId;
    PRINT 'Dropped RecipeIngredient.BaseIngredientId column';
END
ELSE
BEGIN
    PRINT 'RecipeIngredient.BaseIngredientId not found – skipping';
END
GO

PRINT 'Migration 015 complete: cross-service RecipeIngredient references corrected.';
GO
