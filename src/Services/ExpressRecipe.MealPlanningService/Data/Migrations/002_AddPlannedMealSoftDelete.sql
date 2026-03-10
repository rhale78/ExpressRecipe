-- Migration: 002_AddPlannedMealSoftDelete
-- Description: Add soft-delete support to PlannedMeal table
-- Date: 2026-03-09

ALTER TABLE PlannedMeal ADD IsDeleted BIT NOT NULL DEFAULT 0;
ALTER TABLE PlannedMeal ADD DeletedAt DATETIME2 NULL;
GO
