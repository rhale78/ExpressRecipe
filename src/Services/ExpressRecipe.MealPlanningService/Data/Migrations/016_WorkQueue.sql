-- Migration: 016_WorkQueue
-- Description: Add WorkQueueItem and WorkQueueItemSnooze tables for the household work-queue
--              feature. Work-queue items are task-like records surfaced to household members
--              (e.g. "Thaw chicken for tomorrow's dinner", "Buy milk - running low").
-- Date: 2026-03-10

CREATE TABLE WorkQueueItem (
    Id                  UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    HouseholdId         UNIQUEIDENTIFIER NOT NULL,
    UserId              UNIQUEIDENTIFIER NULL,  -- NULL = visible to all household members
    ItemType            NVARCHAR(50)  NOT NULL, -- 'ThawReminder','PriceAlert','LowStock','MealReminder','Custom'
    Title               NVARCHAR(300) NOT NULL,
    Body                NVARCHAR(MAX) NULL,
    Priority            INT           NOT NULL DEFAULT 5,  -- 1=Critical, 5=Normal, 9=Low
    Status              NVARCHAR(20)  NOT NULL DEFAULT 'Pending',  -- 'Pending','Snoozed','Done','Dismissed'
    DueAt               DATETIME2     NULL,
    RelatedEntityType   NVARCHAR(50)  NULL,  -- 'PlannedMeal','InventoryItem','Recipe', etc.
    RelatedEntityId     UNIQUEIDENTIFIER NULL,
    -- Deduplication key: upsert uses this to find an existing item to update
    DeduplicationKey    NVARCHAR(500) NULL,
    CreatedAt           DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt           DATETIME2     NULL,
    DoneAt              DATETIME2     NULL,
    IsDeleted           BIT           NOT NULL DEFAULT 0,
    DeletedAt           DATETIME2     NULL
);

CREATE INDEX IX_WorkQueueItem_HouseholdId        ON WorkQueueItem(HouseholdId) WHERE IsDeleted = 0;
CREATE INDEX IX_WorkQueueItem_Status             ON WorkQueueItem(Status)      WHERE IsDeleted = 0;
CREATE INDEX IX_WorkQueueItem_DuplicationKey     ON WorkQueueItem(DeduplicationKey) WHERE DeduplicationKey IS NOT NULL AND IsDeleted = 0;
CREATE INDEX IX_WorkQueueItem_RelatedEntity      ON WorkQueueItem(RelatedEntityType, RelatedEntityId) WHERE IsDeleted = 0;
GO

CREATE TABLE WorkQueueItemSnooze (
    Id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    WorkQueueItemId UNIQUEIDENTIFIER NOT NULL,
    SnoozedByUserId UNIQUEIDENTIFIER NOT NULL,
    SnoozedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    ResumeAt        DATETIME2        NOT NULL,
    Notes           NVARCHAR(500)    NULL,
    CONSTRAINT FK_WorkQueueItemSnooze_WorkQueueItem
        FOREIGN KEY (WorkQueueItemId) REFERENCES WorkQueueItem(Id) ON DELETE CASCADE
);

CREATE INDEX IX_WorkQueueItemSnooze_WorkQueueItemId ON WorkQueueItemSnooze(WorkQueueItemId);
CREATE INDEX IX_WorkQueueItemSnooze_ResumeAt        ON WorkQueueItemSnooze(ResumeAt);
GO
