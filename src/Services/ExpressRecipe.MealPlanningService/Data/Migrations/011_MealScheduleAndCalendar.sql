-- Migration: 002_MealScheduleAndCalendar
-- Description: Add meal schedule configuration table and calendar/notification columns to PlannedMeal
-- Date: 2026-03-09

CREATE TABLE MealScheduleConfig (
    Id                         UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId                     UNIQUEIDENTIFIER NOT NULL,
    HouseholdId                UNIQUEIDENTIFIER NULL,
    IsHouseholdDefault         BIT NOT NULL DEFAULT 0,
    MealType                   NVARCHAR(50) NOT NULL,
    TargetTime                 TIME NOT NULL,
    NotifyEnabled              BIT NOT NULL DEFAULT 1,
    NotifyMinutesBefore        INT NOT NULL DEFAULT 30,
    FreezerReminderEnabled     BIT NOT NULL DEFAULT 0,
    FreezerReminderHoursBefore INT NOT NULL DEFAULT 8,
    CONSTRAINT UQ_MealSchedule_User_Type UNIQUE (UserId, MealType)
);
CREATE INDEX IX_MealScheduleConfig_UserId ON MealScheduleConfig(UserId);
GO

ALTER TABLE PlannedMeal ADD GoogleCalendarEventId NVARCHAR(200) NULL;
ALTER TABLE PlannedMeal ADD NotificationSent      BIT NOT NULL DEFAULT 0;
ALTER TABLE PlannedMeal ADD FreezerReminderSent   BIT NOT NULL DEFAULT 0;
GO
