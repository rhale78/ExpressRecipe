-- Migration: 001_CreateCookbookTables
-- Description: Create cookbook management tables
-- Date: 2024-11-19

-- Cookbook: User-created collections of recipes
CREATE TABLE Cookbook (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Title NVARCHAR(300) NOT NULL,
    Subtitle NVARCHAR(300) NULL,
    Description NVARCHAR(MAX) NULL,
    CoverImageUrl NVARCHAR(500) NULL,
    AuthorName NVARCHAR(200) NULL,
    Visibility NVARCHAR(50) NOT NULL DEFAULT 'Private', -- Private, Public, SharedLink
    IsFavorite BIT NOT NULL DEFAULT 0,
    Tags NVARCHAR(MAX) NULL, -- Comma-separated tags
    TitlePageContent NVARCHAR(MAX) NULL,
    IntroductionContent NVARCHAR(MAX) NULL,
    IndexContent NVARCHAR(MAX) NULL,
    NotesContent NVARCHAR(MAX) NULL,
    WebSlug NVARCHAR(300) NULL, -- URL-friendly slug for public cookbooks
    ViewCount INT NOT NULL DEFAULT 0,
    OwnerId UNIQUEIDENTIFIER NOT NULL,
    CreatedBy UNIQUEIDENTIFIER NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy UNIQUEIDENTIFIER NULL,
    UpdatedAt DATETIME2 NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    DeletedAt DATETIME2 NULL,
    RowVersion ROWVERSION
);

CREATE INDEX IX_Cookbook_OwnerId ON Cookbook(OwnerId);
CREATE INDEX IX_Cookbook_Visibility ON Cookbook(Visibility);
CREATE INDEX IX_Cookbook_WebSlug ON Cookbook(WebSlug);
CREATE INDEX IX_Cookbook_IsDeleted ON Cookbook(IsDeleted);
GO

-- CookbookSection: Chapters or sections within a cookbook
CREATE TABLE CookbookSection (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    CookbookId UNIQUEIDENTIFIER NOT NULL,
    Title NVARCHAR(300) NOT NULL,
    Description NVARCHAR(MAX) NULL,
    TitlePageContent NVARCHAR(MAX) NULL,
    CategoryOrMealType NVARCHAR(100) NULL,
    SortOrder INT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NULL,
    CONSTRAINT FK_CookbookSection_Cookbook FOREIGN KEY (CookbookId) REFERENCES Cookbook(Id)
);

CREATE INDEX IX_CookbookSection_CookbookId ON CookbookSection(CookbookId);
GO

-- CookbookRecipe: Recipes included in a cookbook, optionally within a section
CREATE TABLE CookbookRecipe (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    CookbookId UNIQUEIDENTIFIER NOT NULL,
    SectionId UNIQUEIDENTIFIER NULL,
    RecipeId UNIQUEIDENTIFIER NOT NULL, -- References RecipeService.Recipe.Id
    RecipeName NVARCHAR(300) NOT NULL,  -- Denormalized for display without cross-service join
    SortOrder INT NOT NULL DEFAULT 0,
    Notes NVARCHAR(MAX) NULL,
    PageNumber INT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NULL,
    CONSTRAINT FK_CookbookRecipe_Cookbook FOREIGN KEY (CookbookId) REFERENCES Cookbook(Id),
    CONSTRAINT FK_CookbookRecipe_Section FOREIGN KEY (SectionId) REFERENCES CookbookSection(Id)
);

CREATE INDEX IX_CookbookRecipe_CookbookId ON CookbookRecipe(CookbookId);
CREATE INDEX IX_CookbookRecipe_SectionId ON CookbookRecipe(SectionId);
CREATE INDEX IX_CookbookRecipe_RecipeId ON CookbookRecipe(RecipeId);
GO

-- CookbookRating: User ratings for cookbooks
CREATE TABLE CookbookRating (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    CookbookId UNIQUEIDENTIFIER NOT NULL,
    UserId UNIQUEIDENTIFIER NOT NULL,
    Rating INT NOT NULL, -- 1-5
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NULL,
    CONSTRAINT FK_CookbookRating_Cookbook FOREIGN KEY (CookbookId) REFERENCES Cookbook(Id),
    CONSTRAINT CK_CookbookRating_Rating CHECK (Rating BETWEEN 1 AND 5),
    CONSTRAINT UQ_CookbookRating_User UNIQUE (CookbookId, UserId)
);

CREATE INDEX IX_CookbookRating_CookbookId ON CookbookRating(CookbookId);
GO

-- CookbookComment: User comments on cookbooks
CREATE TABLE CookbookComment (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    CookbookId UNIQUEIDENTIFIER NOT NULL,
    UserId UNIQUEIDENTIFIER NOT NULL,
    Content NVARCHAR(MAX) NOT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    CONSTRAINT FK_CookbookComment_Cookbook FOREIGN KEY (CookbookId) REFERENCES Cookbook(Id)
);

CREATE INDEX IX_CookbookComment_CookbookId ON CookbookComment(CookbookId);
GO

-- CookbookFavorite: Users who have favorited a cookbook
CREATE TABLE CookbookFavorite (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    CookbookId UNIQUEIDENTIFIER NOT NULL,
    UserId UNIQUEIDENTIFIER NOT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_CookbookFavorite_Cookbook FOREIGN KEY (CookbookId) REFERENCES Cookbook(Id),
    CONSTRAINT UQ_CookbookFavorite_User UNIQUE (CookbookId, UserId)
);

CREATE INDEX IX_CookbookFavorite_UserId ON CookbookFavorite(UserId);
GO

-- CookbookShare: Cookbooks shared with specific users
CREATE TABLE CookbookShare (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    CookbookId UNIQUEIDENTIFIER NOT NULL,
    SharedWithUserId UNIQUEIDENTIFIER NOT NULL,
    CanEdit BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_CookbookShare_Cookbook FOREIGN KEY (CookbookId) REFERENCES Cookbook(Id),
    CONSTRAINT UQ_CookbookShare_User UNIQUE (CookbookId, SharedWithUserId)
);

CREATE INDEX IX_CookbookShare_SharedWithUserId ON CookbookShare(SharedWithUserId);
GO
