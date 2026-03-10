-- Migration: 016_AddCanonicalUnits
-- Description: Add canonical unit columns to RecipeIngredient for unit-aware comparisons and scaling.
-- Date: 2026-03-09

ALTER TABLE RecipeIngredient
    ADD CanonicalAmount  DECIMAL(18,6) NULL,
        CanonicalUnit    NVARCHAR(20)   NULL,
        SourceUnitSystem NVARCHAR(10)  NULL;
GO
