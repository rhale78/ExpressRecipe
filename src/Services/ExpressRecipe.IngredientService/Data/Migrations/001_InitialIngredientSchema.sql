-- Migration: 001_InitialIngredientSchema
-- Description: Create Ingredient table with centralized ingredient data (idempotent)
-- Author: System
-- Date: 2025-01-24

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Ingredient]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Ingredient] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        [Name] NVARCHAR(200) NOT NULL,
        [AlternativeNames] NVARCHAR(1000) NULL,
        [Description] NVARCHAR(MAX) NULL,
        [Category] NVARCHAR(100) NULL,
        [IsCommonAllergen] BIT NOT NULL DEFAULT 0,
        [IngredientListString] NVARCHAR(MAX) NULL,
        [CreatedBy] UNIQUEIDENTIFIER NULL,
        [CreatedAt] DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
        [UpdatedBy] UNIQUEIDENTIFIER NULL,
        [UpdatedAt] DATETIME2(7) NULL,
        [IsDeleted] BIT NOT NULL DEFAULT 0,
        [DeletedAt] DATETIME2(7) NULL
    );

    PRINT 'Created Ingredient table';
END
ELSE
BEGIN
    PRINT 'Ingredient table already exists, skipping creation';
END
GO

-- Index for name lookups (most common operation)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Ingredient_Name' AND object_id = OBJECT_ID('Ingredient'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [IX_Ingredient_Name] 
    ON [dbo].[Ingredient]([Name]) 
    WHERE [IsDeleted] = 0;

    PRINT 'Created IX_Ingredient_Name index';
END
GO

-- Index for category filtering
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Ingredient_Category' AND object_id = OBJECT_ID('Ingredient'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Ingredient_Category] 
    ON [dbo].[Ingredient]([Category]) 
    INCLUDE ([Name], [IsCommonAllergen])
    WHERE [IsDeleted] = 0;

    PRINT 'Created IX_Ingredient_Category index';
END
GO

-- Index for allergen filtering
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Ingredient_IsCommonAllergen' AND object_id = OBJECT_ID('Ingredient'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Ingredient_IsCommonAllergen] 
    ON [dbo].[Ingredient]([IsCommonAllergen]) 
    INCLUDE ([Name], [Category])
    WHERE [IsDeleted] = 0;

    PRINT 'Created IX_Ingredient_IsCommonAllergen index';
END
GO

PRINT 'Migration 001_InitialIngredientSchema completed successfully';
