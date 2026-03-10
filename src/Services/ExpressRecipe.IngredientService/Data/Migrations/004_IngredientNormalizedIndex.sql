IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Ingredient]') AND name = 'NormalizedName')
BEGIN
    ALTER TABLE [dbo].[Ingredient] ADD [NormalizedName] AS LOWER([Name]) PERSISTED;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[Ingredient]') AND name = 'IX_Ingredient_NormalizedName')
BEGIN
    CREATE INDEX IX_Ingredient_NormalizedName ON [dbo].[Ingredient]([NormalizedName]) WHERE [IsDeleted] = 0;
END
GO
