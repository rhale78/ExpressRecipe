-- Migration: 016_IngredientMatchConfidence
-- Description: Add MatchConfidence and MatchStrategy columns to RecipeIngredient
--              to track ingredient matching quality from the IngredientMatchingService.

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[RecipeIngredient]') AND name = 'MatchConfidence')
BEGIN
    ALTER TABLE [dbo].[RecipeIngredient] ADD [MatchConfidence] DECIMAL(5,4) NULL;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[RecipeIngredient]') AND name = 'MatchStrategy')
BEGIN
    ALTER TABLE [dbo].[RecipeIngredient] ADD [MatchStrategy] NVARCHAR(50) NULL;
END
GO
