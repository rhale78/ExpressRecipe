-- Migration: 007_MealPlanFuturePlanning
-- Description: Add future planning and household columns to MealPlan
-- Date: 2025-03-09

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('MealPlan') AND name = 'HouseholdId')
    ALTER TABLE MealPlan ADD HouseholdId UNIQUEIDENTIFIER NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('MealPlan') AND name = 'IsFuturePlan')
    ALTER TABLE MealPlan ADD IsFuturePlan BIT NOT NULL DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('MealPlan') AND name = 'OccasionLabel')
    ALTER TABLE MealPlan ADD OccasionLabel NVARCHAR(200) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('MealPlan') AND name = 'Tags')
    ALTER TABLE MealPlan ADD Tags NVARCHAR(MAX) NULL; -- comma-separated or JSON array
GO

-- Back-fill IsFuturePlan: any plan whose StartDate is more than 30 days from now is a future plan
UPDATE MealPlan
SET IsFuturePlan = 1
WHERE StartDate > DATEADD(DAY, 30, GETUTCDATE())
  AND IsDeleted = 0;
GO
