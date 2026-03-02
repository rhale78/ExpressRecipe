CREATE TABLE GroceryStore (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Name NVARCHAR(200) NOT NULL,
    Chain NVARCHAR(200) NULL,
    StoreType NVARCHAR(100) NULL,
    Address NVARCHAR(500) NULL,
    City NVARCHAR(100) NULL,
    State NVARCHAR(50) NULL,
    ZipCode NVARCHAR(20) NULL,
    County NVARCHAR(100) NULL,
    Latitude DECIMAL(10, 7) NULL,
    Longitude DECIMAL(10, 7) NULL,
    PhoneNumber NVARCHAR(50) NULL,
    Website NVARCHAR(500) NULL,
    ExternalId NVARCHAR(200) NULL,
    DataSource NVARCHAR(100) NULL,
    AcceptsSnap BIT NOT NULL DEFAULT 0,
    IsActive BIT NOT NULL DEFAULT 1,
    OpeningHours NVARCHAR(500) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NULL
);

CREATE INDEX IX_GroceryStore_City_State ON GroceryStore(City, State);
CREATE INDEX IX_GroceryStore_ZipCode ON GroceryStore(ZipCode);
CREATE INDEX IX_GroceryStore_Chain ON GroceryStore(Chain);
CREATE INDEX IX_GroceryStore_Lat_Lon ON GroceryStore(Latitude, Longitude);
CREATE INDEX IX_GroceryStore_ExternalId ON GroceryStore(ExternalId, DataSource);
GO

CREATE TABLE StoreImportLog (
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
