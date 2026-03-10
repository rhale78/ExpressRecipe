-- Migration: 006_MealPlanShare
-- Description: Create MealPlanShare table for sharing meal plans with other users
-- Date: 2025-03-09

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'MealPlanShare')
BEGIN
    CREATE TABLE MealPlanShare (
        Id               UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        MealPlanId       UNIQUEIDENTIFIER NOT NULL,
        SharedByUserId   UNIQUEIDENTIFIER NOT NULL,
        SharedWithUserId UNIQUEIDENTIFIER NULL,
        ShareToken       NVARCHAR(100)    NULL,           -- for link-based sharing
        Permission       NVARCHAR(50)     NOT NULL DEFAULT 'View', -- View, Edit
        ExpiresAt        DATETIME2        NULL,
        CreatedAt        DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        IsDeleted        BIT              NOT NULL DEFAULT 0,

        CONSTRAINT FK_MealPlanShare_MealPlan FOREIGN KEY (MealPlanId)
            REFERENCES MealPlan(Id) ON DELETE CASCADE
    );

    CREATE INDEX IX_MealPlanShare_MealPlanId       ON MealPlanShare(MealPlanId);
    CREATE INDEX IX_MealPlanShare_SharedWithUserId  ON MealPlanShare(SharedWithUserId) WHERE SharedWithUserId IS NOT NULL;
    CREATE UNIQUE INDEX UX_MealPlanShare_Token      ON MealPlanShare(ShareToken) WHERE ShareToken IS NOT NULL AND IsDeleted = 0;
END
GO
