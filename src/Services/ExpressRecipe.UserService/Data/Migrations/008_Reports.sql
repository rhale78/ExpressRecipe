-- Migration: 008_Reports
-- Description: Add report generation and history tracking
-- Date: 2024-11-19

-- ReportType: Types of reports users can generate
CREATE TABLE ReportType (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Name NVARCHAR(100) NOT NULL,
    Description NVARCHAR(500) NULL,
    Category NVARCHAR(50) NOT NULL, -- Shopping, Nutrition, Inventory, Financial, Activity
    RequiresSubscription NVARCHAR(50) NULL, -- NULL for all, 'Plus', 'Premium'
    IsActive BIT NOT NULL DEFAULT 1,
    SortOrder INT NOT NULL DEFAULT 0,
    CONSTRAINT UQ_ReportType_Name UNIQUE (Name)
);

-- Seed report types
INSERT INTO ReportType (Name, Description, Category, RequiresSubscription, SortOrder) VALUES
-- Shopping Reports
('Shopping History', 'Complete shopping purchase history', 'Shopping', NULL, 1),
('Price Comparison', 'Compare prices across stores for your shopping list', 'Shopping', 'Plus', 2),
('Savings Summary', 'Total savings from coupons and sales', 'Shopping', 'Plus', 3),
('Shopping Patterns', 'Your shopping frequency and patterns', 'Shopping', 'Premium', 4),
('Store Spending Analysis', 'Spending breakdown by store', 'Shopping', 'Plus', 5),

-- Nutrition Reports
('Nutrition Summary', 'Daily/weekly/monthly nutrition totals', 'Nutrition', NULL, 10),
('Allergen Exposure', 'Track potential allergen exposures', 'Nutrition', NULL, 11),
('Dietary Goal Progress', 'Progress toward health goals', 'Nutrition', 'Plus', 12),
('Meal Balance Analysis', 'Macronutrient balance over time', 'Nutrition', 'Premium', 13),
('Family Nutrition Report', 'Nutrition summary for all family members', 'Nutrition', 'Plus', 14),

-- Inventory Reports
('Current Inventory', 'What you have on hand', 'Inventory', 'Plus', 20),
('Expiring Soon', 'Items expiring in the next week', 'Inventory', 'Plus', 21),
('Inventory Value', 'Total value of your pantry', 'Inventory', 'Premium', 22),
('Usage Patterns', 'How quickly you use different items', 'Inventory', 'Premium', 23),
('Waste Report', 'Track expired or wasted items', 'Inventory', 'Premium', 24),

-- Financial Reports
('Monthly Spending', 'Total grocery spending by month', 'Financial', 'Plus', 30),
('Budget Tracking', 'Compare actual vs budgeted spending', 'Financial', 'Premium', 31),
('Coupon Effectiveness', 'ROI on your coupon usage', 'Financial', 'Premium', 32),
('Price Trends', 'Price trends for frequently purchased items', 'Financial', 'Premium', 33),
('Category Spending', 'Spending breakdown by product category', 'Financial', 'Plus', 34),

-- Activity Reports
('Recipe Activity', 'Your most cooked recipes', 'Activity', NULL, 40),
('Meal Plan History', 'Past meal plans and adherence', 'Activity', 'Plus', 41),
('Contribution Summary', 'Your contributions to the community', 'Activity', NULL, 42),
('Points History', 'Points earned and spent', 'Activity', NULL, 43),
('App Usage Stats', 'Your app usage patterns', 'Activity', 'Premium', 44);
GO

-- SavedReport: User's saved/scheduled reports
CREATE TABLE SavedReport (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    ReportTypeId UNIQUEIDENTIFIER NOT NULL,
    ReportName NVARCHAR(200) NOT NULL,
    Parameters NVARCHAR(MAX) NULL, -- JSON parameters for the report
    Schedule NVARCHAR(100) NULL, -- NULL for one-time, 'Daily', 'Weekly', 'Monthly'
    ScheduleDay INT NULL, -- Day of week (for Weekly) or day of month (for Monthly)
    LastRunAt DATETIME2 NULL,
    NextRunAt DATETIME2 NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    EmailResults BIT NOT NULL DEFAULT 0,
    EmailAddress NVARCHAR(200) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NULL,
    CONSTRAINT FK_SavedReport_ReportType FOREIGN KEY (ReportTypeId)
        REFERENCES ReportType(Id) ON DELETE CASCADE
);

CREATE INDEX IX_SavedReport_UserId ON SavedReport(UserId);
CREATE INDEX IX_SavedReport_NextRunAt ON SavedReport(NextRunAt);
GO

-- ReportHistory: Generated reports history
CREATE TABLE ReportHistory (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    ReportTypeId UNIQUEIDENTIFIER NOT NULL,
    SavedReportId UNIQUEIDENTIFIER NULL, -- If generated from saved report
    ReportName NVARCHAR(200) NOT NULL,
    Parameters NVARCHAR(MAX) NULL,
    StartDate DATETIME2 NULL, -- Report date range
    EndDate DATETIME2 NULL,
    Format NVARCHAR(50) NOT NULL, -- PDF, Excel, CSV, HTML
    FileUrl NVARCHAR(1000) NULL, -- Temporary download URL
    FileSize BIGINT NULL,
    ExpiresAt DATETIME2 NULL,
    GeneratedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    Status NVARCHAR(50) NOT NULL DEFAULT 'Completed', -- Pending, Completed, Failed
    ErrorMessage NVARCHAR(MAX) NULL,
    CONSTRAINT FK_ReportHistory_ReportType FOREIGN KEY (ReportTypeId)
        REFERENCES ReportType(Id) ON DELETE CASCADE,
    CONSTRAINT FK_ReportHistory_SavedReport FOREIGN KEY (SavedReportId)
        REFERENCES SavedReport(Id) ON DELETE SET NULL
);

CREATE INDEX IX_ReportHistory_UserId ON ReportHistory(UserId);
CREATE INDEX IX_ReportHistory_GeneratedAt ON ReportHistory(GeneratedAt);
CREATE INDEX IX_ReportHistory_ExpiresAt ON ReportHistory(ExpiresAt);
GO

-- UserList: Generic user-created lists (custom shopping lists, wishlists, etc.)
CREATE TABLE UserList (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    ListType NVARCHAR(50) NOT NULL, -- Shopping, Wishlist, Inventory, Custom
    Name NVARCHAR(200) NOT NULL,
    Description NVARCHAR(MAX) NULL,
    IsShared BIT NOT NULL DEFAULT 0,
    ShareCode NVARCHAR(50) NULL UNIQUE,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    DeletedAt DATETIME2 NULL
);

CREATE INDEX IX_UserList_UserId ON UserList(UserId);
CREATE INDEX IX_UserList_ListType ON UserList(ListType);
CREATE INDEX IX_UserList_ShareCode ON UserList(ShareCode);
GO

-- UserListItem: Items in user lists
CREATE TABLE UserListItem (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ListId UNIQUEIDENTIFIER NOT NULL,
    ItemType NVARCHAR(50) NOT NULL, -- Product, Ingredient, Recipe, Custom
    ItemId UNIQUEIDENTIFIER NULL, -- References Product, Ingredient, or Recipe
    ItemName NVARCHAR(300) NULL, -- For custom items not in database
    Quantity DECIMAL(10, 2) NULL,
    Unit NVARCHAR(50) NULL,
    Notes NVARCHAR(500) NULL,
    IsChecked BIT NOT NULL DEFAULT 0,
    CheckedAt DATETIME2 NULL,
    OrderIndex INT NOT NULL DEFAULT 0,
    AddedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_UserListItem_List FOREIGN KEY (ListId)
        REFERENCES UserList(Id) ON DELETE CASCADE
);

CREATE INDEX IX_UserListItem_ListId ON UserListItem(ListId);
CREATE INDEX IX_UserListItem_ItemType_ItemId ON UserListItem(ItemType, ItemId);
GO

-- ListSharing: Share lists with friends/family
CREATE TABLE ListSharing (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ListId UNIQUEIDENTIFIER NOT NULL,
    SharedWithUserId UNIQUEIDENTIFIER NULL, -- Specific user (if NULL, anyone with code)
    CanEdit BIT NOT NULL DEFAULT 0,
    SharedBy UNIQUEIDENTIFIER NOT NULL,
    SharedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    ExpiresAt DATETIME2 NULL,
    CONSTRAINT FK_ListSharing_List FOREIGN KEY (ListId)
        REFERENCES UserList(Id) ON DELETE CASCADE
);

CREATE INDEX IX_ListSharing_ListId ON ListSharing(ListId);
CREATE INDEX IX_ListSharing_SharedWithUserId ON ListSharing(SharedWithUserId);
GO
