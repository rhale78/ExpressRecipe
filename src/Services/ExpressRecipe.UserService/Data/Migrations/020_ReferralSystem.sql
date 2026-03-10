-- Migration: 020_ReferralSystem
-- Description: Add referral codes, conversions and share links
-- Date: 2026-03-10

-- Add ReferredByCode to UserProfile
ALTER TABLE UserProfile ADD ReferredByCode NVARCHAR(20) NULL;
GO

CREATE TABLE ReferralCode (
    Id          UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId      UNIQUEIDENTIFIER NOT NULL,
    Code        NVARCHAR(20) NOT NULL,
    IsActive    BIT NOT NULL DEFAULT 1,
    UsageCount  INT NOT NULL DEFAULT 0,
    CreatedAt   DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT UQ_ReferralCode_Code UNIQUE (Code)
);
CREATE INDEX IX_ReferralCode_UserId ON ReferralCode(UserId);
GO

CREATE TABLE ReferralConversion (
    Id             UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ReferralCodeId UNIQUEIDENTIFIER NOT NULL,
    ReferrerId     UNIQUEIDENTIFIER NOT NULL,
    ReferredUserId UNIQUEIDENTIFIER NOT NULL,
    ConvertedAt    DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    PointsAwarded  INT NOT NULL DEFAULT 0,
    CONSTRAINT FK_ReferralConversion_ReferralCode FOREIGN KEY (ReferralCodeId) REFERENCES ReferralCode(Id)
);
GO

CREATE TABLE ShareLink (
    Id           UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    CreatedBy    UNIQUEIDENTIFIER NOT NULL,
    EntityType   NVARCHAR(50) NOT NULL,
    EntityId     UNIQUEIDENTIFIER NOT NULL,
    Token        NVARCHAR(50) NOT NULL,
    ExpiresAt    DATETIME2 NOT NULL,
    ViewCount    INT NOT NULL DEFAULT 0,
    CreatedAt    DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT UQ_ShareLink_Token UNIQUE (Token)
);
CREATE INDEX IX_ShareLink_CreatedBy ON ShareLink(CreatedBy);
GO
