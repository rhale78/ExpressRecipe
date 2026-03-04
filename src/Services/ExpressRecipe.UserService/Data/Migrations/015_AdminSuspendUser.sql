-- Migration: 015_AdminSuspendUser
-- Description: Add IsSuspended flag to UserProfile for admin user management
-- Date: 2026-03-04

ALTER TABLE UserProfile ADD IsSuspended BIT NOT NULL DEFAULT 0;
GO
ALTER TABLE UserProfile ADD SuspendedAt DATETIME2 NULL;
GO
ALTER TABLE UserProfile ADD SuspendedBy UNIQUEIDENTIFIER NULL;
GO
