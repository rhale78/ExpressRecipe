-- Migration: 002_ApprovalQueue
-- Description: Unified content approval queue for Admin/CS/Moderator review.
--   Consolidates product submissions, recipe reviews, and community content
--   into a single prioritised queue with AI pre-score support.
--   HumanTimeoutMins triggers escalation (orange badge in UI).
-- Date: 2026-03-10

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'ApprovalQueue') AND type = 'U')
BEGIN
    CREATE TABLE ApprovalQueue (
        Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        EntityType      NVARCHAR(50)     NOT NULL,   -- Recipe | Product | Review
        EntityId        UNIQUEIDENTIFIER NOT NULL,
        SubmittedBy     UNIQUEIDENTIFIER NOT NULL,
        SubmittedAt     DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        Status          NVARCHAR(20)     NOT NULL DEFAULT 'Pending', -- Pending | Approved | Rejected | AiReview
        ReviewedBy      UNIQUEIDENTIFIER NULL,
        ReviewedAt      DATETIME2        NULL,
        ReviewNotes     NVARCHAR(MAX)    NULL,
        RejectionReason NVARCHAR(MAX)    NULL,
        AiScore         DECIMAL(5,4)     NULL,   -- 0.0 – 1.0 AI confidence score
        AiFlags         NVARCHAR(MAX)    NULL,   -- JSON array of flagged issues
        HumanTimeoutMins INT             NOT NULL DEFAULT 60,
        EscalatedAt     DATETIME2        NULL
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ApprovalQueue_EntityType_Status' AND object_id = OBJECT_ID('ApprovalQueue'))
BEGIN
    CREATE INDEX IX_ApprovalQueue_EntityType_Status ON ApprovalQueue (EntityType, Status);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ApprovalQueue_SubmittedBy' AND object_id = OBJECT_ID('ApprovalQueue'))
BEGIN
    CREATE INDEX IX_ApprovalQueue_SubmittedBy ON ApprovalQueue (SubmittedBy);
END
GO
