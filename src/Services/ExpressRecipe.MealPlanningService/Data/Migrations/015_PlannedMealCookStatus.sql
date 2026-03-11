-- Migration: 015_PlannedMealCookStatus
-- Description: Add CookedAt and CookedStatus columns to PlannedMeal so the timer expiry
--              and cooking-complete flow can record exactly when a meal was cooked and its
--              current cook state independent of the general IsCompleted flag.
-- Date: 2026-03-10

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'PlannedMeal') AND name = N'CookedStatus')
BEGIN
    ALTER TABLE PlannedMeal ADD CookedStatus NVARCHAR(20) NOT NULL DEFAULT 'Pending';
    -- Valid values: 'Pending', 'InProgress', 'Cooked', 'Skipped'
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'PlannedMeal') AND name = N'CookedAt')
BEGIN
    ALTER TABLE PlannedMeal ADD CookedAt DATETIME2 NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'PlannedMeal') AND name = N'CookedByTimerId')
BEGIN
    ALTER TABLE PlannedMeal ADD CookedByTimerId UNIQUEIDENTIFIER NULL;
    -- References the CookingTimer that triggered the cooked event (if any)
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'PlannedMeal') AND name = N'IX_PlannedMeal_CookedStatus')
BEGIN
    CREATE INDEX IX_PlannedMeal_CookedStatus ON PlannedMeal(CookedStatus);
END
GO
