-- Product Staging Table
-- Stores raw imported product data before processing
CREATE TABLE ProductStaging (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),

    -- OpenFoodFacts core fields
    ExternalId NVARCHAR(100) NOT NULL, -- OpenFoodFacts product code
    Barcode NVARCHAR(50) NULL,
    ProductName NVARCHAR(MAX) NULL,
    GenericName NVARCHAR(MAX) NULL,
    Brands NVARCHAR(MAX) NULL,

    -- Ingredient data (raw text from OpenFoodFacts)
    IngredientsText NVARCHAR(MAX) NULL,
    IngredientsTextEn NVARCHAR(MAX) NULL,

    -- Allergens
    Allergens NVARCHAR(MAX) NULL,
    AllergensHierarchy NVARCHAR(MAX) NULL, -- JSON array

    -- Categories
    Categories NVARCHAR(MAX) NULL,
    CategoriesHierarchy NVARCHAR(MAX) NULL, -- JSON array

    -- Nutrition (JSON from OpenFoodFacts)
    NutritionData NVARCHAR(MAX) NULL,

    -- Images
    ImageUrl NVARCHAR(MAX) NULL,
    ImageSmallUrl NVARCHAR(MAX) NULL,

    -- Quality/metadata
    Lang NVARCHAR(10) NULL,
    Countries NVARCHAR(MAX) NULL,
    NutriScore NVARCHAR(10) NULL,
    NovaGroup INT NULL,
    EcoScore NVARCHAR(10) NULL,

    -- Full JSON (for fields we don't explicitly store)
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

    -- Ensure we don't import duplicates
    CONSTRAINT UQ_ProductStaging_ExternalId UNIQUE (ExternalId)
);
GO

CREATE INDEX IX_ProductStaging_Barcode ON ProductStaging(Barcode) WHERE IsDeleted = 0 AND Barcode IS NOT NULL;
CREATE INDEX IX_ProductStaging_ProcessingStatus ON ProductStaging(ProcessingStatus) WHERE IsDeleted = 0;
CREATE INDEX IX_ProductStaging_CreatedAt ON ProductStaging(CreatedAt) WHERE IsDeleted = 0;
CREATE INDEX IX_ProductStaging_ProcessedAt ON ProductStaging(ProcessedAt) WHERE IsDeleted = 0;
GO
