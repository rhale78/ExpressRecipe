-- Migration: 017_RemoveCrossServiceTables
-- Description: Remove tables that violated the microservice ownership principle.
--   UserCoupon and UserFavoriteStore are user-relationship tables; they now live
--   in UserService (migration 016).  The cross-DB FK constraints are also removed
--   from ProductIngredient so that the Ingredient master data can be served
--   exclusively by IngredientService.
-- Date: 2026-03-05

-- -------------------------------------------------------------------------
-- 1. Drop UserFavoriteStore (now owned by UserService)
-- -------------------------------------------------------------------------
IF OBJECT_ID('UserFavoriteStore', 'U') IS NOT NULL
BEGIN
    DROP TABLE UserFavoriteStore;
    PRINT 'Dropped UserFavoriteStore from ProductService database';
END
ELSE
BEGIN
    PRINT 'UserFavoriteStore does not exist in this database – skipping';
END
GO

-- -------------------------------------------------------------------------
-- 2. Drop UserCoupon (now owned by UserService)
-- -------------------------------------------------------------------------
IF OBJECT_ID('UserCoupon', 'U') IS NOT NULL
BEGIN
    DROP TABLE UserCoupon;
    PRINT 'Dropped UserCoupon from ProductService database';
END
ELSE
BEGIN
    PRINT 'UserCoupon does not exist in this database – skipping';
END
GO

-- -------------------------------------------------------------------------
-- 3. Remove cross-service FK on ProductIngredient → Ingredient
--    The Ingredient table is now authoritative in IngredientService.
--    ProductIngredient retains the IngredientId column as an *external* key only.
-- -------------------------------------------------------------------------
IF EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name = 'FK_ProductIngredient_Ingredient'
      AND parent_object_id = OBJECT_ID('ProductIngredient')
)
BEGIN
    ALTER TABLE ProductIngredient DROP CONSTRAINT FK_ProductIngredient_Ingredient;
    PRINT 'Dropped cross-service FK_ProductIngredient_Ingredient';
END
ELSE
BEGIN
    PRINT 'FK_ProductIngredient_Ingredient not found – skipping';
END
GO

-- -------------------------------------------------------------------------
-- 4. Remove cross-service FK on MenuItemIngredient → Ingredient  (if present)
-- -------------------------------------------------------------------------
IF EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name = 'FK_MenuItemIngredient_Ingredient'
      AND parent_object_id = OBJECT_ID('MenuItemIngredient')
)
BEGIN
    ALTER TABLE MenuItemIngredient DROP CONSTRAINT FK_MenuItemIngredient_Ingredient;
    PRINT 'Dropped cross-service FK_MenuItemIngredient_Ingredient';
END
ELSE
BEGIN
    PRINT 'FK_MenuItemIngredient_Ingredient not found – skipping';
END
GO
