-- Migration: 003_PurchasePatternsAndPriceWatch
-- Description: Purchase patterns, consumption tracking, price watch, and abandoned product inquiry
-- Date: 2026-03-09

-- Records every purchase from any source
CREATE TABLE PurchaseEvent (
    Id           UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId       UNIQUEIDENTIFIER NOT NULL,
    HouseholdId  UNIQUEIDENTIFIER NULL,
    ProductId    UNIQUEIDENTIFIER NULL,
    IngredientId UNIQUEIDENTIFIER NULL,
    CustomName   NVARCHAR(200) NULL,
    Barcode      NVARCHAR(100) NULL,
    Quantity     DECIMAL(10,2) NOT NULL DEFAULT 1,
    Unit         NVARCHAR(50) NULL,
    Price        DECIMAL(10,2) NULL,
    StoreId      UNIQUEIDENTIFIER NULL,
    StoreName    NVARCHAR(200) NULL,
    PurchasedAt  DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    Source       NVARCHAR(50) NOT NULL DEFAULT 'ManualAdd'
    -- Source: 'ShoppingList'|'ReceiptScan'|'ManualAdd'|'ScanSession'
);
CREATE INDEX IX_PurchaseEvent_UserId_ProductId ON PurchaseEvent(UserId, ProductId, PurchasedAt DESC);
CREATE INDEX IX_PurchaseEvent_PurchasedAt ON PurchaseEvent(PurchasedAt DESC);
GO

CREATE TABLE ProductConsumptionPattern (
    Id                        UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId                    UNIQUEIDENTIFIER NOT NULL,
    HouseholdId               UNIQUEIDENTIFIER NULL,
    ProductId                 UNIQUEIDENTIFIER NULL,
    IngredientId              UNIQUEIDENTIFIER NULL,
    CustomName                NVARCHAR(200) NULL,
    AvgDaysBetweenPurchases   DECIMAL(8,2) NULL,
    StdDevDays                DECIMAL(8,2) NULL,
    PurchaseCount             INT NOT NULL DEFAULT 0,
    FirstPurchasedAt          DATETIME2 NULL,
    LastPurchasedAt           DATETIME2 NULL,
    EstimatedNextPurchaseDate DATETIME2 NULL,
    LowStockAlertDaysAhead    INT NOT NULL DEFAULT 3,
    IsAbandoned               BIT NOT NULL DEFAULT 0,
    AbandonedAfterCount       INT NULL,
    CalculatedAt              DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT UQ_ConsumptionPattern UNIQUE (UserId, ProductId, IngredientId, CustomName)
);
CREATE INDEX IX_ConsumptionPattern_UserId ON ProductConsumptionPattern(UserId);
GO

CREATE TABLE PriceWatchAlert (
    Id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId          UNIQUEIDENTIFIER NOT NULL,
    HouseholdId     UNIQUEIDENTIFIER NULL,
    ProductId       UNIQUEIDENTIFIER NULL,
    InventoryItemId UNIQUEIDENTIFIER NULL,
    TargetPrice     DECIMAL(10,2) NULL,
    WatchStartedAt  DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    AlertSentAt     DATETIME2 NULL,
    DealFound       BIT NOT NULL DEFAULT 0,
    DealStoreId     UNIQUEIDENTIFIER NULL,
    DealPrice       DECIMAL(10,2) NULL,
    DealEndsAt      DATETIME2 NULL,
    IsResolved      BIT NOT NULL DEFAULT 0,
    ResolvedAt      DATETIME2 NULL
);
CREATE INDEX IX_PriceWatchAlert_UserId ON PriceWatchAlert(UserId, IsResolved);
GO

CREATE TABLE AbandonedProductInquiry (
    Id                 UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId             UNIQUEIDENTIFIER NOT NULL,
    ProductId          UNIQUEIDENTIFIER NULL,
    CustomName         NVARCHAR(200) NULL,
    NotificationSentAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    Response           NVARCHAR(50) NULL,
    -- 'RareUse'|'DidntLike'|'Substitution'|'Allergy'|'Gift'|'Other'|'NoResponse'
    ResponseNote       NVARCHAR(500) NULL,
    RespondedAt        DATETIME2 NULL,
    IsActioned         BIT NOT NULL DEFAULT 0
);
CREATE INDEX IX_AbandonedInquiry_UserId ON AbandonedProductInquiry(UserId);
GO
