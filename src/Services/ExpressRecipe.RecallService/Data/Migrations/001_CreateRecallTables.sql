-- Migration: 001_CreateRecallTables
-- Description: Create FDA/USDA recall tracking tables
-- Date: 2024-11-19

CREATE TABLE Recall (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ExternalId NVARCHAR(200) NOT NULL, -- FDA/USDA recall ID
    Source NVARCHAR(50) NOT NULL, -- FDA, USDA, Custom
    Title NVARCHAR(500) NOT NULL,
    Description NVARCHAR(MAX) NULL,
    Reason NVARCHAR(MAX) NULL,
    Severity NVARCHAR(50) NOT NULL, -- Critical, High, Medium, Low
    RecallDate DATE NOT NULL,
    PublishedDate DATETIME2 NOT NULL,
    Status NVARCHAR(50) NOT NULL DEFAULT 'Active', -- Active, Resolved, Expired
    SourceUrl NVARCHAR(1000) NULL,
    ImportedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NULL,
    
    CONSTRAINT UQ_Recall_ExternalId UNIQUE (ExternalId)
);

CREATE INDEX IX_Recall_PublishedDate ON Recall(PublishedDate);
CREATE INDEX IX_Recall_Status ON Recall(Status);
GO

CREATE TABLE RecallProduct (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    RecallId UNIQUEIDENTIFIER NOT NULL,
    ProductId UNIQUEIDENTIFIER NULL, -- Link to our product database
    ProductName NVARCHAR(300) NOT NULL,
    Brand NVARCHAR(200) NULL,
    UPC NVARCHAR(50) NULL,
    LotNumber NVARCHAR(100) NULL,
    ExpirationDate DATE NULL,
    DistributionArea NVARCHAR(MAX) NULL,
    
    CONSTRAINT FK_RecallProduct_Recall FOREIGN KEY (RecallId)
        REFERENCES Recall(Id) ON DELETE CASCADE
);

CREATE INDEX IX_RecallProduct_RecallId ON RecallProduct(RecallId);
CREATE INDEX IX_RecallProduct_ProductId ON RecallProduct(ProductId);
GO

CREATE TABLE RecallAlert (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    RecallId UNIQUEIDENTIFIER NOT NULL,
    InventoryItemId UNIQUEIDENTIFIER NULL,
    AlertType NVARCHAR(50) NOT NULL, -- InventoryMatch, Subscription
    IsRead BIT NOT NULL DEFAULT 0,
    ReadAt DATETIME2 NULL,
    IsDismissed BIT NOT NULL DEFAULT 0,
    DismissedAt DATETIME2 NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    
    CONSTRAINT FK_RecallAlert_Recall FOREIGN KEY (RecallId)
        REFERENCES Recall(Id) ON DELETE CASCADE
);

CREATE INDEX IX_RecallAlert_UserId ON RecallAlert(UserId);
CREATE INDEX IX_RecallAlert_CreatedAt ON RecallAlert(CreatedAt);
GO

CREATE TABLE RecallSubscription (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    SubscriptionType NVARCHAR(50) NOT NULL, -- AllRecalls, ByCategory, ByBrand
    FilterValue NVARCHAR(200) NULL, -- Category or brand name
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);

CREATE INDEX IX_RecallSubscription_UserId ON RecallSubscription(UserId);
GO
