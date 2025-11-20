-- Migration: 001_CreateMealPlanningTables
-- Description: Create meal planning and nutritional tracking tables
-- Date: 2024-11-19

CREATE TABLE MealPlan (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    Name NVARCHAR(200) NOT NULL,
    Description NVARCHAR(MAX) NULL,
    StartDate DATE NOT NULL,
    EndDate DATE NOT NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NULL,
    IsDeleted BIT NOT NULL DEFAULT 0
);

CREATE INDEX IX_MealPlan_UserId ON MealPlan(UserId);
CREATE INDEX IX_MealPlan_StartDate ON MealPlan(StartDate);
GO

CREATE TABLE PlannedMeal (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    MealPlanId UNIQUEIDENTIFIER NOT NULL,
    RecipeId UNIQUEIDENTIFIER NULL,
    CustomMealName NVARCHAR(300) NULL,
    MealType NVARCHAR(50) NOT NULL, -- Breakfast, Lunch, Dinner, Snack
    PlannedDate DATE NOT NULL,
    PlannedTime TIME NULL,
    Servings INT NULL,
    Notes NVARCHAR(MAX) NULL,
    IsCompleted BIT NOT NULL DEFAULT 0,
    CompletedAt DATETIME2 NULL,
    
    CONSTRAINT FK_PlannedMeal_MealPlan FOREIGN KEY (MealPlanId)
        REFERENCES MealPlan(Id) ON DELETE CASCADE
);

CREATE INDEX IX_PlannedMeal_MealPlanId ON PlannedMeal(MealPlanId);
CREATE INDEX IX_PlannedMeal_PlannedDate ON PlannedMeal(PlannedDate);
GO

CREATE TABLE NutritionalGoal (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    GoalType NVARCHAR(100) NOT NULL, -- Daily, Weekly, Custom
    StartDate DATE NOT NULL,
    EndDate DATE NULL,
    TargetCalories DECIMAL(10, 2) NULL,
    TargetProtein DECIMAL(10, 2) NULL,
    TargetCarbs DECIMAL(10, 2) NULL,
    TargetFat DECIMAL(10, 2) NULL,
    TargetFiber DECIMAL(10, 2) NULL,
    TargetSodium DECIMAL(10, 2) NULL,
    Notes NVARCHAR(MAX) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);

CREATE INDEX IX_NutritionalGoal_UserId ON NutritionalGoal(UserId);
GO

CREATE TABLE PlanTemplate (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    Name NVARCHAR(200) NOT NULL,
    Description NVARCHAR(MAX) NULL,
    TemplateData NVARCHAR(MAX) NOT NULL, -- JSON
    IsPublic BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);

CREATE INDEX IX_PlanTemplate_UserId ON PlanTemplate(UserId);
GO
