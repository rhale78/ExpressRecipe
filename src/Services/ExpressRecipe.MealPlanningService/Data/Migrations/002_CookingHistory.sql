-- Migration: 002_CookingHistory
-- Description: Create cooking history table for tracking meal completion events
-- Date: 2025-03-09

CREATE TABLE CookingHistory (
    Id               UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId           UNIQUEIDENTIFIER NOT NULL,
    RecipeId         UNIQUEIDENTIFIER NOT NULL,
    PlannedMealId    UNIQUEIDENTIFIER NULL,
    CookedAt         DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    ServingsCooked   DECIMAL(5,2) NOT NULL DEFAULT 1,
    ServingsEaten    DECIMAL(5,2) NULL,
    UserRating       TINYINT NULL CHECK (UserRating BETWEEN 1 AND 5),
    Notes            NVARCHAR(500) NULL,
    WasSubstituted   BIT NOT NULL DEFAULT 0,
    InventoryUpdated BIT NOT NULL DEFAULT 0,
    NutritionLogged  BIT NOT NULL DEFAULT 0,
    CreatedAt        DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_CookingHistory_PlannedMeal FOREIGN KEY (PlannedMealId) REFERENCES PlannedMeal(Id) ON DELETE SET NULL
);
CREATE INDEX IX_CookingHistory_UserId_CookedAt ON CookingHistory(UserId, CookedAt DESC);
CREATE INDEX IX_CookingHistory_RecipeId        ON CookingHistory(RecipeId);
CREATE INDEX IX_CookingHistory_PlannedMealId   ON CookingHistory(PlannedMealId);
GO
