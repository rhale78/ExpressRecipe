-- Product-linked price table (normalized, linked to ProductService products by ID)
CREATE TABLE ProductPrice (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ProductId UNIQUEIDENTIFIER NOT NULL,       -- Links to ProductService product ID
    Upc NVARCHAR(100) NULL,                    -- UPC/barcode for cross-reference
    ProductName NVARCHAR(300) NOT NULL,        -- Denormalized for performance
    StoreId UNIQUEIDENTIFIER NULL,             -- Links to Store table (nullable for non-store prices)
    StoreName NVARCHAR(200) NULL,              -- Denormalized
    StoreChain NVARCHAR(200) NULL,
    City NVARCHAR(100) NULL,
    State NVARCHAR(50) NULL,
    Price DECIMAL(10, 2) NOT NULL,
    Currency NVARCHAR(10) NOT NULL DEFAULT 'USD',
    Unit NVARCHAR(50) NULL,
    Quantity DECIMAL(10, 3) NULL,
    PricePerUnit DECIMAL(10, 4) NULL,          -- Computed: Price / Quantity
    DataSource NVARCHAR(100) NOT NULL,         -- OpenPrices, GroceryDB, USDA, Manual
    ExternalId NVARCHAR(200) NULL,             -- Source system ID
    ObservedAt DATETIME2 NOT NULL,
    ImportedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    IsActive BIT NOT NULL DEFAULT 1
);

CREATE INDEX IX_ProductPrice_ProductId ON ProductPrice(ProductId);
CREATE INDEX IX_ProductPrice_Upc ON ProductPrice(Upc);
CREATE INDEX IX_ProductPrice_StoreName ON ProductPrice(StoreName);
CREATE INDEX IX_ProductPrice_DataSource ON ProductPrice(DataSource);
CREATE INDEX IX_ProductPrice_ObservedAt ON ProductPrice(ObservedAt);
CREATE INDEX IX_ProductPrice_ExternalId ON ProductPrice(ExternalId, DataSource);
GO

-- Import log table
CREATE TABLE PriceImportLog (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    DataSource NVARCHAR(100) NOT NULL,
    ImportedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    RecordsProcessed INT NOT NULL DEFAULT 0,
    RecordsImported INT NOT NULL DEFAULT 0,
    RecordsUpdated INT NOT NULL DEFAULT 0,
    RecordsSkipped INT NOT NULL DEFAULT 0,
    ErrorCount INT NOT NULL DEFAULT 0,
    ErrorMessage NVARCHAR(MAX) NULL,
    Success BIT NOT NULL DEFAULT 0
);
GO
