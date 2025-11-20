-- Migration: 001_CreateScannerTables
-- Description: Create barcode scanning and allergen alert tables
-- Date: 2024-11-19

CREATE TABLE ScanHistory (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    ProductId UNIQUEIDENTIFIER NULL,
    Barcode NVARCHAR(100) NOT NULL,
    ScanType NVARCHAR(50) NOT NULL DEFAULT 'Barcode', -- Barcode, OCR, QRCode
    Result NVARCHAR(50) NOT NULL, -- Success, NotFound, Error
    HasAllergenAlert BIT NOT NULL DEFAULT 0,
    ScannedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    Location NVARCHAR(500) NULL, -- GPS coordinates or store name
    DeviceInfo NVARCHAR(500) NULL
);

CREATE INDEX IX_ScanHistory_UserId ON ScanHistory(UserId);
CREATE INDEX IX_ScanHistory_ScannedAt ON ScanHistory(ScannedAt);
CREATE INDEX IX_ScanHistory_Barcode ON ScanHistory(Barcode);
GO

CREATE TABLE ScanAlert (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ScanHistoryId UNIQUEIDENTIFIER NOT NULL,
    UserId UNIQUEIDENTIFIER NOT NULL,
    ProductId UNIQUEIDENTIFIER NULL,
    AllergenId UNIQUEIDENTIFIER NOT NULL,
    AllergenName NVARCHAR(200) NOT NULL,
    Severity NVARCHAR(50) NOT NULL DEFAULT 'Warning', -- Critical, Warning, Info
    AlertMessage NVARCHAR(MAX) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    
    CONSTRAINT FK_ScanAlert_ScanHistory FOREIGN KEY (ScanHistoryId)
        REFERENCES ScanHistory(Id) ON DELETE CASCADE
);

CREATE INDEX IX_ScanAlert_UserId ON ScanAlert(UserId);
CREATE INDEX IX_ScanAlert_ScanHistoryId ON ScanAlert(ScanHistoryId);
GO

CREATE TABLE UnknownProduct (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    Barcode NVARCHAR(100) NOT NULL,
    ProductName NVARCHAR(300) NULL,
    Brand NVARCHAR(200) NULL,
    Category NVARCHAR(100) NULL,
    ImageUrl NVARCHAR(500) NULL,
    Ingredients NVARCHAR(MAX) NULL,
    Status NVARCHAR(50) NOT NULL DEFAULT 'Pending', -- Pending, Approved, Rejected
    SubmittedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    ReviewedAt DATETIME2 NULL,
    ReviewedBy UNIQUEIDENTIFIER NULL
);

CREATE INDEX IX_UnknownProduct_Barcode ON UnknownProduct(Barcode);
CREATE INDEX IX_UnknownProduct_Status ON UnknownProduct(Status);
GO

CREATE TABLE OCRResult (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ScanHistoryId UNIQUEIDENTIFIER NOT NULL,
    UserId UNIQUEIDENTIFIER NOT NULL,
    RawText NVARCHAR(MAX) NULL,
    ParsedIngredients NVARCHAR(MAX) NULL,
    Confidence DECIMAL(5, 4) NULL,
    ProcessedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    
    CONSTRAINT FK_OCRResult_ScanHistory FOREIGN KEY (ScanHistoryId)
        REFERENCES ScanHistory(Id) ON DELETE CASCADE
);
GO
