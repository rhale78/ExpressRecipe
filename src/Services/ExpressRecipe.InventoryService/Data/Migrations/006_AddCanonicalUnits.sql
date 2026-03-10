-- Migration: 003_AddCanonicalUnits
-- Description: Add canonical unit columns to InventoryItem for unit-aware deductions and comparisons.
-- Date: 2026-03-09

ALTER TABLE InventoryItem
    ADD CanonicalQuantity DECIMAL(18,6) NULL,
        CanonicalUnit     NVARCHAR(20)   NULL;
GO
