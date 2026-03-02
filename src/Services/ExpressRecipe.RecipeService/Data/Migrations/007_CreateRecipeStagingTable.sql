-- Recipe Staging Table
-- Stores raw imported recipe data before processing
CREATE TABLE RecipeStaging (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),

    -- Core fields from JSON
    ExternalId NVARCHAR(100) NULL, -- 'id' from JSON
    Title NVARCHAR(500) NOT NULL,
    Description NVARCHAR(MAX) NULL,
    
    -- Ingredients and Directions (JSON or raw text)
    IngredientsRaw NVARCHAR(MAX) NULL, -- JSON array of strings
    DirectionsRaw NVARCHAR(MAX) NULL, -- JSON array of strings
    
    -- Parsed ingredients (NER)
    NerIngredientsRaw NVARCHAR(MAX) NULL, -- JSON array of strings
    
    -- Metadata
    Source NVARCHAR(200) NULL,
    SourceUrl NVARCHAR(500) NULL,
    CookingTimeMinutes INT NULL,
    Servings INT NULL,
    Rating DECIMAL(3, 2) NULL,
    RatingCount INT NULL,
    
    -- Categorization
    TagsRaw NVARCHAR(MAX) NULL, -- JSON object
    
    -- Publication
    PublishDate NVARCHAR(100) NULL,
    
    -- Images
    ImageName NVARCHAR(500) NULL, -- Filename from JSON
    LocalImagePath NVARCHAR(500) NULL, -- Path to local copy
    
    -- Full JSON (for any other fields)
    RawJson NVARCHAR(MAX) NULL,

    -- Processing status
    ProcessingStatus NVARCHAR(50) NOT NULL DEFAULT 'Pending', -- Pending, Processing, Completed, Failed
    ProcessedAt DATETIME2 NULL,
    ProcessingError NVARCHAR(MAX) NULL,
    ProcessingAttempts INT NOT NULL DEFAULT 0,

    -- Standard audit fields
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    DeletedAt DATETIME2 NULL,
    RowVersion ROWVERSION,

    -- Ensure we don't import duplicates if ExternalId is provided
    -- (Note: Many datasets have duplicate IDs or no IDs, so we might need to handle this carefully)
    -- CONSTRAINT UQ_RecipeStaging_ExternalId UNIQUE (ExternalId) WHERE ExternalId IS NOT NULL
);
GO

CREATE INDEX IX_RecipeStaging_ProcessingStatus ON RecipeStaging(ProcessingStatus) WHERE IsDeleted = 0;
CREATE INDEX IX_RecipeStaging_CreatedAt ON RecipeStaging(CreatedAt) WHERE IsDeleted = 0;
CREATE INDEX IX_RecipeStaging_ProcessedAt ON RecipeStaging(ProcessedAt) WHERE IsDeleted = 0;
CREATE INDEX IX_RecipeStaging_ExternalId ON RecipeStaging(ExternalId) WHERE IsDeleted = 0 AND ExternalId IS NOT NULL;
GO
