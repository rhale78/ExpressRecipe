-- Migration: 001_CreateSearchTables
-- Description: Create full-text search and recommendation tables
-- Date: 2024-11-19

CREATE TABLE SearchIndex (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    EntityType NVARCHAR(100) NOT NULL,
    EntityId UNIQUEIDENTIFIER NOT NULL,
    Title NVARCHAR(500) NOT NULL,
    Description NVARCHAR(MAX) NULL,
    Keywords NVARCHAR(MAX) NULL,
    Tags NVARCHAR(500) NULL, -- Comma-separated
    Category NVARCHAR(100) NULL,
    SearchVector NVARCHAR(MAX) NOT NULL, -- Full-text search data
    PopularityScore DECIMAL(10, 4) NOT NULL DEFAULT 0,
    QualityScore DECIMAL(10, 4) NOT NULL DEFAULT 0,
    LastIndexedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    IsActive BIT NOT NULL DEFAULT 1,
    
    CONSTRAINT UQ_SearchIndex_Entity UNIQUE (EntityType, EntityId)
);

CREATE FULLTEXT INDEX ON SearchIndex(Title, Description, Keywords) 
    KEY INDEX PK__SearchIn__3214EC0728FD2FB6;

CREATE INDEX IX_SearchIndex_EntityType ON SearchIndex(EntityType);
CREATE INDEX IX_SearchIndex_PopularityScore ON SearchIndex(PopularityScore DESC);
GO

CREATE TABLE SearchHistory (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    Query NVARCHAR(500) NOT NULL,
    EntityType NVARCHAR(100) NULL,
    Filters NVARCHAR(MAX) NULL, -- JSON
    ResultCount INT NOT NULL,
    ClickedEntityId UNIQUEIDENTIFIER NULL,
    ClickPosition INT NULL,
    SearchedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);

CREATE INDEX IX_SearchHistory_UserId ON SearchHistory(UserId);
CREATE INDEX IX_SearchHistory_SearchedAt ON SearchHistory(SearchedAt);
GO

CREATE TABLE UserPreference (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    PreferenceType NVARCHAR(100) NOT NULL, -- Category, Brand, Cuisine, etc.
    PreferenceValue NVARCHAR(200) NOT NULL,
    Weight DECIMAL(5, 4) NOT NULL DEFAULT 1.0,
    LearnedFrom NVARCHAR(50) NOT NULL, -- Explicit, Implicit, Inferred
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    
    CONSTRAINT UQ_UserPreference UNIQUE (UserId, PreferenceType, PreferenceValue)
);

CREATE INDEX IX_UserPreference_UserId ON UserPreference(UserId);
GO

CREATE TABLE Recommendation (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    EntityType NVARCHAR(100) NOT NULL,
    EntityId UNIQUEIDENTIFIER NOT NULL,
    RecommendationType NVARCHAR(50) NOT NULL, -- Personalized, Trending, Similar
    Score DECIMAL(10, 4) NOT NULL,
    Reason NVARCHAR(500) NULL,
    GeneratedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    ExpiresAt DATETIME2 NULL,
    IsClicked BIT NOT NULL DEFAULT 0,
    ClickedAt DATETIME2 NULL
);

CREATE INDEX IX_Recommendation_UserId ON Recommendation(UserId);
CREATE INDEX IX_Recommendation_Score ON Recommendation(Score DESC);
GO
