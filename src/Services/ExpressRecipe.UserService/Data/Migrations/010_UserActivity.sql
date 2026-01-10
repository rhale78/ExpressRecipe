-- Migration: 010_UserActivity
-- Description: Enhance user activity tracking with additional indexes and views
-- Date: 2024-11-19
-- Note: UserActivity table already created in migration 007, this adds enhancements

-- Add CHECK constraint if it doesn't exist
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_UserActivity_EntityPair' AND parent_object_id = OBJECT_ID('UserActivity'))
BEGIN
    ALTER TABLE UserActivity ADD CONSTRAINT CK_UserActivity_EntityPair CHECK (
        (EntityType IS NULL AND EntityId IS NULL) OR
        (EntityType IS NOT NULL AND EntityId IS NOT NULL)
    );
END
GO

-- Add missing indexes (check first to avoid errors if they already exist)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_UserActivity_EntityType_EntityId' AND object_id = OBJECT_ID('UserActivity'))
    CREATE INDEX IX_UserActivity_EntityType_EntityId ON UserActivity(EntityType, EntityId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_UserActivity_UserId_ActivityDate' AND object_id = OBJECT_ID('UserActivity'))
    CREATE INDEX IX_UserActivity_UserId_ActivityDate ON UserActivity(UserId, ActivityDate DESC);
GO

-- Recreate ActivityDate index with DESC if it exists without DESC
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_UserActivity_ActivityDate' AND object_id = OBJECT_ID('UserActivity'))
BEGIN
    DROP INDEX IX_UserActivity_ActivityDate ON UserActivity;
    CREATE INDEX IX_UserActivity_ActivityDate ON UserActivity(ActivityDate DESC);
END
GO

-- Create indexed view for daily activity summary (performance optimization)
IF NOT EXISTS (SELECT 1 FROM sys.views WHERE name = 'vw_DailyActivitySummary')
BEGIN
    EXEC('
    CREATE VIEW vw_DailyActivitySummary
    WITH SCHEMABINDING
    AS
    SELECT
        UserId,
        CAST(ActivityDate AS DATE) AS ActivityDay,
        ActivityType,
        COUNT_BIG(*) AS ActivityCount
    FROM dbo.UserActivity
    GROUP BY UserId, CAST(ActivityDate AS DATE), ActivityType
    ');
END
GO

-- Index the view for better query performance
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_vw_DailyActivitySummary' AND object_id = OBJECT_ID('vw_DailyActivitySummary'))
    CREATE UNIQUE CLUSTERED INDEX IX_vw_DailyActivitySummary
        ON vw_DailyActivitySummary(UserId, ActivityDay, ActivityType);
GO
