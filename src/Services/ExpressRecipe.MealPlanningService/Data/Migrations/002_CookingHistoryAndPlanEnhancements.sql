-- Migration: 002_CookingHistoryAndPlanEnhancements
-- Description: Add CookingHistory table, enhance MealPlan and PlannedMeal tables
-- Date: 2026-03-09

CREATE TABLE CookingHistory (
    Id                      UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId                  UNIQUEIDENTIFIER NOT NULL,
    HouseholdId             UNIQUEIDENTIFIER NULL,
    RecipeId                UNIQUEIDENTIFIER NOT NULL,
    RecipeName              NVARCHAR(300) NOT NULL,
    CookedAt                DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    Servings                INT NOT NULL DEFAULT 1,
    MealType                NVARCHAR(50) NOT NULL DEFAULT 'Dinner',
    Rating                  TINYINT NULL,
    WouldCookAgain          BIT NULL,
    Notes                   NVARCHAR(MAX) NULL,
    Source                  NVARCHAR(50) NOT NULL DEFAULT 'PlannedMeal',
    PlannedMealId           UNIQUEIDENTIFIER NULL,
    InventoryDeductionSent  BIT NOT NULL DEFAULT 0,
    RatingPromptSent        BIT NOT NULL DEFAULT 0
);
CREATE INDEX IX_CookingHistory_UserId_CookedAt ON CookingHistory(UserId, CookedAt DESC);
CREATE INDEX IX_CookingHistory_RecipeId ON CookingHistory(RecipeId);
CREATE INDEX IX_CookingHistory_InventoryDeduction ON CookingHistory(InventoryDeductionSent)
    WHERE InventoryDeductionSent = 0;
GO

ALTER TABLE MealPlan
    ADD HouseholdId             UNIQUEIDENTIFIER NULL,
        DefaultSuggestionMode   NVARCHAR(50) NULL,
        DefaultInventorySlider  TINYINT NOT NULL DEFAULT 50;
GO

ALTER TABLE PlannedMeal
    ADD Source              NVARCHAR(50) NOT NULL DEFAULT 'Manual',
        SuggestionScore     DECIMAL(5,2) NULL,
        IsLeftover          BIT NOT NULL DEFAULT 0;
GO
