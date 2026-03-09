-- Migration: 016_AddShareTokenAndFavoriteShare
-- Description: Add RecipeShareToken table and household-share columns to UserFavoriteRecipe.
-- Date: 2026-03-09

CREATE TABLE RecipeShareToken (
    Id          UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    RecipeId    UNIQUEIDENTIFIER NOT NULL,
    Token       NVARCHAR(64) NOT NULL,
    CreatedBy   UNIQUEIDENTIFIER NOT NULL,
    CreatedAt   DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    ExpiresAt   DATETIME2 NOT NULL,
    ViewCount   INT NOT NULL DEFAULT 0,
    MaxViews    INT NULL,
    CONSTRAINT UQ_RecipeShareToken UNIQUE (Token),
    CONSTRAINT FK_RecipeShareToken_Recipe FOREIGN KEY (RecipeId) REFERENCES Recipe(Id) ON DELETE CASCADE
);

CREATE INDEX IX_RecipeShareToken_Token ON RecipeShareToken(Token);

CREATE INDEX IX_RecipeShareToken_ExpiresAt ON RecipeShareToken(ExpiresAt);

ALTER TABLE UserFavoriteRecipe
    ADD IsSharedWithHousehold BIT NOT NULL DEFAULT 0,
        HouseholdId           UNIQUEIDENTIFIER NULL;
