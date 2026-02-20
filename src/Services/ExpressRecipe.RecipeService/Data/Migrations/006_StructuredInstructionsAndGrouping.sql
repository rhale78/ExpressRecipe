-- Migration: 006_StructuredInstructionsAndGrouping
-- Description: Add structured instructions table and grouping support for ingredients
-- Date: 2026-02-19

-- Create RecipeInstruction table if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[RecipeInstruction]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[RecipeInstruction] (
        [Id] UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        [RecipeId] UNIQUEIDENTIFIER NOT NULL,
        [OrderIndex] INT NOT NULL,
        [Instruction] NVARCHAR(MAX) NOT NULL,
        [TimeMinutes] INT NULL,
        [ImageUrl] NVARCHAR(500) NULL,
        [Tips] NVARCHAR(MAX) NULL,
        [CreatedBy] UNIQUEIDENTIFIER NULL,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [UpdatedBy] UNIQUEIDENTIFIER NULL,
        [UpdatedAt] DATETIME2 NULL,
        CONSTRAINT [FK_RecipeInstruction_Recipe] FOREIGN KEY ([RecipeId])
            REFERENCES [dbo].[Recipe] ([Id]) ON DELETE CASCADE
    );

    CREATE INDEX [IX_RecipeInstruction_RecipeId] ON [dbo].[RecipeInstruction]([RecipeId]);
END
GO

-- Add GroupName to RecipeIngredient if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[RecipeIngredient]') AND name = 'GroupName')
BEGIN
    ALTER TABLE [dbo].[RecipeIngredient] ADD [GroupName] NVARCHAR(100) NULL;
END
GO
