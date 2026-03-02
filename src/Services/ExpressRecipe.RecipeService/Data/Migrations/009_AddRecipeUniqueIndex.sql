-- Migration: 009_AddRecipeUniqueIndex
-- Description: Add unique index to Recipe table to support high-performance bulk upserts
-- Date: 2026-02-23

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'UQ_Recipe_Name_AuthorId' AND object_id = OBJECT_ID('Recipe'))
BEGIN
    -- We use a filtered unique index to allow multiple soft-deleted recipes with the same name
    CREATE UNIQUE NONCLUSTERED INDEX UQ_Recipe_Name_AuthorId 
    ON Recipe (Name, AuthorId) 
    WHERE IsDeleted = 0;
END
GO
