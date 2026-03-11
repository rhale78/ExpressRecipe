-- Migration: 020_AdminAuditLog
-- Description: Admin audit trail – every privileged admin/CS action is logged here.
--   Stores the actor (admin/CS agent), the action taken, the target entity,
--   and an optional freetext context note.
-- Date: 2026-03-10

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'AdminAuditLog') AND type = 'U')
BEGIN
    CREATE TABLE AdminAuditLog (
        Id           UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        ActorId      UNIQUEIDENTIFIER NOT NULL,   -- admin / CS user who performed the action
        Action       NVARCHAR(100)    NOT NULL,   -- e.g. SubscriptionCredit, ImpersonateStart, UserSuspended
        TargetId     UNIQUEIDENTIFIER NULL,        -- entity affected (UserId, ProductId, …)
        TargetType   NVARCHAR(50)     NULL,        -- optional: 'User', 'Product', …
        Notes        NVARCHAR(MAX)    NULL,
        OccurredAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE()
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AdminAuditLog_ActorId' AND object_id = OBJECT_ID('AdminAuditLog'))
BEGIN
    CREATE INDEX IX_AdminAuditLog_ActorId ON AdminAuditLog (ActorId);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AdminAuditLog_TargetId' AND object_id = OBJECT_ID('AdminAuditLog'))
BEGIN
    CREATE INDEX IX_AdminAuditLog_TargetId ON AdminAuditLog (TargetId);
END
GO
