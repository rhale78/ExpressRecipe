-- Migration: 019_GdprRequests
-- Description: Add GdprRequest table to track GDPR data subject requests
--   (Export, Delete, Forget) per user. DownloadUrl is populated when an
--   Export request is completed. Status lifecycle: Pending → Processing → Completed | Failed.
-- Date: 2026-03-10

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'GdprRequest') AND type = 'U')
BEGIN
    CREATE TABLE GdprRequest (
        Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        UserId      UNIQUEIDENTIFIER NOT NULL,
        RequestType NVARCHAR(20)     NOT NULL,   -- Export | Delete | Forget
        Status      NVARCHAR(20)     NOT NULL DEFAULT 'Pending',
        RequestedAt DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        CompletedAt DATETIME2        NULL,
        DownloadUrl NVARCHAR(500)    NULL,        -- populated for Export requests
        Notes       NVARCHAR(MAX)    NULL
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_GdprRequest_UserId' AND object_id = OBJECT_ID('GdprRequest'))
BEGIN
    CREATE INDEX IX_GdprRequest_UserId ON GdprRequest (UserId);
END
GO
