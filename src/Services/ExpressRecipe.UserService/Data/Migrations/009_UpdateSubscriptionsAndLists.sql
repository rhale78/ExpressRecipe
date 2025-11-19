-- Migration: 009_UpdateSubscriptionsAndLists
-- Description: Update subscription schema and fix list tables to match repository implementations
-- Date: 2024-11-19

-- Update SubscriptionTier to add missing fields
ALTER TABLE SubscriptionTier ADD TierName NVARCHAR(50) NULL;
ALTER TABLE SubscriptionTier ADD DisplayName NVARCHAR(100) NULL;
ALTER TABLE SubscriptionTier ADD AllowsOfflineSync BIT NOT NULL DEFAULT 0;
ALTER TABLE SubscriptionTier ADD AllowsMenuPlanning BIT NOT NULL DEFAULT 0;
ALTER TABLE SubscriptionTier ADD AllowsPriceComparison BIT NOT NULL DEFAULT 0;
ALTER TABLE SubscriptionTier ADD PointsMultiplier DECIMAL(5, 2) NOT NULL DEFAULT 1.0;
GO

-- Update existing subscription tiers with proper values
UPDATE SubscriptionTier SET
    TierName = Name,
    DisplayName = Name,
    AllowsOfflineSync = 0,
    AllowsMenuPlanning = 0,
    AllowsPriceComparison = 0,
    PointsMultiplier = 1.0
WHERE TierName IS NULL;
GO

-- Update Free tier
UPDATE SubscriptionTier
SET AllowsOfflineSync = 0,
    AllowsMenuPlanning = 0,
    AllowsPriceComparison = 0,
    AllowsRecipeImport = 0,
    AllowsAdvancedReports = 0,
    PointsMultiplier = 1.0
WHERE Name = 'Free';
GO

-- Update Plus tier
UPDATE SubscriptionTier
SET AllowsOfflineSync = 1,
    AllowsMenuPlanning = 1,
    AllowsPriceComparison = 1,
    AllowsRecipeImport = 1,
    AllowsAdvancedReports = 0,
    PointsMultiplier = 1.5
WHERE Name = 'Plus';
GO

-- Update Premium tier
UPDATE SubscriptionTier
SET AllowsOfflineSync = 1,
    AllowsMenuPlanning = 1,
    AllowsPriceComparison = 1,
    AllowsRecipeImport = 1,
    AllowsAdvancedReports = 1,
    PointsMultiplier = 2.0
WHERE Name = 'Premium';
GO

-- Make TierName and DisplayName non-nullable after data migration
ALTER TABLE SubscriptionTier ALTER COLUMN TierName NVARCHAR(50) NOT NULL;
ALTER TABLE SubscriptionTier ALTER COLUMN DisplayName NVARCHAR(100) NOT NULL;
GO

-- Create UserSubscription table for active subscriptions (separate from history)
CREATE TABLE UserSubscription (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    SubscriptionTierId UNIQUEIDENTIFIER NOT NULL,
    Status NVARCHAR(50) NOT NULL DEFAULT 'Active', -- Active, Cancelled, Expired, PastDue
    BillingCycle NVARCHAR(20) NOT NULL, -- Monthly, Yearly
    StartDate DATETIME2 NOT NULL,
    EndDate DATETIME2 NULL,
    NextBillingDate DATETIME2 NULL,
    AutoRenew BIT NOT NULL DEFAULT 1,
    PaymentMethodId NVARCHAR(100) NULL,
    CancellationDate DATETIME2 NULL,
    CancellationReason NVARCHAR(MAX) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy UNIQUEIDENTIFIER NULL,
    UpdatedAt DATETIME2 NULL,
    UpdatedBy UNIQUEIDENTIFIER NULL,
    CONSTRAINT FK_UserSubscription_SubscriptionTier FOREIGN KEY (SubscriptionTierId)
        REFERENCES SubscriptionTier(Id) ON DELETE CASCADE
);

CREATE INDEX IX_UserSubscription_UserId ON UserSubscription(UserId);
CREATE INDEX IX_UserSubscription_Status ON UserSubscription(Status);
CREATE INDEX IX_UserSubscription_NextBillingDate ON UserSubscription(NextBillingDate);
GO

-- Update SubscriptionHistory to add missing fields for compatibility
ALTER TABLE SubscriptionHistory ADD Action NVARCHAR(50) NULL; -- Subscribed, Renewed, Cancelled, Upgraded, Downgraded
ALTER TABLE SubscriptionHistory ADD ChangeDate DATETIME2 NULL;
ALTER TABLE SubscriptionHistory ADD BillingCycle NVARCHAR(20) NULL;
ALTER TABLE SubscriptionHistory ADD Amount DECIMAL(10, 2) NULL;
ALTER TABLE SubscriptionHistory ADD Notes NVARCHAR(MAX) NULL;
GO

-- Set default values for existing SubscriptionHistory records
UPDATE SubscriptionHistory
SET Action = CASE
        WHEN CancelledAt IS NOT NULL THEN 'Cancelled'
        WHEN PaymentStatus = 'Paid' THEN 'Subscribed'
        ELSE 'Subscribed'
    END,
    ChangeDate = CreatedAt,
    BillingCycle = ISNULL(BillingPeriod, 'Monthly'),
    Amount = AmountPaid
WHERE Action IS NULL;
GO

-- Update UserList table to match repository schema
-- Drop ShareCode if it exists (will use ListSharing table instead)
IF COL_LENGTH('UserList', 'ShareCode') IS NOT NULL
BEGIN
    DROP INDEX IF EXISTS IX_UserList_ShareCode ON UserList;
    ALTER TABLE UserList DROP COLUMN ShareCode;
END
GO

-- Add missing fields to UserList
IF COL_LENGTH('UserList', 'ListName') IS NULL
    ALTER TABLE UserList ADD ListName NVARCHAR(200) NULL;
GO

IF COL_LENGTH('UserList', 'Icon') IS NULL
    ALTER TABLE UserList ADD Icon NVARCHAR(100) NULL;
GO

IF COL_LENGTH('UserList', 'ItemCount') IS NULL
    ALTER TABLE UserList ADD ItemCount INT NOT NULL DEFAULT 0;
GO

-- Migrate Name to ListName if needed
UPDATE UserList SET ListName = Name WHERE ListName IS NULL AND Name IS NOT NULL;
GO

-- Make ListName non-nullable after migration
ALTER TABLE UserList ALTER COLUMN ListName NVARCHAR(200) NOT NULL;
GO

-- Drop Name column if it exists and is different from ListName
IF COL_LENGTH('UserList', 'Name') IS NOT NULL AND COL_LENGTH('UserList', 'ListName') IS NOT NULL
BEGIN
    ALTER TABLE UserList DROP COLUMN Name;
END
GO

-- Update UserListItem to match repository schema
IF COL_LENGTH('UserListItem', 'EntityType') IS NULL
    ALTER TABLE UserListItem ADD EntityType NVARCHAR(50) NULL;
GO

IF COL_LENGTH('UserListItem', 'EntityId') IS NULL
    ALTER TABLE UserListItem ADD EntityId UNIQUEIDENTIFIER NULL;
GO

IF COL_LENGTH('UserListItem', 'ItemText') IS NULL
    ALTER TABLE UserListItem ADD ItemText NVARCHAR(300) NULL;
GO

IF COL_LENGTH('UserListItem', 'SortOrder') IS NULL
    ALTER TABLE UserListItem ADD SortOrder INT NOT NULL DEFAULT 0;
GO

-- Migrate ItemType/ItemId to EntityType/EntityId if needed
UPDATE UserListItem
SET EntityType = ItemType,
    EntityId = ItemId,
    ItemText = ISNULL(ItemName, '')
WHERE EntityType IS NULL;
GO

-- Make ItemText non-nullable after migration
ALTER TABLE UserListItem ALTER COLUMN ItemText NVARCHAR(300) NOT NULL;
GO

-- Update ListSharing to match repository schema
IF COL_LENGTH('ListSharing', 'Permission') IS NULL
    ALTER TABLE ListSharing ADD Permission NVARCHAR(50) NOT NULL DEFAULT 'View';
GO

-- Migrate CanEdit to Permission
UPDATE ListSharing
SET Permission = CASE WHEN CanEdit = 1 THEN 'Edit' ELSE 'View' END
WHERE Permission = 'View';
GO

-- Update ReportType to add missing ParameterSchema field
IF COL_LENGTH('ReportType', 'ParameterSchema') IS NULL
    ALTER TABLE ReportType ADD ParameterSchema NVARCHAR(MAX) NULL;
GO

-- Update SavedReport to match repository schema
IF COL_LENGTH('SavedReport', 'ScheduleFrequency') IS NULL
    ALTER TABLE SavedReport ADD ScheduleFrequency NVARCHAR(100) NULL;
GO

IF COL_LENGTH('SavedReport', 'IsScheduled') IS NULL
    ALTER TABLE SavedReport ADD IsScheduled BIT NOT NULL DEFAULT 0;
GO

IF COL_LENGTH('SavedReport', 'IsDeleted') IS NULL
    ALTER TABLE SavedReport ADD IsDeleted BIT NOT NULL DEFAULT 0;
GO

-- Migrate Schedule to ScheduleFrequency if needed
UPDATE SavedReport
SET ScheduleFrequency = Schedule,
    IsScheduled = CASE WHEN Schedule IS NOT NULL THEN 1 ELSE 0 END
WHERE ScheduleFrequency IS NULL;
GO

-- Update ReportHistory to match repository schema
IF COL_LENGTH('ReportHistory', 'Status') IS NULL
    ALTER TABLE ReportHistory ADD Status NVARCHAR(50) NOT NULL DEFAULT 'Completed';
GO

IF COL_LENGTH('ReportHistory', 'ExportFormat') IS NULL
    ALTER TABLE ReportHistory ADD ExportFormat NVARCHAR(50) NULL;
GO

IF COL_LENGTH('ReportHistory', 'Parameters') IS NULL
    ALTER TABLE ReportHistory ADD Parameters NVARCHAR(MAX) NULL;
GO

-- Migrate Format to ExportFormat if needed
UPDATE ReportHistory
SET ExportFormat = Format
WHERE ExportFormat IS NULL AND Format IS NOT NULL;
GO
