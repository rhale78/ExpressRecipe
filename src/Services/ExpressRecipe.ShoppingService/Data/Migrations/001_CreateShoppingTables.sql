-- Migration: 001_CreateShoppingTables
-- Description: Create shopping list management tables
-- Date: 2024-11-19

CREATE TABLE ShoppingList (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    Name NVARCHAR(200) NOT NULL,
    Description NVARCHAR(MAX) NULL,
    Status NVARCHAR(50) NOT NULL DEFAULT 'Active', -- Active, Completed, Archived
    Store NVARCHAR(200) NULL,
    TotalEstimatedCost DECIMAL(10, 2) NULL,
    ActualCost DECIMAL(10, 2) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CompletedAt DATETIME2 NULL,
    IsDeleted BIT NOT NULL DEFAULT 0
);

CREATE INDEX IX_ShoppingList_UserId ON ShoppingList(UserId);
CREATE INDEX IX_ShoppingList_Status ON ShoppingList(Status);
GO

CREATE TABLE ShoppingListItem (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ShoppingListId UNIQUEIDENTIFIER NOT NULL,
    ProductId UNIQUEIDENTIFIER NULL,
    IngredientId UNIQUEIDENTIFIER NULL,
    CustomName NVARCHAR(200) NULL,
    Quantity DECIMAL(10, 2) NOT NULL DEFAULT 1,
    Unit NVARCHAR(50) NULL,
    EstimatedPrice DECIMAL(10, 2) NULL,
    ActualPrice DECIMAL(10, 2) NULL,
    IsChecked BIT NOT NULL DEFAULT 0,
    CheckedAt DATETIME2 NULL,
    Category NVARCHAR(100) NULL,
    Aisle NVARCHAR(100) NULL,
    Notes NVARCHAR(500) NULL,
    RecipeId UNIQUEIDENTIFIER NULL, -- If added from recipe
    OrderIndex INT NOT NULL DEFAULT 0,
    
    CONSTRAINT FK_ShoppingListItem_ShoppingList FOREIGN KEY (ShoppingListId)
        REFERENCES ShoppingList(Id) ON DELETE CASCADE
);

CREATE INDEX IX_ShoppingListItem_ShoppingListId ON ShoppingListItem(ShoppingListId);
GO

CREATE TABLE ListShare (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ShoppingListId UNIQUEIDENTIFIER NOT NULL,
    OwnerUserId UNIQUEIDENTIFIER NOT NULL,
    SharedWithUserId UNIQUEIDENTIFIER NOT NULL,
    CanEdit BIT NOT NULL DEFAULT 0,
    SharedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    
    CONSTRAINT FK_ListShare_ShoppingList FOREIGN KEY (ShoppingListId)
        REFERENCES ShoppingList(Id) ON DELETE CASCADE
);

CREATE INDEX IX_ListShare_SharedWithUserId ON ListShare(SharedWithUserId);
GO

CREATE TABLE StoreLayout (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    StoreName NVARCHAR(200) NOT NULL,
    Category NVARCHAR(100) NOT NULL,
    Aisle NVARCHAR(100) NULL,
    OrderIndex INT NOT NULL DEFAULT 0
);

CREATE INDEX IX_StoreLayout_UserId_StoreName ON StoreLayout(UserId, StoreName);
GO
