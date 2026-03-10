-- Migration: 015_WorkQueue
-- Also add CookedAt to PlannedMeal (required for post-cook trigger)

ALTER TABLE PlannedMeal ADD CookedAt DATETIME2 NULL;
ALTER TABLE PlannedMeal ADD CookedStatus NVARCHAR(20) NULL DEFAULT 'Planned';
    -- Planned | Cooked | Skipped
GO

CREATE TABLE WorkQueueItem (
    Id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    HouseholdId     UNIQUEIDENTIFIER NOT NULL,
    ItemType        NVARCHAR(50) NOT NULL,
        -- Expired|SafetyAlert|ExpiringCritical|HouseholdTaskOverdue|PriceDrop|
        -- ExpiringSoon|HouseholdTaskPending|LowStockReorder|MoveToFreezer|
        -- RateRecipe|SaveCookingNote
    Priority        INT NOT NULL DEFAULT 9,     -- 1 highest, 10 lowest
    Title           NVARCHAR(200) NOT NULL,
    Body            NVARCHAR(500) NULL,
    ActionPayload   NVARCHAR(MAX) NULL,         -- JSON: entity IDs, action params
    Status          NVARCHAR(20) NOT NULL DEFAULT 'Pending',
        -- Pending | Actioned | Dismissed | Snoozed | Expired
    ActionedAt      DATETIME2 NULL,
    ActionedBy      UNIQUEIDENTIFIER NULL,      -- userId who actioned
    ActionTaken     NVARCHAR(100) NULL,         -- e.g. "AddedToShoppingList", "Moved", "Rated"
    ExpiresAt       DATETIME2 NULL,             -- auto-expire stale items
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt       DATETIME2 NULL,
    -- Deduplication: prevent duplicate items of same type for same entity
    SourceEntityId  UNIQUEIDENTIFIER NULL,      -- InventoryItemId, RecipeId, ProductId etc.
    SourceService   NVARCHAR(50) NULL           -- Inventory|Recipe|Price|Tasks
);
CREATE INDEX IX_WorkQueueItem_Household ON WorkQueueItem(HouseholdId);
CREATE INDEX IX_WorkQueueItem_Status    ON WorkQueueItem(Status) WHERE Status='Pending';
CREATE INDEX IX_WorkQueueItem_Priority  ON WorkQueueItem(HouseholdId, Priority, CreatedAt)
    WHERE Status='Pending';
-- Prevent duplicate active items for same source entity + type
CREATE UNIQUE INDEX IX_WorkQueueItem_Dedup
    ON WorkQueueItem(HouseholdId, ItemType, SourceEntityId)
    WHERE Status='Pending' AND SourceEntityId IS NOT NULL;
GO

-- Per-user snooze table (snooze is personal; one member snoozing doesn't hide for others)
CREATE TABLE WorkQueueItemSnooze (
    Id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    WorkQueueItemId UNIQUEIDENTIFIER NOT NULL,
    UserId          UNIQUEIDENTIFIER NOT NULL,
    SnoozedUntil    DATETIME2 NOT NULL,
    CONSTRAINT FK_WorkQueueItemSnooze_Item FOREIGN KEY (WorkQueueItemId)
        REFERENCES WorkQueueItem(Id) ON DELETE CASCADE,
    CONSTRAINT UQ_WorkQueueItemSnooze_User_Item UNIQUE (WorkQueueItemId, UserId)
);
GO
