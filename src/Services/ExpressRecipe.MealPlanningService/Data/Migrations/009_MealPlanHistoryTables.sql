-- Migration: 009_MealPlanHistoryTables
-- Description: Add MealPlanSnapshot and MealChangeLog tables for history/undo support
-- Prerequisite guards: adds SortOrder to PlannedMeal and creates MealCourse if not already
-- present from prior migrations (PR #32 / Part 1).

-- Add SortOrder to PlannedMeal if it does not exist yet
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'PlannedMeal') AND name = N'SortOrder')
BEGIN
    ALTER TABLE PlannedMeal ADD SortOrder INT NOT NULL DEFAULT 0;
END
GO

-- Create MealCourse table if it does not exist yet
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'MealCourse') AND type = N'U')
BEGIN
    CREATE TABLE MealCourse (
        Id            UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        PlannedMealId UNIQUEIDENTIFIER NOT NULL,
        CourseType    NVARCHAR(50) NOT NULL,
        RecipeId      UNIQUEIDENTIFIER NULL,
        Servings      DECIMAL(10,2) NOT NULL DEFAULT 1,
        SortOrder     INT NOT NULL DEFAULT 0,
        CONSTRAINT FK_MealCourse_PlannedMeal FOREIGN KEY (PlannedMealId) REFERENCES PlannedMeal(Id) ON DELETE CASCADE
    );
    CREATE INDEX IX_MealCourse_PlannedMealId ON MealCourse(PlannedMealId);
END
GO

CREATE TABLE MealPlanSnapshot (
    Id           UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    MealPlanId   UNIQUEIDENTIFIER NOT NULL,
    UserId       UNIQUEIDENTIFIER NOT NULL,
    SnapshotType NVARCHAR(30) NOT NULL,
    Label        NVARCHAR(200) NULL,
    Scope        NVARCHAR(20) NOT NULL,
    ScopeDate    DATE NULL,
    SnapshotData NVARCHAR(MAX) NOT NULL,
    CreatedAt    DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_MealPlanSnapshot_Plan FOREIGN KEY (MealPlanId) REFERENCES MealPlan(Id) ON DELETE CASCADE
);
CREATE INDEX IX_MealPlanSnapshot_PlanId ON MealPlanSnapshot(MealPlanId, CreatedAt DESC);
CREATE INDEX IX_MealPlanSnapshot_Scope  ON MealPlanSnapshot(MealPlanId, Scope, ScopeDate);
GO

CREATE TABLE MealChangeLog (
    Id            UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    PlannedMealId UNIQUEIDENTIFIER NOT NULL,
    MealPlanId    UNIQUEIDENTIFIER NOT NULL,
    ChangeType    NVARCHAR(30) NOT NULL,
    ChangedBy     UNIQUEIDENTIFIER NOT NULL,
    BeforeJson    NVARCHAR(MAX) NULL,
    AfterJson     NVARCHAR(MAX) NULL,
    ChangedAt     DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);
CREATE INDEX IX_MealChangeLog_MealId ON MealChangeLog(PlannedMealId, ChangedAt DESC);
CREATE INDEX IX_MealChangeLog_PlanId ON MealChangeLog(MealPlanId,    ChangedAt DESC);
GO
