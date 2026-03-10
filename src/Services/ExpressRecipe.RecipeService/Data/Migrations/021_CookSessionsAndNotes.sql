-- Migration: 021_CookSessionsAndNotes
-- Description: Per-cook session history and personal recipe notes

CREATE TABLE UserCookSession (
    Id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId          UNIQUEIDENTIFIER NOT NULL,
    HouseholdId     UNIQUEIDENTIFIER NOT NULL,
    RecipeId        UNIQUEIDENTIFIER NOT NULL,
    CookedAt        DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    ServingsMade    INT NULL,
    Rating          INT NULL CHECK (Rating BETWEEN 1 AND 5),
    WouldMakeAgain  BIT NULL,
    GeneralNotes    NVARCHAR(MAX) NULL,
    IssueNotes      NVARCHAR(MAX) NULL,
    FixNotes        NVARCHAR(MAX) NULL,
    AIHelpUsed      BIT NOT NULL DEFAULT 0,
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_UserCookSession_Recipe FOREIGN KEY (RecipeId)
        REFERENCES Recipe(Id) ON DELETE CASCADE
);
CREATE INDEX IX_UserCookSession_UserId      ON UserCookSession(UserId);
CREATE INDEX IX_UserCookSession_HouseholdId ON UserCookSession(HouseholdId);
CREATE INDEX IX_UserCookSession_RecipeId    ON UserCookSession(RecipeId);
CREATE INDEX IX_UserCookSession_CookedAt    ON UserCookSession(CookedAt DESC);
GO

CREATE TABLE UserRecipeNote (
    Id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId          UNIQUEIDENTIFIER NOT NULL,
    RecipeId        UNIQUEIDENTIFIER NOT NULL,
    NoteType        NVARCHAR(30) NOT NULL DEFAULT 'General',
    NoteText        NVARCHAR(MAX) NOT NULL,
    IsFromAI        BIT NOT NULL DEFAULT 0,
    IsDismissed     BIT NOT NULL DEFAULT 0,
    DisplayOrder    INT NOT NULL DEFAULT 0,
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt       DATETIME2 NULL,
    CONSTRAINT FK_UserRecipeNote_Recipe FOREIGN KEY (RecipeId)
        REFERENCES Recipe(Id) ON DELETE CASCADE
);
CREATE INDEX IX_UserRecipeNote_UserId   ON UserRecipeNote(UserId);
CREATE INDEX IX_UserRecipeNote_RecipeId ON UserRecipeNote(RecipeId);
GO
