-- Add CreatedAt column to RecipeTag for consistency with other tables
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('RecipeTag') AND name = 'CreatedAt')
BEGIN
    ALTER TABLE RecipeTag ADD CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE();
END
GO
