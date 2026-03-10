-- Migration: 003_DailyNutritionLog
-- Description: Create daily nutrition log table for macro tracking
-- Date: 2025-03-09

CREATE TABLE DailyNutritionLog (
    Id               UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId           UNIQUEIDENTIFIER NOT NULL,
    LogDate          DATE NOT NULL,
    MealType         NVARCHAR(50) NULL,
    CookingHistoryId UNIQUEIDENTIFIER NULL,
    RecipeId         UNIQUEIDENTIFIER NULL,
    RecipeName       NVARCHAR(300) NULL,
    ServingsEaten    DECIMAL(5,2) NOT NULL DEFAULT 1,
    Calories         DECIMAL(10,2) NULL,
    Protein          DECIMAL(10,2) NULL,
    Carbohydrates    DECIMAL(10,2) NULL,
    TotalFat         DECIMAL(10,2) NULL,
    DietaryFiber     DECIMAL(10,2) NULL,
    Sodium           DECIMAL(10,2) NULL,
    IsManualEntry    BIT NOT NULL DEFAULT 0,
    CreatedAt        DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt        DATETIME2 NULL,
    CONSTRAINT FK_DailyNutritionLog_CookingHistory FOREIGN KEY (CookingHistoryId) REFERENCES CookingHistory(Id) ON DELETE SET NULL
);
CREATE INDEX IX_DailyNutritionLog_UserId_Date ON DailyNutritionLog(UserId, LogDate DESC);
CREATE INDEX IX_DailyNutritionLog_Date        ON DailyNutritionLog(LogDate);
GO
