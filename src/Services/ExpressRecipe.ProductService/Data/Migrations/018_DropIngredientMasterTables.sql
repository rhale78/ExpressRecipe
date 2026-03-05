-- Migration: 018_DropIngredientMasterTables
-- Description: Remove Ingredient master tables from ProductService database.
--   These tables (Ingredient, IngredientAllergen, BaseIngredient, IngredientBaseComponent)
--   are authoritatively owned by IngredientService.  ProductService stores only the
--   ProductIngredient join table with an external IngredientId key.
--
--   After this migration:
--   - All Ingredient master CRUD goes through IngredientService API.
--   - ProductIngredient.IngredientId remains as a plain external reference (no FK constraint).
--   - ProductService keeps vw_ProductIngredientFlat for allergen/dietary queries;
--     that view will be recreated without the local Ingredient join once IngredientService
--     provides a search endpoint (tracked separately).
-- Date: 2026-03-05

-- -------------------------------------------------------------------------
-- 1. Drop dependent views that reference the Ingredient table
-- -------------------------------------------------------------------------
IF OBJECT_ID('vw_ProductIngredientFlat', 'V') IS NOT NULL
BEGIN
    DROP VIEW vw_ProductIngredientFlat;
    PRINT 'Dropped view vw_ProductIngredientFlat';
END
GO

-- -------------------------------------------------------------------------
-- 2. Drop IngredientBaseComponent (depends on both Ingredient and BaseIngredient)
-- -------------------------------------------------------------------------
IF OBJECT_ID('IngredientBaseComponent', 'U') IS NOT NULL
BEGIN
    DROP TABLE IngredientBaseComponent;
    PRINT 'Dropped IngredientBaseComponent';
END
ELSE
BEGIN
    PRINT 'IngredientBaseComponent not found – skipping';
END
GO

-- -------------------------------------------------------------------------
-- 3. Drop IngredientAllergen (depends on Ingredient)
-- -------------------------------------------------------------------------
IF OBJECT_ID('IngredientAllergen', 'U') IS NOT NULL
BEGIN
    DROP TABLE IngredientAllergen;
    PRINT 'Dropped IngredientAllergen';
END
ELSE
BEGIN
    PRINT 'IngredientAllergen not found – skipping';
END
GO

-- -------------------------------------------------------------------------
-- 4. Drop BaseIngredient
-- -------------------------------------------------------------------------
IF OBJECT_ID('BaseIngredient', 'U') IS NOT NULL
BEGIN
    DROP TABLE BaseIngredient;
    PRINT 'Dropped BaseIngredient';
END
ELSE
BEGIN
    PRINT 'BaseIngredient not found – skipping';
END
GO

-- -------------------------------------------------------------------------
-- 5. Drop Ingredient (master data – owned by IngredientService)
-- -------------------------------------------------------------------------
IF OBJECT_ID('Ingredient', 'U') IS NOT NULL
BEGIN
    -- ProductIngredient references this table via external key; FK was already
    -- dropped in migration 017.  Safe to drop the table now.
    DROP TABLE Ingredient;
    PRINT 'Dropped Ingredient';
END
ELSE
BEGIN
    PRINT 'Ingredient not found – skipping';
END
GO

-- -------------------------------------------------------------------------
-- 6. Recreate vw_ProductIngredientFlat using only ProductIngredient data.
--    IngredientName now comes from IngredientListString (the raw text stored
--    at product-creation time) so allergen queries continue to work locally
--    without a live call to IngredientService.
-- -------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = 'vw_ProductIngredientFlat' AND type = 'V')
BEGIN
    EXEC sp_executesql N'
    CREATE VIEW vw_ProductIngredientFlat AS
    SELECT
        pi.ProductId,
        -- Use IngredientListString as the ingredient text; fall back to the
        -- Notes column if IngredientListString is empty.
        COALESCE(NULLIF(pi.IngredientListString, ''''), pi.Notes, ''Unknown'') AS IngredientName,
        pi.IngredientId,
        pi.OrderIndex
    FROM ProductIngredient pi
    WHERE pi.IsDeleted = 0';
    PRINT 'Recreated vw_ProductIngredientFlat (no join to Ingredient table)';
END
GO

PRINT 'Migration 018 complete: Ingredient master tables removed from ProductService database.';
GO
