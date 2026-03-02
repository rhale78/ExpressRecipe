-- Migration: 010_IncreaseColumnSizes
-- Description: Increase sizes of Name and Ingredient columns to handle larger/dirtier source data
-- Date: 2026-02-24

-- 1. Increase Recipe Name size (450 is the max for indexable columns)
ALTER TABLE [dbo].[Recipe] ALTER COLUMN [Name] NVARCHAR(450) NOT NULL;

-- 2. Increase Ingredient Name and related fields
ALTER TABLE [dbo].[RecipeIngredient] ALTER COLUMN [IngredientName] NVARCHAR(MAX) NULL;
ALTER TABLE [dbo].[RecipeIngredient] ALTER COLUMN [PreparationNote] NVARCHAR(MAX) NULL;
ALTER TABLE [dbo].[RecipeIngredient] ALTER COLUMN [SubstituteNotes] NVARCHAR(MAX) NULL;
ALTER TABLE [dbo].[RecipeIngredient] ALTER COLUMN [GroupName] NVARCHAR(MAX) NULL;

-- 3. Increase Image/Video URL sizes
ALTER TABLE [dbo].[Recipe] ALTER COLUMN [ImageUrl] NVARCHAR(MAX) NULL;
ALTER TABLE [dbo].[Recipe] ALTER COLUMN [VideoUrl] NVARCHAR(MAX) NULL;
ALTER TABLE [dbo].[Recipe] ALTER COLUMN [SourceUrl] NVARCHAR(MAX) NULL;

-- 4. Increase Category/Cuisine/Difficulty sizes
-- Drop indexes first
IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Recipe_Category' AND object_id = OBJECT_ID('Recipe'))
    DROP INDEX [IX_Recipe_Category] ON [dbo].[Recipe];

IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Recipe_Cuisine' AND object_id = OBJECT_ID('Recipe'))
    DROP INDEX [IX_Recipe_Cuisine] ON [dbo].[Recipe];

ALTER TABLE [dbo].[Recipe] ALTER COLUMN [Category] NVARCHAR(450) NULL;
ALTER TABLE [dbo].[Recipe] ALTER COLUMN [Cuisine] NVARCHAR(450) NULL;
ALTER TABLE [dbo].[Recipe] ALTER COLUMN [DifficultyLevel] NVARCHAR(MAX) NULL;

-- Recreate indexes
CREATE INDEX [IX_Recipe_Category] ON [dbo].[Recipe]([Category]);
CREATE INDEX [IX_Recipe_Cuisine] ON [dbo].[Recipe]([Cuisine]);

-- 5. Increase RecipeImage sizes
ALTER TABLE [dbo].[RecipeImage] ALTER COLUMN [ImageUrl] NVARCHAR(MAX) NOT NULL;
ALTER TABLE [dbo].[RecipeImage] ALTER COLUMN [LocalPath] NVARCHAR(MAX) NULL;

-- 6. Increase Tag Name size
-- Note: There is a unique index/constraint on Tag Name usually, so 450 is safe.
ALTER TABLE [dbo].[RecipeTag] ALTER COLUMN [Name] NVARCHAR(450) NOT NULL;
GO
