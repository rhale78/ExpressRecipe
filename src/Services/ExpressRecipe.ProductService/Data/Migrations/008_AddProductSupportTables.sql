-- Product Metadata Table
-- Stores flexible key-value metadata for products
CREATE TABLE ProductMetadata (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ProductId UNIQUEIDENTIFIER NOT NULL,
    MetaKey NVARCHAR(100) NOT NULL,
    MetaValue NVARCHAR(MAX) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy UNIQUEIDENTIFIER NULL,
    UpdatedAt DATETIME2 NULL,
    UpdatedBy UNIQUEIDENTIFIER NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    DeletedAt DATETIME2 NULL,
    RowVersion ROWVERSION,
    CONSTRAINT FK_ProductMetadata_Product FOREIGN KEY (ProductId)
        REFERENCES Product(Id) ON DELETE CASCADE,
    CONSTRAINT UQ_ProductMetadata_Product_Key UNIQUE (ProductId, MetaKey)
);
GO

CREATE INDEX IX_ProductMetadata_ProductId ON ProductMetadata(ProductId) WHERE IsDeleted = 0;
CREATE INDEX IX_ProductMetadata_MetaKey ON ProductMetadata(MetaKey) WHERE IsDeleted = 0;
GO

-- Product External Links Table
-- Links products to external sources (OpenFoodFacts, USDA, etc.)
CREATE TABLE ProductExternalLink (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ProductId UNIQUEIDENTIFIER NOT NULL,
    Source NVARCHAR(100) NOT NULL, -- e.g., "OpenFoodFacts", "USDA", "Barcode Lookup"
    ExternalId NVARCHAR(200) NOT NULL, -- External system's product ID
    ExternalUrl NVARCHAR(500) NULL, -- Optional direct link
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy UNIQUEIDENTIFIER NULL,
    UpdatedAt DATETIME2 NULL,
    UpdatedBy UNIQUEIDENTIFIER NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    DeletedAt DATETIME2 NULL,
    RowVersion ROWVERSION,
    CONSTRAINT FK_ProductExternalLink_Product FOREIGN KEY (ProductId)
        REFERENCES Product(Id) ON DELETE CASCADE,
    CONSTRAINT UQ_ProductExternalLink_Source_ExternalId UNIQUE (Source, ExternalId)
);
GO

CREATE INDEX IX_ProductExternalLink_ProductId ON ProductExternalLink(ProductId) WHERE IsDeleted = 0;
CREATE INDEX IX_ProductExternalLink_Source ON ProductExternalLink(Source) WHERE IsDeleted = 0;
CREATE INDEX IX_ProductExternalLink_ExternalId ON ProductExternalLink(ExternalId) WHERE IsDeleted = 0;
GO

-- Product Allergens Table
-- Tracks allergens present in products
CREATE TABLE ProductAllergen (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ProductId UNIQUEIDENTIFIER NOT NULL,
    AllergenName NVARCHAR(200) NOT NULL,
    AllergenType NVARCHAR(50) NULL, -- e.g., "Contains", "May Contain", "Processed On Equipment"
    Severity NVARCHAR(50) NULL, -- e.g., "High", "Medium", "Low"
    Notes NVARCHAR(MAX) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy UNIQUEIDENTIFIER NULL,
    UpdatedAt DATETIME2 NULL,
    UpdatedBy UNIQUEIDENTIFIER NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    DeletedAt DATETIME2 NULL,
    RowVersion ROWVERSION,
    CONSTRAINT FK_ProductAllergen_Product FOREIGN KEY (ProductId)
        REFERENCES Product(Id) ON DELETE CASCADE
);
GO

CREATE INDEX IX_ProductAllergen_ProductId ON ProductAllergen(ProductId) WHERE IsDeleted = 0;
CREATE INDEX IX_ProductAllergen_AllergenName ON ProductAllergen(AllergenName) WHERE IsDeleted = 0;
GO

-- Product Labels Table
-- Stores product labels (Organic, Non-GMO, Fair Trade, etc.)
CREATE TABLE ProductLabel (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ProductId UNIQUEIDENTIFIER NOT NULL,
    LabelName NVARCHAR(200) NOT NULL,
    LabelType NVARCHAR(50) NULL, -- e.g., "Certification", "Dietary", "Quality"
    CertifyingBody NVARCHAR(200) NULL, -- e.g., "USDA", "Non-GMO Project"
    VerifiedDate DATETIME2 NULL,
    ExpirationDate DATETIME2 NULL,
    Notes NVARCHAR(MAX) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy UNIQUEIDENTIFIER NULL,
    UpdatedAt DATETIME2 NULL,
    UpdatedBy UNIQUEIDENTIFIER NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    DeletedAt DATETIME2 NULL,
    RowVersion ROWVERSION,
    CONSTRAINT FK_ProductLabel_Product FOREIGN KEY (ProductId)
        REFERENCES Product(Id) ON DELETE CASCADE
);
GO

CREATE INDEX IX_ProductLabel_ProductId ON ProductLabel(ProductId) WHERE IsDeleted = 0;
CREATE INDEX IX_ProductLabel_LabelName ON ProductLabel(LabelName) WHERE IsDeleted = 0;
GO
