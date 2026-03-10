-- Migration: 005_MealAttendee
-- Description: Create MealAttendee table for tracking who is eating each planned meal
-- Date: 2025-03-09

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'MealAttendee')
BEGIN
    CREATE TABLE MealAttendee (
        Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        PlannedMealId   UNIQUEIDENTIFIER NOT NULL,
        UserId          UNIQUEIDENTIFIER NULL,
        FamilyMemberId  UNIQUEIDENTIFIER NULL,
        GuestName       NVARCHAR(200)    NULL,
        CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),

        CONSTRAINT FK_MealAttendee_PlannedMeal FOREIGN KEY (PlannedMealId)
            REFERENCES PlannedMeal(Id) ON DELETE CASCADE
    );

    CREATE INDEX IX_MealAttendee_PlannedMealId ON MealAttendee(PlannedMealId);
    CREATE INDEX IX_MealAttendee_UserId ON MealAttendee(UserId) WHERE UserId IS NOT NULL;
END
GO
