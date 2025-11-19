-- Migration: 010_UserActivity
-- Description: Add user activity tracking and analytics
-- Date: 2024-11-19

-- UserActivity: Track all user actions for analytics and gamification
CREATE TABLE UserActivity (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    ActivityType NVARCHAR(100) NOT NULL, -- Login, Logout, RecipeViewed, RecipeCooked, ProductScanned, etc.
    EntityType NVARCHAR(50) NULL, -- Recipe, Product, Store, etc.
    EntityId UNIQUEIDENTIFIER NULL, -- ID of the related entity
    Metadata NVARCHAR(MAX) NULL, -- JSON for additional context
    ActivityDate DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    DeviceType NVARCHAR(200) NULL, -- User agent string or device identifier
    IPAddress NVARCHAR(50) NULL,
    CONSTRAINT CK_UserActivity_EntityPair CHECK (
        (EntityType IS NULL AND EntityId IS NULL) OR
        (EntityType IS NOT NULL AND EntityId IS NOT NULL)
    )
);

CREATE INDEX IX_UserActivity_UserId ON UserActivity(UserId);
CREATE INDEX IX_UserActivity_ActivityType ON UserActivity(ActivityType);
CREATE INDEX IX_UserActivity_ActivityDate ON UserActivity(ActivityDate DESC);
CREATE INDEX IX_UserActivity_EntityType_EntityId ON UserActivity(EntityType, EntityId);
CREATE INDEX IX_UserActivity_UserId_ActivityDate ON UserActivity(UserId, ActivityDate DESC);
GO

-- Create indexed view for daily activity summary (performance optimization)
CREATE VIEW vw_DailyActivitySummary
WITH SCHEMABINDING
AS
SELECT
    UserId,
    CAST(ActivityDate AS DATE) AS ActivityDay,
    ActivityType,
    COUNT_BIG(*) AS ActivityCount
FROM dbo.UserActivity
GROUP BY UserId, CAST(ActivityDate AS DATE), ActivityType;
GO

-- Index the view for better query performance
CREATE UNIQUE CLUSTERED INDEX IX_vw_DailyActivitySummary
    ON vw_DailyActivitySummary(UserId, ActivityDay, ActivityType);
GO
