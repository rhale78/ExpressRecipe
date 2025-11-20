-- Migration: 003_RecipeImportAndVersioning
-- Description: Add recipe import, export, versioning, and forking capabilities
-- Date: 2024-11-19

-- RecipeVersion: Track recipe versions when users copy/modify recipes
CREATE TABLE RecipeVersion (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    RecipeId UNIQUEIDENTIFIER NOT NULL,
    VersionNumber INT NOT NULL,
    ChangeDescription NVARCHAR(MAX) NULL,
    CreatedBy UNIQUEIDENTIFIER NOT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    -- Snapshot of recipe data at this version
    SnapshotData NVARCHAR(MAX) NOT NULL, -- JSON snapshot of entire recipe
    CONSTRAINT FK_RecipeVersion_Recipe FOREIGN KEY (RecipeId)
        REFERENCES Recipe(Id) ON DELETE CASCADE,
    CONSTRAINT UQ_RecipeVersion_Recipe_Number UNIQUE (RecipeId, VersionNumber)
);

CREATE INDEX IX_RecipeVersion_RecipeId ON RecipeVersion(RecipeId);
CREATE INDEX IX_RecipeVersion_CreatedAt ON RecipeVersion(CreatedAt);
GO

-- RecipeFork: Track when users fork/copy recipes to make their own versions
CREATE TABLE RecipeFork (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    OriginalRecipeId UNIQUEIDENTIFIER NOT NULL,
    ForkedRecipeId UNIQUEIDENTIFIER NOT NULL,
    ForkedBy UNIQUEIDENTIFIER NOT NULL,
    ForkReason NVARCHAR(500) NULL, -- "Adapted for dietary restrictions", "Modified ingredients", etc.
    ForkedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_RecipeFork_Original FOREIGN KEY (OriginalRecipeId)
        REFERENCES Recipe(Id) ON DELETE NO ACTION,
    CONSTRAINT FK_RecipeFork_Forked FOREIGN KEY (ForkedRecipeId)
        REFERENCES Recipe(Id) ON DELETE CASCADE
);

CREATE INDEX IX_RecipeFork_OriginalRecipeId ON RecipeFork(OriginalRecipeId);
CREATE INDEX IX_RecipeFork_ForkedRecipeId ON RecipeFork(ForkedRecipeId);
CREATE INDEX IX_RecipeFork_ForkedBy ON RecipeFork(ForkedBy);
GO

-- RecipeImportSource: Supported import sources
CREATE TABLE RecipeImportSource (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Name NVARCHAR(100) NOT NULL,
    SourceType NVARCHAR(50) NOT NULL, -- MealMaster, JSON, XML, WebScraper, API
    Description NVARCHAR(500) NULL,
    ParserClassName NVARCHAR(200) NULL, -- Name of parser class to use
    IsActive BIT NOT NULL DEFAULT 1,
    SupportedFileExtensions NVARCHAR(200) NULL, -- ".mmf,.mm,.mxp"
    RequiresApiKey BIT NOT NULL DEFAULT 0,
    Website NVARCHAR(500) NULL,
    CONSTRAINT UQ_RecipeImportSource_Name UNIQUE (Name)
);

-- Seed import sources
INSERT INTO RecipeImportSource (Name, SourceType, Description, ParserClassName, SupportedFileExtensions) VALUES
('MealMaster', 'MealMaster', 'MealMaster recipe format (.mmf, .mm)', 'MealMasterParser', '.mmf,.mm'),
('MasterCook', 'MealMaster', 'MasterCook/MealMaster MX2/MXP format', 'MasterCookParser', '.mxp,.mx2'),
('JSON', 'JSON', 'JSON recipe format', 'JsonRecipeParser', '.json'),
('Recipe Keeper', 'JSON', 'Recipe Keeper export format', 'RecipeKeeperParser', '.json'),
('Paprika', 'JSON', 'Paprika recipe manager format', 'PaprikaParser', '.paprikarecipe'),
('Plain Text', 'Text', 'Plain text with natural language parsing', 'PlainTextParser', '.txt'),
('Web URL', 'WebScraper', 'Import from recipe website URL', 'WebScraperParser', NULL),
('AllRecipes', 'WebScraper', 'AllRecipes.com web scraper', 'AllRecipesParser', NULL),
('Food Network', 'WebScraper', 'Food Network web scraper', 'FoodNetworkParser', NULL),
('Tasty', 'WebScraper', 'Tasty (BuzzFeed) web scraper', 'TastyParser', NULL),
('Serious Eats', 'WebScraper', 'Serious Eats web scraper', 'SeriousEatsParser', NULL),
('NYT Cooking', 'WebScraper', 'NYT Cooking web scraper', 'NYTCookingParser', NULL);
GO

-- RecipeImportJob: Track recipe import jobs
CREATE TABLE RecipeImportJob (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    ImportSourceId UNIQUEIDENTIFIER NOT NULL,
    FileName NVARCHAR(500) NULL,
    FileUrl NVARCHAR(1000) NULL, -- If importing from URL
    Status NVARCHAR(50) NOT NULL DEFAULT 'Pending', -- Pending, Processing, Completed, Failed, PartialSuccess
    TotalRecipes INT NOT NULL DEFAULT 0,
    SuccessCount INT NOT NULL DEFAULT 0,
    FailureCount INT NOT NULL DEFAULT 0,
    ErrorLog NVARCHAR(MAX) NULL,
    StartedAt DATETIME2 NULL,
    CompletedAt DATETIME2 NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_RecipeImportJob_Source FOREIGN KEY (ImportSourceId)
        REFERENCES RecipeImportSource(Id) ON DELETE CASCADE
);

CREATE INDEX IX_RecipeImportJob_UserId ON RecipeImportJob(UserId);
CREATE INDEX IX_RecipeImportJob_Status ON RecipeImportJob(Status);
CREATE INDEX IX_RecipeImportJob_CreatedAt ON RecipeImportJob(CreatedAt);
GO

-- RecipeImportResult: Individual recipe import results
CREATE TABLE RecipeImportResult (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ImportJobId UNIQUEIDENTIFIER NOT NULL,
    SourceRecipeId NVARCHAR(200) NULL, -- ID in source system if available
    SourceRecipeName NVARCHAR(500) NULL,
    ImportedRecipeId UNIQUEIDENTIFIER NULL, -- References Recipe.Id if successful
    Status NVARCHAR(50) NOT NULL, -- Success, Failed, Skipped, Duplicate
    ErrorMessage NVARCHAR(MAX) NULL,
    RawData NVARCHAR(MAX) NULL, -- Original recipe data for debugging
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_RecipeImportResult_Job FOREIGN KEY (ImportJobId)
        REFERENCES RecipeImportJob(Id) ON DELETE CASCADE,
    CONSTRAINT FK_RecipeImportResult_Recipe FOREIGN KEY (ImportedRecipeId)
        REFERENCES Recipe(Id) ON DELETE SET NULL
);

CREATE INDEX IX_RecipeImportResult_ImportJobId ON RecipeImportResult(ImportJobId);
CREATE INDEX IX_RecipeImportResult_Status ON RecipeImportResult(Status);
GO

-- RecipeExportHistory: Track recipe exports
CREATE TABLE RecipeExportHistory (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    ExportFormat NVARCHAR(50) NOT NULL, -- MealMaster, JSON, PDF, XML, etc.
    RecipeCount INT NOT NULL,
    FileName NVARCHAR(500) NULL,
    FileSize BIGINT NULL,
    FileUrl NVARCHAR(1000) NULL, -- Temporary download URL
    ExpiresAt DATETIME2 NULL, -- When download link expires
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);

CREATE INDEX IX_RecipeExportHistory_UserId ON RecipeExportHistory(UserId);
CREATE INDEX IX_RecipeExportHistory_CreatedAt ON RecipeExportHistory(CreatedAt);
GO

-- RecipeCollection: User-created recipe collections/cookbooks
CREATE TABLE RecipeCollection (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    Name NVARCHAR(200) NOT NULL,
    Description NVARCHAR(MAX) NULL,
    ImageUrl NVARCHAR(500) NULL,
    IsPublic BIT NOT NULL DEFAULT 0,
    SortOrder INT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    DeletedAt DATETIME2 NULL
);

CREATE INDEX IX_RecipeCollection_UserId ON RecipeCollection(UserId);
CREATE INDEX IX_RecipeCollection_IsPublic ON RecipeCollection(IsPublic);
GO

-- RecipeCollectionItem: Recipes within collections
CREATE TABLE RecipeCollectionItem (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    CollectionId UNIQUEIDENTIFIER NOT NULL,
    RecipeId UNIQUEIDENTIFIER NOT NULL,
    OrderIndex INT NOT NULL DEFAULT 0,
    Notes NVARCHAR(500) NULL,
    AddedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_RecipeCollectionItem_Collection FOREIGN KEY (CollectionId)
        REFERENCES RecipeCollection(Id) ON DELETE CASCADE,
    CONSTRAINT FK_RecipeCollectionItem_Recipe FOREIGN KEY (RecipeId)
        REFERENCES Recipe(Id) ON DELETE CASCADE,
    CONSTRAINT UQ_RecipeCollectionItem_Collection_Recipe UNIQUE (CollectionId, RecipeId)
);

CREATE INDEX IX_RecipeCollectionItem_CollectionId ON RecipeCollectionItem(CollectionId);
CREATE INDEX IX_RecipeCollectionItem_RecipeId ON RecipeCollectionItem(RecipeId);
GO

-- Add foreign/parent recipe reference to Recipe table for tracking original source
ALTER TABLE Recipe ADD ParentRecipeId UNIQUEIDENTIFIER NULL;
ALTER TABLE Recipe ADD IsForked BIT NOT NULL DEFAULT 0;
ALTER TABLE Recipe ADD CurrentVersion INT NOT NULL DEFAULT 1;
GO

ALTER TABLE Recipe ADD CONSTRAINT FK_Recipe_Parent
    FOREIGN KEY (ParentRecipeId) REFERENCES Recipe(Id) ON DELETE NO ACTION;
GO

CREATE INDEX IX_Recipe_ParentRecipeId ON Recipe(ParentRecipeId);
GO
