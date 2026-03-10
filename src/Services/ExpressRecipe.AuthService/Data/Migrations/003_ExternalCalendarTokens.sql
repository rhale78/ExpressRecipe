-- Migration: 003_ExternalCalendarTokens
-- Description: Create table for storing external calendar OAuth tokens (Google, etc.)
-- Date: 2026-03-09

CREATE TABLE ExternalCalendarToken (
    Id           UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId       UNIQUEIDENTIFIER NOT NULL,
    Provider     NVARCHAR(50) NOT NULL DEFAULT 'Google',
    AccessToken  NVARCHAR(2000) NOT NULL,
    RefreshToken NVARCHAR(2000) NOT NULL,
    ExpiresAt    DATETIME2 NOT NULL,
    Scopes       NVARCHAR(500) NOT NULL,
    CreatedAt    DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt    DATETIME2 NULL,
    CONSTRAINT UQ_CalendarToken_UserProvider UNIQUE (UserId, Provider)
);
CREATE INDEX IX_ExternalCalendarToken_UserId ON ExternalCalendarToken(UserId);
GO
