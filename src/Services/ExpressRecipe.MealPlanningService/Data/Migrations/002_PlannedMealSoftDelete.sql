-- Migration: 002_PlannedMealSoftDelete
-- Description: Add soft-delete and NutritionalGoal columns to align with repository expectations
-- Date: 2026-03-10

-- Add IsDeleted to PlannedMeal if it doesn't already exist
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PlannedMeal') AND name = 'IsDeleted')
BEGIN
    ALTER TABLE PlannedMeal ADD IsDeleted BIT NOT NULL DEFAULT 0;
END
GO

-- Add NutritionalGoal supporting columns if not already present
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('NutritionalGoal') AND name = 'TargetValue')
BEGIN
    ALTER TABLE NutritionalGoal ADD TargetValue DECIMAL(10,2) NOT NULL DEFAULT 0;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('NutritionalGoal') AND name = 'Unit')
BEGIN
    ALTER TABLE NutritionalGoal ADD Unit NVARCHAR(50) NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('NutritionalGoal') AND name = 'IsActive')
BEGIN
    ALTER TABLE NutritionalGoal ADD IsActive BIT NOT NULL DEFAULT 1;
END
GO

-- Add UpdatedAt to MealPlan if not present
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('MealPlan') AND name = 'UpdatedAt')
BEGIN
    ALTER TABLE MealPlan ADD UpdatedAt DATETIME2 NULL;
END
GO
