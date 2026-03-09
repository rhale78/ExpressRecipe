-- Migration: 003_AddCanonicalUnits
-- Description: Add canonical unit columns to ShoppingListItem for unit-aware aggregation.
-- Date: 2026-03-09

ALTER TABLE ShoppingListItem
    ADD CanonicalQuantity DECIMAL(18,6) NULL,
        CanonicalUnit     NVARCHAR(20)   NULL;
GO
