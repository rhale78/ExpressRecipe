-- Migration: 019_StripeIntegration
-- Description: Add Stripe customer/subscription fields to UserProfile and create StripeEventLog
--              table for idempotent webhook processing.
-- Date: 2026-03-10

-- Add Stripe columns to UserProfile
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('UserProfile') AND name = 'StripeCustomerId'
)
BEGIN
    ALTER TABLE UserProfile ADD StripeCustomerId NVARCHAR(100) NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('UserProfile') AND name = 'StripeSubscriptionId'
)
BEGIN
    ALTER TABLE UserProfile ADD StripeSubscriptionId NVARCHAR(100) NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('UserProfile') AND name = 'TrialStartDate'
)
BEGIN
    ALTER TABLE UserProfile ADD TrialStartDate DATETIME2 NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('UserProfile') AND name = 'TrialEndDate'
)
BEGIN
    ALTER TABLE UserProfile ADD TrialEndDate DATETIME2 NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('UserProfile') AND name = 'SubscriptionCancelledAt'
)
BEGIN
    ALTER TABLE UserProfile ADD SubscriptionCancelledAt DATETIME2 NULL;
END
GO

-- Index on StripeCustomerId for fast lookup
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('UserProfile') AND name = 'IX_UserProfile_StripeCustomerId'
)
BEGIN
    CREATE INDEX IX_UserProfile_StripeCustomerId
        ON UserProfile(StripeCustomerId)
        WHERE StripeCustomerId IS NOT NULL;
END
GO

-- Stripe event log (idempotency — prevents processing the same webhook event twice)
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('StripeEventLog') AND type = 'U')
BEGIN
    CREATE TABLE StripeEventLog (
        Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        StripeEventId   NVARCHAR(100)    NOT NULL,
        EventType       NVARCHAR(100)    NOT NULL,
        ProcessedAt     DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        Success         BIT              NOT NULL DEFAULT 1,
        Notes           NVARCHAR(MAX)    NULL,
        CONSTRAINT UQ_StripeEventLog_EventId UNIQUE (StripeEventId)
    );
END
GO
