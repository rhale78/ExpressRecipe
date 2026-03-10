-- Migration: 002_NotificationSoftDelete
-- Description: Add soft-delete columns to Notification table to support GDPR
--   Regular deletes → soft delete (IsDeleted = 1).
--   GDPR right-to-erasure uses the hard-delete endpoints which bypass IsDeleted.
-- Date: 2026-03-10

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Notification') AND name = 'IsDeleted')
BEGIN
    ALTER TABLE Notification ADD IsDeleted BIT NOT NULL DEFAULT 0;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Notification') AND name = 'DeletedAt')
BEGIN
    ALTER TABLE Notification ADD DeletedAt DATETIME2 NULL;
END
GO

-- Index to speed up filtering out soft-deleted rows in common queries
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('Notification') AND name = 'IX_Notification_IsDeleted')
BEGIN
    CREATE INDEX IX_Notification_IsDeleted ON Notification(UserId, IsDeleted, CreatedAt DESC);
END
GO
