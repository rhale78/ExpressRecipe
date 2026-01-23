-- Migration: 014_AddProductImageUniqueConstraint.sql
-- Description: Add unique constraint on (ProductId, ImageUrl) to prevent duplicate images
-- Date: 2026-01-22
-- Purpose: Prevent duplicate image URLs from being uploaded for the same product
--          This saves disk space and prevents redundant image processing

BEGIN TRANSACTION;

-- Add unique constraint to prevent duplicate image URLs per product
-- This constraint allows NULL values in ImageUrl (multiple NULL entries are allowed)
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS
    WHERE TABLE_NAME = 'ProductImage'
    AND CONSTRAINT_NAME = 'UQ_ProductImage_ProductId_ImageUrl'
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UQ_ProductImage_ProductId_ImageUrl
    ON [ProductImage] ([ProductId], [ImageUrl])
    WHERE [ImageUrl] IS NOT NULL AND [IsDeleted] = 0;

    PRINT 'Created unique constraint UQ_ProductImage_ProductId_ImageUrl on (ProductId, ImageUrl)';
END
ELSE
BEGIN
    PRINT 'Unique constraint UQ_ProductImage_ProductId_ImageUrl already exists';
END

COMMIT TRANSACTION;
