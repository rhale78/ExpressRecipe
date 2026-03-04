-- Migration: 015_AdminSuspendUser
-- Description: Add IsSuspended flag to UserProfile for admin user management
-- Date: 2026-03-04

IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = N'IsSuspended' AND Object_ID = Object_ID(N'UserProfile'))
BEGIN
    ALTER TABLE UserProfile ADD IsSuspended BIT NOT NULL DEFAULT 0;
END
GO
IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = N'SuspendedAt' AND Object_ID = Object_ID(N'UserProfile'))
BEGIN
    ALTER TABLE UserProfile ADD SuspendedAt DATETIME2 NULL;
END
GO
IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = N'SuspendedBy' AND Object_ID = Object_ID(N'UserProfile'))
BEGIN
    ALTER TABLE UserProfile ADD SuspendedBy UNIQUEIDENTIFIER NULL;
END
GO
