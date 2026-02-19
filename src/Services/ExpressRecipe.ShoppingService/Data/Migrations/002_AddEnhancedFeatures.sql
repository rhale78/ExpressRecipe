-- Migration: 002_AddEnhancedFeatures
-- Description: Add price comparison, recipe integration, and advanced shopping features
-- Date: 2026-02-16

-- Add household support to ShoppingList
ALTER TABLE ShoppingList ADD HouseholdId UNIQUEIDENTIFIER NULL;
ALTER TABLE ShoppingList ADD ListType NVARCHAR(50) NOT NULL DEFAULT 'Standard'; -- Standard, Future, Template
ALTER TABLE ShoppingList ADD ScheduledFor DATETIME2 NULL; -- For future lists
ALTER TABLE ShoppingList ADD StoreId UNIQUEIDENTIFIER NULL; -- Preferred store for this list

CREATE INDEX IX_ShoppingList_HouseholdId ON ShoppingList(HouseholdId);
CREATE INDEX IX_ShoppingList_StoreId ON ShoppingList(StoreId);
CREATE INDEX IX_ShoppingList_ListType ON ShoppingList(ListType);
CREATE INDEX IX_ShoppingList_ScheduledFor ON ShoppingList(ScheduledFor) WHERE ScheduledFor IS NOT NULL;
GO

-- Enhance ShoppingListItem with more fields
ALTER TABLE ShoppingListItem ADD IsFavorite BIT NOT NULL DEFAULT 0;
ALTER TABLE ShoppingListItem ADD IsGeneric BIT NOT NULL DEFAULT 0; -- Generic item like "ketchup" vs specific "Kraft Ketchup 32oz"
ALTER TABLE ShoppingListItem ADD PreferredBrand NVARCHAR(200) NULL;
ALTER TABLE ShoppingListItem ADD MinQuantity DECIMAL(10, 2) NULL;
ALTER TABLE ShoppingListItem ADD MaxPrice DECIMAL(10, 2) NULL; -- Price alert threshold
ALTER TABLE ShoppingListItem ADD AddedFromRecipeId UNIQUEIDENTIFIER NULL;
ALTER TABLE ShoppingListItem ADD AddedFromInventory BIT NOT NULL DEFAULT 0;
ALTER TABLE ShoppingListItem ADD StoreId UNIQUEIDENTIFIER NULL; -- Which store to buy from
ALTER TABLE ShoppingListItem ADD BestPriceStoreId UNIQUEIDENTIFIER NULL;
ALTER TABLE ShoppingListItem ADD BestPrice DECIMAL(10, 2) NULL;
ALTER TABLE ShoppingListItem ADD UnitPrice DECIMAL(10, 2) NULL; -- Price per unit for comparison
ALTER TABLE ShoppingListItem ADD UnitSize DECIMAL(10, 2) NULL; -- Size for unit price calculation
ALTER TABLE ShoppingListItem ADD UnitOfMeasure NVARCHAR(50) NULL; -- oz, lb, g, ml, etc.
ALTER TABLE ShoppingListItem ADD HasDeal BIT NOT NULL DEFAULT 0;
ALTER TABLE ShoppingListItem ADD DealType NVARCHAR(50) NULL; -- BOGO, Buy1Get50Off, Sale, etc.
ALTER TABLE ShoppingListItem ADD DealDescription NVARCHAR(500) NULL;
ALTER TABLE ShoppingListItem ADD PurchasedAt DATETIME2 NULL;
ALTER TABLE ShoppingListItem ADD AddToInventoryOnPurchase BIT NOT NULL DEFAULT 1;

CREATE INDEX IX_ShoppingListItem_IsFavorite ON ShoppingListItem(IsFavorite) WHERE IsFavorite = 1;
CREATE INDEX IX_ShoppingListItem_StoreId ON ShoppingListItem(StoreId);
CREATE INDEX IX_ShoppingListItem_AddedFromRecipeId ON ShoppingListItem(AddedFromRecipeId) WHERE AddedFromRecipeId IS NOT NULL;
GO

-- FavoriteItem: User's favorite shopping items for quick add
CREATE TABLE FavoriteItem (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    HouseholdId UNIQUEIDENTIFIER NULL,
    ProductId UNIQUEIDENTIFIER NULL,
    CustomName NVARCHAR(200) NULL,
    PreferredBrand NVARCHAR(200) NULL,
    TypicalQuantity DECIMAL(10, 2) NOT NULL DEFAULT 1,
    TypicalUnit NVARCHAR(50) NULL,
    Category NVARCHAR(100) NULL,
    IsGeneric BIT NOT NULL DEFAULT 0,
    LastUsed DATETIME2 NULL,
    UseCount INT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);

CREATE INDEX IX_FavoriteItem_UserId ON FavoriteItem(UserId);
CREATE INDEX IX_FavoriteItem_HouseholdId ON FavoriteItem(HouseholdId);
CREATE INDEX IX_FavoriteItem_UseCount ON FavoriteItem(UseCount DESC);
GO

-- Store: Enhanced store information
CREATE TABLE Store (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Name NVARCHAR(200) NOT NULL,
    Chain NVARCHAR(200) NULL,
    Address NVARCHAR(500) NULL,
    City NVARCHAR(100) NULL,
    State NVARCHAR(50) NULL,
    ZipCode NVARCHAR(20) NULL,
    Latitude DECIMAL(10, 7) NULL,
    Longitude DECIMAL(10, 7) NULL,
    Phone NVARCHAR(50) NULL,
    Hours NVARCHAR(500) NULL,
    IsPreferred BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NULL,
    IsDeleted BIT NOT NULL DEFAULT 0
);

CREATE INDEX IX_Store_ZipCode ON Store(ZipCode);
CREATE INDEX IX_Store_Location ON Store(Latitude, Longitude) WHERE Latitude IS NOT NULL AND Longitude IS NOT NULL;
CREATE INDEX IX_Store_Chain ON Store(Chain);
GO

-- Update StoreLayout to reference Store table
ALTER TABLE StoreLayout ADD StoreId UNIQUEIDENTIFIER NULL;
ALTER TABLE StoreLayout ADD 
    CONSTRAINT FK_StoreLayout_Store FOREIGN KEY (StoreId)
        REFERENCES Store(Id);

CREATE INDEX IX_StoreLayout_StoreId ON StoreLayout(StoreId);
GO

-- ShoppingListTemplate: Reusable shopping list templates
CREATE TABLE ShoppingListTemplate (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    HouseholdId UNIQUEIDENTIFIER NULL,
    Name NVARCHAR(200) NOT NULL,
    Description NVARCHAR(MAX) NULL,
    Category NVARCHAR(100) NULL, -- Weekly, Monthly, Party, Holiday, etc.
    UseCount INT NOT NULL DEFAULT 0,
    LastUsed DATETIME2 NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted BIT NOT NULL DEFAULT 0
);

CREATE INDEX IX_ShoppingListTemplate_UserId ON ShoppingListTemplate(UserId);
CREATE INDEX IX_ShoppingListTemplate_HouseholdId ON ShoppingListTemplate(HouseholdId);
GO

-- ShoppingListTemplateItem: Items in templates
CREATE TABLE ShoppingListTemplateItem (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    TemplateId UNIQUEIDENTIFIER NOT NULL,
    ProductId UNIQUEIDENTIFIER NULL,
    CustomName NVARCHAR(200) NULL,
    Quantity DECIMAL(10, 2) NOT NULL DEFAULT 1,
    Unit NVARCHAR(50) NULL,
    Category NVARCHAR(100) NULL,
    OrderIndex INT NOT NULL DEFAULT 0,
    
    CONSTRAINT FK_ShoppingListTemplateItem_Template FOREIGN KEY (TemplateId)
        REFERENCES ShoppingListTemplate(Id) ON DELETE CASCADE
);

CREATE INDEX IX_ShoppingListTemplateItem_TemplateId ON ShoppingListTemplateItem(TemplateId);
GO

-- PriceComparison: Track price comparisons for shopping decisions
CREATE TABLE PriceComparison (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ShoppingListItemId UNIQUEIDENTIFIER NOT NULL,
    ProductId UNIQUEIDENTIFIER NULL,
    StoreId UNIQUEIDENTIFIER NOT NULL,
    Price DECIMAL(10, 2) NOT NULL,
    UnitPrice DECIMAL(10, 2) NULL,
    Size DECIMAL(10, 2) NULL,
    Unit NVARCHAR(50) NULL,
    HasDeal BIT NOT NULL DEFAULT 0,
    DealType NVARCHAR(50) NULL,
    DealEndDate DATETIME2 NULL,
    IsAvailable BIT NOT NULL DEFAULT 1,
    LastChecked DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    
    CONSTRAINT FK_PriceComparison_ShoppingListItem FOREIGN KEY (ShoppingListItemId)
        REFERENCES ShoppingListItem(Id) ON DELETE CASCADE,
    CONSTRAINT FK_PriceComparison_Store FOREIGN KEY (StoreId)
        REFERENCES Store(Id)
);

CREATE INDEX IX_PriceComparison_ShoppingListItemId ON PriceComparison(ShoppingListItemId);
CREATE INDEX IX_PriceComparison_StoreId ON PriceComparison(StoreId);
CREATE INDEX IX_PriceComparison_UnitPrice ON PriceComparison(UnitPrice);
GO

-- ShoppingScanSession: Track scanning sessions for purchasing
CREATE TABLE ShoppingScanSession (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    ShoppingListId UNIQUEIDENTIFIER NOT NULL,
    StoreId UNIQUEIDENTIFIER NULL,
    SessionType NVARCHAR(50) NOT NULL DEFAULT 'Purchasing', -- Purchasing, Verifying
    StartedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    EndedAt DATETIME2 NULL,
    ItemsScanned INT NOT NULL DEFAULT 0,
    TotalSpent DECIMAL(10, 2) NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    
    CONSTRAINT FK_ShoppingScanSession_ShoppingList FOREIGN KEY (ShoppingListId)
        REFERENCES ShoppingList(Id) ON DELETE CASCADE,
    CONSTRAINT FK_ShoppingScanSession_Store FOREIGN KEY (StoreId)
        REFERENCES Store(Id)
);

CREATE INDEX IX_ShoppingScanSession_UserId ON ShoppingScanSession(UserId);
CREATE INDEX IX_ShoppingScanSession_ShoppingListId ON ShoppingScanSession(ShoppingListId);
CREATE INDEX IX_ShoppingScanSession_IsActive ON ShoppingScanSession(IsActive) WHERE IsActive = 1;
GO
