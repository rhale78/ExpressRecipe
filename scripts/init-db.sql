-- ExpressRecipe Database Initialization Script
-- Creates database and initial tables for all microservices

USE master;
GO

-- Create database if it doesn't exist
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'ExpressRecipe')
BEGIN
    CREATE DATABASE ExpressRecipe;
END
GO

USE ExpressRecipe;
GO

-- ============================================
-- Notification Service Tables
-- ============================================

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Notification')
BEGIN
    CREATE TABLE Notification (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        UserId UNIQUEIDENTIFIER NOT NULL,
        Type NVARCHAR(50) NOT NULL, -- ExpiringItem, ProductRecall, LowStock, etc.
        Title NVARCHAR(200) NOT NULL,
        Message NVARCHAR(MAX) NOT NULL,
        Priority NVARCHAR(20) NOT NULL DEFAULT 'Normal', -- High, Normal, Low
        IsRead BIT NOT NULL DEFAULT 0,
        ReadAt DATETIME2 NULL,
        ContextData NVARCHAR(MAX) NULL, -- JSON
        ActionUrl NVARCHAR(500) NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        ExpiresAt DATETIME2 NULL,
        IsDeleted BIT NOT NULL DEFAULT 0,
        DeletedAt DATETIME2 NULL,
        RowVersion ROWVERSION,
        INDEX IX_Notification_UserId (UserId),
        INDEX IX_Notification_Type (Type),
        INDEX IX_Notification_IsRead (IsRead),
        INDEX IX_Notification_CreatedAt (CreatedAt)
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'NotificationPreferences')
BEGIN
    CREATE TABLE NotificationPreferences (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        UserId UNIQUEIDENTIFIER NOT NULL UNIQUE,
        EmailEnabled BIT NOT NULL DEFAULT 1,
        EmailOnExpiringItems BIT NOT NULL DEFAULT 1,
        EmailOnProductRecalls BIT NOT NULL DEFAULT 1,
        EmailOnLowStock BIT NOT NULL DEFAULT 1,
        PushEnabled BIT NOT NULL DEFAULT 1,
        PushOnExpiringItems BIT NOT NULL DEFAULT 1,
        PushOnProductRecalls BIT NOT NULL DEFAULT 1,
        PushOnLowStock BIT NOT NULL DEFAULT 1,
        InAppEnabled BIT NOT NULL DEFAULT 1,
        ExpiringItemsDaysAhead INT NOT NULL DEFAULT 3,
        LowStockThreshold INT NOT NULL DEFAULT 2,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        UpdatedAt DATETIME2 NULL,
        RowVersion ROWVERSION,
        INDEX IX_NotificationPreferences_UserId (UserId)
    );
END
GO

-- ============================================
-- Analytics Service Tables
-- ============================================

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SpendingRecord')
BEGIN
    CREATE TABLE SpendingRecord (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        UserId UNIQUEIDENTIFIER NOT NULL,
        StoreName NVARCHAR(200) NOT NULL,
        Amount DECIMAL(18, 2) NOT NULL,
        TransactionDate DATETIME2 NOT NULL,
        Category NVARCHAR(100) NULL,
        Notes NVARCHAR(500) NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        RowVersion ROWVERSION,
        INDEX IX_SpendingRecord_UserId (UserId),
        INDEX IX_SpendingRecord_TransactionDate (TransactionDate)
    );
END
GO

-- ============================================
-- Community Service Tables
-- ============================================

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'RecipeRating')
BEGIN
    CREATE TABLE RecipeRating (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        RecipeId UNIQUEIDENTIFIER NOT NULL,
        UserId UNIQUEIDENTIFIER NOT NULL,
        UserName NVARCHAR(200) NOT NULL,
        Rating INT NOT NULL CHECK (Rating BETWEEN 1 AND 5),
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        UpdatedAt DATETIME2 NULL,
        RowVersion ROWVERSION,
        UNIQUE (RecipeId, UserId),
        INDEX IX_RecipeRating_RecipeId (RecipeId),
        INDEX IX_RecipeRating_UserId (UserId)
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'RecipeReview')
BEGIN
    CREATE TABLE RecipeReview (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        RecipeId UNIQUEIDENTIFIER NOT NULL,
        UserId UNIQUEIDENTIFIER NOT NULL,
        UserName NVARCHAR(200) NOT NULL,
        UserAvatar NVARCHAR(500) NULL,
        Rating INT NOT NULL CHECK (Rating BETWEEN 1 AND 5),
        Title NVARCHAR(200) NOT NULL,
        Comment NVARCHAR(MAX) NOT NULL,
        Photos NVARCHAR(MAX) NULL, -- JSON array
        HelpfulCount INT NOT NULL DEFAULT 0,
        IsVerifiedPurchase BIT NOT NULL DEFAULT 0,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        UpdatedAt DATETIME2 NULL,
        IsDeleted BIT NOT NULL DEFAULT 0,
        DeletedAt DATETIME2 NULL,
        RowVersion ROWVERSION,
        INDEX IX_RecipeReview_RecipeId (RecipeId),
        INDEX IX_RecipeReview_UserId (UserId),
        INDEX IX_RecipeReview_CreatedAt (CreatedAt)
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ReviewHelpful')
BEGIN
    CREATE TABLE ReviewHelpful (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        ReviewId UNIQUEIDENTIFIER NOT NULL,
        UserId UNIQUEIDENTIFIER NOT NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        UNIQUE (ReviewId, UserId),
        INDEX IX_ReviewHelpful_ReviewId (ReviewId),
        INDEX IX_ReviewHelpful_UserId (UserId)
    );
END
GO

-- ============================================
-- Price Service Tables
-- ============================================

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ProductPrice')
BEGIN
    CREATE TABLE ProductPrice (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        ProductId UNIQUEIDENTIFIER NOT NULL,
        ProductName NVARCHAR(200) NOT NULL,
        Brand NVARCHAR(200) NOT NULL,
        StoreName NVARCHAR(200) NOT NULL,
        Price DECIMAL(18, 2) NOT NULL,
        Size NVARCHAR(50) NULL,
        Unit NVARCHAR(20) NULL,
        RecordedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        RecordedByUserId UNIQUEIDENTIFIER NULL,
        RowVersion ROWVERSION,
        INDEX IX_ProductPrice_ProductId (ProductId),
        INDEX IX_ProductPrice_StoreName (StoreName),
        INDEX IX_ProductPrice_RecordedAt (RecordedAt)
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PriceAlert')
BEGIN
    CREATE TABLE PriceAlert (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        UserId UNIQUEIDENTIFIER NOT NULL,
        ProductId UNIQUEIDENTIFIER NOT NULL,
        ProductName NVARCHAR(200) NOT NULL,
        Brand NVARCHAR(200) NOT NULL,
        TargetPrice DECIMAL(18, 2) NOT NULL,
        StoreName NVARCHAR(200) NULL,
        IsActive BIT NOT NULL DEFAULT 1,
        IsTriggered BIT NOT NULL DEFAULT 0,
        TriggeredAt DATETIME2 NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        RowVersion ROWVERSION,
        INDEX IX_PriceAlert_UserId (UserId),
        INDEX IX_PriceAlert_ProductId (ProductId),
        INDEX IX_PriceAlert_IsActive (IsActive)
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ShoppingBudget')
BEGIN
    CREATE TABLE ShoppingBudget (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        UserId UNIQUEIDENTIFIER NOT NULL,
        BudgetName NVARCHAR(200) NOT NULL,
        MonthlyLimit DECIMAL(18, 2) NOT NULL,
        CurrentSpending DECIMAL(18, 2) NOT NULL DEFAULT 0,
        StartDate DATETIME2 NOT NULL,
        EndDate DATETIME2 NOT NULL,
        IsActive BIT NOT NULL DEFAULT 1,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        UpdatedAt DATETIME2 NULL,
        RowVersion ROWVERSION,
        INDEX IX_ShoppingBudget_UserId (UserId),
        INDEX IX_ShoppingBudget_IsActive (IsActive)
    );
END
GO

PRINT 'ExpressRecipe database initialized successfully!';
GO
