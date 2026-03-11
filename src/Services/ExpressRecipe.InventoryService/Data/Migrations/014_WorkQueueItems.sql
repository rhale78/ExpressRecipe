-- Migration: 014_WorkQueueItems
-- Description: Create WorkQueueItem table for user action queue (WQ1)
-- Date: 2026-03-10
-- Items surface from various business rules (expiration, low stock, price drops, recipe ratings, etc.)
-- and are shown in the Blazor work-queue UI (/queue page).

CREATE TABLE WorkQueueItem (
    Id                UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId            UNIQUEIDENTIFIER NOT NULL,
    HouseholdId       UNIQUEIDENTIFIER NOT NULL,
    ItemType          NVARCHAR(60)     NOT NULL,
        -- Expired | ExpiringCritical | ExpiringSoon | PriceDrop | LowStockReorder
        -- MoveToFreezer | RateRecipe | SaveCookingNote
        -- HouseholdTaskPending | HouseholdTaskOverdue
    Title             NVARCHAR(300)    NOT NULL,
    Body              NVARCHAR(MAX)    NULL,
    Priority          INT              NOT NULL DEFAULT 5,
        -- 1 = highest (Safety/Expired), 10 = lowest (Nice-to-have)
    ActionPayload     NVARCHAR(MAX)    NULL,     -- JSON: type-specific action data
    Status            NVARCHAR(30)     NOT NULL DEFAULT 'Pending',
        -- Pending | Actioned | Dismissed | Snoozed
    ActionTaken       NVARCHAR(100)    NULL,
    ActionedAt        DATETIME2        NULL,
    SnoozeUntil       DATETIME2        NULL,
    RelatedEntityType NVARCHAR(100)    NULL,     -- InventoryItem | Recipe | HouseholdTask
    RelatedEntityId   UNIQUEIDENTIFIER NULL,
    CreatedAt         DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt         DATETIME2        NULL,
    IsDeleted         BIT              NOT NULL DEFAULT 0,
    DeletedAt         DATETIME2        NULL
);

CREATE INDEX IX_WorkQueueItem_User     ON WorkQueueItem(UserId, Status) WHERE IsDeleted = 0;
CREATE INDEX IX_WorkQueueItem_Household ON WorkQueueItem(HouseholdId, Status) WHERE IsDeleted = 0;
CREATE INDEX IX_WorkQueueItem_Priority  ON WorkQueueItem(UserId, Priority, CreatedAt) WHERE Status = 'Pending' AND IsDeleted = 0;
CREATE INDEX IX_WorkQueueItem_Snooze    ON WorkQueueItem(SnoozeUntil) WHERE Status = 'Snoozed' AND IsDeleted = 0;

-- Unique constraint to prevent duplicate pending items per user/household/type/entity.
-- Filtered on IsDeleted = 0 and Status = 'Pending' so actioned/dismissed duplicates don't block new insertions.
-- NOTE: SQL Server filtered unique indexes cannot cover nullable columns in all cases.
-- NULL RelatedEntityId is handled by including a sentinel placeholder in UpsertItemAsync.
CREATE UNIQUE INDEX UX_WorkQueueItem_PendingDedup
    ON WorkQueueItem(UserId, HouseholdId, ItemType, RelatedEntityId)
    WHERE IsDeleted = 0 AND Status = 'Pending' AND RelatedEntityId IS NOT NULL;
GO
