-- Migration: 001_CreateSearchTables
-- Description: Create full-text search and recommendation tables
-- Date: 2024-11-19

-- Ensure SearchIndex table exists with a named primary key
IF OBJECT_ID(N'dbo.SearchIndex', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SearchIndex (
        Id UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
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
        CONSTRAINT PK_SearchIndex PRIMARY KEY (Id),
        CONSTRAINT UQ_SearchIndex_Entity UNIQUE (EntityType, EntityId)
    );
END

-- Only attempt FULLTEXT when the feature is installed
IF FULLTEXTSERVICEPROPERTY('IsFullTextInstalled') = 1
BEGIN
    -- Ensure a full-text catalog exists (avoid relying on DEFAULT)
    IF NOT EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name = N'SearchCatalog')
    BEGIN
        CREATE FULLTEXT CATALOG [SearchCatalog] WITH ACCENT_SENSITIVITY = OFF AUTHORIZATION [dbo];
    END

    -- Create FULLTEXT INDEX using the actual PK index (or fallback unique index)
    IF NOT EXISTS (
        SELECT 1
        FROM sys.fulltext_indexes fti
        WHERE fti.object_id = OBJECT_ID(N'dbo.SearchIndex')
    )
    BEGIN
        DECLARE @keyIndex sysname;
        SELECT TOP 1 @keyIndex = i.name
        FROM sys.indexes AS i
        INNER JOIN sys.key_constraints AS kc
            ON kc.parent_object_id = i.object_id AND kc.unique_index_id = i.index_id
        WHERE kc.[type] = 'PK' AND OBJECT_NAME(i.object_id) = 'SearchIndex';

        IF @keyIndex IS NULL
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_SearchIndex_Id' AND object_id = OBJECT_ID(N'dbo.SearchIndex'))
            BEGIN
                CREATE UNIQUE INDEX UX_SearchIndex_Id ON dbo.SearchIndex(Id);
            END
            SET @keyIndex = N'UX_SearchIndex_Id';
        END

        DECLARE @sql NVARCHAR(MAX) =
            N'CREATE FULLTEXT INDEX ON dbo.SearchIndex(Title, Description, Keywords) ' +
            N'KEY INDEX ' + QUOTENAME(@keyIndex) + N' ON [SearchCatalog];';
        EXEC(@sql);
    END
END
ELSE
BEGIN
    PRINT 'Full-Text Search is not installed. Skipping full-text index creation for dbo.SearchIndex.';
END

-- Secondary indexes for SearchIndex
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SearchIndex_EntityType' AND object_id = OBJECT_ID(N'dbo.SearchIndex'))
BEGIN
    CREATE INDEX IX_SearchIndex_EntityType ON dbo.SearchIndex(EntityType);
END
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SearchIndex_PopularityScore' AND object_id = OBJECT_ID(N'dbo.SearchIndex'))
BEGIN
    CREATE INDEX IX_SearchIndex_PopularityScore ON dbo.SearchIndex(PopularityScore DESC);
END
GO

-- SearchHistory table
IF OBJECT_ID(N'dbo.SearchHistory', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SearchHistory (
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
END
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SearchHistory_UserId' AND object_id = OBJECT_ID(N'dbo.SearchHistory'))
BEGIN
    CREATE INDEX IX_SearchHistory_UserId ON dbo.SearchHistory(UserId);
END
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SearchHistory_SearchedAt' AND object_id = OBJECT_ID(N'dbo.SearchHistory'))
BEGIN
    CREATE INDEX IX_SearchHistory_SearchedAt ON dbo.SearchHistory(SearchedAt);
END
GO

-- UserPreference table
IF OBJECT_ID(N'dbo.UserPreference', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserPreference (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        UserId UNIQUEIDENTIFIER NOT NULL,
        PreferenceType NVARCHAR(100) NOT NULL, -- Category, Brand, Cuisine, etc.
        PreferenceValue NVARCHAR(200) NOT NULL,
        Weight DECIMAL(5, 4) NOT NULL DEFAULT 1.0,
        LearnedFrom NVARCHAR(50) NOT NULL, -- Explicit, Implicit, Inferred
        UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT UQ_UserPreference UNIQUE (UserId, PreferenceType, PreferenceValue)
    );
END
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_UserPreference_UserId' AND object_id = OBJECT_ID(N'dbo.UserPreference'))
BEGIN
    CREATE INDEX IX_UserPreference_UserId ON dbo.UserPreference(UserId);
END
GO

-- Recommendation table
IF OBJECT_ID(N'dbo.Recommendation', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Recommendation (
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
END
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Recommendation_UserId' AND object_id = OBJECT_ID(N'dbo.Recommendation'))
BEGIN
    CREATE INDEX IX_Recommendation_UserId ON dbo.Recommendation(UserId);
END
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Recommendation_Score' AND object_id = OBJECT_ID(N'dbo.Recommendation'))
BEGIN
    CREATE INDEX IX_Recommendation_Score ON dbo.Recommendation(Score DESC);
END
GO
