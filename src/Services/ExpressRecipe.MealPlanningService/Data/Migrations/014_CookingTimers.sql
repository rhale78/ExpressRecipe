-- Migration: 014_CookingTimers
-- Description: Create CookingTimer table for per-user cooking timers linked to recipes and planned meals
-- Date: 2026-03-09

CREATE TABLE CookingTimer (
    Id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId          UNIQUEIDENTIFIER NOT NULL,
    HouseholdId     UNIQUEIDENTIFIER NOT NULL,
    Label           NVARCHAR(200) NOT NULL,
    RecipeId        UNIQUEIDENTIFIER NULL,
    PlannedMealId   UNIQUEIDENTIFIER NULL,
    DurationSeconds INT NOT NULL,
    StartedAt       DATETIME2 NULL,
    ExpiresAt       DATETIME2 NULL,
    Status          NVARCHAR(30) NOT NULL DEFAULT 'Preset',
        -- Preset|Running|Paused|Expired|Cancelled|Acknowledged
    PausedAt        DATETIME2 NULL,
    PausedSeconds   INT NOT NULL DEFAULT 0,
    NotificationSent BIT NOT NULL DEFAULT 0,
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt       DATETIME2 NULL
);
CREATE INDEX IX_CookingTimer_UserId    ON CookingTimer(UserId, Status);
CREATE INDEX IX_CookingTimer_ExpiresAt ON CookingTimer(ExpiresAt) WHERE Status='Running';
GO
