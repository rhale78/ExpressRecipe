-- Migration: 002_HouseholdTasks
-- Description: Create HouseholdTask table for thaw reminders and household task management
-- Date: 2026-03-09
-- Note: HouseholdId is a logical FK to InventoryService.Household; not enforced cross-DB.

CREATE TABLE HouseholdTask (
    Id                UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    HouseholdId       UNIQUEIDENTIFIER NOT NULL,
    TaskType          NVARCHAR(50)     NOT NULL,     -- ThawReminder|StorageMove|General
    Title             NVARCHAR(300)    NOT NULL,
    Description       NVARCHAR(MAX)    NULL,
    DueAt             DATETIME2        NOT NULL,
    RelatedEntityType NVARCHAR(100)    NULL,         -- PlannedMeal|InventoryItem
    RelatedEntityId   UNIQUEIDENTIFIER NULL,
    Status            NVARCHAR(30)     NOT NULL DEFAULT 'Pending',
        -- Pending|Actioned|Dismissed|Escalated
    ActionTaken       NVARCHAR(50)     NULL,
        -- Moved|AlreadyMoved|Ignored
    ActionedBy        UNIQUEIDENTIFIER NULL,
    ActionedAt        DATETIME2        NULL,
    ReminderSent      BIT              NOT NULL DEFAULT 0,
    EscalationSent    BIT              NOT NULL DEFAULT 0,
    EscalateAfterMins INT              NOT NULL DEFAULT 120,
    CreatedAt         DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt         DATETIME2        NULL
);

CREATE INDEX IX_HouseholdTask_HouseholdId   ON HouseholdTask(HouseholdId, Status);
CREATE INDEX IX_HouseholdTask_DueAt         ON HouseholdTask(DueAt) WHERE Status IN ('Pending','Escalated');
CREATE INDEX IX_HouseholdTask_RelatedEntity ON HouseholdTask(RelatedEntityId) WHERE RelatedEntityId IS NOT NULL;
GO
