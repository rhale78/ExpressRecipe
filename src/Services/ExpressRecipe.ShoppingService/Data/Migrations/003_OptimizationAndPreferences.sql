-- Migration: 003_OptimizationAndPreferences
-- Description: Add shopping optimization, category store preferences, and price search profiles
-- Date: 2026-03-09

-- Per-category store preferences
CREATE TABLE UserStoreCategoryPreference (
    Id               UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId           UNIQUEIDENTIFIER NOT NULL,
    HouseholdId      UNIQUEIDENTIFIER NULL,
    Category         NVARCHAR(100) NOT NULL,
    PreferredStoreId UNIQUEIDENTIFIER NOT NULL,
    RankOrder        TINYINT NOT NULL DEFAULT 1,
    IsActive         BIT NOT NULL DEFAULT 1,
    CONSTRAINT UQ_StoreCatPref UNIQUE (UserId, Category, RankOrder)
);
CREATE INDEX IX_StoreCatPref_UserId ON UserStoreCategoryPreference(UserId, IsActive);
GO

-- Optimization result stored per list
CREATE TABLE ShoppingListOptimization (
    Id             UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ShoppingListId UNIQUEIDENTIFIER NOT NULL UNIQUE,
    Strategy       NVARCHAR(50) NOT NULL,
    OptimizedAt    DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    TotalEstimate  DECIMAL(10,2) NULL,
    TotalWithDeals DECIMAL(10,2) NULL,
    StoreCount     INT NOT NULL DEFAULT 1,
    ResultJson     NVARCHAR(MAX) NOT NULL
);
GO

-- Price search preference profile
CREATE TABLE UserPriceSearchProfile (
    Id                   UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId               UNIQUEIDENTIFIER NOT NULL UNIQUE,
    StrategyPriority     NVARCHAR(MAX) NOT NULL DEFAULT '["PreferredBrands","Cheapest"]',
    MaxStoreDistanceMiles INT NOT NULL DEFAULT 25,
    OnlineAllowed        BIT NOT NULL DEFAULT 1,
    DeliveryAllowed      BIT NOT NULL DEFAULT 1,
    PreferredBrandIds    NVARCHAR(MAX) NULL,
    MinRating            DECIMAL(3,1) NULL,
    TryNewBrandsEnabled  BIT NOT NULL DEFAULT 0,
    UpdatedAt            DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);
GO

CREATE TABLE NewBrandTryHistory (
    Id           UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId       UNIQUEIDENTIFIER NOT NULL,
    ProductId    UNIQUEIDENTIFIER NOT NULL,
    Category     NVARCHAR(100) NULL,
    OfferedAt    DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    WasPurchased BIT NOT NULL DEFAULT 0
);
CREATE INDEX IX_NewBrandTry_UserId ON NewBrandTryHistory(UserId, OfferedAt DESC);
GO

-- Extend StoreLayout
ALTER TABLE StoreLayout
    ADD ZoneType     NVARCHAR(50) NULL,
        IsEndOfStore BIT NOT NULL DEFAULT 0;
GO

-- Shopping strategy per list
ALTER TABLE ShoppingList
    ADD ShoppingStrategy NVARCHAR(50) NOT NULL DEFAULT 'SingleStore',
        OptimizedAt      DATETIME2 NULL;
GO
