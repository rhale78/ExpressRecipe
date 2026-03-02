-- Add RecipeImage table for multiple images
CREATE TABLE RecipeImage (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    RecipeId UNIQUEIDENTIFIER NOT NULL,
    ImageUrl NVARCHAR(500) NOT NULL,
    LocalPath NVARCHAR(500) NULL,
    IsPrimary BIT NOT NULL DEFAULT 0,
    DisplayOrder INT NOT NULL DEFAULT 0,
    SourceSystem NVARCHAR(100) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_RecipeImage_Recipe FOREIGN KEY (RecipeId) REFERENCES Recipe(Id) ON DELETE CASCADE
);
GO

CREATE INDEX IX_RecipeImage_RecipeId ON RecipeImage(RecipeId);
GO

-- Add OriginalText to RecipeIngredient for preservation
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[RecipeIngredient]') AND name = 'OriginalText')
BEGIN
    ALTER TABLE [dbo].[RecipeIngredient] ADD [OriginalText] NVARCHAR(MAX) NULL;
END
GO
