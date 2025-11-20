-- Migration: 001_CreateInventoryTables
-- Description: Create inventory management tables
-- Date: 2024-11-19

-- StorageLocation: Where items are stored
CREATE TABLE StorageLocation (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    Name NVARCHAR(100) NOT NULL, -- Pantry, Fridge, Freezer, etc.
    Description NVARCHAR(500) NULL,
    Temperature NVARCHAR(50) NULL, -- Room, Cold, Frozen
    IsDefault BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NULL,
    IsDeleted BIT NOT NULL DEFAULT 0
);

CREATE INDEX IX_StorageLocation_UserId ON StorageLocation(UserId);
GO

-- InventoryItem: User's inventory tracking
CREATE TABLE InventoryItem (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    ProductId UNIQUEIDENTIFIER NULL, -- References ProductService.Product.Id
    IngredientId UNIQUEIDENTIFIER NULL, -- References ProductService.Ingredient.Id
    CustomName NVARCHAR(200) NULL, -- For items not in database
    StorageLocationId UNIQUEIDENTIFIER NOT NULL,
    Quantity DECIMAL(10, 2) NOT NULL DEFAULT 1,
    Unit NVARCHAR(50) NULL, -- count, oz, lb, g, kg, etc.
    PurchaseDate DATETIME2 NULL,
    ExpirationDate DATETIME2 NULL,
    OpenedDate DATETIME2 NULL,
    Notes NVARCHAR(MAX) NULL,
    Barcode NVARCHAR(100) NULL,
    Price DECIMAL(10, 2) NULL,
    Store NVARCHAR(200) NULL,
    IsOpened BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,

    CONSTRAINT FK_InventoryItem_StorageLocation FOREIGN KEY (StorageLocationId)
        REFERENCES StorageLocation(Id) ON DELETE CASCADE
);

CREATE INDEX IX_InventoryItem_UserId ON InventoryItem(UserId);
CREATE INDEX IX_InventoryItem_ProductId ON InventoryItem(ProductId);
CREATE INDEX IX_InventoryItem_ExpirationDate ON InventoryItem(ExpirationDate);
CREATE INDEX IX_InventoryItem_StorageLocationId ON InventoryItem(StorageLocationId);
GO

-- InventoryHistory: Track usage and changes
CREATE TABLE InventoryHistory (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    InventoryItemId UNIQUEIDENTIFIER NOT NULL,
    UserId UNIQUEIDENTIFIER NOT NULL,
    ActionType NVARCHAR(50) NOT NULL, -- Added, Used, Removed, Expired, Updated
    QuantityChange DECIMAL(10, 2) NOT NULL,
    QuantityBefore DECIMAL(10, 2) NOT NULL,
    QuantityAfter DECIMAL(10, 2) NOT NULL,
    Reason NVARCHAR(500) NULL,
    RecipeId UNIQUEIDENTIFIER NULL, -- If used in a recipe
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

    CONSTRAINT FK_InventoryHistory_InventoryItem FOREIGN KEY (InventoryItemId)
        REFERENCES InventoryItem(Id) ON DELETE CASCADE
);

CREATE INDEX IX_InventoryHistory_InventoryItemId ON InventoryHistory(InventoryItemId);
CREATE INDEX IX_InventoryHistory_UserId ON InventoryHistory(UserId);
CREATE INDEX IX_InventoryHistory_CreatedAt ON InventoryHistory(CreatedAt);
GO

-- ExpirationAlert: Expiration notifications
CREATE TABLE ExpirationAlert (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    InventoryItemId UNIQUEIDENTIFIER NOT NULL,
    AlertType NVARCHAR(50) NOT NULL, -- Warning, Critical, Expired
    DaysUntilExpiration INT NOT NULL,
    AlertDate DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    IsDismissed BIT NOT NULL DEFAULT 0,
    DismissedAt DATETIME2 NULL,
    IsNotified BIT NOT NULL DEFAULT 0,
    NotifiedAt DATETIME2 NULL,

    CONSTRAINT FK_ExpirationAlert_InventoryItem FOREIGN KEY (InventoryItemId)
        REFERENCES InventoryItem(Id) ON DELETE CASCADE
);

CREATE INDEX IX_ExpirationAlert_UserId ON ExpirationAlert(UserId);
CREATE INDEX IX_ExpirationAlert_AlertDate ON ExpirationAlert(AlertDate);
GO

-- UsagePrediction: ML-based usage predictions
CREATE TABLE UsagePrediction (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    ProductId UNIQUEIDENTIFIER NULL,
    IngredientId UNIQUEIDENTIFIER NULL,
    PredictedUsagePerWeek DECIMAL(10, 2) NOT NULL,
    ConfidenceScore DECIMAL(5, 4) NOT NULL,
    ReorderThreshold DECIMAL(10, 2) NULL,
    SuggestedQuantity DECIMAL(10, 2) NULL,
    CalculatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    BasedOnDays INT NOT NULL, -- Historical data window

    CONSTRAINT UQ_UsagePrediction_User_Product UNIQUE (UserId, ProductId, IngredientId)
);

CREATE INDEX IX_UsagePrediction_UserId ON UsagePrediction(UserId);
GO
