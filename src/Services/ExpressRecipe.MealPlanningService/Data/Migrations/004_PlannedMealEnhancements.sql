-- Migration: 004_PlannedMealEnhancements
-- Description: Add UserId, CreatedAt, IsDeleted to PlannedMeal; create MealCourse table
-- Date: 2025-03-09

-- Add missing columns to PlannedMeal (only if they don't already exist)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PlannedMeal') AND name = 'UserId')
    ALTER TABLE PlannedMeal ADD UserId UNIQUEIDENTIFIER NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PlannedMeal') AND name = 'CreatedAt')
    ALTER TABLE PlannedMeal ADD CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE();
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PlannedMeal') AND name = 'IsDeleted')
    ALTER TABLE PlannedMeal ADD IsDeleted BIT NOT NULL DEFAULT 0;
GO

CREATE INDEX IX_PlannedMeal_UserId ON PlannedMeal(UserId) WHERE UserId IS NOT NULL;
GO

-- Create MealCourse table for multi-course meal support
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'MealCourse')
BEGIN
    CREATE TABLE MealCourse (
        Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        PlannedMealId   UNIQUEIDENTIFIER NOT NULL,
        CourseType      NVARCHAR(100)    NOT NULL, -- Starter, Main, Dessert, Side, Drink, etc.
        RecipeId        UNIQUEIDENTIFIER NULL,
        CustomName      NVARCHAR(300)    NULL,
        Servings        DECIMAL(10, 2)   NOT NULL DEFAULT 1,
        SortOrder       INT              NOT NULL DEFAULT 0,
        IsCompleted     BIT              NOT NULL DEFAULT 0,
        CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),

        CONSTRAINT FK_MealCourse_PlannedMeal FOREIGN KEY (PlannedMealId)
            REFERENCES PlannedMeal(Id) ON DELETE CASCADE
    );

    CREATE INDEX IX_MealCourse_PlannedMealId ON MealCourse(PlannedMealId);
END
GO
